/*
 * 역할: 들기와 던지기를 지원하는 대상의 확장 계약을 정의합니다.
 * 핵심 설계: 일반 상호작용 기능을 유지하면서 운반 시스템에 필요한 생명주기만 추가합니다.
 */
using UnityEngine;

/// <summary>
/// IInteractable에 부착·투척 동작을 추가한 운반 대상 인터페이스입니다.
/// </summary>
public interface IPickable : IInteractable
{
    void OnPickedUp(Transform holdPoint);
    void OnThrown(Vector3 direction, float force);
    bool HidesFloraOutline { get; }
}
