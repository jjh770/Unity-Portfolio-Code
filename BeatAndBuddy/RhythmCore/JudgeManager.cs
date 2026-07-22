/*
 * 역할: 입력 방향과 노트 목표 시각의 오차를 비교해 가장 적합한 노트를 판정하고 점수·콤보 통계를 갱신합니다.
 * 핵심 설계: 여러 후보가 판정 창에 들어온 경우 절대 시간 오차가 가장 작은 노트 하나만 선택합니다.
 */
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 시간 오차 구간에 따른 판정 등급입니다.
/// </summary>
public enum EHitType
{
    Perfect,
    Good,
    Bad,
    Miss
}

/// <summary>
/// 입력 판정, 통계와 판정 피드백을 조정하는 씬 단위 매니저입니다.
/// </summary>
public class JudgeManager : SceneSingleton<JudgeManager>
{
    [Header("판정 범위 (초)")]
    [SerializeField] private float _perfectWindow = 0.05f;
    [SerializeField] private float _goodWindow = 0.1f;
    [SerializeField] private float _badWindow = 0.15f;

    [Header("점수 설정")]
    [SerializeField] private int _perfectScore = 100;
    [SerializeField] private int _goodScore = 70;
    [SerializeField] private int _badScore = 30;
    [SerializeField] private int _comboBonus = 10;

    [Header("설정")]
    [SerializeField] private NoteSpawner _noteSpawner;
    [SerializeField] private Transform _judgePoint;
    [SerializeField] private GameObject _feverEffect;

    [Header("타격 피드백")]
    [SerializeField] private InputFeedback _leftFeedback;
    [SerializeField] private InputFeedback _rightFeedback;

    private SoundManager _soundManager;
    private PlayerManager _playerManager;
    private PoolManager _poolManager;

    private int _score = 0;
    // 성공 판정이 이어진 현재 연속 횟수이며 실패 판정에서 초기화됩니다.
    private int _combo = 0;
    private int _maxCombo = 0;
    private int _perfectCount = 0;
    private int _goodCount = 0;
    private int _badCount = 0;
    private int _missCount = 0;

    public int Score => _score;
    public int Combo => _combo;
    public int MaxCombo => _maxCombo;
    public int PerfectCount => _perfectCount;
    public int GoodCount => _goodCount;
    public int BadCount => _badCount;
    public int MissCount => _missCount;
    public float BadWindow => _badWindow;

    protected override void Awake()
    {
        base.Awake();
    }

    void Start()
    {
        if (_soundManager == null) _soundManager = SoundManager.Instance;
        if (_playerManager == null) _playerManager = PlayerManager.Instance;
        if (_poolManager == null) _poolManager = PoolManager.Instance;
        if (_noteSpawner == null)
        {
            Debug.LogError("[JudgeManager] NoteSpawner가 할당되지 않았습니다!");
            return;
        }
        _feverEffect.transform.localScale = Vector3.zero;
    }


    void Update()
    {
        if (StageManager.Instance.StageFlowCoroutine == null)
        {
            return;
        }

        if (InputManager.Instance.GetKeyDown(EGameKeyType.Left))
        {
            _soundManager.PlaySFX(ESoundType.SFX_HitDrum, 0);

            // 입력 피드백 트리거
            TriggerLeftFeedback();
            CheckHit(ENoteType.LNote);
        }

        if (InputManager.Instance.GetKeyDown(EGameKeyType.Right))
        {
            _soundManager.PlaySFX(ESoundType.SFX_HitClap, 0);

            // 입력 피드백 트리거
            TriggerRightFeedback();
            CheckHit(ENoteType.RNote);
        }
    }

    void CheckHit(ENoteType inputType)
    {
        if (_noteSpawner == null) return;
        if (!SongPlayManager.Instance.IsPlaying()) return;

        float currentTime = SongPlayManager.Instance.BgmPosition;
        Note closestNote = null;
        float closestAbsDiff = float.MaxValue;
        float closestSignedDiff = 0f;

        foreach (Note note in _noteSpawner.GetActiveNotes())
        {
            if (!note.CanBeJudged() || note.NoteType != inputType) continue;

            float targetTime = note.TargetBeat * SongPlayManager.Instance.SecPerBeat;
            float signedDiff = currentTime - targetTime;
            float absDiff = Mathf.Abs(signedDiff);

            if (absDiff <= 0.2f && absDiff < closestAbsDiff)
            {
                closestAbsDiff = absDiff;
                closestSignedDiff = signedDiff;
                closestNote = note;
            }
        }

        if (closestNote != null)
        {
            EHitType hitType = DetermineHitType(closestAbsDiff);
            ProcessHit(closestNote, hitType, closestSignedDiff, inputType);
        }
    }
    /// <summary>
    /// 절대 시간 오차를 Perfect·Good·Bad·Miss 구간으로 분류합니다.
    /// </summary>
    private EHitType DetermineHitType(float timeDifference)
    {
        if (timeDifference <= _perfectWindow)
        {
            SpawnEffect(ENoteEffectType.PerfectEffect);
            return EHitType.Perfect;
        }
        else if (timeDifference <= _goodWindow)
        {
            SpawnEffect(ENoteEffectType.GoodEffect);
            return EHitType.Good;
        }
        else if (timeDifference <= _badWindow)
        {
            SpawnEffect(ENoteEffectType.BadEffect);
            return EHitType.Bad;
        }
        else
        {
            SpawnEffect(ENoteEffectType.MissEffect);
            return EHitType.Miss;
        }
    }

    void ProcessHit(Note note, EHitType hitType, float signedDiff, ENoteType noteType)
    {
        note.OnHit(hitType);

        _playerManager.OnHit(hitType);
        int basePoints = GetBaseScore(hitType);
        bool maintainCombo = UpdateHitStatistics(hitType);

        if (maintainCombo)
        {
            _combo++;
            if (_combo > _maxCombo)
            {
                _maxCombo = _combo;
            }

            int comboMultiplier = _combo / _comboBonus;
            _score += basePoints + comboMultiplier;
        }
        else
        {
            _score += basePoints;
            _combo = 0;
        }

        _noteSpawner.RemoveNote(note);
    }

    /// <summary>
    /// 판정 등급별 기본 점수를 반환합니다.
    /// </summary>
    private int GetBaseScore(EHitType hitType)
    {
        switch (hitType)
        {
            case EHitType.Perfect:
                return _perfectScore;
            case EHitType.Good:
                return _goodScore;
            case EHitType.Bad:
                return _badScore;
            case EHitType.Miss:
                return 0;
            default:
                return 0;
        }
    }
    /// <summary>
    /// 판정 카운트와 콤보·최대 콤보를 일관되게 갱신합니다.
    /// </summary>
    private bool UpdateHitStatistics(EHitType hitType)
    {
        switch (hitType)
        {
            case EHitType.Perfect:
                _perfectCount++;
                ComboManager.Instance.IncreaseCombo();
                return true;
            case EHitType.Good:
                _goodCount++;
                ComboManager.Instance.IncreaseCombo();
                return true;
            case EHitType.Bad:
                _badCount++;
                ComboManager.Instance.ResetCombo();
                return false;
            case EHitType.Miss:
                _missCount++;
                ComboManager.Instance.ResetCombo();
                return false;
            default:
                return false;
        }
    }
    /// <summary>
    /// 입력 없이 판정 지점을 지난 노트의 Miss 통계와 피드백을 처리합니다.
    /// </summary>
    public void OnNoteMiss()
    {
        _missCount++;
        _combo = 0;
        _playerManager.OnHit(EHitType.Miss);
        SpawnEffect(ENoteEffectType.MissEffect);
    }

    private void SpawnEffect(ENoteEffectType effectType)
    {
        NoteEffect noteEffect = _poolManager.SpawnGetComponent<NoteEffectPool, ENoteEffectType, NoteEffect>(effectType);
        noteEffect.SetEffectType(effectType);
        noteEffect.transform.position = _judgePoint.position;
    }
    /// <summary>
    /// 현재 누적 상태를 불변 결과 구조체로 묶어 반환합니다.
    /// </summary>
    public GameResult GetGameResult()
    {
        return new GameResult
        {
            score = _score,
            maxCombo = _maxCombo,
            perfectCount = _perfectCount,
            goodCount = _goodCount,
            badCount = _badCount,
            missCount = _missCount
        };
    }

    /// <summary>
    /// 새 곡 시작 전 모든 판정 통계를 초기화합니다.
    /// </summary>
    public void ResetScoreStats()
    {
        _score = 0;
        _combo = 0;
        _maxCombo = 0;
        _perfectCount = 0;
        _goodCount = 0;
        _badCount = 0;
        _missCount = 0;

        Debug.Log("[JudgeManager] Stats reset");
    }

    public void GetFeverOnJudgePoint(bool isFever)
    {
        if (isFever)
        {
            _feverEffect.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutQuad);
        }
        else
        {
            _feverEffect.transform.DOScale(Vector3.zero, 0.5f).SetEase(Ease.OutQuad);
        }
    }

    public void TriggerLeftFeedback()
    {
        if (_leftFeedback != null)
        {
            _leftFeedback.Trigger();
        }
    }

    public void TriggerRightFeedback()
    {
        if (_rightFeedback != null)
        {
            _rightFeedback.Trigger();
        }
    }
}

/// <summary>
/// 스테이지 종료 시 결과 화면에 전달할 누적 판정 통계입니다.
/// </summary>
[System.Serializable]
public struct GameResult
{
    public int score;
    public int maxCombo;
    public int perfectCount;
    public int goodCount;
    public int badCount;
    public int missCount;
}
