// 분석 이벤트를 보내는 쪽의 약속이다. 기획서 §21.10.
//
// 분석이 실패해도 게임은 막지 않는다. 구현은 예외를 밖으로 내보내지 않고, 보내지 못한 이벤트는 버린다.
public interface IAnalyticsService
{
    // 동의한 뒤에만 켠다. 꺼져 있는 동안 보낸 이벤트는 쌓이지 않고 버려진다.
    void SetCollectionEnabled(bool isEnabled);

    // 광고 이벤트 하나를 기록한다. 매개변수는 placement 하나와, 이벤트에 따라 숫자나 문자열 하나를 더 받는다.
    // 포인트나 슬라임 상태 같은 진행 값과 계정 식별자는 이 경로로 보내지 않는다.
    void LogAdEvent(string eventName, string placement, string extraKey = null, string extraText = null, long extraNumber = 0);
}
