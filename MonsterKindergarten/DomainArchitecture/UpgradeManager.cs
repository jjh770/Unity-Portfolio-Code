/*
 * 역할: 업그레이드 도메인 생성·조회, 재화 소모, 저장과 변경 이벤트를 조정합니다.
 * 핵심 설계: 서로 다른 Currency와 Upgrade 도메인의 협력은 상위 Manager에서 처리합니다.
 */
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
/// <summary>
/// 업그레이드 도메인의 애플리케이션 서비스이자 Repository 어댑터입니다.
/// </summary>
public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    // 이벤트는 도메인이 아닌 매니저가 가져야함.
    public static event Action OnDataChanged;
    public static event Action OnDataInitialized;
    // 업그레이드 성공 시 어떤 업그레이드가 변경되었는지 알려주는 이벤트 (SpawnManager 등이 구독)
    public static event Action<EUpgradeType, ESlimeGrade> OnUpgraded;
    [SerializeField] private UpgradeSpecTableSO _specTable;
    //private IUpgradeRepository _repository;
    private IRepository<UpgradeSaveData> _repository;
    private Dictionary<(EUpgradeType, ESlimeGrade), Upgrade> _upgrades = new();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        _ = InitAsync();
    }

    /// <summary>
    /// 저장 레벨과 ScriptableObject 스펙을 조합해 모든 업그레이드 도메인을 생성합니다.
    /// </summary>
    private async UniTaskVoid InitAsync()
    {
        await UniTask.Yield();

#if !UNITY_WEBGL || UNITY_EDITOR
        _repository = new HybridRepository<UpgradeSaveData>(new PlayerPrefsUpgradeRepository(AccountManager.Instance.Email), new FirebaseUpgradeRepository());
#else
        _repository = new PlayerPrefsUpgradeRepository(AccountManager.Instance.Email);
#endif

        var saveData = await _repository.Load();
        // Entries를 딕셔너리로 변환해서 빠르게 조회
        var savedLevels = new Dictionary<(EUpgradeType, ESlimeGrade), int>();
        foreach (var entry in saveData.Entries)
        {
            savedLevels[(entry.GetUpgradeType(), entry.GetSlimeGrade())] = entry.Level;
        }

        foreach (var specData in _specTable.Datas)
        {
            var key = (specData.Type, specData.SlimeGrade);
            if (_upgrades.ContainsKey(key))
            {
                throw new Exception($"이미 같은 타입의 업그레이드 정보를 가지고 있습니다. {specData.Type}, {specData.SlimeGrade}");
            }

            int savedLevel = savedLevels.TryGetValue(key, out var lv) ? lv : 0;
            _upgrades.Add(key, new Upgrade(specData, savedLevel));
        }

        OnDataChanged?.Invoke();
        OnDataInitialized?.Invoke();
    }

    // 업그레이드를 가져오기
    public Upgrade Get(EUpgradeType type, ESlimeGrade grade) =>
        _upgrades.TryGetValue((type, grade), out var upgrade) ? upgrade : null;

    // 슬라임 개별 업그레이드만 반환 (SpawnTimeSub, MaxCountAdd 등 전체 공통 업그레이드 제외)
    public List<Upgrade> GetSlimeUpgrades() =>
        _upgrades.Values.Where(u => u.SpecData.SlimeGrade != ESlimeGrade.None).ToList();

    /// <summary>
    /// 업그레이드의 최대 레벨과 현재 재화 보유량을 상태 변경 없이 함께 확인합니다.
    /// </summary>
    public bool CanLevelUp(UpgradeSpecData specData)
    {
        if (!_upgrades.TryGetValue((specData.Type, specData.SlimeGrade), out Upgrade upgrade)) return false;

        if (!upgrade.CanLevelUp()) return false;
        // Upgrade와 Currency의 협력은 어느 한 도메인에 결합하지 않고 Manager에서 조정합니다.
        return CurrencyManager.Instance.CanAfford(ECurrencyType.Point, upgrade.Cost);
    }

    /// <summary>
    /// 비용 차감과 도메인 레벨업을 순서대로 실행하고 실패 시 차감 비용을 환불합니다.
    /// </summary>
    public bool TryLevelUp(EUpgradeType type, ESlimeGrade grade)
    {
        if (!_upgrades.TryGetValue((type, grade), out Upgrade upgrade)) return false;

        Currency cost = upgrade.Cost;

        if (!CurrencyManager.Instance.TrySpend(ECurrencyType.Point, cost)) return false;

        if (!upgrade.TryLevelUp())
        {
            // 레벨업 실패 시 포인트 환불
            CurrencyManager.Instance.Add(ECurrencyType.Point, cost);
            return false;
        }
        Save();
        OnDataChanged?.Invoke();
        OnUpgraded?.Invoke(type, grade);

        return true;
    }

    /// <summary>
    /// 런타임 Dictionary를 저장 가능한 Entry 목록으로 변환합니다.
    /// </summary>
    private void Save()
    {
        var data = new UpgradeSaveData();
        foreach (var pair in _upgrades)
        {
            data.Entries.Add(new UpgradeEntry(pair.Key.Item1, pair.Key.Item2, pair.Value.Level));
        }
        _repository.Save(data).Forget();
    }
}
