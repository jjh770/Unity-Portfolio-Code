using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;

public class SlimeManager : MonoBehaviour
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
    public SlimeStatus Status => _status;
    public ESlimeGrade HighestGrade => _status.HighestGrade;
    public EGameStage CurrentStage => _status.CurrentStage;
    public bool SkyIntroCompleted => _status.SkyIntroCompleted;
    public bool IsSkyUnlocked =>
        _status != null &&
        GameStageRules.IsSkyUnlocked(_status.HighestGrade);
    public bool HasExistingProgress =>
        _status != null &&
        (_status.HighestGrade > ESlimeGrade.Grade1 || _status.ActiveSlimes.Count > 0);
    public bool IsDisplayRoomUnlocked =>
        _status != null &&
        _status.HighestGrade >= UnlockGrades.DisplayRoom;
    public bool IsHigherGradeSpawnUnlocked =>
        _spawnWeightTable != null &&
        _status != null &&
        _status.HighestGrade >=
        _spawnWeightTable.GetRequiredHighestGradeForTier(0);
    public int NormalCollectionCount => _status?.NormalCollectionCount ?? 0;

    public bool IsHigherGradeSpawnTierLocked(int currentUpgradeLevel)
    {
        return _spawnWeightTable == null ||
               _status == null ||
               _spawnWeightTable.IsUpgradeTierLocked(
                   _status.HighestGrade,
                   currentUpgradeLevel);
    }

    public bool IsSpawnCapRaisedAtNextLevel(int currentUpgradeLevel)
    {
        return _spawnWeightTable != null &&
               _spawnWeightTable.IsSpawnCapRaisedAt(currentUpgradeLevel + 1);
    }

    public ESlimeGrade GetRequiredHighestGradeForSpawnTier(int currentUpgradeLevel)
    {
        return _spawnWeightTable != null
            ? _spawnWeightTable.GetRequiredHighestGradeForTier(currentUpgradeLevel)
            : ESlimeGrade.Grade1;
    }

    public static event Action OnDataInitialized;
    public static event Action<ESlimeGrade> OnHighestGradeChanged;
    public static event Action<ESlimeGrade> OnNormalCollectionRegistered;
    public static event Action<int> OnNormalCollectionCountChanged;

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

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && _statsDirty)
        {
            Save();
        }
    }

    private void OnApplicationQuit()
    {
        if (_statsDirty)
        {
            Save();
        }
    }

    private async UniTaskVoid InitAsync()
    {
        await UniTask.Yield();

#if UNITY_ANDROID && !UNITY_EDITOR
        _statusRepository = new HybridRepository<SlimeStatusSaveData>(new PlayerPrefsSlimeStatusRepository(AccountManager.Instance.UserId), new FirebaseSlimeStatusRepository());
#else
        _statusRepository = new PlayerPrefsSlimeStatusRepository(AccountManager.Instance.UserId);
#endif

        SlimeStatusSaveData saveData;
        try
        {
            saveData = await _statusRepository.Load();
        }
        catch (UnsupportedSaveVersionException e)
        {
            Debug.LogError(
                $"[SlimeManager] 현재 앱에서 저장 데이터를 불러올 수 없습니다. " +
                $"앱을 업데이트해 주세요. {e.Message}");
            return;
        }

        var activeSlimes = new List<SlimeInstance>();
        var restoredIds = new HashSet<string>();
        foreach (SlimeInstanceSaveData instanceData in saveData.ActiveSlimes)
        {
            if (instanceData == null)
            {
                Debug.LogWarning("비어 있는 슬라임 저장 항목을 건너뜁니다.");
                continue;
            }

            try
            {
                SlimeInstance instance = instanceData.ToDomain();
                if (!restoredIds.Add(instance.InstanceId))
                {
                    Debug.LogWarning(
                        $"중복된 슬라임 개체를 건너뜁니다: {instance.InstanceId}");
                    continue;
                }

                activeSlimes.Add(instance);
            }
            catch (ArgumentException e)
            {
                Debug.LogWarning(
                    $"복원할 수 없는 슬라임 개체를 건너뜁니다: {e.Message}");
            }
        }

        var registeredNormalCollection = new List<ESlimeGrade>(
            GetRegisteredNormalCollection(saveData.NormalCollectionRegistered));
        var registeredBeforeRestore = new HashSet<ESlimeGrade>(
            registeredNormalCollection);
        _collectionStats = new NormalSlimeCollectionStats(saveData);
        _status = new SlimeStatus(
            saveData.GetHighestGrade(),
            activeSlimes,
            registeredNormalCollection,
            (EGameStage)saveData.CurrentStage,
            saveData.SkyIntroCompleted);

        bool restoredRegistrationStats = false;
        DateTime restoredAt = DateTime.UtcNow;
        for (int gradeValue = (int)ESlimeGrade.Grade1;
             gradeValue < (int)ESlimeGrade.Count;
             gradeValue++)
        {
            ESlimeGrade grade = (ESlimeGrade)gradeValue;
            if (_status.IsNormalCollectionRegistered(grade) &&
                !registeredBeforeRestore.Contains(grade))
            {
                restoredRegistrationStats |=
                    _collectionStats.RecordRegistration(grade, restoredAt);
            }
        }

        if (saveData.WasMigrated ||
            _status.NormalCollectionCount > registeredNormalCollection.Count ||
            restoredRegistrationStats)
        {
            await SaveMigratedAsync();
        }

        OnDataInitialized?.Invoke();
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
        OnHighestGradeChanged?.Invoke(newGrade);
        Save();
        return true;
    }

    public bool IsMaxLevelUnlocked()
    {
        ESlimeGrade maxGrade = _slimes[^1].SpecData.Grade;
        return _status.HighestGrade >= maxGrade;
    }

    public void UpdateStageProgress(
        EGameStage currentStage,
        bool skyIntroCompleted)
    {
        if (_status.CurrentStage == currentStage &&
            _status.SkyIntroCompleted == skyIntroCompleted)
        {
            return;
        }

        _status.UpdateStageProgress(currentStage, skyIntroCompleted);
        Save();
    }

    public void AddSlime(SlimeInstance instance)
    {
        _status.AddSlime(instance);
        Save();
    }

    public void MoveSlime(string instanceId, ESlimeLocation location)
    {
        ESlimeGrade? registeredGrade = _status.MoveSlime(instanceId, location);
        if (registeredGrade.HasValue)
        {
            _collectionStats.RecordRegistration(
                registeredGrade.Value,
                DateTime.UtcNow);
            MarkStatsDirty();
        }

        Save();
        if (!registeredGrade.HasValue)
        {
            return;
        }

        OnNormalCollectionRegistered?.Invoke(registeredGrade.Value);
        OnNormalCollectionCountChanged?.Invoke(_status.NormalCollectionCount);
    }

    public bool IsNormalCollectionRegistered(ESlimeGrade grade)
    {
        return _status != null &&
               _status.IsNormalCollectionRegistered(grade);
    }

    public NormalSlimeCollectionStatsSnapshot GetNormalCollectionStats(
        ESlimeGrade grade)
    {
        return _collectionStats != null
            ? _collectionStats.Get(grade)
            : default;
    }

    public void RecordNaturalSpawn(ESlimeGrade grade)
    {
        if (_collectionStats == null) return;

        _collectionStats.RecordNaturalSpawn(grade);
        MarkStatsDirty();
    }

    public void RecordProduction(
        ESlimeGrade grade,
        EClickType clickType,
        double point)
    {
        if (_collectionStats == null) return;

        _collectionStats.RecordProduction(grade, clickType, point);
        MarkStatsDirty();
    }

    public bool CanMoveToDisplayRoom(ESlimeGrade grade, bool isSpecial)
    {
        return _status != null &&
               !_status.HasDisplayRoomSlime(grade, isSpecial);
    }

    public void MergeSlime(
        string keeperId,
        string removedId,
        ESlimeGrade toGrade)
    {
        _status.MergeSlimes(keeperId, removedId, toGrade);
        _collectionStats?.RecordMergeCreated(toGrade);
        MarkStatsDirty();
        Save();
    }

    private void Save()
    {
        SaveCurrentAsync().Forget();
    }

    public UniTask SaveCurrentAsync()
    {
        if (!GameplaySaveGate.IsSavingEnabled)
        {
            return UniTask.CompletedTask;
        }

        _statsDirty = false;
        _statsSaveTimer = 0f;
        return _statusRepository.Save(BuildSaveData());
    }

    private UniTask SaveMigratedAsync()
    {
        return _statusRepository.Save(BuildSaveData());
    }

    private SlimeStatusSaveData BuildSaveData()
    {
        var saveData = new SlimeStatusSaveData
        {
            SchemaVersion = SaveSchema.SlimeCurrentVersion,
            HighestGrade = (int)_status.HighestGrade,
            ActiveSlimes = new List<SlimeInstanceSaveData>(),
            CurrentStage = (int)_status.CurrentStage,
            SkyIntroCompleted = _status.SkyIntroCompleted,
            NormalCollectionRegistered = BuildNormalCollectionSaveData(),
            NormalFirstRegisteredAt = _collectionStats.BuildFirstRegisteredAt(),
            NormalNaturalSpawnCounts = _collectionStats.BuildNaturalSpawnCounts(),
            NormalMergeCreatedCounts = _collectionStats.BuildMergeCreatedCounts(),
            NormalManualTouchCounts = _collectionStats.BuildManualTouchCounts(),
            NormalProducedPointTotals = _collectionStats.BuildProducedPointTotals(),
        };

        foreach (SlimeInstance instance in _status.ActiveSlimes)
        {
            saveData.ActiveSlimes.Add(
                SlimeInstanceSaveData.FromDomain(instance));
        }

        return saveData;
    }

    private static IEnumerable<ESlimeGrade> GetRegisteredNormalCollection(
        IReadOnlyList<bool> registered)
    {
        if (registered == null)
        {
            yield break;
        }

        int count = Math.Min(
            registered.Count,
            SlimeStatusSaveData.NormalCollectionSize);
        for (int i = 0; i < count; i++)
        {
            if (registered[i])
            {
                yield return (ESlimeGrade)(
                    (int)ESlimeGrade.Grade1 + i);
            }
        }
    }

    private List<bool> BuildNormalCollectionSaveData()
    {
        List<bool> registered =
            SlimeStatusSaveData.CreateEmptyNormalCollection();
        for (int i = 0; i < registered.Count; i++)
        {
            ESlimeGrade grade = (ESlimeGrade)(
                (int)ESlimeGrade.Grade1 + i);
            registered[i] = _status.IsNormalCollectionRegistered(grade);
        }

        return registered;
    }

    private void MarkStatsDirty()
    {
        _statsDirty = true;
    }
}
