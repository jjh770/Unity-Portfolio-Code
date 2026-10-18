using System;
using System.Threading;
using Cysharp.Threading.Tasks;

// 에디터에서 쓰는 가짜 광고다. 정해진 시간 뒤에 NextResult를 돌려준다. 보여 준 뒤에는 다음 광고를 불러오는
// 척하며 잠시 준비되지 않은 상태가 되어, 준비 여부가 바뀌는 흐름도 같이 확인할 수 있다.
public sealed class FakeAdService : IAdService
{
    public const float DefaultShowSeconds = 2f;
    public const float DefaultLoadSeconds = 1f;

    private readonly float _showSeconds;
    private readonly float _loadSeconds;
    private bool _isReady = true;
    private bool _isLoading;

    public FakeAdService(float showSeconds = DefaultShowSeconds, float loadSeconds = DefaultLoadSeconds)
    {
        _showSeconds = showSeconds;
        _loadSeconds = loadSeconds;
    }

    public bool IsReady => _isReady;
    public bool IsShowing { get; private set; }

    // 중도 닫기나 실패를 흉내 낼 때 바꾼다.
    public EAdShowResult NextResult { get; set; } = EAdShowResult.Rewarded;

    public event Action ReadyChanged;

    // 가짜 광고는 불러오기에 실패하지 않는다. 인터페이스를 맞추려는 자리다.
    public event Action<string> LoadFailed
    {
        add { }
        remove { }
    }

    public void SetReady(bool isReady)
    {
        if (_isReady == isReady) return;

        _isReady = isReady;
        ReadyChanged?.Invoke();
    }

    public void Preload()
    {
        if (_isReady || _isLoading) return;

        LoadAsync().Forget();
    }

    public async UniTask<EAdShowResult> ShowRewardedAsync(CancellationToken token)
    {
        if (IsShowing) return EAdShowResult.Busy;
        if (!_isReady) return EAdShowResult.NotReady;

        IsShowing = true;
        SetReady(false);
        bool cancelled = await UniTask.Delay(
                TimeSpan.FromSeconds(_showSeconds),
                DelayType.UnscaledDeltaTime,
                cancellationToken: token)
            .SuppressCancellationThrow();
        IsShowing = false;
        Preload();
        return cancelled ? EAdShowResult.ClosedEarly : NextResult;
    }

    private async UniTaskVoid LoadAsync()
    {
        _isLoading = true;
        await UniTask.Delay(TimeSpan.FromSeconds(_loadSeconds), DelayType.UnscaledDeltaTime);
        _isLoading = false;
        SetReady(true);
    }
}
