/*
 * 역할: Unity 입력을 이동 값과 공격·대시·상호작용 이벤트로 변환합니다.
 * 핵심 설계: 행동 컴포넌트에서 직접 KeyCode와 마우스 입력을 읽지 않게 해 입력 해석과 게임 로직을 분리합니다.
 */
using System;
using UnityEngine;

/// <summary>
/// 플레이어 입력의 단일 진입점이며 현재 상태에 따라 동일 입력을 다른 행동 이벤트로 해석합니다.
/// </summary>
public class PlayerInputHandler : MonoBehaviour
{
    [Header("Key Bindings")]
    [SerializeField] private KeyCode _dashKey = KeyCode.Space;
    [SerializeField] private KeyCode _skillKey = KeyCode.Q;
    [SerializeField] private KeyCode _interactKey = KeyCode.E;

    private PlayerStateManager _stateManager;

    public event Action OnAttackInput;
    public event Action OnThrowInput;
    public event Action OnDashInput;
    public event Action OnSkillInput;
    public event Action OnInteractInput;

    // 일반 이동 컴포넌트가 매 프레임 읽는 정규화 전 이동 입력입니다.
    public Vector2 MoveInput { get; private set; }

    private void Awake()
    {
        _stateManager = GetComponent<PlayerStateManager>();
    }
    private void Start()
    {
        _stateManager.OnPlayState += HandleCanMove;

        // 초기 상태 설정 - Playing 상태가 아니면 입력 비활성화
        if (GameStateManager.Instance != null)
        {
            this.enabled = GameStateManager.Instance.IsPlaying;
        }
    }
    private void OnDestroy()
    {
        _stateManager.OnPlayState -= HandleCanMove;
    }
    private void HandleCanMove(bool canInput)
    {
        this.enabled = canInput;
    }

    /// <summary>
    /// 입력 장치 상태를 읽고 필요한 행동 이벤트를 한 번만 발생시킵니다.
    /// </summary>
    private void Update()
    {
        if (_stateManager.IsDead) return;

        HandleMoveInput();
        HandleActionInput();
    }

    private void HandleMoveInput()
    {
        MoveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );
    }

    /// <summary>
    /// 동일한 좌클릭을 현재 운반 상태에 따라 공격 또는 던지기 이벤트로 해석합니다.
    /// </summary>
    private void HandleActionInput()
    {
        // 마우스 좌클릭 - 상태에 따라 다른 이벤트
        if (Input.GetMouseButtonDown(0))
        {
            if (_stateManager.IsHolding)
                OnThrowInput?.Invoke();
            else
                OnAttackInput?.Invoke();
        }

        // 마우스 홀드 - 연속 공격용
        else if (Input.GetMouseButton(0) && !_stateManager.IsHolding)
        {
            OnAttackInput?.Invoke();
        }

        if (Input.GetKeyDown(_dashKey))
            OnDashInput?.Invoke();

        if (Input.GetKeyDown(_skillKey))
            OnSkillInput?.Invoke();

        if (Input.GetKeyDown(_interactKey))
            OnInteractInput?.Invoke();
    }
}
