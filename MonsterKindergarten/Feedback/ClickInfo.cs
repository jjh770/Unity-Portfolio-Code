/*
 * 역할: 한 번의 클릭 결과와 피드백 실행에 필요한 정보를 전달하는 데이터 객체입니다.
 * 설계 의도: 피드백 구현체가 구체 클래스에 의존하지 않고 클릭 종류·위치·획득량·슬라임 등급만 전달받도록 합니다.
 */
using UnityEngine;

/// <summary>
/// 클릭 처리와 피드백 컴포넌트 사이에서 공유하는 입력 컨텍스트입니다.
/// </summary>
public struct ClickInfo
{
    public EClickType ClickType;
    public Vector2 Position;
    public double Point;
    public ESlimeGrade Grade;
}
