/*
 * 역할: 스킬 해금·레벨·쿨타임·FSM 상태와 애니메이션 이벤트를 관리합니다.
 * 핵심 설계: 레벨에 따라 근접 공격, 단일 투사체, 3방향 투사체 기능을 단계적으로 활성화합니다.
 */
using System;
using UnityEngine;

/// <summary>
/// 스킬 입력에서 판정·투사체 실행까지의 상위 흐름을 조정합니다.
/// </summary>
public class PlayerSkillController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private PlayerSkillData _skillData;

    private PlayerStateManager _stateManager;
    private PlayerAnimatorController _animatorController;
    private PlayerMove _playerMove;
    private PlayerMouseHelper _mouseHelper;
    private PlayerInputHandler _inputHandler;
    private TweenMovement _tweenMovement;
    private PlayerSkillRange _skillRange;
    private PlayerEffectController _effectController;
    private PlayerSound _playerSound;

    // 해금 여부와 투사체 확장 단계를 결정하는 현재 스킬 레벨입니다.
    private int _skillLevel = 0;
    // ScriptableObject 쿨타임을 기준으로 다음 사용 가능 시점을 계산합니다.
    private float _lastUseTime = -999f;

    public int MaxSkillLevel => _skillData.MaxSkillLevel;
    public int SkillLevel => _skillLevel;
    public bool IsUnlocked => _skillLevel >= 1;
    public bool HasProjectile => _skillLevel >= 2;
    public bool HasTripleProjectile => _skillLevel >= 3;
    public bool IsReady => Time.time >= _lastUseTime + _skillData.Cooldown;
    public float CooldownProgress => Mathf.Clamp01((Time.time - _lastUseTime) / _skillData.Cooldown);


    public event Action OnSkillUnlocked;
    public event Action<int> OnSkillUpgraded;
    public event Action OnSkillUsed;

    private void Awake()
    {
        _stateManager = GetComponent<PlayerStateManager>();
        _animatorController = GetComponent<PlayerAnimatorController>();
        _playerMove = GetComponent<PlayerMove>();
        _mouseHelper = GetComponent<PlayerMouseHelper>();
        _inputHandler = GetComponent<PlayerInputHandler>();
        _tweenMovement = GetComponent<TweenMovement>();
        _skillRange = GetComponent<PlayerSkillRange>();
        _effectController = GetComponent<PlayerEffectController>();
        _playerSound = GetComponent<PlayerSound>();
    }

    private void OnEnable()
    {
        _inputHandler.OnSkillInput += HandleSkillInput;
        StopSkillEffect();
        StopSkillUpEffect();
    }

    private void OnDisable()
    {
        _inputHandler.OnSkillInput -= HandleSkillInput;
    }

    private void HandleSkillInput()
    {
        if (!CanUseSkill()) return;

        UseSkill();
    }

    /// <summary>
    /// 해금, 쿨타임과 FSM의 Skill 가능 상태를 함께 검사합니다.
    /// </summary>
    private bool CanUseSkill()
    {
        return IsUnlocked && IsReady && _stateManager.CanSkill;
    }

    /// <summary>
    /// 최대 레벨을 넘지 않게 증가시키고 최초 해금·일반 업그레이드 이벤트를 구분합니다.
    /// </summary>
    public void UpgradeSkill()
    {
        if (_skillLevel >= MaxSkillLevel) return;

        _skillLevel++;

        if (_skillLevel == 1)
        {
            OnSkillUnlocked?.Invoke();
        }

        OnSkillUpgraded?.Invoke(_skillLevel);
        PlaySkillUpEffect();
    }

    /// <summary>
    /// Skill 상태 전환, 방향 회전, 애니메이션과 사용 이벤트를 시작합니다.
    /// </summary>
    private void UseSkill()
    {
        _stateManager.ChangeState(PlayerState.Skill);
        _mouseHelper.RotateTowardsMouse();
        _animatorController.SkillAnimation();
        _lastUseTime = Time.time;
        OnSkillUsed?.Invoke();
    }

    #region Skill Movement

    private void StartSkillMovement()
    {
        Vector3 mouseWorldPos = _mouseHelper.GetMouseWorldPosition();

        Vector3 offset = mouseWorldPos - transform.position;
        offset.y = 0;

        float maxDistance = _skillData.SkillMaxDistance;
        Vector3 clampedOffset = Vector3.ClampMagnitude(offset, maxDistance);
        float finalHorizontalDistance = clampedOffset.magnitude;

        Vector3 moveDirection = clampedOffset.normalized;
        if (moveDirection == Vector3.zero) moveDirection = transform.forward;

        float moveDuration = _animatorController.GetCurrentAnimationDuration();

        _tweenMovement.StartParabolicMovement(
            moveDirection,
            finalHorizontalDistance,
            _skillData.SkillJumpHeight,
            moveDuration,
            _skillData.SkillMoveEase);
    }

    #endregion

    #region Animation Events

    public void OnSkillAnimationStart()
    {
        StartSkillMovement();
    }

    /// <summary>
    /// 애니메이션 타격 프레임에서 근접 범위 판정을 실행합니다.
    /// </summary>
    public void OnSkillHit()
    {
        if (_skillRange != null)
        {
            _skillRange.ExecuteSkillHit();
            PlaySkillEffect();
            _playerSound?.PlaySkill(_skillLevel - 1);
            CameraShake.Instance?.Shake();
        }
    }

    /// <summary>
    /// 현재 레벨에 따라 단일 또는 3방향 투사체를 발사합니다.
    /// </summary>
    public void OnSkillProjectile()
    {
        if (!HasProjectile) return;

        if (_skillRange != null)
        {
            _skillRange.FireProjectile(HasTripleProjectile);
        }
    }

    /// <summary>
    /// 스킬 동작이 끝난 뒤 플레이어 상태를 Idle로 복구합니다.
    /// </summary>
    public void OnSkillAnimationEnd()
    {
        _stateManager.ChangeState(PlayerState.Idle);
    }

    #endregion

    private void StopSkillEffect()
    {
        _effectController?.StopEffect(PlayerEffectType.Skill);
    }

    private void PlaySkillEffect()
    {
        _effectController?.PlayEffect(PlayerEffectType.Skill);
    }

    private void StopSkillUpEffect()
    {
        _effectController?.StopEffect(PlayerEffectType.SkillUp);
    }
    private void PlaySkillUpEffect()
    {
        _effectController?.PlayEffect(PlayerEffectType.SkillUp);
    }
}
