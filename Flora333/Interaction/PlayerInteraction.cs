/*
 * 역할: 주변 IInteractable을 수집하고 타입 우선순위와 거리로 현재 상호작용 대상을 선택합니다.
 * 핵심 설계: 매 프레임 전체 정렬을 피하고 일정 주기마다 거리 제곱을 비교해 대상을 갱신합니다.
 */
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 트리거 범위의 상호작용 대상 목록과 현재 선택 대상을 관리합니다.
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float _updateInterval = 0.1f;

    private List<IInteractable> _nearbyInteractables = new List<IInteractable>();
    // 현재 우선순위와 거리 조건을 만족해 입력을 받을 대상입니다.
    private IInteractable _closestInteractable;
    private PlayerInputHandler _inputHandler;
    private float _nextUpdateTime;

    // 우선순위: Move > SpeedUp > PickUp > Use
    private static readonly InteractionType[] _priorityOrder = new InteractionType[]
    {
        InteractionType.TalkToMove,
        InteractionType.TalkToSpeedUp,
        InteractionType.Use,
        InteractionType.PickUp
    };
    private Dictionary<InteractionType, int> _priorityMap;

    public IInteractable ClosestInteractable => _closestInteractable;
    public bool HasInteractable => _closestInteractable != null;
    public InteractionType? CurrentInteractionType => _closestInteractable?.Type;

    public event Action<IInteractable> OnInteractableChanged;
    public event Action<IInteractable> OnInteract;

    private void Awake()
    {
        _inputHandler = GetComponent<PlayerInputHandler>();
        _priorityMap = new Dictionary<InteractionType, int>();
        for (int i = 0; i < _priorityOrder.Length; i++)
        {
            _priorityMap[_priorityOrder[i]] = i;
        }
    }

    private void OnEnable()
    {
        InteractionEvents.OnInteractableDestroyed += HandleInteractableDestroyed;
        _inputHandler.OnInteractInput += HandleInteractInput;
    }

    private void OnDisable()
    {
        InteractionEvents.OnInteractableDestroyed -= HandleInteractableDestroyed;
        _inputHandler.OnInteractInput -= HandleInteractInput;
    }

    private void HandleInteractInput()
    {
        if (_closestInteractable != null)
        {
            TryInteract();
        }
    }

    private void HandleInteractableDestroyed(IInteractable interactable)
    {
        Remove(interactable);
    }

    private void Update()
    {
        if (Time.time >= _nextUpdateTime)
        {
            UpdateClosestInteractable();
            _nextUpdateTime = Time.time + _updateInterval;
        }
    }

    /// <summary>
    /// 현재 대상이 바뀐 경우에만 UI 등 구독자에게 변경 이벤트를 발행합니다.
    /// </summary>
    private void UpdateClosestInteractable()
    {
        IInteractable previous = _closestInteractable;
        _closestInteractable = GetClosestInteractableByPriority();

        if (previous != _closestInteractable)
        {
            OnInteractableChanged?.Invoke(_closestInteractable);
        }
    }

    /// <summary>
    /// 선택 대상의 현재 사용 가능 여부를 다시 검사한 뒤 상호작용을 실행합니다.
    /// </summary>
    private void TryInteract()
    {
        if (_closestInteractable == null || !_closestInteractable.CanInteract)
            return;

        // Interact 중 객체가 파괴될 수 있으므로 미리 참조 저장
        IInteractable interactable = _closestInteractable;
        interactable.Interact(gameObject);
        OnInteract?.Invoke(interactable);
    }

    /// <summary>
    /// 우선순위에 따라 가장 가까운 상호작용 가능한 오브젝트 반환
    /// </summary>
    private IInteractable GetClosestInteractableByPriority()
    {
        CleanupNull();
        if (_nearbyInteractables.Count == 0) return null;

        IInteractable bestMatch = null;
        int bestPriorityIndex = int.MaxValue;
        float bestDistanceSqr = float.MaxValue;

        Vector3 playerPos = transform.position;

        foreach (var interactable in _nearbyInteractables)
        {
            if (!interactable.CanInteract) continue;

            if (!_priorityMap.TryGetValue(interactable.Type, out int priorityIndex)) continue;

            float distSqr = (interactable.Transform.position - playerPos).sqrMagnitude;

            // 1. 우선순위가 더 높거나 2. 우선순위는 같은데 거리가 더 가까운 경우 교체
            if (priorityIndex < bestPriorityIndex || (priorityIndex == bestPriorityIndex && distSqr < bestDistanceSqr))
            {
                bestPriorityIndex = priorityIndex;
                bestDistanceSqr = distSqr;
                bestMatch = interactable;
            }
        }
        return bestMatch;
    }

    /// <summary>
    /// 파괴된 Unity Object가 인터페이스 목록에 남아 발생하는 예외 참조를 제거합니다.
    /// </summary>
    private void CleanupNull()
    {
        _nearbyInteractables.RemoveAll(i =>
        {
            // 1. 인터페이스 참조를 UnityEngine.Object로 캐스팅
            // 유니티의 == null 연산자가 파괴된 객체를 true로 판정해줍니다.
            if (i is UnityEngine.Object obj)
            {
                if (obj == null) return true;

                // 비활성화된 오브젝트도 제거 (풀링된 오브젝트 대응)
                if (i.Transform != null && !i.Transform.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }

            // 2. 만약 유니티 오브젝트가 아닌 일반 C# 클래스라면 단순 null 체크
            return i == null;
        });
    }

    public void Remove(IInteractable interactable)
    {
        _nearbyInteractables.Remove(interactable);

        // 제거된 것이 현재 선택된 것이면 다시 업데이트
        if (_closestInteractable == interactable)
        {
            UpdateClosestInteractable();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        var interactables = other.GetComponents<IInteractable>();
        foreach (var interactable in interactables)
        {
            if (!_nearbyInteractables.Contains(interactable))
            {
                _nearbyInteractables.Add(interactable);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var interactables = other.GetComponents<IInteractable>();
        foreach (var interactable in interactables)
        {
            _nearbyInteractables.Remove(interactable);
        }
    }
}
