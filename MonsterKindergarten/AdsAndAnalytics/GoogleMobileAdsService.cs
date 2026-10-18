using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GoogleMobileAds.Api;
using UnityEngine;

// Android에서 쓰는 AdMob 보상형 광고다. 광고 하나를 불러 두고, 보여 주고, 다음 광고를 불러 오는 것까지만 맡는다.
// 보상 지급과 하루 횟수는 AdRewardService가 정한다. 기획서 §21.7.
//
// 초기화는 처음 광고를 불러올 때 한다. 초기화나 불러오기가 실패해도 게임은 막지 않고 광고가 준비되지 않은 것으로
// 다룬다. 실패한 뒤에는 잠시 쉬었다가 다시 시도하므로, 네트워크가 없는 동안 매 프레임 요청하지 않는다.
//
// 보상형 광고는 한 번 보여 주면 다시 못 쓰고, 불러온 지 한 시간이 지나면 만료된다. 그래서 보여 준 뒤에는 광고를
// 버리고 새로 불러오며, 오래된 광고도 준비된 것으로 치지 않는다.
public sealed class GoogleMobileAdsService : IAdService
{
    // 불러오기에 실패한 뒤 다시 시도하기까지 기다리는 시간이다.
    private const float RetryDelaySeconds = 30f;
    // 초기화 응답을 기다리는 한계다. 넘기면 실패로 보고, 다음 시도에서 다시 초기화한다.
    private const int InitializeTimeoutMilliseconds = 15000;
    // 보상형 광고의 유효 시간은 한 시간이다. 여유를 두고 그 전에 쓸 수 없는 것으로 친다.
    private const float MaxAdAgeSeconds = 55f * 60f;

    private readonly string _adUnitId;
    private RewardedAd _ad;
    private float _loadedAt;
    private float _nextLoadAllowedAt;
    private bool _isLoading;
    private bool _wasReady;
    private UniTaskCompletionSource<bool> _initializeSource;
    private bool _isInitialized;

    public GoogleMobileAdsService(string adUnitId)
    {
        _adUnitId = adUnitId;
    }

    public bool IsReady =>
        _ad != null &&
        Time.realtimeSinceStartup - _loadedAt < MaxAdAgeSeconds &&
        _ad.CanShowAd();

    public bool IsShowing { get; private set; }

    public event Action ReadyChanged;
    public event Action<string> LoadFailed;

    public void Preload()
    {
        if (_isLoading || IsShowing || IsReady) return;
        if (Time.realtimeSinceStartup < _nextLoadAllowedAt) return;

        // 만료됐거나 쓸 수 없게 된 광고는 새로 받기 전에 버린다.
        DestroyAd();
        LoadAsync().Forget();
    }

    public async UniTask<EAdShowResult> ShowRewardedAsync(CancellationToken token)
    {
        if (IsShowing) return EAdShowResult.Busy;
        if (!IsReady)
        {
            Preload();
            return EAdShowResult.NotReady;
        }

        // 한 번 보여 주는 광고는 다시 쓰지 않는다. 손에서 떼어 두고 끝나면 버린다.
        RewardedAd ad = _ad;
        _ad = null;
        var closed = new UniTaskCompletionSource<EAdShowResult>();
        bool earned = false;
        ad.OnAdFullScreenContentClosed += () =>
            closed.TrySetResult(earned ? EAdShowResult.Rewarded : EAdShowResult.ClosedEarly);
        ad.OnAdFullScreenContentFailed += error =>
        {
            Debug.LogWarning($"보상형 광고를 보여 주지 못했습니다. : {error}");
            closed.TrySetResult(EAdShowResult.Failed);
        };

        IsShowing = true;
        NotifyReadyChanged();
        EAdShowResult result;
        try
        {
            ad.Show(_ => earned = true);
            using (token.Register(() => closed.TrySetResult(EAdShowResult.ClosedEarly)))
            {
                result = await closed.Task;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"보상형 광고 재생 중 오류가 났습니다. : {exception.Message}");
            result = EAdShowResult.Failed;
        }
        finally
        {
            IsShowing = false;
            ad.Destroy();
        }

        Preload();
        return result;
    }

    private async UniTaskVoid LoadAsync()
    {
        _isLoading = true;
        try
        {
            if (!await InitializeAsync())
            {
                LoadFailed?.Invoke("init_timeout");
                ScheduleRetry();
                return;
            }

            var loaded = new UniTaskCompletionSource<RewardedAd>();
            RewardedAd.Load(_adUnitId, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.LogWarning($"보상형 광고를 불러오지 못했습니다. : {error}");
                    LoadFailed?.Invoke(error != null ? error.GetCode().ToString() : "null_ad");
                    loaded.TrySetResult(null);
                    return;
                }

                loaded.TrySetResult(ad);
            });

            RewardedAd result = await loaded.Task;
            if (result == null)
            {
                ScheduleRetry();
                return;
            }

            _ad = result;
            _loadedAt = Time.realtimeSinceStartup;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"보상형 광고를 불러오는 중 오류가 났습니다. : {exception.Message}");
            ScheduleRetry();
        }
        finally
        {
            _isLoading = false;
            NotifyReadyChanged();
        }
    }

    // 초기화는 한 번만 한다. 동시에 부르면 같은 결과를 기다린다. 실패하면 다음 시도에서 처음부터 다시 한다.
    private async UniTask<bool> InitializeAsync()
    {
        if (_isInitialized) return true;

        if (_initializeSource == null)
        {
            var source = new UniTaskCompletionSource<bool>();
            _initializeSource = source;

            // 콜백을 유니티 메인 스레드에서 받는다. 광고 이벤트가 다른 스레드에서 오면 게임 객체를 만질 수 없다.
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            // 대상 연령이 13세 이상(13~15, 16~17, 18+)이라 청소년에게 적절한 T 등급까지로 광고를 제한한다. 기획서 §21.8.
            MobileAds.SetRequestConfiguration(new RequestConfiguration
            {
                MaxAdContentRating = MaxAdContentRating.T,
            });
            MobileAds.Initialize(_ => source.TrySetResult(true));
        }

        var current = _initializeSource;
        int winner = await UniTask.WhenAny(
            current.Task.AsUniTask(),
            UniTask.Delay(InitializeTimeoutMilliseconds, DelayType.UnscaledDeltaTime));
        if (winner == 0)
        {
            _isInitialized = true;
            return true;
        }

        Debug.LogWarning("광고 SDK 초기화 응답이 없어 이번 시도는 실패로 둡니다.");
        if (_initializeSource == current) _initializeSource = null;
        return false;
    }

    private void ScheduleRetry()
    {
        _nextLoadAllowedAt = Time.realtimeSinceStartup + RetryDelaySeconds;
    }

    private void DestroyAd()
    {
        if (_ad == null) return;

        _ad.Destroy();
        _ad = null;
    }

    // 준비 여부가 실제로 바뀐 때만 알린다. 불러오기가 실패하거나 광고가 만료돼도 같은 값이면 부르지 않는다.
    private void NotifyReadyChanged()
    {
        bool isReady = IsReady;
        if (isReady == _wasReady) return;

        _wasReady = isReady;
        ReadyChanged?.Invoke();
    }
}
