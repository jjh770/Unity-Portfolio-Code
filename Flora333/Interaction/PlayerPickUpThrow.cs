/*
 * 역할: 상호작용 결과를 들기 상태로 연결하고 복수 오브젝트를 보관·투척합니다.
 * 핵심 설계: 실제 부착과 Rigidbody 투척은 애니메이션 이벤트 시점에 실행해 시각 동작과 게임 상태를 맞춥니다.
 */
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// IPickable 목록, 보유 지점과 PickUp·Throw 상태 전환을 관리합니다.
/// </summary>
public class PlayerPickUpThrow : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int _maxPickUpCount = 2;
    [SerializeField] private Transform[] _holdPoints;
    [SerializeField] private float _throwForce = 10f;
    [SerializeField] private float _throwUpwardAngle = 0.5f;

    [Header("Debug")]
    [SerializeField] private List<IPickable> _heldObjects = new List<IPickable>();

    private PlayerStateManager _stateManager;
    private PlayerAnimatorController _animatorController;
    private PlayerMouseHelper _mouseHelper;
    private PlayerInteraction _playerInteraction;
    private PlayerInputHandler _inputHandler;

    // 상호작용 후 PickUp 애니메이션 이벤트에서 실제 부착할 대상을 임시 보관합니다.
    private IPickable _pendingPickable;

    // 현재 들고 있는 오브젝트를 획득 순서대로 보관해 FIFO 투척에 사용합니다.
    public bool IsHoldingObject => _heldObjects.Count > 0;
    public bool CanPickUp => _heldObjects.Count < _maxPickUpCount && _heldObjects.Count < _holdPoints.Length;
    public int HeldCount => _heldObjects.Count;

    public event Action<bool> OnHoldingChanged;
    public event Action<IPickable> OnPickedUpItem;
    public event Action<IPickable> OnThrownItem;

    private void Awake()
    {
        _stateManager = GetComponent<PlayerStateManager>();
        _animatorController = GetComponent<PlayerAnimatorController>();
        _mouseHelper = GetComponent<PlayerMouseHelper>();
        _playerInteraction = GetComponent<PlayerInteraction>();
        _inputHandler = GetComponent<PlayerInputHandler>();
    }

    private void OnEnable()
    {
        _playerInteraction.OnInteract += HandleInteract;
        _inputHandler.OnThrowInput += HandleThrowInput;
    }

    private void OnDisable()
    {
        _playerInteraction.OnInteract -= HandleInteract;
        _inputHandler.OnThrowInput -= HandleThrowInput;
    }

    private void HandleThrowInput()
    {
        if (!IsHoldingObject || !CanDoThrow()) return;

        StartThrow();
    }

    /// <summary>
    /// 선택 대상이 IPickable이고 보유 한도와 상태 조건을 만족하면 들기 흐름을 시작합니다.
    /// </summary>
    private void HandleInteract(IInteractable interactable)
    {
        if (interactable is IPickable pickable)
        {
            if (CanPickUp && CanDoPickUp())
            {
                _pendingPickable = pickable;
                StartPickUp();
            }
        }
    }

    private bool CanDoPickUp()
    {
        return _stateManager.IsInStates(PlayerState.Idle, PlayerState.Moving, PlayerState.PickUp);
    }

    private bool CanDoThrow()
    {
        return _stateManager.IsState(PlayerState.PickUp);
    }

    private void StartPickUp()
    {
        _stateManager.ChangeState(PlayerState.PickUp);
        _animatorController.PickUpAnimation();
    }

    private void StartThrow()
    {
        _mouseHelper.RotateTowardsMouse();
        _stateManager.ChangeState(PlayerState.Throw);
        _animatorController.ThrowAnimation();
    }

    /// <summary>
    /// 애니메이션 이벤트: PickUp 실행
    /// </summary>
    public void OnPickUp()
    {
        if (_pendingPickable != null)
        {
            int holdIndex = _heldObjects.Count;
            if (holdIndex < _holdPoints.Length)
            {
                bool wasEmpty = _heldObjects.Count == 0;

                IPickable pickedUp = _pendingPickable;
                _pendingPickable.OnPickedUp(_holdPoints[holdIndex]);
                _heldObjects.Add(_pendingPickable);
                _playerInteraction.Remove(_pendingPickable);
                _pendingPickable = null;

                OnPickedUpItem?.Invoke(pickedUp);

                if (wasEmpty)
                {
                    OnHoldingChanged?.Invoke(true);
                }
            }
        }
    }

    /// <summary>
    /// 애니메이션 이벤트: PickUp 애니메이션 종료
    /// </summary>
    public void OnPickUpAnimationEnd()
    {
        // PickUp 상태 유지 (들고 있는 상태)
    }

    /// <summary>
    /// 애니메이션 이벤트: Throw 실행
    /// </summary>
    public void OnThrow()
    {
        if (_heldObjects.Count > 0)
        {
            // FIFO: 먼저 집은 것부터 던짐
            IPickable pickable = _heldObjects[0];
            _heldObjects.RemoveAt(0);

            Vector3 throwDirection = (transform.forward + Vector3.up * _throwUpwardAngle).normalized;
            pickable.OnThrown(throwDirection, _throwForce);

            OnThrownItem?.Invoke(pickable);

            // 남은 오브젝트 위치 재정렬
            RearrangeHeldObjects();
        }
    }

    /// <summary>
    /// 투척 후 남은 오브젝트를 앞쪽 HoldPoint부터 재배치합니다.
    /// </summary>
    private void RearrangeHeldObjects()
    {
        for (int i = 0; i < _heldObjects.Count; i++)
        {
            if (i < _holdPoints.Length)
            {
                Transform holdPoint = _holdPoints[i];
                _heldObjects[i].Transform.SetParent(holdPoint);
                _heldObjects[i].Transform.localPosition = Vector3.zero;
                _heldObjects[i].Transform.localRotation = Quaternion.identity;
            }
        }
    }

    /// <summary>
    /// 애니메이션 이벤트: Throw 애니메이션 종료
    /// </summary>
    public void OnThrowAnimationEnd()
    {
        // 아직 들고 있는게 있으면 PickUp 상태 유지, 없으면 Idle
        if (_heldObjects.Count > 0)
        {
            _stateManager.ChangeState(PlayerState.PickUp);
            _animatorController.PickUpAnimation();
        }
        else
        {
            _stateManager.ChangeState(PlayerState.Idle);
            _animatorController.ThrowFinishAnimation();
            OnHoldingChanged?.Invoke(false);
        }
    }
}
