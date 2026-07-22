/*
 * 역할: 한 업그레이드의 고정 스펙, 현재 레벨, 비용·효과 계산과 레벨업 규칙을 보유합니다.
 * 핵심 설계: 재화 시스템을 직접 참조하지 않아 업그레이드 자체의 규칙에만 집중합니다.
 */
// '업그레이드' 라는 게임 콘텐츠의 도메인 클래스
// 도메인이란 핵심 데이터와 규칙을 뜻함.
// 가장 먼저 만들고, 가장 나중에 바뀐다. (게임의 본질이기 때문)
// 핵심 데이터와 규칙을 모두 가지고 있다 -> 응집도가 높다. -> 표현력이 높다. 
using System;

/// <summary>
/// 기획 스펙과 런타임 레벨을 결합한 업그레이드 도메인 객체입니다.
/// </summary>
public class Upgrade
{
    // 기획 데이터 (변하면 안됨)
    // 1. 기획 테이블의 데이터를 가져오다.
    // UpgradeSpecData.cs로 이전
    public readonly UpgradeSpecData SpecData;

    // 3. 런타임 데이터 (게임 중간에 바뀌는 데이터)
    public int Level { get; private set; }

    // 업그레이드 비용 : 기본 비용 + 증가량^레벨
    public Currency Cost => SpecData.BaseCost + Math.Pow(SpecData.CostMultiplier, Level); // 지수 공식 : 기본 비용 + 증가량 ^ 레벨

    // 레벨 0이면 보너스 없음
    // Linear : 선형 공식 (BasePoint + Level * PointMultiplier)
    // Fixed  : 고정값 공식 (레벨과 무관하게 항상 BasePoint)
    public double Point => Level == 0 ? 0 : CalculatePoint(Level);
    /// <summary>
    /// 고정 또는 선형 공식에 따라 현재 레벨의 효과값을 계산합니다.
    /// </summary>
    public double NextPoint => IsMaxLevel ? Point : CalculatePoint(Level + 1);
    // 런타임에서 변경하지 않는 업그레이드 기획 수치입니다.
    public bool IsMaxLevel => Level >= SpecData.MaxLevel;

    private double CalculatePoint(int level)
    {
        return SpecData.PointFormula switch
        {
            EPointFormula.Fixed => SpecData.BasePoint,
            _ => SpecData.BasePoint + level * SpecData.PointMultiplier, // Linear
        };
    }

    // 2. 핵심 규칙을 작성한다.
    public Upgrade(UpgradeSpecData specData, int level)
    {
        SpecData = specData;
        Level = level;

        if (specData.MaxLevel < 0) throw new System.ArgumentException($"최대 레벨은 0보다 커야합니다. : {specData.MaxLevel}");
        if (specData.BaseCost <= 0) throw new System.ArgumentException($"기본 비용은 0보다 크거나 같아야 합니다. : {specData.BaseCost}");
        if (specData.BasePoint < 0) throw new System.ArgumentException($"기본 포인트는 0보다 작을 순 없습니다. : {specData.BasePoint}");
        if (specData.CostMultiplier <= 0) throw new System.ArgumentException($"비용 증가량은 0보다 크거나 같아야 합니다. : {specData.CostMultiplier}");
        // Fixed 공식은 PointMultiplier를 사용하지 않으므로 검증 생략
        if (specData.PointFormula != EPointFormula.Fixed && specData.PointMultiplier <= 0)
            throw new System.ArgumentException($"포인트 증가량은 0보다 크거나 같아야 합니다. : {specData.PointMultiplier}");
        if (string.IsNullOrEmpty(specData.Name)) throw new System.ArgumentException($"이름은 비어있을 수 없습니다.");
        if (string.IsNullOrEmpty(specData.Description)) throw new System.ArgumentException($"설명은 비어있을 수 없습니다.");
    }

    public bool CanLevelUp()
    {
        return !IsMaxLevel;
    }

    /// <summary>
    /// 최대 레벨 규칙을 만족할 때만 상태를 한 단계 증가시킵니다.
    /// </summary>
    public bool TryLevelUp()
    {
        if (!CanLevelUp()) return false;

        Level++;
        return true;
    }
}
