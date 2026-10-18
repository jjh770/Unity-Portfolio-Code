using System;
using System.Threading;
using Cysharp.Threading.Tasks;

// 보상형 광고 하나를 불러오고 보여 주는 쪽의 약속이다. 구현은 플랫폼마다 다르고, 보상 지급과 하루 횟수는
// 이 인터페이스 바깥(AdRewardService)이 맡는다.
public interface IAdService
{
    bool IsReady { get; }
    bool IsShowing { get; }
    event Action ReadyChanged;

    // 광고를 불러오지 못했을 때 사유 코드와 함께 알린다. 분석이 듣는다.
    event Action<string> LoadFailed;

    // 준비된 광고가 없고 불러오는 중도 아니면 불러오기를 시작한다. 이미 준비됐거나 불러오는 중이면 아무것도 하지 않는다.
    void Preload();

    UniTask<EAdShowResult> ShowRewardedAsync(CancellationToken token);
}
