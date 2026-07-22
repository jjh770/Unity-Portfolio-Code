/*
 * 역할: 공격·대시의 선형 이동과 스킬의 포물선 이동을 CharacterController 기반으로 공통 처리합니다.
 * 핵심 설계: Tween 누적값의 차이만 Move에 전달해 충돌 처리를 유지하고 카메라 경계 안으로 이동량을 제한합니다.
 */
using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 일반 WASD 이동과 분리된 행동성 이동의 실행·취소를 담당합니다.
/// </summary>
public class TweenMovement : MonoBehaviour
{
    private CharacterController _controller;
    private PlayerMove _playerMove;
    private Camera _mainCamera;
    // 새 행동 시작이나 상태 취소 시 이전 이동을 중단하기 위해 보관하는 Tween입니다.
    private Tweener _currentTween;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _playerMove = GetComponent<PlayerMove>();
        _mainCamera = Camera.main;
    }

    private void OnDestroy()
    {
        _currentTween?.Kill();
    }

    /// <summary>
    /// 선형 이동 (공격, 대시용)
    /// </summary>
    public void StartLinearMovement(Vector3 direction, float distance, float duration, Ease ease, Action onComplete = null)
    {
        if (_controller == null) return;

        _currentTween?.Kill();

        float previousValue = 0f;

        _currentTween = DOVirtual.Float(0f, distance, duration, (currentValue) =>
        {
            float delta = currentValue - previousValue;
            Vector3 moveVector = direction * delta;

            Vector3 clampedMove = CameraBoundsHelper.ClampMovementToCameraBounds(
                transform.position, moveVector, _mainCamera, _playerMove.ViewportMargin);

            _controller.Move(clampedMove);

            previousValue = currentValue;
        })
        .SetEase(ease)
        .SetLink(gameObject)
        .OnComplete(() => onComplete?.Invoke())
        .OnKill(() => _currentTween = null);
    }

    /// <summary>
    /// 포물선 이동 (스킬용)
    /// </summary>
    public void StartParabolicMovement(Vector3 direction, float distance, float jumpHeight, float duration, Ease ease)
    {
        if (_controller == null) return;

        _currentTween?.Kill();

        float prevHorizontal = 0f;
        float prevVertical = 0f;

        _currentTween = DOVirtual.Float(0f, 1f, duration, (t) =>
        {
            float currentHorizontal = t * distance;
            float deltaHorizontal = currentHorizontal - prevHorizontal;

            // 포물선: 4 * h * t * (1 - t)
            float currentVertical = 4 * jumpHeight * t * (1 - t);
            float deltaVertical = currentVertical - prevVertical;

            Vector3 moveVector = (direction * deltaHorizontal) + (Vector3.up * deltaVertical);

            Vector3 clampedMove = CameraBoundsHelper.ClampMovementToCameraBounds(
                transform.position, moveVector, _mainCamera, _playerMove.ViewportMargin);

            _controller.Move(clampedMove);

            prevHorizontal = currentHorizontal;
            prevVertical = currentVertical;
        })
        .SetEase(ease)
        .SetLink(gameObject)
        .OnKill(() => _currentTween = null);
    }

    /// <summary>
    /// 이동 중단
    /// </summary>
    public void Stop()
    {
        if (_currentTween != null && _currentTween.IsActive())
        {
            _currentTween.Kill();
        }
    }

    public bool IsMoving => _currentTween != null && _currentTween.IsActive();
}
