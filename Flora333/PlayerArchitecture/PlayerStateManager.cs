/*
 * 역할: 플레이어의 현재 상태와 상태 전환 규칙, 행동별 실행 가능 여부를 중앙 관리합니다.
 * 핵심 설계: 기능 컴포넌트가 서로 직접 상태를 수정하지 않고 동일한 전환 정책을 사용하도록 합니다.
 */
using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// 플레이어 행동 충돌을 제어하기 위한 FSM 상태 집합입니다.
/// </summary>
public enum PlayerState
{
    Idle,
    Moving,
    Attacking,
    Dashing,
    PickUp,
    Throw,
    Skill,
    Die,
    Clear
}

/// <summary>
/// 상태 변경 검증과 변경 이벤트를 제공하는 FSM 관리자입니다.
/// </summary>
public class PlayerStateManager : MonoBehaviour
{
    [SerializeField] private PlayerState _currentState = PlayerState.Idle;

    // 모든 행동 가능 여부와 전환 검증의 기준이 되는 현재 상태입니다.
    public PlayerState CurrentState => _currentState;

    public event Action<bool> OnPlayState;
    public event Action<PlayerState, PlayerState> OnStateChanged;
    // 기본 전환 규칙 외에 외부 컴포넌트가 추가 조건을 제공하는 확장 지점입니다.
    public event Func<PlayerState, PlayerState, bool> OnValidateStateChange;
    public bool IsDead => _currentState == PlayerState.Die;
    public bool IsClear => _currentState == PlayerState.Clear;
    public bool CanMove => !IsDead && !IsClear && (_currentState == PlayerState.Idle || _currentState == PlayerState.Moving || _currentState == PlayerState.Attacking || _currentState == PlayerState.PickUp);
    public bool CanCarry => !IsDead && !IsClear && (_currentState == PlayerState.Idle || _currentState == PlayerState.Moving || _currentState == PlayerState.PickUp);
    public bool CanThrow => !IsDead && !IsClear && _currentState == PlayerState.PickUp;
    public bool CanAttack => !IsDead && !IsClear && (_currentState == PlayerState.Idle || _currentState == PlayerState.Moving || _currentState == PlayerState.Attacking);
    public bool CanDash => !IsDead && !IsClear && (_currentState == PlayerState.Moving || _currentState == PlayerState.Attacking);
    public bool CanSkill => !IsDead && !IsClear && (_currentState == PlayerState.Idle || _currentState == PlayerState.Moving);
    public bool IsHolding => _currentState == PlayerState.PickUp || _currentState == PlayerState.Throw;

    private void Start()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.OnGameStateChanged += HandleGameStateChanged;

            // 초기 상태 수동 적용 (이벤트 구독 전에 상태가 변경되었을 수 있음)
            bool isPlaying = GameStateManager.Instance.IsPlaying;
            OnPlayState?.Invoke(isPlaying);
        }
    }
    private void OnDestroy()
    {
        GameStateManager.OnGameStateChanged -= HandleGameStateChanged;
    }

    private void HandleGameStateChanged(GameState oldState, GameState newState)
    {
        bool isPlaying = (newState == GameState.Playing);
        OnPlayState?.Invoke(isPlaying);

        if (newState == GameState.Outro)
        {
            ChangeState(PlayerState.Clear);

        }
    }
    /// <summary>
    /// 중복 전환과 금지된 전환을 거부한 뒤 상태 변경 이벤트를 발행합니다.
    /// </summary>
    public void ChangeState(PlayerState newState)
    {
        if (_currentState == newState)
            return;

        PlayerState previousState = _currentState;

        // 상태 전환 규칙 검증
        if (!IsValidTransition(previousState, newState))
        {
            Debug.Log($"{previousState}에서는 {newState}로 갈수 없음");
            return;
        }

        _currentState = newState;
        OnStateChanged?.Invoke(previousState, newState);
    }

    /// <summary>
    /// 사망·클리어와 행동 상태별 허용 전환을 한곳에서 판정합니다.
    /// </summary>
    private bool IsValidTransition(PlayerState from, PlayerState to)
    {
        if (from == PlayerState.Die || from == PlayerState.Clear)
            return false;
        // 외부 검증자들에게 먼저 검증 요청
        if (OnValidateStateChange != null)
        {
            foreach (var validator in OnValidateStateChange.GetInvocationList())
            {
                if (!(bool)validator.DynamicInvoke(from, to))
                    return false;
            }
        }

        // Die 상태로는 어느 상태에서든 전환 가능
        if (to == PlayerState.Die || to == PlayerState.Clear)
            return true;
        // 기본 상태 전환 규칙 검증
        if (to == PlayerState.Idle)
            return true;

        switch (from)
        {
            case PlayerState.Idle:
                return true; // Idle에서는 모든 상태로 전환 가능

            case PlayerState.Moving:
                return true; // Moving에서도 모든 상태로 전환 가능

            case PlayerState.Attacking:
                // 공격 중에는 이동, 대시, 스킬로 전환 가능
                return to == PlayerState.Moving || to == PlayerState.Dashing;

            case PlayerState.Dashing:
                // 대시 중에는 상태 전환 불가 (대시가 끝나야 함)
                return false;

            case PlayerState.PickUp:
                // PickUp 상태에서는 Throw로 전환 가능
                return to == PlayerState.Throw;

            case PlayerState.Throw:
                // Throw 상태에서는 Idle이나 PickUp으로 전환 가능
                return to == PlayerState.PickUp || to == PlayerState.Idle;

            case PlayerState.Skill:
                // Skill 상태에서는 Idle로만 전환 가능 (스킬 종료 시)
                return to == PlayerState.Idle || to == PlayerState.Moving;

            default:
                return false;
        }
    }

    public bool IsState(PlayerState state)
    {
        return _currentState == state;
    }

    /// <summary>
    /// 여러 허용 상태 중 하나인지 행동 컴포넌트가 간단히 확인하게 합니다.
    /// </summary>
    public bool IsInStates(params PlayerState[] states)
    {
        return states.Contains(_currentState);
    }
}
