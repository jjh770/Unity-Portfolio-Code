/*
 * 역할: 슬라임 도메인 객체를 Unity 오브젝트와 연결하고 클릭·드래그·머지 진입점을 제공합니다.
 * 피드백 설계: 클릭 처리 시 자식 오브젝트의 IFeedback 구현체를 조회해 일괄 실행합니다.
 *              개별 효과를 직접 참조하지 않으므로 피드백 종류가 늘어도 이 클래스는 변경하지 않습니다.
 */
using System;
using UnityEngine;

public class SlimeController : MonoBehaviour, IClickable
{
    private Slime _slime;
    public Slime Slime => _slime;

    [SerializeField] private AudioClip[] _levelUpSounds;
    [SerializeField] private AudioClip _landSound;

    private int _randomLevelUpSound;
    private bool _hasLanded;
    private bool _isDragging;

    public ESlimeGrade Grade => _slime.SpecData.Grade;
    public bool IsDragging => _isDragging;
    public int Point => _slime != null ? _slime.SpecData.Point : 1;
    public float AutoClickInterval => _slime != null ? _slime.SpecData.AutoClickInterval : 1f;

    public event Action<ESlimeGrade> OnGradeChanged;
    public event Action OnInteracted;

    /// <summary>
    /// 런타임 슬라임 데이터를 교체하고 등급 변경 및 레벨업 사운드를 반영합니다.
    /// </summary>
    public void SetSlime(Slime slime)
    {
        _slime = slime;
        OnGradeChanged?.Invoke(_slime.SpecData.Grade);

        if (AudioManager.Instance == null ||
            _levelUpSounds == null ||
            _levelUpSounds.Length == 0)
        {
            return;
        }

        _randomLevelUpSound = UnityEngine.Random.Range(0, _levelUpSounds.Length);
        AudioManager.Instance.PlaySFX(_levelUpSounds[_randomLevelUpSound]);
    }

    public void OnSpawn()
    {
        _isDragging = false;
        _hasLanded = false;
    }

    public void OnDespawn()
    {
        _isDragging = false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (_hasLanded)
        {
            return;
        }

        _hasLanded = true;

        if (AudioManager.Instance != null && _landSound != null)
        {
            AudioManager.Instance.PlaySFX(_landSound);
        }
    }

    public void StartDrag()
    {
        _isDragging = true;

        if (TryGetComponent(out Rigidbody2D rigidbody))
        {
            rigidbody.linearVelocity = Vector2.zero;
        }
    }

    public void EndDrag()
    {
        _isDragging = false;
        OnInteracted?.Invoke();
        TryMerge();
    }

    /// <summary>
    /// 현재 위치 주변에서 동일 등급 슬라임을 찾아 MergeManager에 합성을 요청합니다.
    /// </summary>
    private void TryMerge()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 0.5f);

        foreach (Collider2D hit in hits)
        {
            SlimeController other = hit.GetComponent<SlimeController>();

            if (other != null &&
                other != this &&
                other.Grade == Grade)
            {
                MergeManager.Instance.Merge(this, other);
                return;
            }
        }
    }

    /// <summary>
    /// 재화를 지급한 뒤 현재 슬라임에 조립된 모든 피드백 컴포넌트를 실행합니다.
    /// 수동 클릭과 자동 클릭의 차이는 ClickInfo를 통해 각 구현체가 선택적으로 판단합니다.
    /// </summary>
    public bool OnClick(ClickInfo clickInfo)
    {
        if (clickInfo.ClickType == EClickType.Manual)
        {
            OnInteracted?.Invoke();
        }

        CurrencyManager.Instance.Add(ECurrencyType.Point, clickInfo.Point);

        // SlimeController는 구체 효과를 알지 않고 IFeedback 계약만 호출합니다.
        // 프리팹에 구현 컴포넌트를 추가하거나 제거하는 것만으로 피드백 구성을 변경할 수 있습니다.
        IFeedback[] feedbacks = GetComponentsInChildren<IFeedback>();

        foreach (IFeedback feedback in feedbacks)
        {
            feedback.Play(clickInfo);
        }

        return true;
    }
}
