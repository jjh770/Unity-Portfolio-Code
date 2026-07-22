/*
 * 역할: 한 노트의 Beat 기반 위치, 판정 상태, Hit·Miss 연출과 풀 재사용 초기화를 관리합니다.
 * 핵심 설계: 속도를 누적하지 않고 현재 음악 Beat에서 진행도를 다시 계산해 프레임 저하에 따른 위치 오차를 줄입니다.
 */
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 채보 데이터 한 개를 화면 오브젝트로 표현하고 판정 결과에 따라 생명주기를 종료합니다.
/// </summary>
public class Note : MonoBehaviour, IPoolable
{
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private Sprite _lNoteSprite;
    [SerializeField] private Sprite _rNoteSprite;

    [Header("히트 애니메이션")]
    [SerializeField] private float _perfectAnimDuration = 0.3f;
    [SerializeField] private float _goodAnimDuration = 0.5f;
    [SerializeField] private float _hitMoveDistance = 1f;

    private Transform _hitTargetTransform;

    public ENoteType NoteType { get; private set; }
    public float TargetBeat { get; private set; }

    public Transform JudgePoint;
    private Vector3 _spawnPosition;
    // 생성 지점에서 판정 지점까지 이동하는 데 사용할 Beat 길이입니다.
    private float _beatsToTravel;
    // 한 노트가 중복 판정되거나 Miss 처리되는 것을 막는 상태입니다.
    private bool _isHit = false;
    private JudgeManager _judgeManager;
    private NoteSpawner _noteSpawner;
    // 풀 반환 전에 진행 중인 DOTween 연출을 종료하기 위해 보관합니다.
    private Sequence _hitSequence;

    private Vector3 _initialScale = Vector3.one;
    private Quaternion _initialRotation = Quaternion.identity;
    private Color _initialColor;

    private void Awake()
    {
        _initialScale = transform.localScale;
        _initialRotation = transform.rotation;

        if (_spriteRenderer != null)
        {
            _initialColor = _spriteRenderer.color;
        }
    }

    // 게임 로직 관련 초기화 (OnSpawn 이후 호출)
    public void Initialize(float targetBeat, ENoteType type, float beatsInAdvance, Transform judgePoint, NoteSpawner spawner)
    {
        TargetBeat = targetBeat;
        NoteType = type;
        _beatsToTravel = beatsInAdvance;
        JudgePoint = judgePoint;
        _spawnPosition = transform.position;
        _judgeManager = JudgeManager.Instance;
        _noteSpawner = spawner;
        _hitTargetTransform = NoteManager.Instance.NoteTargetTransform;

        if (_spriteRenderer != null)
        {
            _spriteRenderer.sprite = (type == ENoteType.LNote) ? _lNoteSprite : _rNoteSprite;
        }
    }

    /// <summary>
    /// 현재 Beat로 진행도와 위치를 계산하고 판정 구간을 지나면 Miss를 발생시킵니다.
    /// </summary>
    private void Update()
    {
        if (_isHit) return;

        float currentBeat = SongPlayManager.Instance.BgmPositionInBeats;
        float progress = Mathf.Clamp01(1f - (TargetBeat - currentBeat) / _beatsToTravel);
        transform.position = Vector3.Lerp(_spawnPosition, JudgePoint.position, progress);

        float secPerBeat = SongPlayManager.Instance.SecPerBeat;
        float missWindowBeats = (_judgeManager != null)
            ? (_judgeManager.BadWindow / secPerBeat) + 0.1f
            : 0.5f;

        if (currentBeat > TargetBeat + missWindowBeats)
        {
            OnMiss();
        }
    }

    /// <summary>
    /// 외부 판정 결과를 한 번만 수락하고 Hit 처리와 연출을 시작합니다.
    /// </summary>
    public void OnHit(EHitType hitType)
    {
        _isHit = true;
        NoteHit(hitType);
    }

    private void NoteHit(EHitType hitType)
    {
        if (_hitSequence != null)
        {
            _hitSequence.Kill();
        }

        float duration;
        float jumpPower;

        // 타겟 위치 결정
        Vector3 targetPosition = _hitTargetTransform != null
            ? _hitTargetTransform.position
            : transform.position + Vector3.up * _hitMoveDistance;

        switch (hitType)
        {
            case EHitType.Perfect:
                duration = _perfectAnimDuration;
                jumpPower = 2f;
                break;

            case EHitType.Good:
                duration = _goodAnimDuration;
                jumpPower = 1.5f;
                break;

            case EHitType.Bad:
                duration = 0.3f;
                jumpPower = 0f;
                break;

            default:
                duration = 0.3f;
                jumpPower = 0f;
                break;
        }

        HitTypeManager.Instance.SetText(hitType);

        _hitSequence = DOTween.Sequence();

        // Miss와 Bad는 제자리에서 페이드 아웃만
        if (hitType == EHitType.Bad || hitType == EHitType.Miss)
        {
            // 페이드 아웃만
            _hitSequence.Append(_spriteRenderer.DOFade(0f, duration).SetEase(Ease.InQuad));

            // 약간 작아지는 효과 (선택사항)
            _hitSequence.Join(transform.DOScale(_initialScale * 0.5f, duration).SetEase(Ease.InQuad));
        }
        else
        {
            // Perfect/Good는 포물선 이동
            _hitSequence.Append(transform.DOJump(targetPosition, jumpPower, 1, duration));
            _hitSequence.Join(_spriteRenderer.DOFade(0f, duration).SetEase(Ease.InQuad));

            // Perfect 시 스케일 효과
            if (hitType == EHitType.Perfect)
            {
                _hitSequence.Join(transform.DOScale(_initialScale * 1.2f, duration * 0.5f)
                    .SetEase(Ease.OutQuad));
            }
        }

        _hitSequence.OnComplete(() =>
        {
            if (_noteSpawner != null)
            {
                _noteSpawner.ReturnNoteAfterAnimation(gameObject);
                NoteManager.Instance.SpawnArriveEffect(ENoteArriveEffectType.NoteArriveEffect);
            }
            else
            {
                gameObject.SetActive(false);
            }
        });
    }


    private void OnMiss()
    {
        _isHit = true;
        if (_judgeManager != null)
        {
            _judgeManager.OnNoteMiss();
        }
        HitTypeManager.Instance.SetText(EHitType.Miss);
        ComboManager.Instance.ResetCombo();
        NoteHit(EHitType.Miss);
        if (_noteSpawner != null)
        {
            _noteSpawner.ReturnNoteToPool(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
    public bool CanBeJudged() => !_isHit;
    public float GetDistanceToTarget()
    {
        return Vector3.Distance(transform.position, JudgePoint.position);
    }

    /// <summary>
    /// 보스 패턴이 노트의 현재 진행도를 조건으로 조회할 수 있게 합니다.
    /// </summary>
    public float GetProgressToTarget()
    {
        float currentBeat = SongPlayManager.Instance.BgmPositionInBeats;
        return Mathf.Clamp01(1f - (TargetBeat - currentBeat) / _beatsToTravel);
    }

    // 노트 조작 메서드들...
    public void SetColor(Color color, float duration = 0.3f)
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.DOColor(color, duration);
        }
    }

    public void SetSpeed(float multiplier)
    {
        // 구현 필요
    }

    public void SetScale(float scale, float duration = 0.3f)
    {
        transform.DOScale(Vector3.one * scale, duration);
    }

    public void SetAlpha(float alpha, float duration = 0.3f)
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.DOFade(alpha, duration);
        }
    }

    public void Shake(float intensity = 0.1f, float duration = 0.3f)
    {
        transform.DOShakePosition(duration, intensity);
    }

    /// <summary>
    /// 풀에서 꺼낼 때 이전 Tween·색상·크기·판정 상태를 초기화합니다.
    /// </summary>
    public void OnSpawn()
    {
        _isHit = false;

        transform.localScale = _initialScale;
        transform.rotation = _initialRotation;

        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = _initialColor;
        }

        if (_hitSequence != null)
        {
            _hitSequence.Kill();
            _hitSequence = null;
        }
    }

    /// <summary>
    /// 풀 반환 시 실행 중인 연출을 정리해 다음 재사용에 영향을 주지 않게 합니다.
    /// </summary>
    public void OnDespawn()
    {
        if (_hitSequence != null)
        {
            _hitSequence.Kill();
            _hitSequence = null;
        }

        transform.DOKill();
        if (_spriteRenderer != null)
        {
            _spriteRenderer.DOKill();
        }

        transform.localScale = _initialScale;
        transform.rotation = _initialRotation;

        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = _initialColor;
        }

        _isHit = false;
    }
}
