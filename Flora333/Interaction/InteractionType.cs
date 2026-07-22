/*
 * 역할: 주변 대상이 여러 개일 때 플레이어 상호작용 우선순위를 결정하는 종류를 정의합니다.
 */
/// <summary>
/// 대화, 사용, 들기 등 상호작용의 의미와 선택 우선순위를 구분합니다.
/// </summary>
public enum InteractionType
{
    TalkToMove,
    TalkToSpeedUp,
    Use,
    PickUp,
}
