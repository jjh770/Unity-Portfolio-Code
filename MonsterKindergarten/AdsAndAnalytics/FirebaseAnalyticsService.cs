using UnityEngine;

// Android의 Firebase Analytics를 JNI로 부른다. 기획서 §21.10.
//
// 프로젝트에는 Analytics용 Unity 라이브러리가 없고, Android 쪽 firebase-analytics 라이브러리만 의존성으로 이미
// 들어 있다. 그래서 새 패키지를 들이지 않고 그 라이브러리를 직접 부른다. 쓰는 것은 getInstance, logEvent,
// setAnalyticsCollectionEnabled 세 가지뿐이다.
//
// 모든 호출은 예외를 삼킨다. 분석이 깨졌다고 보상 지급이나 화면이 막히면 안 된다.
public sealed class FirebaseAnalyticsService : IAnalyticsService
{
    private const string AnalyticsClass = "com.google.firebase.analytics.FirebaseAnalytics";

    private AndroidJavaObject _analytics;
    private bool _isInitialized;

    public void SetCollectionEnabled(bool isEnabled)
    {
        try
        {
            AndroidJavaObject instance = GetInstance();
            instance?.Call("setAnalyticsCollectionEnabled", isEnabled);
        }
        catch (AndroidJavaException exception)
        {
            Debug.LogWarning($"분석 수집 설정을 바꾸지 못했습니다. : {exception.Message}");
        }
    }

    public void LogAdEvent(string eventName, string placement, string extraKey = null, string extraText = null, long extraNumber = 0)
    {
        try
        {
            AndroidJavaObject instance = GetInstance();
            if (instance == null) return;

            using var bundle = new AndroidJavaObject("android.os.Bundle");
            bundle.Call("putString", "placement", placement);
            if (!string.IsNullOrEmpty(extraKey))
            {
                if (extraText != null)
                {
                    bundle.Call("putString", extraKey, extraText);
                }
                else
                {
                    bundle.Call("putLong", extraKey, extraNumber);
                }
            }

            instance.Call("logEvent", eventName, bundle);
        }
        catch (AndroidJavaException exception)
        {
            Debug.LogWarning($"분석 이벤트를 보내지 못했습니다. : {eventName}, {exception.Message}");
        }
    }

    // 처음 쓸 때 한 번만 얻는다. 얻지 못하면 이후에도 다시 시도하지 않는다.
    private AndroidJavaObject GetInstance()
    {
        if (_isInitialized) return _analytics;

        _isInitialized = true;
        try
        {
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var analyticsClass = new AndroidJavaClass(AnalyticsClass);
            _analytics = analyticsClass.CallStatic<AndroidJavaObject>("getInstance", activity);
        }
        catch (AndroidJavaException exception)
        {
            Debug.LogWarning($"Firebase Analytics를 불러오지 못했습니다. : {exception.Message}");
        }

        return _analytics;
    }
}
