using System;
using System.Threading;
using Cysharp.Threading.Tasks;

// 광고가 없는 빌드에서 쓴다. 광고는 언제나 준비되지 않은 것으로 다뤄서 광고 버튼이 숨겨진다.
public sealed class NullAdService : IAdService
{
    public bool IsReady => false;
    public bool IsShowing => false;

    public event Action ReadyChanged
    {
        add { }
        remove { }
    }

    public event Action<string> LoadFailed
    {
        add { }
        remove { }
    }

    public void Preload()
    {
    }

    public UniTask<EAdShowResult> ShowRewardedAsync(CancellationToken token)
    {
        return UniTask.FromResult(EAdShowResult.NotReady);
    }
}
