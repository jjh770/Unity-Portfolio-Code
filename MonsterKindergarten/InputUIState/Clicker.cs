using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Clicker : MonoBehaviour
{
    [SerializeField] private float _dragThresholdTime = 0.2f;
    [SerializeField] private float _dragThresholdDistance = 0.3f;
    [SerializeField] private float _mergeDetectionRadius = 0.65f;

    // 콜라이더 실효 크기가 0.75 x 0.42이고 그려지는 슬라임은 0.8 x 0.5쯤이다.
    // 둘의 차이(모서리 둥글림 포함)가 약 0.1, 손가락 굵기가 약 0.1이라 0.2면
    // 그림 위를 누른 손가락은 모두 닿는다. 더 키우면 빈 곳을 눌러도 잡히므로
    // 메인 필드의 클릭이 쉬워지는 쪽으로 성격이 바뀐다.
    [Tooltip("터치가 빗나갔을 때 이 반경 안에서 가장 가까운 슬라임을 집습니다. 0이면 정확히 닿은 것만 고릅니다.")]
    [SerializeField, Min(0f)] private float _selectionRadius = 0.2f;

    [Header("Drag Bounds")]
    [SerializeField] private Vector2 _dragMinBounds = new Vector2(-5f, -3f);
    [SerializeField] private Vector2 _dragMaxBounds = new Vector2(5f, 3f);

    private SlimeController _selectedTarget;
    private SlimeController _mergeCandidate;
    private Camera _mainCamera;
    private Vector2 _mouseDownPos;
    private float _mouseDownTime;
    private bool _isDragging;
    private bool _isClickEnabled = true;
    private bool _isDragEnabled = true;
    private bool _invokeClickAction = true;
    private SlimeController _restrictedTarget;
    private SlimeController _secondaryRestrictedTarget;

    // 손가락 보정용. 프레임마다 새 리스트를 만들지 않도록 하나를 돌려 쓴다.
    private readonly List<Collider2D> _selectionHits = new(8);
    private ContactFilter2D _selectionFilter;
    // 합성 후보 탐색용이다. 드래그 중 프레임마다 돌므로 배열을 새로 받지 않고 재사용한다.
    // 후보는 SlimeController가 있는 것만 쓰므로 레이어와 트리거는 거르지 않는다.
    private readonly List<Collider2D> _mergeHits = new(8);

    // 소유자별 입력 요청. 우선순위가 높은 요청을 적용한다.
    // 같은 우선순위에서는 나중에 등록된 요청이 우선한다.
    private readonly List<ModeRequest> _modeRequests = new();

    public event System.Action<SlimeController> MergeCandidateChanged;
    public event System.Action<SlimeController> TargetClicked;
    public event System.Action<SlimeController> TargetDragCompleted;

    private void Awake()
    {
        _mainCamera = Camera.main;

        // 기본 레이캐스트 대상만 본다. 장식장 놀이터 오브젝트처럼 Ignore Raycast에
        // 둔 것은 여기서도 빠진다. 트리거는 보지 않는다 - 슬라임 콜라이더는 트리거가
        // 아니고, 범퍼 같은 트리거를 집어도 쓸 데가 없다.
        _selectionFilter = default;
        _selectionFilter.useTriggers = false;
        _selectionFilter.SetLayerMask(Physics2D.DefaultRaycastLayers);
        _selectionFilter.useLayerMask = true;
    }

    private void Update()
    {
        if (!GameplayGate.IsActive)
        {
            CancelSelection();
            return;
        }

        if (PointerGestureUtility.HasMultipleActiveTouches())
        {
            CancelSelection();
            return;
        }

        Pointer pointer = Pointer.current;
        if (pointer == null || _mainCamera == null) return;

        Vector2 pointerPosition = pointer.position.ReadValue();

        if (pointer.press.wasPressedThisFrame)
        {
            TrySelect(pointerPosition);
        }
        else if (pointer.press.isPressed && _selectedTarget != null)
        {
            CheckDragStart(pointerPosition);
            if (_isDragging)
            {
                UpdateDrag(pointerPosition);
            }
        }
        else if (pointer.press.wasReleasedThisFrame && _selectedTarget != null)
        {
            OnPointerUp(pointerPosition);
        }
    }

    private void OnDisable()
    {
        CancelSelection();
    }

    private void TrySelect(Vector2 pointerPosition)
    {
        if (!_isClickEnabled && !_isDragEnabled) return;

        Vector2 worldPos = _mainCamera.ScreenToWorldPoint(pointerPosition);
        SlimeController clickTarget = FindSelectionTarget(worldPos);
        if (clickTarget == null) return;

        _selectedTarget = clickTarget;
        _mouseDownPos = worldPos;
        _mouseDownTime = Time.time;
        _isDragging = false;
    }

    // 손가락은 점이 아니다. 콜라이더를 그림보다 작게 잡으면 점 레이캐스트로는 자주
    // 빗나가는데, 콜라이더를 다시 키우면 슬라임끼리 부딪히는 자리가 그림 밖으로
    // 나간다. 충돌 모양과 터치 범위는 서로 다른 요구라 여기서 나눈다.
    //
    // 정확히 닿은 것을 먼저 보고, 없을 때만 반경 안에서 가장 가까운 것을 집는다.
    // 겹쳐 있는 슬라임 중 엉뚱한 쪽을 빼앗지 않기 위해서다.
    private SlimeController FindSelectionTarget(Vector2 worldPos)
    {
        RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero, 0f);
        if (hit)
        {
            SlimeController exactTarget = hit.collider.GetComponent<SlimeController>();
            if (IsSelectable(exactTarget)) return exactTarget;
        }

        if (_selectionRadius <= 0f) return null;

        _selectionHits.Clear();
        Physics2D.OverlapCircle(
            worldPos, _selectionRadius, _selectionFilter, _selectionHits);

        SlimeController nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (Collider2D candidateCollider in _selectionHits)
        {
            if (candidateCollider == null) continue;

            SlimeController candidate =
                candidateCollider.GetComponent<SlimeController>();
            if (!IsSelectable(candidate)) continue;

            float distance =
                ((Vector2)candidate.transform.position - worldPos).sqrMagnitude;
            if (distance >= nearestDistance) continue;

            nearestDistance = distance;
            nearest = candidate;
        }

        return nearest;
    }

    // 화면 좌표 아래에 집을 수 있는 슬라임이 있는가. 같은 손가락을 나눠 쓰는 쪽
    // (확대한 장식장의 화면 이동)이 슬라임을 누른 손가락을 가려내려고 묻는다.
    public bool HasSelectionTargetAt(Vector2 screenPosition)
    {
        return _mainCamera != null &&
               FindSelectionTarget(_mainCamera.ScreenToWorldPoint(screenPosition)) != null;
    }

    // 튜토리얼이 대상을 지정한 동안에는 그 슬라임만 고를 수 있다.
    private bool IsSelectable(SlimeController target)
    {
        return target != null &&
               (_restrictedTarget == null ||
                target == _restrictedTarget ||
                target == _secondaryRestrictedTarget);
    }

    private void CheckDragStart(Vector2 pointerPosition)
    {
        if (!_isDragEnabled || _isDragging) return;

        Vector2 currentPos = _mainCamera.ScreenToWorldPoint(pointerPosition);
        float distance = Vector2.Distance(_mouseDownPos, currentPos);
        float heldTime = Time.time - _mouseDownTime;

        // 일정 거리 이상 이동하거나 일정 시간 이상 누르면 드래그 시작
        if (distance > _dragThresholdDistance || heldTime > _dragThresholdTime)
        {
            _isDragging = true;
            _selectedTarget.StartDrag();
        }
    }

    private void UpdateDrag(Vector2 pointerPosition)
    {
        Vector2 mousePos = _mainCamera.ScreenToWorldPoint(pointerPosition);

        mousePos.x = Mathf.Clamp(mousePos.x, _dragMinBounds.x, _dragMaxBounds.x);
        mousePos.y = Mathf.Clamp(mousePos.y, _dragMinBounds.y, _dragMaxBounds.y);

        _selectedTarget.transform.position = mousePos;
        UpdateMergeCandidate();
    }

    private void OnPointerUp(Vector2 pointerPosition)
    {
        SlimeController selectedTarget = _selectedTarget;

        if (_isDragging)
        {
            // 릴리스 프레임의 포인터 위치까지 반영한 뒤 표시된 대상을 우선 합성한다.
            UpdateDrag(pointerPosition);
            _selectedTarget.EndDrag(_mergeCandidate);

            Vector2 releaseWorldPosition = _mainCamera.ScreenToWorldPoint(pointerPosition);
            if (Vector2.Distance(_mouseDownPos, releaseWorldPosition) >= _dragThresholdDistance)
            {
                TargetDragCompleted?.Invoke(selectedTarget);
            }
        }
        else if (_isClickEnabled)
        {
            if (_invokeClickAction)
            {
                // 클릭 처리 - ClickTarget의 레벨별 포인트 사용
                ClickInfo clickInfo = new ClickInfo
                {
                    ClickType = EClickType.Manual,
                    Point = PointCalculator.Calculate(
                        _selectedTarget.Point,
                        _selectedTarget.Grade,
                        EClickType.Manual,
                        _selectedTarget.IsSpecial),
                    Position = _mouseDownPos,
                    Grade = _selectedTarget.Grade
                };
                _selectedTarget.OnClick(clickInfo);
            }

            TargetClicked?.Invoke(selectedTarget);
        }

        SetMergeCandidate(null);
        _selectedTarget = null;
        _isDragging = false;
    }

    public void PushMode(
        object owner,
        ClickerInputMode mode,
        ClickerInputPriority priority = ClickerInputPriority.Selection)
    {
        if (owner == null)
        {
            throw new ArgumentNullException(nameof(owner));
        }

        int index = FindRequestIndex(owner);
        if (index >= 0)
        {
            // 같은 소유자의 갱신은 제자리에서 처리한다.
            // 갱신만으로 같은 우선순위의 다른 요청을 밀어내지 않는다.
            _modeRequests[index] = new ModeRequest(owner, mode, priority);
        }
        else
        {
            _modeRequests.Add(new ModeRequest(owner, mode, priority));
            WarnIfStackTooDeep();
        }

        ApplyEffectiveMode();
    }

    // 소유자는 사라질 때 반드시 해제해야 한다. 빠뜨리면 입력이 잠긴 채 남는다.
    public void ReleaseMode(object owner)
    {
        if (owner == null || !RemoveRequest(owner)) return;

        ApplyEffectiveMode();
    }

    private bool RemoveRequest(object owner)
    {
        int index = FindRequestIndex(owner);
        if (index < 0) return false;

        _modeRequests.RemoveAt(index);
        return true;
    }

    private int FindRequestIndex(object owner)
    {
        for (int i = 0; i < _modeRequests.Count; ++i)
        {
            if (ReferenceEquals(_modeRequests[i].Owner, owner)) return i;
        }

        return -1;
    }

    // 초기화 순서가 달라도 공간 기본값이 튜토리얼을 덮지 않게 한다.
    private void ApplyEffectiveMode()
    {
        int effectiveIndex = -1;
        for (int i = 0; i < _modeRequests.Count; ++i)
        {
            if (effectiveIndex < 0 ||
                _modeRequests[i].Priority >= _modeRequests[effectiveIndex].Priority)
            {
                effectiveIndex = i;
            }
        }

        ApplyMode(effectiveIndex >= 0
            ? _modeRequests[effectiveIndex].Mode
            : ClickerInputMode.Free);
    }

    private void ApplyMode(ClickerInputMode mode)
    {
        CancelSelection();
        _isClickEnabled = mode.ClickEnabled;
        _isDragEnabled = mode.DragEnabled;
        _restrictedTarget = mode.RestrictedTarget;
        _secondaryRestrictedTarget = mode.SecondaryRestrictedTarget;
        _invokeClickAction = mode.InvokeClickAction;
    }

    private void WarnIfStackTooDeep()
    {
#if UNITY_EDITOR
        if (_modeRequests.Count <= ModeStackWarningDepth) return;

        var owners = new List<string>(_modeRequests.Count);
        foreach (ModeRequest request in _modeRequests)
        {
            owners.Add(request.Owner?.GetType().Name ?? "(null)");
        }

        Debug.LogWarning(
            $"입력 모드 스택이 {_modeRequests.Count}단입니다. 해제를 빠뜨린 소유자가 있는지 확인하세요. : " +
            string.Join(" > ", owners),
            this);
#endif
    }

#if UNITY_EDITOR
    // 정상 상태에서 겹칠 수 있는 최대 깊이. 공간 + 모드 + 튜토리얼 + 여유 1.
    private const int ModeStackWarningDepth = 4;
#endif

    private readonly struct ModeRequest
    {
        public object Owner { get; }
        public ClickerInputMode Mode { get; }
        public ClickerInputPriority Priority { get; }

        public ModeRequest(object owner, ClickerInputMode mode, ClickerInputPriority priority)
        {
            Owner = owner;
            Mode = mode;
            Priority = priority;
        }
    }

    private void UpdateMergeCandidate()
    {
        SlimeController nearestCandidate = null;
        float nearestDistanceSqr = float.MaxValue;
        _mergeHits.Clear();
        Physics2D.OverlapCircle(
            _selectedTarget.transform.position,
            Mathf.Max(0f, _mergeDetectionRadius),
            ContactFilter2D.noFilter,
            _mergeHits);

        foreach (Collider2D hit in _mergeHits)
        {
            SlimeController candidate = hit.GetComponent<SlimeController>();
            if (!_selectedTarget.CanMergeWith(candidate)) continue;

            float distanceSqr = (
                candidate.transform.position - _selectedTarget.transform.position).sqrMagnitude;
            if (distanceSqr >= nearestDistanceSqr) continue;

            nearestCandidate = candidate;
            nearestDistanceSqr = distanceSqr;
        }

        SetMergeCandidate(nearestCandidate);
    }

    private void SetMergeCandidate(SlimeController candidate)
    {
        if (_mergeCandidate == candidate) return;

        _mergeCandidate = candidate;
        MergeCandidateChanged?.Invoke(candidate);
    }

    private void CancelSelection()
    {
        SetMergeCandidate(null);

        if (_selectedTarget != null && _isDragging)
        {
            _selectedTarget.CancelDrag();
        }

        _selectedTarget = null;
        _isDragging = false;
    }
}
