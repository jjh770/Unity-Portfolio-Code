/*
 * 역할: 컷씬과 Gameplay 씬 사이에서 스테이지 런타임 데이터를 보존하고 초기화 순서를 연결합니다.
 * 핵심 설계: 방장 양도와 비동기 씬 복제 지연을 고려해 필요한 매니저·프리팹·데이터가 준비될 때까지 제한 시간 동안 기다립니다.
 */
using Cysharp.Threading.Tasks;
using Photon.Pun;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DontDillyDally.StageFlow
{
    /// <summary>
    /// 컷씬 씬에서 시작되어 Gameplay 씬까지 유지되는 브리지입니다.
    /// Cutscene: StagePreloader를 초기화합니다.
    /// Gameplay: StageFlowManager를 런타임 데이터로 초기화합니다.
    /// WaitingRoom: 자신을 정리하고 파괴합니다.
    /// </summary>
    public class StageFlowBootstrapper : MonoBehaviour
    {
        private const float StageFlowManagerWaitTimeoutSec = 10f;
        private const float RoomDataReadyTimeoutSec = 5f;
        private const string WaitingRoomSceneName = "WaitingRoom";

        public static StageFlowBootstrapper Instance { get; private set; }
        public static event Action StageFlowReady;

        public bool IsStageFlowReady { get; private set; }

        // 컷씬에서 준비되어 Gameplay 초기화에 전달할 스테이지 런타임 데이터입니다.
        private StageRuntimeData _stageData;
        private GameObject _stagePrefab;
        // 마스터가 생성한 네트워크 스테이지 프리팹을 정리하기 위해 보관합니다.
        private GameObject _spawnedStageInstance;
        private bool _isCleaningUp;
        // 씬 이탈 또는 객체 파괴 시 진행 중인 비동기 대기를 취소합니다.
        private CancellationTokenSource _cts;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            IsStageFlowReady = false;
            _cts = new CancellationTokenSource();
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnUnitySceneLoaded;
        }

        private void Start()
        {
            BeginBootstrapAsync(_cts.Token).Forget();
        }

        // 방장 양도 직후 사이클 2 진입 시 RoomDataManager/StagePreloader 동기화가 Start 시점보다
        // 한 박자 늦어 _stageData/_stagePrefab이 null로 잡히는 경우가 있다. 짧게 폴링하여 기다린다.
        private async UniTaskVoid BeginBootstrapAsync(CancellationToken ct)
        {
            float deadline = Time.unscaledTime + RoomDataReadyTimeoutSec;

            while (!ct.IsCancellationRequested && Time.unscaledTime < deadline)
            {
                if (RoomDataManager.Instance != null && StagePreloader.Instance != null)
                {
                    StageRuntimeData stageData = RoomDataManager.Instance.CreateCurrentStageRuntimeData();
                    GameObject stagePrefab = RoomDataManager.Instance.CurrentStagePrefab;

                    if (stageData != null && stagePrefab != null)
                    {
                        _stageData = stageData;
                        _stagePrefab = stagePrefab;
                        break;
                    }
                }

                await UniTask.Yield(ct);
            }

            if (ct.IsCancellationRequested)
            {
                return;
            }

            if (_stageData == null || _stagePrefab == null || StagePreloader.Instance == null)
            {
                Debug.LogError($"[StageFlowBootstrapper] 부트스트랩 준비 실패 - stageData={_stageData != null}, stagePrefab={_stagePrefab != null}, preloader={StagePreloader.Instance != null}");
                return;
            }

            StagePreloader.Instance.Initialize(_stageData);
            SceneLoadManager.Instance.OnSceneLoadComplete += HandleSceneLoadComplete;
        }

        // 비마스터 클라이언트에서는 WaitingRoom 복귀가 SceneLoadManager.BeginSceneLoad를 거치지
        // 않아 OnSceneLoadComplete(WaitingRoom)이 발화하지 않는다. 누가 씬 전환을 트리거했는지와
        // 무관하게 Unity의 SceneManager.sceneLoaded로 감지해 Cleanup을 보장한다.
        private void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == WaitingRoomSceneName)
            {
                Cleanup();
            }
        }

        private void HandleSceneLoadComplete(ESceneType sceneType)
        {
            if (sceneType == ESceneType.Gameplay)
            {
                InitializeGameplay().Forget();
            }
            else if (sceneType == ESceneType.WaitingRoom)
            {
                Cleanup();
            }
        }

        /// <summary>
        /// 데이터 준비와 StageFlowManager 생성을 기다린 뒤 스테이지를 최종 초기화합니다.
        /// </summary>
        private async UniTaskVoid InitializeGameplay()
        {
            if (PhotonNetwork.IsMasterClient)
            {
                // Start 단계에서 null이었던 값이 Gameplay 진입 전에 동기화되었을 수 있어 다시 조회한다.
                if (_stagePrefab == null && RoomDataManager.Instance != null)
                {
                    _stagePrefab = RoomDataManager.Instance.CurrentStagePrefab;
                }
                if (_stageData == null && RoomDataManager.Instance != null)
                {
                    _stageData = RoomDataManager.Instance.CreateCurrentStageRuntimeData();
                    if (_stageData != null && StagePreloader.Instance != null)
                    {
                        StagePreloader.Instance.Initialize(_stageData);
                    }
                }

                if (_stagePrefab == null)
                {
                    Debug.LogError("[StageFlowBootstrapper] Stage Prefab이 설정되지 않았습니다.");
                    return;
                }

                // 마스터는 스테이지 프리팹을 먼저 생성해 비마스터로의 복제 지연을 줄인다.
                CleanupSpawnedStageInstance();
                _spawnedStageInstance = PhotonNetwork.Instantiate(_stagePrefab.name, Vector3.zero, Quaternion.identity);
            }

            // 마스터/비마스터 공통 — DataPrep 완료까지 대기한다.
            // 컷씬 Timeline이 Gemini/TTS 생성보다 짧아 씬 전환이 선행될 때,
            // UI가 IsStageFlowReady=false 상태에서 조용히 대기하다 준비 완료 후 일괄 바인딩되도록 한다.
            if (StagePreloader.Instance != null)
            {
                await StagePreloader.Instance.WaitForDataPrep(_cts.Token);
            }

            float deadline = Time.unscaledTime + StageFlowManagerWaitTimeoutSec;
            while (StageFlowManager.Instance == null &&
                   Time.unscaledTime < deadline &&
                   !_cts.Token.IsCancellationRequested)
            {
                await UniTask.Yield(_cts.Token);
            }

            if (StageFlowManager.Instance == null)
            {
                Debug.LogError("[StageFlowBootstrapper] Gameplay 씬에서 StageFlowManager 인스턴스가 준비되지 않았습니다.");
                return;
            }

            EventManager.Instance?.ResetForNewGame();
            CommentaryController.Instance?.ResetForNewGame();

            StageFlowManager.Instance.Initialize(_stageData);
            IsStageFlowReady = true;
            StageFlowReady?.Invoke();
            StagePreloader.Instance?.Cleanup();
        }

        /// <summary>
        /// WaitingRoom 복귀 시 중복 정리를 방지하며 네트워크 인스턴스와 이벤트 구독을 해제합니다.
        /// </summary>
        private void Cleanup()
        {
            if (_isCleaningUp)
            {
                return;
            }

            CleanupSpawnedStageInstance();
            ReleaseResources();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            ReleaseResources();
            SceneManager.sceneLoaded -= OnUnitySceneLoaded;

            if (Instance == this)
            {
                Instance = null;
                IsStageFlowReady = false;
            }
        }

        private void ReleaseResources()
        {
            if (_isCleaningUp)
            {
                return;
            }

            _isCleaningUp = true;
            IsStageFlowReady = false;

            if (SceneLoadManager.Instance != null)
            {
                SceneLoadManager.Instance.OnSceneLoadComplete -= HandleSceneLoadComplete;
            }
        }

        /// <summary>
        /// 현재 권한과 방 상태에 따라 PhotonNetwork.Destroy 또는 Unity Destroy를 선택합니다.
        /// </summary>
        public bool CleanupSpawnedStageInstance()
        {
            if (_spawnedStageInstance == null)
            {
                return false;
            }

            GameObject target = _spawnedStageInstance;
            _spawnedStageInstance = null;

            PhotonView pv = target.GetComponent<PhotonView>();
            if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient && pv != null && pv.IsMine)
            {
                PhotonNetwork.Destroy(target);
            }
            else
            {
                Destroy(target);
            }

            return true;
        }
    }
}
