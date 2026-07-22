/*
 * 역할: 컷씬 동안 역할 배정과 환자·질병 데이터를 선행 준비하고 다른 시스템이 완료를 기다릴 수 있게 합니다.
 * 핵심 설계: 외부 데이터 생성 실패나 부분 결과에도 폴백 데이터를 채워 Gameplay 진입이 중단되지 않도록 합니다.
 */
using Cysharp.Threading.Tasks;
using DontDillyDally.Data;
using DontDillyDally.StageFlow;
using Photon.Pun;
using ExitGames.Client.Photon;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// 컷씬 씬에서 질병/음성 데이터를 사전 생성하여 Gameplay 씬의 StageFlowManager에 전달합니다.
/// DontDestroyOnLoad로 씬 전환 간 데이터를 보존하며, PhotonView가 필요 없습니다.
/// </summary>
public class StagePreloader : MonoBehaviourPunCallbacks
{
    public static StagePreloader Instance { get; private set; }

    [Header("병 정보 생성")]
    [SerializeField] private DiseaseGenerationManager _diseaseGenManager;

    // Gameplay로 전달할 현재 스테이지 런타임 데이터입니다.
    public StageRuntimeData StageData { get; private set; }
    // 마스터가 결정하고 Room Custom Property로 공유한 집도의 ActorNumber입니다.
    public int SurgeonActorNumber { get; private set; } = -1;
    public bool IsRoleAssignmentComplete { get; private set; }
    public bool IsDataPrepComplete { get; private set; }
    public event Action<int> RoleAssignmentCompleted;

    // 데이터 준비 완료를 여러 대기자가 공유하기 위한 완료 소스입니다.
    private UniTaskCompletionSource _dataPrepTcs;
    // 외부 생성 요청과 대기 작업을 씬 종료 시 취소합니다.
    private CancellationTokenSource _cts;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 새 스테이지 데이터를 설정하고 이전 준비 상태를 초기화합니다.
    /// </summary>
    public void Initialize(StageRuntimeData stageData)
    {
        StageData = stageData;
        SurgeonActorNumber = -1;
        IsRoleAssignmentComplete = false;
        IsDataPrepComplete = false;
        _cts = new CancellationTokenSource();
        _dataPrepTcs = new UniTaskCompletionSource();

        if (PhotonNetwork.IsMasterClient)
        {
            RoomProperties.SetStageDataPrepComplete(false);
        }
    }

    // ── 집도의 선정 ─────────────────────────────────────────────

    /// <summary>
    /// [MasterClient] 집도의를 선정합니다. SelectRoleManager가 자체 PhotonView로 RPC 전파합니다.
    /// </summary>
    public void AssignRoles()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        SurgeonActorNumber = SelectRoleManager.Instance?.AssignRoles() ?? -1;
        CompleteRoleAssignment();

        if (SurgeonActorNumber < 0)
        {
            Debug.LogError("[StagePreloader] 집도의 선정 실패");
            return;
        }

        Debug.Log($"[StagePreloader] ✓ 집도의 선정 완료: Actor {SurgeonActorNumber}");
    }

    /// <summary>
    /// 집도의가 선정될 때까지 대기합니다. (비마스터는 CustomProperties 동기화 대기)
    /// </summary>
    public async UniTask WaitForRoleAssignment(CancellationToken ct)
    {
        await UniTask.WaitUntil(() =>
        {
            foreach (var p in PhotonNetwork.PlayerList)
            {
                if (RoleProperties.GetPlayerRole(p) == RoleType.Surgeon)
                    return true;
            }
            return false;
        }, cancellationToken: ct);

        // 비마스터도 결과 저장
        foreach (var p in PhotonNetwork.PlayerList)
        {
            if (RoleProperties.GetPlayerRole(p) == RoleType.Surgeon)
            {
                SurgeonActorNumber = p.ActorNumber;
                break;
            }
        }
        CompleteRoleAssignment();
    }

    // ── 데이터 사전 생성 ─────────────────────────────────────────

    /// <summary>
    /// 컷씬과 병렬로 실행: 질병 생성 + 음성 사전 생성 (MasterClient 전용)
    /// </summary>
    public void StartDataPrep()
    {
        if (_cts == null)
        {
            return;
        }

        RunDataPrepAsync(_cts.Token).Forget();
    }

    /// <summary>
    /// 데이터 준비 완료까지 대기합니다.
    /// </summary>
    public async UniTask WaitForDataPrep(CancellationToken ct)
    {
        if (IsDataPrepComplete || RoomProperties.GetStageDataPrepComplete())
        {
            CompleteDataPrep();
            return;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            await _dataPrepTcs.Task.AttachExternalCancellation(ct);
            return;
        }

        await UniTask.WaitUntil(RoomProperties.GetStageDataPrepComplete, cancellationToken: ct);
        CompleteDataPrep();
    }

    /// <summary>
    /// 여러 환자 데이터를 병렬 생성하고 실패 결과를 폴백 데이터로 보완합니다.
    /// </summary>
    private async UniTaskVoid RunDataPrepAsync(CancellationToken ct)
    {
        try
        {
            // 1. 질병 데이터 병렬 생성 — 한 스테이지 환자 ≤5명 가정.
            //    Gemini Free Tier RPM 한도(15) 안에서 모두 동시에 쏘는 것이 가장 빠르다.
            //    1요청 ≈ 2~3초이므로 5요청 동시 발사 후 단일 웨이브로 ~3~5초 안에 종료.
            int patientCount = StageData.Settings.PatientSettings.PatientCount;
            int difficulty = StageData.Settings.PatientSettings.Difficulty;
            Debug.Log($"[StagePreloader] (1/3) 질병 데이터 병렬 생성 시작 (환자 {patientCount}명, 동시 발사)");
            StageData.Patients.Clear();

            float startTime = Time.realtimeSinceStartup;

            // 모든 환자 생성 작업을 한 번에 발사 (Semaphore 불필요 — 5건 << 15 RPM)
            // 각 환자 인덱스를 전달해 병렬 호출마다 서로 다른 힌트(신체 부위/소재)가 적용되도록 한다.
            var tasks = new UniTask<DiseaseData>[patientCount];
            for (int i = 0; i < patientCount; i++)
            {
                tasks[i] = GenerateOnePatientAsync(difficulty, StageData.StageId, i, patientCount, ct);
            }

            DiseaseData[] results = await UniTask.WhenAll(tasks);

            // 결과를 순서대로 등록 — null이면 폴백으로 즉시 대체
            // 사용된 폴백 ID를 추적하여 중복 방지
            var usedFallbackIds = new HashSet<string>();
            List<DiseaseData> stageFallbacks = FallbackDiseaseLoader.GetByStage(StageData.StageId);

            for (int i = 0; i < results.Length; i++)
            {
                DiseaseData disease = results[i];
                bool needsFallback = disease == null || disease.Source != RecipeSource.AIGenerated;

                if (needsFallback)
                {
                    disease = PickUniqueFallback(stageFallbacks, usedFallbackIds, StageData.StageId);
                    if (disease != null)
                    {
                        usedFallbackIds.Add(disease.DiseaseId);
                    }
                    Debug.LogWarning($"[StagePreloader] 환자 {i + 1}/{patientCount} AI 실패 → 폴백 사용: {disease?.DiseaseName ?? "없음"}");
                }
                else
                {
                    Debug.Log($"[StagePreloader] 환자 {i + 1}/{patientCount}: {disease.DiseaseName} (출처: {disease.Source})");
                }
                StageData.Patients.Add(disease);
            }

            Debug.Log($"[StagePreloader] (1/2) 질병 데이터 생성 완료: {StageData.Patients.Count}개 (소요: {Time.realtimeSinceStartup - startTime:F1}초)");

            // 2. 환자 소개 음성 사전 생성 (동적형만 TTS 사용)
            Debug.Log("[StagePreloader] (2/2) 환자 소개 음성 사전 생성 중...");
            if (CommentaryController.Instance != null)
            {
                var infos = new List<(string, string)>();
                foreach (var patient in StageData.Patients)
                {
                    infos.Add((patient.PatientName, patient.DiseaseName));
                }
                await CommentaryController.Instance.PreGeneratePatientIntros(infos, ct);
                Debug.Log("[StagePreloader] (2/2) 환자 소개 음성 사전 생성 완료");
            }
            else
            {
                Debug.LogWarning("[StagePreloader] CommentaryController.Instance가 null입니다. 환자 소개 사전 생성 스킵.");
            }

            CompleteDataPrep();
            RoomProperties.SetStageDataPrepComplete(true);
            Debug.Log("[StagePreloader] ✓ 데이터 준비 완료");
        }
        catch (OperationCanceledException)
        {
            Debug.Log("[StagePreloader] 데이터 준비 취소됨");
        }
        catch (Exception ex)
        {
            // 여기서 그냥 종료하면 _dataPrepTcs가 resolve되지 않아 모든 클라이언트의
            // StageFlowBootstrapper.WaitForDataPrep이 영구 대기한다.
            // 폴백으로 환자를 채우고 완료 신호를 반드시 내보낸다.
            Debug.LogError($"[StagePreloader] 데이터 준비 중 예외: {ex}. 폴백으로 완료 처리.");
            EnsureFallbackPatients();
            CompleteDataPrep();
            if (PhotonNetwork.IsMasterClient)
            {
                RoomProperties.SetStageDataPrepComplete(true);
            }
        }
    }

    // 예외 발생 시 부족한 환자 슬롯을 폴백 데이터로 채웁니다.
    private void EnsureFallbackPatients()
    {
        if (StageData == null)
        {
            return;
        }

        int targetCount = StageData.Settings.PatientSettings.PatientCount;
        if (StageData.Patients.Count >= targetCount)
        {
            return;
        }

        var usedIds = new HashSet<string>();
        for (int i = 0; i < StageData.Patients.Count; i++)
        {
            if (StageData.Patients[i] != null)
            {
                usedIds.Add(StageData.Patients[i].DiseaseId);
            }
        }

        List<DiseaseData> stageFallbacks = FallbackDiseaseLoader.GetByStage(StageData.StageId);
        while (StageData.Patients.Count < targetCount)
        {
            DiseaseData fallback = PickUniqueFallback(stageFallbacks, usedIds, StageData.StageId);
            if (fallback == null)
            {
                break;
            }

            usedIds.Add(fallback.DiseaseId);
            StageData.Patients.Add(fallback);
        }

        Debug.LogWarning($"[StagePreloader] 폴백 환자 {StageData.Patients.Count}/{targetCount}명으로 진행");
    }

    // 사용되지 않은 폴백 데이터를 랜덤으로 하나 선택합니다 (Query 전용, 부수 효과 없음).
    // usedIds 갱신은 호출자가 담당합니다.
    private static DiseaseData PickUniqueFallback(
        List<DiseaseData> stageFallbacks, HashSet<string> usedIds, string stageId)
    {
        if (stageFallbacks == null || stageFallbacks.Count == 0)
        {
            return FallbackDiseaseLoader.GetRandom(stageId);
        }

        // 사용 가능한 후보를 모아서 랜덤 선택 (순차 선택 방지)
        var available = new List<DiseaseData>();
        for (int i = 0; i < stageFallbacks.Count; i++)
        {
            if (!usedIds.Contains(stageFallbacks[i].DiseaseId))
            {
                available.Add(stageFallbacks[i]);
            }
        }

        if (available.Count > 0)
        {
            return available[UnityEngine.Random.Range(0, available.Count)];
        }

        // 모든 폴백 소진 — 어쩔 수 없이 랜덤 (중복 허용)
        return stageFallbacks[UnityEngine.Random.Range(0, stageFallbacks.Count)];
    }

    /// <summary>
    /// 단일 환자 1명을 생성합니다. 예외는 내부에서 흡수하고 실패 시 null을 반환합니다.
    /// (호출부에서 null이면 폴백으로 대체)
    /// </summary>
    private async UniTask<DiseaseData> GenerateOnePatientAsync(
        int difficulty, string stageId, int patientIndex, int totalPatients, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            DiseaseData disease = await _diseaseGenManager.GenerateDisease(
                difficulty, stageId, patientIndex, totalPatients);
            return disease;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[StagePreloader] 개별 환자 생성 예외: {e.Message}");
            return null;
        }
    }

    private void CompleteRoleAssignment()
    {
        IsRoleAssignmentComplete = true;
        RoleAssignmentCompleted?.Invoke(SurgeonActorNumber);
    }

    private void CompleteDataPrep()
    {
        if (IsDataPrepComplete)
        {
            return;
        }

        IsDataPrepComplete = true;
        _dataPrepTcs?.TrySetResult();
    }

    public override void OnRoomPropertiesUpdate(Hashtable changedProps)
    {
        if (!changedProps.TryGetValue(RoomProperties.IsStageDataPrepCompleteKey, out object value) ||
            value is not bool isComplete ||
            !isComplete)
        {
            return;
        }

        CompleteDataPrep();
    }

    public void Cleanup()
    {
        ReleaseResources();
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        ReleaseResources();
    }

    private void ReleaseResources()
    {
        DisposeCts();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void DisposeCts()
    {
        if (_cts == null) return;
        try { _cts.Cancel(); } catch (ObjectDisposedException) { }
        _cts.Dispose();
        _cts = null;
    }
}
