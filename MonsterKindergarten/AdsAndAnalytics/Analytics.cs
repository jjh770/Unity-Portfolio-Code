using UnityEngine;

// 분석 서비스를 한 곳에서 꺼내 쓰게 하는 진입점이다. 기획서 §21.10.
//
// 플랫폼 분기는 Create 한 곳에 있다. Android는 Firebase Analytics, 에디터는 로그만 남기고, 그 밖의 빌드는 보내지
// 않는다. 에디터의 로그 구현이 기기 동작을 대신하지 않게 하려는 것이다.
public static class Analytics
{
    private static IAnalyticsService s_service;

    public static IAnalyticsService Service => s_service ??= Create();

    // 동의한 뒤에만 수집을 켠다. 켜고 끄는 때는 동의를 아는 쪽(로그인 흐름과 계정 삭제)이 정한다. 이 클래스가
    // 동의 기록을 직접 읽으면 가장 아래 계층인 Core가 Outgame에 기대게 된다. 앱이 시작할 때의 수집은
    // AppConsent.androidlib의 매니페스트가 꺼 둔다.
    public static void SetCollectionEnabled(bool isEnabled)
    {
        Service.SetCollectionEnabled(isEnabled);
    }

    private static IAnalyticsService Create()
    {
#if UNITY_EDITOR
        return new LogAnalyticsService();
#elif UNITY_ANDROID
        return new FirebaseAnalyticsService();
#else
        return new LogAnalyticsService();
#endif
    }

    // 에디터와 지원하지 않는 플랫폼용이다. 이벤트를 보내지 않고 로그만 남긴다.
    private sealed class LogAnalyticsService : IAnalyticsService
    {
        public void SetCollectionEnabled(bool isEnabled)
        {
            Debug.Log($"[Analytics] 수집 {(isEnabled ? "켜짐" : "꺼짐")}");
        }

        public void LogAdEvent(string eventName, string placement, string extraKey = null, string extraText = null, long extraNumber = 0)
        {
            string extra = string.IsNullOrEmpty(extraKey) ? string.Empty : $" {extraKey}={(extraText ?? extraNumber.ToString())}";
            Debug.Log($"[Analytics] {eventName} placement={placement}{extra}");
        }
    }
}
