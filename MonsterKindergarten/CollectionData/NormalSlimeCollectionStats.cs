using System;
using System.Collections.Generic;

public readonly struct NormalSlimeCollectionStatsSnapshot
{
    public string FirstRegisteredAt { get; }
    public long NaturalSpawnCount { get; }
    public long MergeCreatedCount { get; }
    public long ManualTouchCount { get; }
    public double ProducedPointTotal { get; }

    public NormalSlimeCollectionStatsSnapshot(
        string firstRegisteredAt,
        long naturalSpawnCount,
        long mergeCreatedCount,
        long manualTouchCount,
        double producedPointTotal)
    {
        FirstRegisteredAt = firstRegisteredAt;
        NaturalSpawnCount = naturalSpawnCount;
        MergeCreatedCount = mergeCreatedCount;
        ManualTouchCount = manualTouchCount;
        ProducedPointTotal = producedPointTotal;
    }
}

public sealed class NormalSlimeCollectionStats
{
    private readonly string[] _firstRegisteredAt;
    private readonly long[] _naturalSpawnCounts;
    private readonly long[] _mergeCreatedCounts;
    private readonly long[] _manualTouchCounts;
    private readonly double[] _producedPointTotals;

    public NormalSlimeCollectionStats(SlimeStatusSaveData saveData)
    {
        if (saveData == null)
        {
            throw new ArgumentNullException(nameof(saveData));
        }

        _firstRegisteredAt = saveData.NormalFirstRegisteredAt.ToArray();
        _naturalSpawnCounts = saveData.NormalNaturalSpawnCounts.ToArray();
        _mergeCreatedCounts = saveData.NormalMergeCreatedCounts.ToArray();
        _manualTouchCounts = saveData.NormalManualTouchCounts.ToArray();
        _producedPointTotals = saveData.NormalProducedPointTotals.ToArray();
    }

    public NormalSlimeCollectionStatsSnapshot Get(ESlimeGrade grade)
    {
        int index = GetIndex(grade);
        return new NormalSlimeCollectionStatsSnapshot(
            _firstRegisteredAt[index],
            _naturalSpawnCounts[index],
            _mergeCreatedCounts[index],
            _manualTouchCounts[index],
            _producedPointTotals[index]);
    }

    public bool RecordRegistration(ESlimeGrade grade, DateTime registeredAtUtc)
    {
        int index = GetIndex(grade);
        if (!string.IsNullOrEmpty(_firstRegisteredAt[index])) return false;

        _firstRegisteredAt[index] = registeredAtUtc.ToUniversalTime().ToString("o");
        return true;
    }

    public void RecordNaturalSpawn(ESlimeGrade grade)
    {
        Increment(_naturalSpawnCounts, GetIndex(grade));
    }

    public void RecordMergeCreated(ESlimeGrade grade)
    {
        Increment(_mergeCreatedCounts, GetIndex(grade));
    }

    public void RecordProduction(
        ESlimeGrade grade,
        EClickType clickType,
        double point)
    {
        int index = GetIndex(grade);
        if (clickType == EClickType.Manual)
        {
            Increment(_manualTouchCounts, index);
        }

        if (point <= 0d || double.IsNaN(point)) return;

        double total = _producedPointTotals[index] + point;
        _producedPointTotals[index] = double.IsInfinity(total)
            ? double.MaxValue
            : total;
    }

    public List<string> BuildFirstRegisteredAt() => new(_firstRegisteredAt);
    public List<long> BuildNaturalSpawnCounts() => new(_naturalSpawnCounts);
    public List<long> BuildMergeCreatedCounts() => new(_mergeCreatedCounts);
    public List<long> BuildManualTouchCounts() => new(_manualTouchCounts);
    public List<double> BuildProducedPointTotals() => new(_producedPointTotals);

    private static void Increment(long[] values, int index)
    {
        if (values[index] < long.MaxValue)
        {
            values[index]++;
        }
    }

    private static int GetIndex(ESlimeGrade grade)
    {
        int index = (int)grade - (int)ESlimeGrade.Grade1;
        if (index < 0 || index >= SlimeStatusSaveData.NormalCollectionSize)
        {
            throw new ArgumentOutOfRangeException(nameof(grade), grade, null);
        }

        return index;
    }
}
