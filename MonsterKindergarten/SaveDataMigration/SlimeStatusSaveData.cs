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

    [FirestoreProperty]
    public int CurrentStage { get; set; }

    [FirestoreProperty]
    public bool SkyIntroCompleted { get; set; }

    [FirestoreProperty]
    public List<bool> NormalCollectionRegistered { get; set; } =
        CreateEmptyNormalCollection();

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

    [FirestoreProperty]
    public string LastSaveTime { get; set; }

    [JsonIgnore]
    public bool WasMigrated { get; set; }

    public ESlimeGrade GetHighestGrade() => (ESlimeGrade)HighestGrade;

    public static SlimeStatusSaveData Default => new SlimeStatusSaveData
    {
        SchemaVersion = SaveSchema.SlimeCurrentVersion,
        HighestGrade = (int)ESlimeGrade.Grade1,
        ActiveSlimes = new List<SlimeInstanceSaveData>(),
        CurrentStage = (int)EGameStage.Ground,
        SkyIntroCompleted = false,
        NormalCollectionRegistered = CreateEmptyNormalCollection(),
        NormalFirstRegisteredAt = CreateEmptyStringStats(),
        NormalNaturalSpawnCounts = CreateEmptyLongStats(),
        NormalMergeCreatedCounts = CreateEmptyLongStats(),
        NormalManualTouchCounts = CreateEmptyLongStats(),
        NormalProducedPointTotals = CreateEmptyDoubleStats(),
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
    }
}

public static class SlimeStatusSaveMigration
{
    // v0/v1의 { Grade, Count }를 Count 수만큼의 일반 MainStage 개체로 승격한다.
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
                    ESlimeLocation.MainStage));
            }
        }

        return new SlimeStatusSaveData
        {
            SchemaVersion = SaveSchema.SlimeCurrentVersion,
            HighestGrade = legacyData.HighestGrade,
            ActiveSlimes = activeSlimes,
            CurrentStage = legacyData.CurrentStage,
            SkyIntroCompleted = legacyData.SkyIntroCompleted,
            NormalCollectionRegistered =
                SlimeStatusSaveData.CreateEmptyNormalCollection(),
            NormalFirstRegisteredAt = SlimeStatusSaveData.CreateEmptyStringStats(),
            NormalNaturalSpawnCounts = SlimeStatusSaveData.CreateEmptyLongStats(),
            NormalMergeCreatedCounts = SlimeStatusSaveData.CreateEmptyLongStats(),
            NormalManualTouchCounts = SlimeStatusSaveData.CreateEmptyLongStats(),
            NormalProducedPointTotals = SlimeStatusSaveData.CreateEmptyDoubleStats(),
            LastSaveTime = legacyData.LastSaveTime,
            WasMigrated = true,
        };
    }

    public static SlimeStatusSaveData UpgradeInstanceData(
        SlimeStatusSaveData saveData)
    {
        if (saveData == null)
        {
            return SlimeStatusSaveData.Default;
        }

        saveData.SchemaVersion = SaveSchema.SlimeCurrentVersion;
        saveData.ActiveSlimes ??= new List<SlimeInstanceSaveData>();
        saveData.NormalCollectionRegistered =
            SlimeStatusSaveData.NormalizeNormalCollection(
                saveData.NormalCollectionRegistered);
        SlimeStatusSaveData.NormalizeCollectionStats(saveData);
        saveData.WasMigrated = true;
        return saveData;
    }
}
