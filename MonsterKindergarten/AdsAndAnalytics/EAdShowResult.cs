public enum EAdShowResult
{
    // 보상 획득 신호가 왔고 보상을 지급했다. 보상은 이때만 지급한다.
    Rewarded,
    // 보상 없이 닫혔다.
    ClosedEarly,
    // 보여 줄 광고가 준비되지 않았다.
    NotReady,
    // 이미 다른 광고를 보여 주는 중이다.
    Busy,
    // 해금 전이거나 한도에 닿아 광고를 시작하지 않았다.
    NotAllowed,
    // 광고를 보여 주다가 실패했거나 보상을 지급하지 못했다.
    Failed,
}
