// 광고 흐름의 분석 이벤트 이름과 매개변수를 한 곳에 모은다. 기획서 §21.10.
//
// AdRewardService는 광고를 보여 주고 보상을 지급하는 일만 하고, 어떤 이벤트를 어떤 이름으로 보내는지는 여기서
// 정한다. 분석이 실패해도 보상은 막지 않으므로(IAnalyticsService) 이 호출은 결과를 돌려주지 않는다.
public static class AdAnalytics
{
    public static void LogStart(EAdPlacement placement)
    {
        Analytics.Service.LogAdEvent("ad_start", GetPlacementName(placement));
    }

    // 불러오기는 어느 버튼과도 상관없이 미리 일어나므로 placement는 preload다.
    public static void LogLoadFailed(string code)
    {
        Analytics.Service.LogAdEvent("ad_load_fail", "preload", "fail_code", code);
    }

    public static void LogNotRewarded(EAdShowResult result, EAdPlacement placement)
    {
        switch (result)
        {
            case EAdShowResult.ClosedEarly:
                Analytics.Service.LogAdEvent("ad_close_early", GetPlacementName(placement));
                break;
            case EAdShowResult.Failed:
                Analytics.Service.LogAdEvent("ad_load_fail", GetPlacementName(placement), "fail_code", "show_failed");
                break;
        }
    }

    // todayCount가 0 이하이면 하루 한도가 없는 보상이다(오프라인 2배). 한도가 있는 보상은 오늘 몇 번째인지를 싣고,
    // 이 보상으로 한도에 닿았으면 그 사실도 따로 기록한다.
    public static void LogRewarded(EAdPlacement placement, int todayCount, bool reachedDailyLimit)
    {
        string placementName = GetPlacementName(placement);
        if (todayCount <= 0)
        {
            Analytics.Service.LogAdEvent("ad_reward", placementName);
            return;
        }

        Analytics.Service.LogAdEvent("ad_reward", placementName, "today_count", null, todayCount);
        if (reachedDailyLimit)
        {
            Analytics.Service.LogAdEvent("ad_limit_reached", placementName);
        }
    }

    private static string GetPlacementName(EAdPlacement placement)
    {
        switch (placement)
        {
            case EAdPlacement.OfflineDouble:
                return "offline_double";
            case EAdPlacement.PointBoost:
                return "point_boost";
            default:
                return "ticket";
        }
    }
}
