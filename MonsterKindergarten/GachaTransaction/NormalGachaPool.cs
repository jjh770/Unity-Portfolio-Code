using System.Collections.Generic;
using UnityEngine;

public enum EGachaRarity
{
    Common,
    Uncommon,
    Rare,
    Jackpot,
    // 특별한 슬라임 결과. 등급과 별개의 축이라 최고 등급과의 거리로는 나오지 않는다.
    Special,
}

public readonly struct NormalGachaResult
{
    public ESlimeGrade Grade { get; }
    public EGachaRarity Rarity { get; }

    public NormalGachaResult(ESlimeGrade grade, EGachaRarity rarity)
    {
        Grade = grade;
        Rarity = rarity;
    }
}

public readonly struct NormalGachaProbability
{
    public ESlimeGrade Grade { get; }
    public double Probability { get; }

    public NormalGachaProbability(ESlimeGrade grade, double probability)
    {
        Grade = grade;
        Probability = probability;
    }
}

// 일반 가챠 결과의 후보와 가중치.
//
// 후보는 Lv.1부터 최고 해금 등급까지 전부다. 최고 등급 자신도 나올 수 있고, 그 위는
// 절대 나오지 않는다. 가챠가 아직 만나지 못한 등급의 슬라임을 만들어 주면 안 된다.
//
// 낮은 등급일수록 무겁다. Lv.n의 무게는 (최고 - n + 1)이라 Lv.1이 가장 잘 나오고
// 최고 등급이 가장 드물다. 값은 확정된 밸런스라 에셋으로 빼지 않았다. 조정이 필요해지면
// 그때 SpawnWeightTable처럼 ScriptableObject로 옮긴다.
public static class NormalGachaPool
{
    // 포털 색은 최고 등급과의 거리로 정한다. 후보가 몇 개든 "얼마나 좋은 결과인지"가
    // 같은 뜻으로 남는다. 거리 0은 최고 등급 자신이다.
    private const int JackpotMaxDistance = 1;
    private const int RareMaxDistance = 3;
    private const int UncommonMaxDistance = 6;

    public static List<NormalGachaProbability> GetProbabilities(ESlimeGrade highestGrade)
    {
        var probabilities = new List<NormalGachaProbability>();
        if (highestGrade < ESlimeGrade.Grade1) return probabilities;

        int highest = (int)highestGrade;
        int totalWeight = 0;
        for (int grade = (int)ESlimeGrade.Grade1; grade <= highest; grade++)
        {
            totalWeight += GetWeight(highest, grade);
        }

        for (int grade = (int)ESlimeGrade.Grade1; grade <= highest; grade++)
        {
            probabilities.Add(new NormalGachaProbability(
                (ESlimeGrade)grade,
                (double)GetWeight(highest, grade) / totalWeight));
        }

        return probabilities;
    }

    public static bool TryPick(ESlimeGrade highestGrade, out NormalGachaResult result)
    {
        result = default;
        if (highestGrade < ESlimeGrade.Grade1) return false;

        int highest = (int)highestGrade;
        int totalWeight = 0;
        for (int grade = (int)ESlimeGrade.Grade1; grade <= highest; grade++)
        {
            totalWeight += GetWeight(highest, grade);
        }

        int roll = Random.Range(0, totalWeight);
        for (int grade = (int)ESlimeGrade.Grade1; grade <= highest; grade++)
        {
            roll -= GetWeight(highest, grade);
            if (roll >= 0) continue;

            result = new NormalGachaResult((ESlimeGrade)grade, GetRarity(highest, grade));
            return true;
        }

        return false;
    }

    // 가챠 튜토리얼의 무료 한 장. 첫 경험이 시시하지 않도록 최고 -1과 최고 중에서만
    // 같은 확률로 뽑는다. 최고 -1이 Lv.1 아래로 내려가면 그 자리는 뺀다.
    public static bool TryPickTutorial(ESlimeGrade highestGrade, out NormalGachaResult result)
    {
        result = default;
        if (highestGrade < ESlimeGrade.Grade1) return false;

        int highest = (int)highestGrade;
        int lowest = System.Math.Max((int)ESlimeGrade.Grade1, highest - 1);
        int grade = Random.Range(lowest, highest + 1);
        result = new NormalGachaResult((ESlimeGrade)grade, GetRarity(highest, grade));
        return true;
    }

    public static int GetWeight(int highest, int grade) => highest - grade + 1;

    public static EGachaRarity GetRarity(int highest, int grade)
    {
        int distance = highest - grade;
        if (distance <= JackpotMaxDistance) return EGachaRarity.Jackpot;
        if (distance <= RareMaxDistance) return EGachaRarity.Rare;
        if (distance <= UncommonMaxDistance) return EGachaRarity.Uncommon;
        return EGachaRarity.Common;
    }
}
