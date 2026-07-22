/*
 * 역할: 콤보 입력, 공격 상태 전환, 공격 이동과 애니메이션 판정 구간을 조정합니다.
 * 핵심 설계: 공격 중 대시 전환 시 이동·판정을 취소하되 콤보 진행 정책은 유지합니다.
 */
using UnityEngine;

/// <summary>
/// 입력 이벤트와 애니메이션 이벤트를 실제 공격 상태·판정 시스템에 연결합니다.
/// </summary>
public class PlayerAttack : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private PlayerAttackData _attackData;

    [Header("Attack Movement")]
    [SerializeField] private bool _enableAttackMovement = true;

    private PlayerMouseHelper _mouseHelper;
    private PlayerAnimatorController _animatorController;
    private PlayerStateManager _stateManager;
    private PlayerAttackRange _attackRange;
    private PlayerMove _playerMove;
    private PlayerDash _playerDash;
    private PlayerInputHandler _inputHandler;
    private TweenMovement _tweenMovement;
    private PlayerEffectController _effectController;
    private PlayerSound _playerSound;

    // 애니메이션 구간 중 중복 공격 시작을 차단하는 로컬 게이트입니다.
    private bool _canAttack = true;
    [SerializeField] private int _comboIndex = 0;
    private float _lastAttackTime;
    private const float AttackTimeout = 1f;
    private void Awake()
    {
        _animatorController = GetComponent<PlayerAnimatorController>();
        _stateManager = GetComponent<PlayerStateManager>();
        _attackRange = GetComponent<PlayerAttackRange>();
        _mouseHelper = GetComponent<PlayerMouseHelper>();
        _playerMove = GetComponent<PlayerMove>();
        _playerDash = GetComponent<PlayerDash>();
        _inputHandler = GetComponent<PlayerInputHandler>();
        _tweenMovement = GetComponent<TweenMovement>();
        _effectController = GetComponent<PlayerEffectController>();
        _playerSound = GetComponent<PlayerSound>();
    }

    private void OnEnable()
    {
        _stateManager.OnStateChanged += OnStateChanged;
        _playerDash.OnDashFinish += OnDashFinished;
        _inputHandler.OnAttackInput += HandleAttackInput;
    }

    private void OnDisable()
    {
        _stateManager.OnStateChanged -= OnStateChanged;
        _playerDash.OnDashFinish -= OnDashFinished;
        _inputHandler.OnAttackInput -= HandleAttackInput;
    }

    /// <summary>
    /// 공격이 대시로 취소될 때 이동·히트 판정을 정리하고 콤보 정책을 적용합니다.
    /// </summary>
    private void OnStateChanged(PlayerState from, PlayerState to)
    {
        if (from == PlayerState.Attacking && to == PlayerState.Dashing)
        {
            StopAttackMovement();

            if (_attackRange != null)
            {
                _attackRange.StopAttack();
            }

            _comboIndex++;
            if (_comboIndex >= _attackData.MaxComboCount)
            {
                _comboIndex = 0;
            }
        }
    }

    private void Update()
    {
        CheckComboTimeout();
        CheckAttackStateTimeout();
    }

    private void CheckComboTimeout()
    {
        if (_comboIndex > 0 && Time.time - _lastAttackTime > _attackData.ComboWindowTime)
        {
            ResetCombo();
        }
    }

    private void CheckAttackStateTimeout()
    {
        if (!_stateManager.IsState(PlayerState.Attacking)) return;
        if (Time.time - _lastAttackTime < AttackTimeout) return;

        _attackRange?.StopAttack();
        _canAttack = true;
        FinishAttack();
        ResetCombo();
    }

    /// <summary>
    /// 상태와 쿨다운을 검사한 뒤 마우스 방향 회전과 공격 시작을 수행합니다.
    /// </summary>
    private void HandleAttackInput()
    {
        if (!_canAttack || !_stateManager.CanAttack) return;

        _mouseHelper.RotateTowardsMouse();
        StartAttack();
    }

    private void StartAttack()
    {
        _stateManager.ChangeState(PlayerState.Attacking);
        _canAttack = false;
        _lastAttackTime = Time.time;

        AttackAnimation();
    }

    private void ResetCombo()
    {
        _comboIndex = 0;
    }

    private void FinishAttack()
    {
        _stateManager.ChangeState(PlayerState.Idle);
        StopAttackMovement();
    }

    private void AttackAnimation()
    {
        _animatorController.AttackAnimation(_comboIndex);
    }

    #region Attack Movement

    /// <summary>
    /// 콤보별 거리와 Ease를 사용해 TweenMovement에 공격 전진을 위임합니다.
    /// </summary>
    private void StartAttackMovement(int comboIndex)
    {
        if (!_enableAttackMovement)
        {
            return;
        }

        if (comboIndex < 0 || comboIndex >= _attackData.AttackMoveDistance.Length ||
            comboIndex >= _attackData.AttackMoveEase.Length)
        {
            Debug.LogError($"Invalid combo index: {comboIndex}");
            return;
        }

        Vector3 direction = _playerMove.GetMovementDirection();
        if (direction.magnitude < 0.1f) return;

        float moveDuration = _animatorController.GetCurrentAnimationDuration();
        _tweenMovement.StartLinearMovement(
            transform.forward,
            _attackData.AttackMoveDistance[comboIndex],
            moveDuration,
            _attackData.AttackMoveEase[comboIndex]);
    }

    private void StopAttackMovement()
    {
        _tweenMovement.Stop();
    }

    #endregion

    #region Animation Events

    public void OnAttackAnimationStart()
    {
        if (!_stateManager.IsState(PlayerState.Attacking)) return;

        StartAttackMovement(_comboIndex);
    }

    /// <summary>
    /// 애니메이션의 실제 타격 프레임부터 연속 범위 판정을 시작합니다.
    /// </summary>
    public void OnAttackHitStart()
    {
        if (_attackRange != null)
        {
            SlashStart();
            _attackRange.StartAttack(_comboIndex);
        }

        _playerSound?.PlayAttack(_comboIndex);
    }

    /// <summary>
    /// 검 휘두르기 종료 시점에 범위 판정을 중단합니다.
    /// </summary>
    public void OnAttackHitFinish()
    {
        if (_attackRange != null)
        {
            _attackRange.StopAttack();
        }
    }

    public void OnAttackAnimationEnd()
    {
        if (!_stateManager.IsState(PlayerState.Attacking)) return;

        _canAttack = true;
        FinishAttack();

        _comboIndex++;
        if (_comboIndex >= _attackData.MaxComboCount)
        {
            _comboIndex = 0;
        }
    }

    public void OnFinishAttackAnimationEnd()
    {
        if (!_stateManager.IsState(PlayerState.Attacking)) return;

        _canAttack = true;
        FinishAttack();
        ResetCombo();
    }

    #endregion

    private void OnDashFinished()
    {
        _canAttack = true;
    }

    private void SlashStart()
    {
        _effectController?.PlaySlash(_comboIndex);
    }
}
