/*
 * 역할: 클릭 반응을 독립적인 컴포넌트로 분리하기 위한 공통 계약입니다.
 * 설계 의도: SlimeController를 수정하지 않고도 색상, 크기, 사운드, 플로팅 텍스트 피드백을 추가할 수 있게 합니다.
 */

/// <summary>
/// ClickInfo를 받아 하나의 피드백 효과를 실행합니다.
/// </summary>
public interface IFeedback
{
    void Play(ClickInfo clickInfo);
}
