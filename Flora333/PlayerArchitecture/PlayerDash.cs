/*
 * 역할: 대시 입력, 사용 가능 상태와 쿨타임을 검사하고 선형 행동 이동을 실행합니다.
 * 핵심 설계: 매 프레임 직접 대시 좌표를 계산하지 않고 TweenMovement의 완료 콜백으로 상태를 복구합니다.
 */
using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// FSM과 TweenMovement를 연결하는 대시 행동 컴포넌트입니다.
/// </summary>
public class PlayerDash : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private PlayerDashData _dashData;

    private PlayerAnimatorController _animatorController;
    private PlayerMove _playerMove;
    private PlayerStateManager _stateManager;
    private PlayerInputHandler _inputHandler;
    private TweenMovement _tweenMovement;
    private PlayerSound _playerSound;
    // 대시 재사용 가능 시점을 프레임 독립적으로 제한합니다.
    private float _dashCooldownTimer;

    public event Action<float> OnDashStart;
    public event Action OnDashFinish;

    private void Awake()
    {
        _playerMove = GetComponent<PlayerMove>();
        _animatorController = GetComponent<PlayerAnimatorController>();
        _stateManager = GetComponent<PlayerStateManager>();
        _inputHandler = GetComponent<PlayerInputHandler>();
        _tweenMovement = GetComponent<TweenMovement>();
        _playerSound = GetComponent<PlayerSound>();
    }

    private void OnEnable()
    {
        _inputHandler.OnDashInput += HandleDashInput;
    }

    private void OnDisable()
    {
        _inputHandler.OnDashInput -= HandleDashInput;
    }

    private void Update()
    {
        if (_dashCooldownTimer > 0)
        {
            _dashCooldownTimer -= Time.deltaTime;
        }
    }

    /// <summary>
    /// 쿨타임과 FSM 상태를 확인하고 이동 방향이 있을 때만 대시를 시작합니다.
    /// </summary>
    private void HandleDashInput()
    {
        if (_dashCooldownTimer > 0 || !_stateManager.CanDash) return;

        Vector3 direction = _playerMove.GetMovementDirection();

        if (direction.magnitude >= 0.1f)
        {
            StartDash(direction);
        }
    }

    /// <summary>
    /// 방향 회전, 상태 전환과 이동 거리 계산 후 공통 선형 이동을 실행합니다.
    /// </summary>
    private void StartDash(Vector3 direction)
    {
        _stateManager.ChangeState(PlayerState.Dashing);
        _dashCooldownTimer = _dashData.DashCoolDown;
        _animatorController.DashAnimation();
        _playerSound?.PlayDash();

        transform.rotation = Quaternion.LookRotation(direction);

        float dashDistance = _dashData.DashSpeed * _dashData.DashDuration;
        _tweenMovement.StartLinearMovement(
            direction,
            dashDistance,
            _dashData.DashDuration,
            Ease.Linear,
            EndDash);

        OnDashStart?.Invoke(_dashData.DashDuration);
    }

    /// <summary>
    /// 이동 완료 콜백에서 Idle 상태와 종료 이벤트를 복구합니다.
    /// </summary>
    private void EndDash()
    {
        _stateManager.ChangeState(PlayerState.Idle);
        OnDashFinish?.Invoke();
    }

    public bool IsDashing => _stateManager.IsState(PlayerState.Dashing);
    public float CooldownRemaining => _dashCooldownTimer;
}
