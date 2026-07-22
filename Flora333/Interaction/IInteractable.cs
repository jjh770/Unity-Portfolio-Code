/*
 * 역할: 플레이어가 구체 클래스에 의존하지 않고 상호작용할 수 있도록 공통 계약을 정의합니다.
 * 핵심 설계: 대상 선택에 필요한 타입·Transform·사용 가능 여부와 실행 메서드만 노출합니다.
 */
using UnityEngine;

/// <summary>
/// 모든 상호작용 대상이 구현해야 하는 최소 인터페이스입니다.
/// </summary>
public interface IInteractable
{
    InteractionType Type { get; }
    IconType IconType { get; }
    Transform Transform { get; }
    bool CanInteract { get; }
    void Interact(GameObject interactor);
}
