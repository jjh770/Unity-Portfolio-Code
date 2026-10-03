using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;

public class SlimeManager : MonoBehaviour, IGameDataDomainManager
{
    public static SlimeManager Instance { get; private set; }

    [SerializeField] private SlimeSpecTable _specTable;
    [SerializeField] private SpawnWeightTable _spawnWeightTable;
    [SerializeField, Min(1f)] private float _statsSaveIntervalSeconds = 30f;
    private List<Slime> _slimes = new();

    private IRepository<SlimeStatusSaveData> _statusRepository;
    private SlimeStatus _status;
    private NormalSlimeCollectionStats _collectionStats;
    private bool _statsDirty;
    private float _statsSaveTimer;
    // 도메인 객체를 통째로 내주면 저장을 거치지 않고 상태를 바꿀 수 있다.
    // 밖에서는 개체 목록을 읽기만 하므로 그것만 공개한다.
    public IReadOnlyList<SlimeInstance> ActiveSlimes =>
        _status != null ? _status.ActiveSlimes : Array.Empty<SlimeInstance>();
    // 호출부가 SlimeStatus 내부 구조를 거치지 않도록 최고 등급은 매니저가 직접 노출한다.
    public ESlimeGrade HighestGrade => _status.HighestGrade;
    public EBackgroundTheme SelectedBackgroundTheme =>
        _status.SelectedBackgroundTheme;
    public bool BackgroundUnlockCompleted =>
        _status.BackgroundUnlockCompleted;
    public bool IsBackgroundThemeUnlocked =>
        _status != null &&
        BackgroundThemeRules.IsUnlocked(_status.HighestGrade);
    public bool IsMaxCountExpansionUnlocked =>
        _status != null &&
        _status.HighestGrade >= UnlockGrades.MaxCountExpansion;
    public bool HasExistingProgress =>
        _status != null &&
        (_status.HighestGrade > ESlimeGrade.Grade1 || _status.ActiveSlimes.Count > 0);
    public bool IsDisplayRoomUnlocked =>
        _status != null &&
        _status.HighestGrade >= UnlockGrades.DisplayRoom;
    // 저장을 읽기 전에는 기본값인 켜짐으로 답한다.
    public bool IsAutoSpawnEnabled => _status == null || _status.IsAutoSpawnEnabled;
    public bool IsAutoMergeUnlocked =>
        NormalCollectionCount >= NormalCollectionRules.AutoMergeCount;
    public bool IsShopUnlocked =>
        _status != null &&
        _status.HighestGrade >= UnlockGrades.Shop;
    public bool IsGachaUnlocked =>
        _status != null &&
        _status.HighestGrade >= UnlockGrades.Gacha;
    public bool IsHigherGradeSpawnUnlocked =>
        _spawnWeightTable != null &&
        _status != null &&
        _status.HighestGrade >=
        _spawnWeightTable.GetRequiredHighestGradeForTier(0);
    public ESlimeGrade HigherGradeSpawnUnlockGrade =>
        _spawnWeightTable != null
            ? _spawnWeightTable.GetRequiredHighestGradeForTier(0)
            : ESlimeGrade.Count;
    public int NormalCollectionCount => _status?.NormalCollectionCount ?? 0;
    public int DisplayRoomSlimeCount => _status?.DisplayRoomSlimeCount ?? 0;
    public bool IsGraduationDisplayComplete =>
        _status?.IsGraduationDisplayComplete ?? false;
    public bool IsTicketBulkCollectUnlocked =>
        NormalCollectionCount >= NormalCollectionRules.TicketBulkCollectCount;
    public bool IsOfflineTicketRewardUnlocked =>
        NormalCollectionCount >= NormalCollectionRules.OfflineTicketRewardCount;
    public bool IsHiddenFeverUnlocked =>
        NormalCollectionCount >= NormalCollectionRules.HiddenFeverCount;
    public bool IsMainEndingSeen => _status?.MainEndingSeen ?? false;
    public DateTime GameStartedAtUtc =>
        _status?.GameStartedAtUtc ?? DateTime.MinValue;
    public DateTime? MainEndingReachedAtUtc =>
        _status?.MainEndingReachedAtUtc;
    public GraduationStatistics? GraduationSnapshot => _status?.GraduationSnapshot;
    public long GachaTicketsObtainedTotal => _status?.GachaTicketsObtainedTotal ?? 0;
    public long AutoMergeUseCount => _status?.AutoMergeUseCount ?? 0;
    public float SpecialGachaChance => SpecialGachaFever.GetChance(
        _status?.SpecialGachaMissCount ?? 0);
    // 저장된 문서를 읽었는지. 문서가 없어 기본값으로 출발한 경우와 구분한다.
    public bool HasStoredSaveData { get; private set; }

    // 자연 스폰 상한은 최고 해금 등급으로 결정되므로 슬라임 도메인이 판정한다.
    public bool IsHigherGradeSpawnTierLocked(int currentUpgradeLevel)
    {
        return _spawnWeightTable == null ||
               _status == null ||
               _spawnWeightTable.IsUpgradeTierLocked(
                   _status.HighestGrade,
                   currentUpgradeLevel);
    }

    // 다음 레벨에서 자연 스폰 상한이 올라가는지 판정한다.
    public bool IsSpawnCapRaisedAtNextLevel(int currentUpgradeLevel)
    {
        return _spawnWeightTable != null &&
               _spawnWeightTable.IsSpawnCapRaisedAt(currentUpgradeLevel + 1);
    }

    // 다음 스폰 상한 구간을 열기 위해 필요한 최고 해금 등급.
    public ESlimeGrade GetRequiredHighestGradeForSpawnTier(int currentUpgradeLevel)
    {
        return _spawnWeightTable != null
            ? _spawnWeightTable.GetRequiredHighestGradeForTier(currentUpgradeLevel)
            : ESlimeGrade.Grade1;
    }

    public event Action DataInitialized;
    public event Action<bool> AutoSpawnChanged;
    public event Action<ESlimeGrade> HighestGradeChanged;
    public event Action<int> NormalCollectionCountChanged;
    public event Action<ESlimeGrade> NormalCollectionRegistered;
    // 특별 슬라임을 장식장에 처음 넣어 등록했을 때. 도감 카테고리 연출이 구독한다.
    public event Action PlaygroundChanged;
    public event Action BackgroundThemesChanged;
    public bool IsInitialized { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        foreach (var specData in _specTable.slimeSpecs)
        {
            _slimes.Add(new Slime(specData));
        }
    }

    private void Start()
    {
        _ = InitAsync();
    }

    private void Update()
    {
        if (!_statsDirty ||
            _statusRepository == null ||
            !GameplaySaveGate.IsSavingEnabled)
        {
            return;
        }

        _statsSaveTimer += Time.unscaledDeltaTime;
        if (_statsSaveTimer >= _statsSaveIntervalSeconds)
        {
            Save();
        }
    }

    private async UniTaskVoid InitAsync()
    {
        // 예외가 난 자리가 신고할 종류를 정한다. 읽어 온 문서를 해석하는 구간의 예외만 저장
        // 문제(Unreadable)이고, 그 밖은 앱 코드의 결함이라 초기화 수단을 열면 안 된다.
        ESaveLoadFailure exceptionFailure = ESaveLoadFailure.InitializationFailed;
        try
        {
            await UniTask.Yield();

#if UNITY_ANDROID && !UNITY_EDITOR
            _statusRepository = new HybridRepository<SlimeStatusSaveData>(new PlayerPrefsSlimeStatusRepository(AccountManager.Instance.UserId), new FirebaseSlimeStatusRepository());
#else
            _statusRepository = new PlayerPrefsSlimeStatusRepository(AccountManager.Instance.UserId);
#endif

            SaveLoadResult<SlimeStatusSaveData> loadResult = await _statusRepository.Load();
            if (loadResult.IsFailed)
            {
                // 읽지 못한 세션은 초기화하지 않는다. 세션 처리는 SaveDataLoadGuard가 맡는다.
                SaveDataLoadGuard.Report(
                    loadResult.Failure,
                    $"SlimeStatus : {loadResult.FailureMessage}");
                return;
            }

            HasStoredSaveData = loadResult.IsLoaded;
            // 여기서부터는 읽어 온 문서를 해석한다. 이 구간의 예외는 저장 문제다.
            exceptionFailure = ESaveLoadFailure.Unreadable;
            SlimeStatusSaveData saveData = loadResult.IsLoaded
                ? loadResult.Data
                : SlimeStatusSaveData.Default;

            // 문서 단위의 검증과 변환은 SlimeStatusSaveMapper가 맡는다.
            // 복원할 수 없으면 세션을 차단하는 것까지는 기존과 같다.
            if (!SlimeStatusSaveMapper.TryRestore(
                    saveData,
                    ServerClock.TrustedUtcNow,
                    out SlimeStatus restoredStatus,
                    out NormalSlimeCollectionStats restoredStats,
                    out bool needsMigrationSave,
                    out string failureMessage))
            {
                SaveDataLoadGuard.Report(ESaveLoadFailure.Unreadable, failureMessage);
                return;
            }

            _status = restoredStatus;
            _collectionStats = restoredStats;

            // 문서가 없던 신규 계정은 승격할 원본이 없다. 기본값에서 채운 시작 시각은
            // 메모리에만 두고, 튜토리얼을 마칠 때 세 문서와 함께 기록한다. 여기서 저장하면
            // SlimeStatus 문서만 먼저 생겨 다음 진입의 교차 검사가 결손으로 본다.
            // 다른 도메인이 이미 로드 실패를 신고한 세션도 저장하지 않는다. 원본은 그대로
            // 남으므로 다음 정상 세션에서 같은 승격이 다시 판정된다.
            if (needsMigrationSave &&
                loadResult.IsLoaded &&
                !SaveDataLoadGuard.HasFailure)
            {
                await SaveMigratedAsync();
            }

            // 복원이 끝났다. 이제 호출되는 것은 구독자 코드라 저장과 무관하다.
            exceptionFailure = ESaveLoadFailure.InitializationFailed;
            IsInitialized = true;
            DataInitialized?.Invoke();
        }
        catch (Exception e)
        {
            SaveDataLoadGuard.Report(
                exceptionFailure,
                $"SlimeStatus : 초기화 중 예외 : {e.Message}");
        }
    }

    public Slime Get(ESlimeGrade grade)
    {
        return _slimes.Find(s => s.SpecData.Grade == grade);
    }

    public string GetName(ESlimeGrade grade)
    {
        Slime slime = Get(grade);

        if (slime == null)
        {
            throw new InvalidOperationException($"{grade}에 해당하는 슬라임 스펙이 없습니다.");
        }

        return slime.SpecData.Name;
    }

    public bool CanMerge(Slime slime1, Slime slime2)
    {
        ESlimeGrade maxGrade = _slimes[^1].SpecData.Grade;

        return slime1.CanMerge(slime2) && slime1.SpecData.Grade < maxGrade;
    }

    public bool TryUpdateHighestLevel(ESlimeGrade newGrade)
    {
        if (newGrade <= _status.HighestGrade) return false;

        _status.UpdateHighestGrade(newGrade);
        HighestGradeChanged?.Invoke(newGrade);
        Save();
        return true;
    }

    public void UpdateBackgroundProgress(
        EBackgroundTheme selectedBackgroundTheme,
        bool backgroundUnlockCompleted)
    {
        if (_status.SelectedBackgroundTheme == selectedBackgroundTheme &&
            _status.BackgroundUnlockCompleted == backgroundUnlockCompleted)
        {
            return;
        }

        _status.UpdateBackgroundProgress(
            selectedBackgroundTheme,
            backgroundUnlockCompleted);
        Save();
    }

    public int GetPendingTicketCount()
    {
        return _status?.PendingTickets ?? 0;
    }

    // --- 배경 테마 소유 ---

    public bool IsBackgroundThemeOwned(EBackgroundTheme theme)
    {
        return _status != null && _status.IsBackgroundThemeOwned(theme);
    }

    // 값을 치르는 것은 부르는 쪽이 맡는다. 재화는 다른 도메인이라 여기서 끌어오면
    // 서로 침범한다. UpgradeManager.TryLevelUp과 같은 분담이다.
    public bool TryAddBackgroundTheme(EBackgroundTheme theme)
    {
        if (_status == null || !_status.TryAddBackgroundTheme(theme)) return false;

        Save();
        BackgroundThemesChanged?.Invoke();
        return true;
    }

    // --- 놀이터 오브젝트 ---

    public IReadOnlyList<PlacedPlaygroundObject> PlacedPlaygroundObjects =>
        _status != null
            ? _status.PlacedObjects
            : Array.Empty<PlacedPlaygroundObject>();

    public int GetOwnedPlaygroundObjectCount(EPlaygroundObjectType type)
    {
        return _status?.GetOwnedPlaygroundObjectCount(type) ?? 0;
    }

    public int GetPlacedPlaygroundObjectCount(EPlaygroundObjectType type)
    {
        return _status?.GetPlacedPlaygroundObjectCount(type) ?? 0;
    }

    public bool TryBuyPlaygroundObject(EPlaygroundObjectType type)
    {
        if (_status == null || !_status.TryBuyPlaygroundObject(type)) return false;

        Save();
        PlaygroundChanged?.Invoke();
        return true;
    }

    public bool IsPlaygroundPositionAvailable(float x, float y, int ignoreIndex = -1)
    {
        return _status != null &&
               _status.IsPlaygroundPositionAvailable(x, y, ignoreIndex);
    }

    public bool TryPlacePlaygroundObject(EPlaygroundObjectType type, float x, float y)
    {
        if (_status == null || !_status.TryPlacePlaygroundObject(type, x, y)) return false;

        Save();
        PlaygroundChanged?.Invoke();
        return true;
    }

    public bool TryMovePlacedObject(int index, float x, float y)
    {
        if (_status == null || !_status.TryMovePlacedObject(index, x, y)) return false;

        Save();
        PlaygroundChanged?.Invoke();
        return true;
    }

    public bool TryRemovePlacedObject(int index)
    {
        if (_status == null || !_status.TryRemovePlacedObject(index)) return false;

        Save();
        PlaygroundChanged?.Invoke();
        return true;
    }

    public void SetAutoSpawnEnabled(bool isEnabled)
    {
        if (_status.IsAutoSpawnEnabled == isEnabled) return;

        _status.SetAutoSpawnEnabled(isEnabled);
        Save();
        AutoSpawnChanged?.Invoke(isEnabled);
    }

    // 가챠권이 떨어졌을 때 호출한다.
    public void AddPendingTicket()
    {
        _status.AddPendingTicket();
        Save();
    }

    // 가챠권을 한 장 주웠을 때 호출한다. 저장에 남은 장수가 없으면 false다.
    public bool TryConsumePendingTicket()
    {
        if (!_status.TryConsumePendingTicket()) return false;

        Save();
        return true;
    }

    // 슬라임 스폰 시 호출
    public void AddSlime(SlimeInstance instance)
    {
        _status.AddSlime(instance);
        Save();
    }

    // 이동 검증과 저장을 한 경계에서 처리해 UI가 개체를 직접 변경하지 않게 한다.
    public void MoveSlime(string instanceId, ESlimeLocation location)
    {
        ESlimeGrade? registeredGrade = _status.MoveSlime(instanceId, location);
        if (registeredGrade.HasValue)
        {
            _collectionStats.RecordRegistration(
                registeredGrade.Value,
                ServerClock.TrustedUtcNow);
            MarkStatsDirty();
        }

        Save();
        if (!registeredGrade.HasValue)
        {
            return;
        }

        NormalCollectionRegistered?.Invoke(registeredGrade.Value);
        NormalCollectionCountChanged?.Invoke(_status.NormalCollectionCount);
    }

    public bool IsNormalCollectionRegistered(ESlimeGrade grade)
    {
        return _status != null &&
               _status.IsNormalCollectionRegistered(grade);
    }

    public bool IsTutorialCompleted(string tutorialId)
    {
        return _status != null && _status.IsTutorialCompleted(tutorialId);
    }

    // 여러 개를 한꺼번에 기록해도 저장은 한 번만 한다. 새로 기록한 것이 없으면
    // 저장하지 않는다.
    public void RecordTutorialsCompleted(IEnumerable<string> tutorialIds)
    {
        if (_status == null || tutorialIds == null) return;

        bool isChanged = false;
        foreach (string tutorialId in tutorialIds)
        {
            isChanged |= _status.TryMarkTutorialCompleted(tutorialId);
        }

        if (isChanged)
        {
            Save();
        }
    }

    public bool TryMarkMainEndingSeen()
    {
        if (_status == null || !_status.TryMarkMainEndingSeen()) return false;

        Save();
        return true;
    }

    public bool TryCaptureGraduationSnapshot(DateTime capturedAtUtc)
    {
        if (_status == null || _status.GraduationSnapshot.HasValue) return false;
        if (!_status.TrySetGraduationSnapshot(CreateCurrentStatistics(capturedAtUtc))) return false;
        Save();
        return true;
    }

    public GraduationStatistics CreateCurrentStatistics(DateTime capturedAtUtc)
    {
        long naturalSpawns = 0;
        long mergeCreated = 0;
        long manualTouches = 0;
        double producedPoints = 0d;
        long mostTouches = -1;
        ESlimeGrade mostTouchedGrade = ESlimeGrade.Grade1;
        for (int value = (int)ESlimeGrade.Grade1; value < (int)ESlimeGrade.Count; value++)
        {
            ESlimeGrade grade = (ESlimeGrade)value;
            NormalSlimeCollectionStatsSnapshot stats = GetNormalCollectionStats(grade);
            naturalSpawns = SaturatingAdd(naturalSpawns, stats.NaturalSpawnCount);
            mergeCreated = SaturatingAdd(mergeCreated, stats.MergeCreatedCount);
            manualTouches = SaturatingAdd(manualTouches, stats.ManualTouchCount);
            producedPoints = SaturatingAdd(producedPoints, stats.ProducedPointTotal);
            if (stats.ManualTouchCount > mostTouches)
            {
                mostTouches = stats.ManualTouchCount;
                mostTouchedGrade = grade;
            }
        }

        DateTime startedAt = GameStartedAtUtc == DateTime.MinValue ? capturedAtUtc : GameStartedAtUtc;
        return new GraduationStatistics(
            startedAt, capturedAtUtc, naturalSpawns, mergeCreated, manualTouches,
            producedPoints, mostTouchedGrade, Math.Max(0, mostTouches),
            GachaTicketsObtainedTotal, AutoMergeUseCount);
    }

    private static long SaturatingAdd(long left, long right)
    {
        if (right <= 0) return left;
        return left > long.MaxValue - right ? long.MaxValue : left + right;
    }

    private static double SaturatingAdd(double left, double right)
    {
        if (right <= 0d || double.IsNaN(right)) return left;
        double result = left + right;
        return double.IsInfinity(result) ? double.MaxValue : result;
    }

    // Phase 5의 스페셜 결과 판정 지점에서 호출한다. 해금 전에는 상태를 만들지 않고,
    // 스페셜 성공 시 3%로 초기화하며 일반 결과면 최대 10%까지 0.5%p씩 올린다.
    public void RecordSpecialGachaResult(bool wasSpecial)
    {
        if (_status == null || !_status.RecordSpecialGachaResult(wasSpecial)) return;

        Save();
    }

    public NormalSlimeCollectionStatsSnapshot GetNormalCollectionStats(
        ESlimeGrade grade)
    {
        return _collectionStats != null
            ? _collectionStats.Get(grade)
            : default;
    }

    // 가챠로 슬라임을 얻었을 때 부른다. 특별한 슬라임도 같은 등급의 기록에 합산한다.
    public void RecordGachaObtained(ESlimeGrade grade)
    {
        if (_collectionStats == null) return;

        _collectionStats.RecordGachaObtained(grade);
        MarkStatsDirty();
    }

    // 가챠권이 지갑에 들어간 순간에 부른다. 필드에서 주운 것, 오프라인 보상, 튜토리얼의
    // 한 장이 해당하고, 뽑기 실패로 돌려받은 환불은 해당하지 않는다.
    public void RecordGachaTicketsObtained(int count)
    {
        if (_status == null || count <= 0) return;

        _status.RecordGachaTicketsObtained(count);
        MarkStatsDirty();
    }

    // 자동 합성 버튼이 실제로 한 쌍 이상 합쳤을 때 한 번 센다. 합칠 쌍이 없어 안내만 뜬 경우는 세지 않는다.
    public void RecordAutoMergeUse()
    {
        if (_status == null) return;

        _status.RecordAutoMergeUse();
        MarkStatsDirty();
    }

    public void RecordNaturalSpawn(ESlimeGrade grade)
    {
        if (_collectionStats == null) return;

        _collectionStats.RecordNaturalSpawn(grade);
        MarkStatsDirty();
    }

    // 특별한 슬라임의 생산도 같은 등급의 일반 기록에 합산한다. 도감에는 등급 한 칸의
    // 기록만 있으므로 종류로 나누지 않는다.
    public void RecordProduction(
        ESlimeGrade grade,
        EClickType clickType,
        double point)
    {
        if (_collectionStats == null) return;

        _collectionStats.RecordProduction(grade, clickType, point);
        MarkStatsDirty();
    }

    // 오프라인 보상으로 받은 포인트를 등급별 생산 기록에 더한다. 자동 생산과 같은 기록이라
    // 터치 횟수는 늘리지 않는다. 배열은 1등급부터 순서대로 등급별 몫이고 합이 받은 포인트다.
    public void RecordOfflineProduction(IReadOnlyList<double> pointsByGrade)
    {
        if (_collectionStats == null || pointsByGrade == null) return;

        for (int i = 0; i < pointsByGrade.Count; i++)
        {
            double point = pointsByGrade[i];
            if (point <= 0d) continue;

            _collectionStats.RecordProduction(
                (ESlimeGrade)((int)ESlimeGrade.Grade1 + i),
                EClickType.Auto,
                point);
        }

        MarkStatsDirty();
    }

    // 지금 장식장에 전시 중인지. 도감 등록 여부와 다르다.
    //
    // 등록은 한 번 들어가면 꺼내도 남는 영구 기록이고, 이쪽은 현재 상태다.
    // 도감이 전시 중인 개체에만 표식을 붙이는 데 쓴다.
    public bool IsDisplayedInDisplayRoom(ESlimeGrade grade)
    {
        return _status != null &&
               _status.HasDisplayRoomSlime(grade);
    }

    // 장식장에 있는 것이 특별한 슬라임인지. 도감이 특별한 모습으로 보여 줄지 정한다.
    public bool IsSpecialDisplayedInDisplayRoom(ESlimeGrade grade)
    {
        return _status != null &&
               _status.HasSpecialDisplayRoomSlime(grade);
    }

    // 같은 등급은 종류와 관계없이 한 마리만 넣을 수 있다.
    public bool CanMoveToDisplayRoom(ESlimeGrade grade)
    {
        return _status != null &&
               !_status.HasDisplayRoomSlime(grade);
    }

    // 한 발동의 합성, 통계, 최고 등급을 모두 반영한 뒤 저장은 한 번만 한다.
    public void MergeSlimesBatch(IReadOnlyList<SlimeMergeRequest> requests)
    {
        if (requests == null || requests.Count == 0) return;

        _status.MergeSlimesBatch(requests);

        ESlimeGrade highestCreated = _status.HighestGrade;
        foreach (SlimeMergeRequest request in requests)
        {
            _collectionStats?.RecordMergeCreated(request.ToGrade);
            if (request.ToGrade > highestCreated)
            {
                highestCreated = request.ToGrade;
            }
        }

        MarkStatsDirty();
        bool highestChanged = highestCreated > _status.HighestGrade;
        if (highestChanged)
        {
            _status.UpdateHighestGrade(highestCreated);
        }

        Save();
        if (highestChanged)
        {
            HighestGradeChanged?.Invoke(highestCreated);
        }
    }

    private void Save()
    {
        SaveCurrentAsync().Forget();
    }

    // 앱이 내려갈 때 미뤄 둔 클라우드 쓰기를 지금 내보낸다.
    public void FlushPendingSave()
    {
        _statusRepository?.FlushPendingSave();
    }

    public UniTask SaveCurrentAsync()
    {
        if (!GameplaySaveGate.IsSavingEnabled)
        {
            return UniTask.CompletedTask;
        }

        // 초기화가 끝나지 않았거나 로드가 막힌 세션에는 저장할 상태 자체가 없다.
        // 예외를 삼키는 것과 다르다. 쓸 내용이 없으니 쓰지 않는 것이고, 그대로 두면
        // 종료 시점의 저장이 예외로 끊겨 뒤따르는 정리까지 건너뛴다.
        if (_status == null)
        {
            return UniTask.CompletedTask;
        }

        _statsDirty = false;
        _statsSaveTimer = 0f;
        return _statusRepository.Save(BuildSaveData());
    }

    // 데이터 형식 승격은 튜토리얼 진행 저장 게이트와 무관하게 반영한다.
    private UniTask SaveMigratedAsync()
    {
        return _statusRepository.Save(BuildSaveData());
    }

    private SlimeStatusSaveData BuildSaveData()
    {
        return SlimeStatusSaveMapper.Build(_status, _collectionStats);
    }

    private void MarkStatsDirty()
    {
        _statsDirty = true;
    }
}
