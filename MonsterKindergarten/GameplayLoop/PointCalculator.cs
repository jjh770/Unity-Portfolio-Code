/*
 * 역할: 슬라임 기본 포인트와 수동·자동 클릭별 업그레이드를 결합해 최종 획득량을 계산합니다.
 * 핵심 설계: 계산 공식을 입력·UI·슬라임 컨트롤러에서 분리해 한곳에서 유지합니다.
 */
public static class PointCalculator
{
    /// <summary>
    /// 기본값과 고정 보너스를 더한 뒤 비율 보너스를 곱해 최종 포인트를 계산합니다.
    /// </summary>
    public static double Calculate(double basePoint, ESlimeGrade grade, EClickType clickType)
    {
        double flatBonus = GetFlatBonus(grade, clickType);
        double percentBonus = GetPercentBonus(grade, clickType);

        return (basePoint + flatBonus) * (1 + percentBonus);
    }

    /// <summary>
    /// 클릭 종류에 맞는 고정 증가 업그레이드 효과를 조회합니다.
    /// </summary>
    private static double GetFlatBonus(ESlimeGrade grade, EClickType clickType)
    {
        var type = clickType == EClickType.Manual
            ? EUpgradeType.ManualPointPlusAdd
            : EUpgradeType.AutoPointPlusAdd;

        return GetUpgradePoint(type, grade);
    }

    /// <summary>
    /// 클릭 종류에 맞는 비율 증가 업그레이드 효과를 조회합니다.
    /// </summary>
    private static double GetPercentBonus(ESlimeGrade grade, EClickType clickType)
    {
        var type = clickType == EClickType.Manual
            ? EUpgradeType.ManualPointPercentAdd
            : EUpgradeType.AutoPointPercentAdd;

        return GetUpgradePoint(type, grade);
    }

    private static double GetUpgradePoint(EUpgradeType type, ESlimeGrade grade)
    {
        if (UpgradeManager.Instance == null) return 0;

        var upgrade = UpgradeManager.Instance.Get(type, grade);
        return upgrade?.Point ?? 0;
    }
}
