/*
 * 역할: 슬라임 스펙, 진행 상태 도메인과 Repository를 Unity 런타임에 연결합니다.
 * 핵심 설계: 스폰·디스폰·머지 결과를 SlimeStatus에 반영하고 최고 등급 변경 이벤트를 발행합니다.
 */
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 슬라임 도메인 컬렉션과 진행 상태 저장을 조정하는 애플리케이션 서비스입니다.
/// </summary>
public class SlimeManager : MonoBehaviour
{
    public static SlimeManager Instance { get; private set; }

    [SerializeField] private SlimeSpecTable _specTable;
    private List<Slime> _slimes = new();

    //private ISlimeStatusRepository _statusRepository;
    private IRepository<SlimeStatusSaveData> _statusRepository;
    // 현재 최고 등급과 활성 슬라임 개수를 가진 런타임 도메인입니다.
    private SlimeStatus _status;
    public SlimeStatus Status => _status;

    public static event Action OnDataInitialized;
    public static event Action<ESlimeGrade> OnHighestGradeChanged;

    private void Awake()
    {
        Instance = this;

        foreach (var specData in _specTable.slimeSpecs)
        {
            _slimes.Add(new Slime(specData));
        }
    }

    private void Start()
    {
        _ = InitAsync();
    }

    /// <summary>
    /// 저장 데이터를 로드해 SlimeStatus를 생성하고 초기화 완료를 알립니다.
    /// </summary>
    private async UniTaskVoid InitAsync()
    {
        await UniTask.Yield();

#if !UNITY_WEBGL || UNITY_EDITOR
        _statusRepository = new HybridRepository<SlimeStatusSaveData>(new PlayerPrefsSlimeStatusRepository(AccountManager.Instance.Email), new FirebaseSlimeStatusRepository());
#else
        _statusRepository = new PlayerPrefsSlimeStatusRepository(AccountManager.Instance.Email);
#endif

        SlimeStatusSaveData saveData = await _statusRepository.Load();
        _status = new SlimeStatus(saveData.GetHighestGrade(), saveData.GetActiveSlimesDict());

        OnDataInitialized?.Invoke();
    }

    public Slime Get(ESlimeGrade grade)
    {
        return _slimes.Find(s => s.SpecData.Grade == grade);
    }

    /// <summary>
    /// 도메인 규칙과 프로젝트 최대 등급을 함께 검사합니다.
    /// </summary>
    public bool CanMerge(Slime slime1, Slime slime2)
    {
        ESlimeGrade maxGrade = _slimes[^1].SpecData.Grade;

        return slime1.CanMerge(slime2) && slime1.SpecData.Grade < maxGrade;
    }

    /// <summary>
    /// 더 높은 등급을 처음 만들었을 때만 상태·이벤트·저장을 갱신합니다.
    /// </summary>
    public bool TryUpdateHighestLevel(ESlimeGrade newGrade)
    {
        if (newGrade <= _status.HighestGrade) return false;

        _status.UpdateHighestGrade(newGrade);
        OnHighestGradeChanged?.Invoke(newGrade);
        Save();
        return true;
    }

    public bool IsMaxLevelUnlocked()
    {
        ESlimeGrade maxGrade = _slimes[^1].SpecData.Grade;
        return _status.HighestGrade >= maxGrade;
    }

    // 슬라임 스폰 시 호출
    public void AddSlime(ESlimeGrade grade)
    {
        _status.AddSlime(grade);
        Save();
    }

    // 슬라임 디스폰 시 호출
    public void RemoveSlime(ESlimeGrade grade)
    {
        _status.RemoveSlime(grade);
        Save();
    }

    // 머지 시 호출 (두 슬라임 제거 + 새 슬라임 추가)
    public void MergeSlime(ESlimeGrade fromGrade, ESlimeGrade toGrade)
    {
        _status.RemoveSlime(fromGrade); // keeper 기존 등급 제거
        _status.RemoveSlime(fromGrade); // removed 슬라임 제거
        _status.AddSlime(toGrade);      // 새 등급 추가
        Save();
    }

    /// <summary>
    /// 현재 Dictionary 상태를 저장 DTO의 Entry 목록으로 변환합니다.
    /// </summary>
    private void Save()
    {
        var saveData = new SlimeStatusSaveData
        {
            HighestGrade = (int)_status.HighestGrade,
            ActiveSlimes = new List<SlimeEntry>()
        };

        foreach (var pair in _status.ActiveSlimes)
        {
            saveData.ActiveSlimes.Add(new SlimeEntry(pair.Key, pair.Value));
        }

        _statusRepository.Save(saveData).Forget();
    }
}
