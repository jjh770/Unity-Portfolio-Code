/*
 * 역할: 하나의 마우스 입력을 짧은 클릭과 슬라임 드래그로 구분합니다.
 * 핵심 설계: 누른 시간 또는 이동 거리 임계값을 넘을 때만 드래그로 전환해 클릭 오작동을 줄입니다.
 */
using UnityEngine;

/// <summary>
/// 슬라임 선택, 클릭 보상 요청과 드래그 위치 갱신을 담당합니다.
/// </summary>
public class Clicker : MonoBehaviour
{
    [SerializeField] private float _dragThresholdTime = 0.2f;
    [SerializeField] private float _dragThresholdDistance = 0.3f;

    [Header("Drag Bounds")]
    [SerializeField] private Vector2 _dragMinBounds = new Vector2(-5f, -3f);
    [SerializeField] private Vector2 _dragMaxBounds = new Vector2(5f, 3f);

    private SlimeController _selectedTarget;
    private Camera _mainCamera;
    // 클릭 보상 이펙트 위치와 드래그 거리 계산의 기준점입니다.
    private Vector2 _mouseDownPos;
    private float _mouseDownTime;
    // MouseUp 시 클릭과 드래그 종료 중 하나만 실행하게 하는 상태입니다.
    private bool _isDragging;

    private void Awake()
    {
        _mainCamera = Camera.main;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            TrySelect();
        }
        else if (Input.GetMouseButton(0) && _selectedTarget != null)
        {
            CheckDragStart();
            if (_isDragging)
            {
                UpdateDrag();
            }
        }
        else if (Input.GetMouseButtonUp(0) && _selectedTarget != null)
        {
            OnMouseUp();
        }
    }

    /// <summary>
    /// 마우스 월드 좌표에서 SlimeController를 찾아 입력 세션을 시작합니다.
    /// </summary>
    private void TrySelect()
    {
        Vector2 worldPos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero, 0f);

        if (hit)
        {
            SlimeController clickTarget = hit.collider.GetComponent<SlimeController>();
            if (clickTarget != null)
            {
                _selectedTarget = clickTarget;
                _mouseDownPos = worldPos;
                _mouseDownTime = Time.time;
                _isDragging = false;
            }
        }
    }

    /// <summary>
    /// 누른 시간과 이동 거리 중 하나가 임계값을 넘으면 드래그로 전환합니다.
    /// </summary>
    private void CheckDragStart()
    {
        if (_isDragging) return;

        Vector2 currentPos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
        float distance = Vector2.Distance(_mouseDownPos, currentPos);
        float heldTime = Time.time - _mouseDownTime;

        // 일정 거리 이상 이동하거나 일정 시간 이상 누르면 드래그 시작
        if (distance > _dragThresholdDistance || heldTime > _dragThresholdTime)
        {
            _isDragging = true;
            _selectedTarget.StartDrag();
        }
    }

    /// <summary>
    /// 마우스 위치를 플레이 영역 안으로 제한해 선택 슬라임을 이동시킵니다.
    /// </summary>
    private void UpdateDrag()
    {
        Vector2 mousePos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);

        mousePos.x = Mathf.Clamp(mousePos.x, _dragMinBounds.x, _dragMaxBounds.x);
        mousePos.y = Mathf.Clamp(mousePos.y, _dragMinBounds.y, _dragMaxBounds.y);

        _selectedTarget.transform.position = mousePos;
    }

    /// <summary>
    /// 드래그면 머지 탐색을 종료하고, 클릭이면 업그레이드가 반영된 ClickInfo를 전달합니다.
    /// </summary>
    private void OnMouseUp()
    {
        if (_isDragging)
        {
            // 드래그 종료
            _selectedTarget.EndDrag();
        }
        else
        {
            // 클릭 처리 - ClickTarget의 레벨별 포인트 사용
            ClickInfo clickInfo = new ClickInfo
            {
                ClickType = EClickType.Manual,
                Point = PointCalculator.Calculate(_selectedTarget.Point, _selectedTarget.Grade, EClickType.Manual),
                Position = _mouseDownPos,
                Grade = _selectedTarget.Grade
            };
            _selectedTarget.OnClick(clickInfo);
        }

        _selectedTarget = null;
        _isDragging = false;
    }
}
