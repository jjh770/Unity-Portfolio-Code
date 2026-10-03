using Firebase.Firestore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

[Serializable]
[FirestoreData]
public sealed class LegacySlimeEntry
{
    [FirestoreProperty]
    public int Grade { get; set; }

    [FirestoreProperty]
    public int Count { get; set; }

    public LegacySlimeEntry() { }
}

[Serializable]
[FirestoreData]
public sealed class LegacySlimeStatusSaveData : ISaveData
{
    [FirestoreProperty]
    public int SchemaVersion { get; set; }

    [FirestoreProperty]
    public int HighestGrade { get; set; }

    [FirestoreProperty]
    public List<LegacySlimeEntry> ActiveSlimes { get; set; } = new();

    [FirestoreProperty]
    public int CurrentStage { get; set; }

    [FirestoreProperty]
    public bool SkyIntroCompleted { get; set; }

    [FirestoreProperty]
    public string LastSaveTime { get; set; }
}

// 장식장에 놓인 놀이터 오브젝트 하나. 종류와 자리만 담는다.
[Serializable]
[FirestoreData]
public sealed class PlacedObjectSaveData
{
    [FirestoreProperty]
    public int Type { get; set; }

    [FirestoreProperty]
    public float X { get; set; }

    [FirestoreProperty]
    public float Y { get; set; }

    public PlacedObjectSaveData() { }

    public PlacedObjectSaveData(int type, float x, float y)
    {
        Type = type;
        X = x;
        Y = y;
    }
}

[Serializable]
[FirestoreData]
public sealed class SlimeStatusSaveData : ISaveData
{
    public const int NormalCollectionSize =
        (int)ESlimeGrade.Count - (int)ESlimeGrade.Grade1;

    [FirestoreProperty]
    public int SchemaVersion { get; set; }

    [FirestoreProperty]
    public int HighestGrade { get; set; }

    [FirestoreProperty]
    public List<SlimeInstanceSaveData> ActiveSlimes { get; set; } = new();

    // v8 이하 배경 선택 승격 전용. 새 저장에서는 기본값으로만 남는다.
    [FirestoreProperty]
    public int CurrentStage { get; set; }

    [FirestoreProperty]
    public bool SkyIntroCompleted { get; set; }

    // v8 이하 호환 필드는 위에 남기고, v9부터는 배경 선택과 해금 연출 완료를
    // 스테이지 진행과 분리해 저장한다.
    [FirestoreProperty]
    public int SelectedBackgroundTheme { get; set; }

    [FirestoreProperty]
    public bool BackgroundUnlockCompleted { get; set; }

    // v5~v8의 스테이지별 티켓 승격 전용. 새 저장에서는 둘 다 0이다.
    [FirestoreProperty]
    public int PendingGroundTickets { get; set; }

    [FirestoreProperty]
    public int PendingSkyTickets { get; set; }

    // v9부터 모든 슬라임과 티켓이 한 필드에 있으므로 미수령 수량도 하나로 저장한다.
    [FirestoreProperty]
    public int PendingTickets { get; set; }

    // 플레이어가 자연 스폰을 껐는지. 켜짐이 기본값이라 일부러 뒤집어 담는다.
    //
    // Firestore도 JSON도 없는 필드를 C# 기본값으로 남긴다. AutoSpawnEnabled로
    // 두면 v5 이하 문서가 전부 "꺼짐"으로 읽혀 기존 플레이어의 스폰이 멎는다.
    // 승격 함수에서 true로 채우는 방법도 있지만, 그 함수는 어느 버전에서 왔는지
    // 모르는 채 실행되므로 나중에 v7이 생기면 플레이어가 꺼 둔 설정을 도로
    // 켜 버린다. 뒤집어 담으면 그런 자리가 아예 없다.
    [FirestoreProperty]
    public bool AutoSpawnDisabled { get; set; }

    // v6에서 쓰던 자동 합성 ON/OFF 값이다. 지금은 버튼을 누를 때 한 번만 발동하므로
    // 런타임에서는 읽지 않는다. 기존 로컬 JSON과 Firestore 문서의 필드 호환을 위해
    // 이름과 타입만 유지하고, 새 저장은 항상 false를 쓴다.
    [FirestoreProperty]
    public bool AutoMergeEnabled { get; set; }

    // 일반 도감 20종 완성 후 메인 엔딩을 이미 본 적이 있는지.
    // 이전 문서의 기본값 false가 정확한 미확인 상태라 별도 승격값이 필요 없다.
    [FirestoreProperty]
    public bool MainEndingSeen { get; set; }

    // v11. 엔딩 크레딧에 표시할 여정의 시작과 졸업 시각이다.
    // UTC round-trip 문자열로 저장해 기기 시간대가 달라도 같은 순간을 가리킨다.
    [FirestoreProperty]
    public string GameStartedAtUtc { get; set; }

    [FirestoreProperty]
    public string MainEndingReachedAtUtc { get; set; }

    // v15. 최초 졸업식 재생 시점에 한 번만 기록하고 이후에는 바꾸지 않는다.
    [FirestoreProperty]
    public string GraduationSnapshotAtUtc { get; set; }

    [FirestoreProperty]
    public long GraduationNaturalSpawnCount { get; set; }

    [FirestoreProperty]
    public long GraduationMergeCreatedCount { get; set; }

    [FirestoreProperty]
    public long GraduationManualTouchCount { get; set; }

    [FirestoreProperty]
    public double GraduationProducedPointTotal { get; set; }

    [FirestoreProperty]
    public int GraduationMostTouchedGrade { get; set; }

    [FirestoreProperty]
    public long GraduationMostTouchedCount { get; set; }

    [FirestoreProperty]
    public long GraduationGachaTicketsObtainedTotal { get; set; }

    [FirestoreProperty]
    public long GraduationAutoMergeUseCount { get; set; }

    // 스페셜 가챠에 연속 실패한 횟수. 피버 해금 여부는 도감 수에서 파생한다.
    // 이전 문서의 기본값 0은 기본 확률 3%를 뜻한다.
    [FirestoreProperty]
    public int SpecialGachaMissCount { get; set; }

    // v15. 지금까지 얻은 가챠권의 누적과 자동 합성을 쓴 횟수다. 필드가 없는 문서는 0으로 읽혀
    // "기록 없음"이 된다.
    [FirestoreProperty]
    public long GachaTicketsObtainedTotal { get; set; }

    [FirestoreProperty]
    public long AutoMergeUseCount { get; set; }

    // 이 계정이 마친 튜토리얼 식별자. 로컬 완료 표시는 앱 데이터를 지우면 사라진다.
    // 기본값은 빈 목록이다. 미리 채운 목록은 로컬 JSON 읽기에서 뒤에 이어 붙는다.
    // 필드가 없는 v7 이하 문서는 빈 목록, 즉 "기록 없음"으로 읽혀 지금까지와 같다.
    [FirestoreProperty]
    public List<string> CompletedTutorials { get; set; } = new();

    // v10. 플레이어가 장식장에 직접 놓은 오브젝트다. 슬라임과 달리 자리를 저장한다.
    // 미리 채운 목록은 로컬 JSON 읽기에서 뒤에 이어 붙으므로 빈 목록으로 둔다.
    [FirestoreProperty]
    public List<PlacedObjectSaveData> PlacedObjects { get; set; } = new();

    // v10. 종류별로 사 둔 개수다. 놓은 것도 포함한 총량이라, 아직 안 놓은 수는
    // 이 값에서 놓은 수를 뺀 값이 된다. 두 수를 따로 저장하면 서로 어긋날 수 있다.
    [FirestoreProperty]
    public List<int> OwnedPlaygroundObjects { get; set; } = new();

    // v10. 상점에서 산 배경 테마 번호다. 기본 제공되는 땅은 여기에 담지 않는다.
    [FirestoreProperty]
    public List<int> OwnedBackgroundThemes { get; set; } = new();

    [FirestoreProperty]
    public List<bool> NormalCollectionRegistered { get; set; } =
        CreateEmptyNormalCollection();

    // v12. 예전 특별 도감의 등록 상태다. 특별 도감은 없어져 읽기만 한다. 등록한 등급은
    // 로드할 때 일반 도감 등록으로 옮기고, 새 저장에는 빈 목록을 쓴다. 미리 채운 목록은
    // 로컬 JSON 읽기에서 뒤에 이어 붙으므로 초기값은 빈 목록으로 둔다.
    [FirestoreProperty]
    public List<bool> SpecialCollectionRegistered { get; set; } = new();

    // v13. 예전 특별 도감의 등급별 기록이다. 이것도 읽지 않고 새 저장에는 빈 목록을 쓴다.
    // 기록은 일반 도감 통계에 합산하지 않고 버린다. 필드를 지우지 않는 것은 이미 이 형식으로
    // 저장된 문서와 클라우드 문서가 그대로 읽혀야 하기 때문이다.
    [FirestoreProperty]
    public List<string> SpecialFirstRegisteredAt { get; set; } = new();

    [FirestoreProperty]
    public List<long> SpecialObtainedCounts { get; set; } = new();

    [FirestoreProperty]
    public List<long> SpecialManualTouchCounts { get; set; } = new();

    [FirestoreProperty]
    public List<double> SpecialProducedPointTotals { get; set; } = new();

    [FirestoreProperty]
    public List<string> NormalFirstRegisteredAt { get; set; } =
        CreateEmptyStringStats();

    [FirestoreProperty]
    public List<long> NormalNaturalSpawnCounts { get; set; } =
        CreateEmptyLongStats();

    [FirestoreProperty]
    public List<long> NormalMergeCreatedCounts { get; set; } =
        CreateEmptyLongStats();

    [FirestoreProperty]
    public List<long> NormalManualTouchCounts { get; set; } =
        CreateEmptyLongStats();

    [FirestoreProperty]
    public List<double> NormalProducedPointTotals { get; set; } =
        CreateEmptyDoubleStats();

    // v14. 등급별로 가챠에서 얻은 횟수다. 다른 통계와 같은 스무 칸이고, 필드가 없는 이전 문서는
    // 0으로 채워진 목록으로 읽히며 그것이 "가챠로 얻은 기록 없음"이다.
    [FirestoreProperty]
    public List<long> NormalGachaObtainedCounts { get; set; } =
        CreateEmptyLongStats();

    [FirestoreProperty]
    public string LastSaveTime { get; set; }

    [JsonIgnore]
    public bool WasMigrated { get; set; }

    public ESlimeGrade GetHighestGrade() => (ESlimeGrade)HighestGrade;

    public static SlimeStatusSaveData Default => new SlimeStatusSaveData
    {
        SchemaVersion = GameDataDomains.SlimeStatus.CurrentSchemaVersion,
        HighestGrade = (int)ESlimeGrade.Grade1,
        ActiveSlimes = new List<SlimeInstanceSaveData>(),
        SelectedBackgroundTheme = (int)EBackgroundTheme.Ground,
        BackgroundUnlockCompleted = false,
        NormalCollectionRegistered = CreateEmptyNormalCollection(),
        NormalFirstRegisteredAt = CreateEmptyStringStats(),
        NormalNaturalSpawnCounts = CreateEmptyLongStats(),
        NormalMergeCreatedCounts = CreateEmptyLongStats(),
        NormalManualTouchCounts = CreateEmptyLongStats(),
        NormalProducedPointTotals = CreateEmptyDoubleStats(),
        NormalGachaObtainedCounts = CreateEmptyLongStats(),
    };

    public static List<bool> CreateEmptyNormalCollection()
    {
        return new List<bool>(new bool[NormalCollectionSize]);
    }

    public static List<bool> NormalizeNormalCollection(
        IReadOnlyList<bool> registered)
    {
        List<bool> normalized = CreateEmptyNormalCollection();
        if (registered == null)
        {
            return normalized;
        }

        int copyCount = Math.Min(registered.Count, normalized.Count);
        for (int i = 0; i < copyCount; i++)
        {
            normalized[i] = registered[i];
        }

        return normalized;
    }

    public static List<string> CreateEmptyStringStats() =>
        new(new string[NormalCollectionSize]);

    public static List<long> CreateEmptyLongStats() =>
        new(new long[NormalCollectionSize]);

    public static List<double> CreateEmptyDoubleStats() =>
        new(new double[NormalCollectionSize]);

    public static List<string> NormalizeStringStats(IReadOnlyList<string> values)
    {
        List<string> normalized = CreateEmptyStringStats();
        if (values == null) return normalized;

        int copyCount = Math.Min(values.Count, normalized.Count);
        for (int i = 0; i < copyCount; i++)
        {
            normalized[i] = values[i] ?? string.Empty;
        }

        return normalized;
    }

    public static List<long> NormalizeLongStats(IReadOnlyList<long> values)
    {
        List<long> normalized = CreateEmptyLongStats();
        if (values == null) return normalized;

        int copyCount = Math.Min(values.Count, normalized.Count);
        for (int i = 0; i < copyCount; i++)
        {
            normalized[i] = Math.Max(0L, values[i]);
        }

        return normalized;
    }

    public static List<double> NormalizeDoubleStats(IReadOnlyList<double> values)
    {
        List<double> normalized = CreateEmptyDoubleStats();
        if (values == null) return normalized;

        int copyCount = Math.Min(values.Count, normalized.Count);
        for (int i = 0; i < copyCount; i++)
        {
            double value = values[i];
            if (value < 0d || double.IsNaN(value))
            {
                continue;
            }

            normalized[i] = double.IsPositiveInfinity(value)
                ? double.MaxValue
                : value;
        }

        return normalized;
    }

    public static void NormalizeCollectionStats(SlimeStatusSaveData saveData)
    {
        if (saveData == null) return;

        saveData.NormalFirstRegisteredAt = NormalizeStringStats(
            saveData.NormalFirstRegisteredAt);
        saveData.NormalNaturalSpawnCounts = NormalizeLongStats(
            saveData.NormalNaturalSpawnCounts);
        saveData.NormalMergeCreatedCounts = NormalizeLongStats(
            saveData.NormalMergeCreatedCounts);
        saveData.NormalManualTouchCounts = NormalizeLongStats(
            saveData.NormalManualTouchCounts);
        saveData.NormalProducedPointTotals = NormalizeDoubleStats(
            saveData.NormalProducedPointTotals);
        saveData.NormalGachaObtainedCounts = NormalizeLongStats(
            saveData.NormalGachaObtainedCounts);
    }
}

public static class SlimeStatusSaveMigration
{
    public static bool HasLegacyPendingTickets(SlimeStatusSaveData saveData)
    {
        return saveData != null &&
               saveData.PendingTickets == 0 &&
               (saveData.PendingGroundTickets != 0 ||
                saveData.PendingSkyTickets != 0);
    }

    // v0/v1의 { Grade, Count }를 Count 수만큼의 일반 MainField 개체로 승격한다.
    public static SlimeStatusSaveData Upgrade(
        LegacySlimeStatusSaveData legacyData)
    {
        if (legacyData == null)
        {
            return SlimeStatusSaveData.Default;
        }

        var countsByGrade = new SortedDictionary<int, int>();
        if (legacyData.ActiveSlimes != null)
        {
            foreach (LegacySlimeEntry entry in legacyData.ActiveSlimes)
            {
                if (entry == null || entry.Count <= 0) continue;

                if (entry.Grade < (int)ESlimeGrade.Grade1 ||
                    entry.Grade >= (int)ESlimeGrade.Count)
                {
                    continue;
                }

                countsByGrade.TryGetValue(entry.Grade, out int currentCount);
                countsByGrade[entry.Grade] = currentCount + entry.Count;
            }
        }

        // 같은 레거시 내용은 항목 순서와 중복 여부에 관계없이 같은 ID를 만든다.
        var activeSlimes = new List<SlimeInstanceSaveData>();
        foreach (KeyValuePair<int, int> pair in countsByGrade)
        {
            for (int i = 0; i < pair.Value; i++)
            {
                activeSlimes.Add(new SlimeInstanceSaveData(
                    $"legacy-{pair.Key}-{i}",
                    (ESlimeGrade)pair.Key,
                    false,
                    ESlimeLocation.MainField));
            }
        }

        return new SlimeStatusSaveData
        {
            SchemaVersion = GameDataDomains.SlimeStatus.CurrentSchemaVersion,
            HighestGrade = legacyData.HighestGrade,
            ActiveSlimes = activeSlimes,
            SelectedBackgroundTheme = legacyData.CurrentStage,
            BackgroundUnlockCompleted = legacyData.SkyIntroCompleted,
            PendingTickets = 0,
            NormalCollectionRegistered =
                SlimeStatusSaveData.CreateEmptyNormalCollection(),
            NormalFirstRegisteredAt = SlimeStatusSaveData.CreateEmptyStringStats(),
            NormalNaturalSpawnCounts = SlimeStatusSaveData.CreateEmptyLongStats(),
            NormalMergeCreatedCounts = SlimeStatusSaveData.CreateEmptyLongStats(),
            NormalManualTouchCounts = SlimeStatusSaveData.CreateEmptyLongStats(),
            NormalProducedPointTotals = SlimeStatusSaveData.CreateEmptyDoubleStats(),
            NormalGachaObtainedCounts = SlimeStatusSaveData.CreateEmptyLongStats(),
            LastSaveTime = legacyData.LastSaveTime,
            WasMigrated = true,
        };
    }

    public static SlimeStatusSaveData UpgradeInstanceData(
        SlimeStatusSaveData saveData,
        int sourceSchemaVersion)
    {
        if (saveData == null)
        {
            return SlimeStatusSaveData.Default;
        }

        if (sourceSchemaVersion < 9)
        {
            saveData.SelectedBackgroundTheme = saveData.CurrentStage;
            saveData.BackgroundUnlockCompleted = saveData.SkyIntroCompleted;
        }

        // v9 개발 중간본이 스테이지별 티켓만 가진 채 저장됐을 가능성도 흡수한다.
        // 최종 v9 저장은 두 레거시 필드를 항상 0으로 쓰므로 정상 데이터와 충돌하지 않는다.
        if (sourceSchemaVersion < 9 ||
            HasLegacyPendingTickets(saveData))
        {
            long combinedTickets = (long)saveData.PendingGroundTickets +
                                   saveData.PendingSkyTickets;
            saveData.PendingTickets = saveData.PendingGroundTickets < 0 ||
                                      saveData.PendingSkyTickets < 0 ||
                                      combinedTickets > int.MaxValue
                ? -1
                : (int)combinedTickets;
        }

        saveData.SchemaVersion = GameDataDomains.SlimeStatus.CurrentSchemaVersion;
        saveData.ActiveSlimes ??= new List<SlimeInstanceSaveData>();
        saveData.CompletedTutorials ??= new List<string>();
        saveData.SpecialCollectionRegistered ??= new List<bool>();
        saveData.NormalCollectionRegistered =
            SlimeStatusSaveData.NormalizeNormalCollection(
                saveData.NormalCollectionRegistered);
        SlimeStatusSaveData.NormalizeCollectionStats(saveData);
        saveData.WasMigrated = true;
        return saveData;
    }
}
