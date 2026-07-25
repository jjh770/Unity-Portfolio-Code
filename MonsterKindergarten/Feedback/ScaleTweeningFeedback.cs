/*
 * 역할: 클릭 시 슬라임 크기를 순간적으로 튕겨 입력에 대한 생동감을 제공합니다.
 * 설계 의도: DOTween 실행과 풀 반환 시 Tween 정리를 독립 컴포넌트 안에서 책임집니다.
 */
using DG.Tweening;
using UnityEngine;

public class ScaleTweeningFeedback : MonoBehaviour, IFeedback
{
    [SerializeField] private float _punchScale = 0.5f;
    [SerializeField] private float _duration = 0.5f;
    [SerializeField] private int _vibrato = 10;
    [SerializeField] private float _elasticity = 1f;

    private Tween _scaleTween;
    private Vector3 _originalScale;

    private void Awake()
    {
        _originalScale = transform.localScale;
    }

    /// <summary>
    /// 연속 클릭 시 기존 Tween을 종료하고 기준 크기에서 새 피드백을 실행합니다.
    /// </summary>
    public void Play(ClickInfo clickInfo)
    {
        CleanupTween();
        transform.localScale = _originalScale;

        _scaleTween = transform.DOPunchScale(
            Vector3.one * _punchScale,
            _duration,
            _vibrato,
            _elasticity);
    }

    private void OnDisable()
    {
        CleanupTween();
        transform.localScale = _originalScale;
    }

    private void OnDestroy()
    {
        CleanupTween();
    }

    private void CleanupTween()
    {
        _scaleTween?.Kill();
        _scaleTween = null;
    }
}
