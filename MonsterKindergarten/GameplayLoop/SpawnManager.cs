/*
 * 역할: 저장된 활성 슬라임을 복원하고 일정 간격 자동 생성과 스폰 관련 업그레이드를 적용합니다.
 * 핵심 설계: 모든 도메인 데이터 초기화가 끝난 뒤에만 스폰을 시작해 초기 이벤트 순서 문제를 방지합니다.
 */
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스폰 타이머, 최대 활성 개수와 런타임 생성·제거 진입점을 관리합니다.
/// </summary>
public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

    [Header("Spawn Settings")]
    [SerializeField] private float _spawnInterval = 3f;
    [SerializeField] private int _maxActiveCount = 10;

    [Header("Spawn Area")]
    [SerializeField] private Vector2 _spawnAreaMin = new Vector2(-3f, -2f);
    [SerializeField] private Vector2 _spawnAreaMax = new Vector2(3f, 2f);

    [Header("Interval Area")]
    [SerializeField] private float _spawnIntervalDecreaseValue = 0.1f;
    [SerializeField] private int _spawnMaxIncreaseValue = 1;
    [SerializeField] private float _minSpawnInterval = 0.5f;
    // 현재 스폰 간격에 대한 누적 시간과 UI 진행률의 기준입니다.
    private float _timer;

    // 재화·업그레이드·슬라임 저장 데이터가 모두 준비되기 전 Update 스폰을 차단합니다.
    private bool _isInitialized;

    public float SpawnProgress => Mathf.Clamp01(_timer / _spawnInterval);
    public float RemainingTime => Mathf.Max(0f, _spawnInterval - _timer);
    public float MinSpawnInterval => _minSpawnInterval;
    public int MaxActiveCount
    {
        get => _maxActiveCount;
        set
        {
            _maxActiveCount = Mathf.Max(value, _maxActiveCount);
            OnSpawnMaxChanged?.Invoke(_maxActiveCount);
        }
    }
    public float SpawnInterval
    {
        get => _spawnInterval;
        set
        {
            _spawnInterval = Mathf.Max(value, _minSpawnInterval);
            OnSpawnIntervalChanged?.Invoke(_spawnInterval, _minSpawnInterval);
        }
    }
    public event Action<float, float> OnSpawnIntervalChanged;
    public event Action<int> OnSpawnMaxChanged;
    public event Action OnSpawned;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        GameManager.OnAllDataInitialized += OnAllDataInitialized;
        UpgradeManager.OnUpgraded += OnUpgraded;

        // 이미 초기화가 완료된 경우
        if (GameManager.Instance.IsAllDataInitialized)
        {
            OnAllDataInitialized();
        }
    }

    private void OnDestroy()
    {
        GameManager.OnAllDataInitialized -= OnAllDataInitialized;
        UpgradeManager.OnUpgraded -= OnUpgraded;
    }

    /// <summary>
    /// 저장된 업그레이드와 활성 슬라임을 적용한 뒤 자동 스폰을 활성화합니다.
    /// </summary>
    private void OnAllDataInitialized()
    {
        _isInitialized = true;
        ApplySavedUpgrades();
        InitSlimeSpawns();
        Spawn(ESlimeGrade.Grade1);
    }

    /// <summary>
    /// 스폰 간격과 최대 개수 업그레이드의 저장 레벨을 런타임 값에 반영합니다.
    /// </summary>
    private void ApplySavedUpgrades()
    {
        var intervalUpgrade = UpgradeManager.Instance.Get(EUpgradeType.SpawnTimeSub, ESlimeGrade.None);
        if (intervalUpgrade != null)
        {
            SpawnInterval -= _spawnIntervalDecreaseValue * intervalUpgrade.Level;
        }

        var maxCountUpgrade = UpgradeManager.Instance.Get(EUpgradeType.MaxCountAdd, ESlimeGrade.None);
        if (maxCountUpgrade != null)
        {
            MaxActiveCount += _spawnMaxIncreaseValue * maxCountUpgrade.Level;
        }
    }

    /// <summary>
    /// SlimeStatus의 등급별 개수만큼 오브젝트를 저장 없이 복원합니다.
    /// </summary>
    private void InitSlimeSpawns()
    {
        SlimeStatus status = SlimeManager.Instance.Status;

        foreach (var item in status.ActiveSlimes)
        {
            int count = item.Value;

            for (int i = 0; i < count; ++i)
            {
                Spawn(item.Key, shouldSave: false);
            }
        }
    }

    /// <summary>
    /// 스폰 관련 업그레이드만 선택해 즉시 런타임 속성을 갱신합니다.
    /// </summary>
    private void OnUpgraded(EUpgradeType type, ESlimeGrade grade)
    {
        switch (type)
        {
            case EUpgradeType.SpawnTimeSub:
                DecreaseInterval();
                break;
            case EUpgradeType.MaxCountAdd:
                IncreaseMaxCount();
                break;
        }
    }

    private void Update()
    {
        if (!_isInitialized) return;

        if (SlimeSpawner.Instance != null &&
            SlimeSpawner.Instance.GetActiveCount() >= _maxActiveCount)
        {
            return;
        }

        _timer += Time.deltaTime;

        if (_timer >= _spawnInterval)
        {
            _timer = 0f;
            Spawn(ESlimeGrade.Grade1);
            OnSpawned?.Invoke();
        }

        if (!Input.GetKeyDown(KeyCode.F1)) return;
        Spawn(ESlimeGrade.Grade1);
    }

    /// <summary>
    /// 랜덤 영역 좌표를 계산하고 실제 생성·풀 처리는 SlimeSpawner에 위임합니다.
    /// </summary>
    public SlimeController Spawn(ESlimeGrade grade, bool shouldSave = true)
    {
        if (SlimeSpawner.Instance == null) return null;

        Vector2 randomPos = new Vector2(
            UnityEngine.Random.Range(_spawnAreaMin.x, _spawnAreaMax.x),
            UnityEngine.Random.Range(_spawnAreaMin.y, _spawnAreaMax.y)
        );

        return SlimeSpawner.Instance.Spawn(grade, randomPos, shouldSave);
    }

    public void Despawn(SlimeController target)
    {
        if (SlimeSpawner.Instance == null) return;

        SlimeSpawner.Instance.Despawn(target);
    }

    public void DecreaseInterval()
    {
        SpawnInterval -= _spawnIntervalDecreaseValue;
    }

    public void IncreaseMaxCount()
    {
        MaxActiveCount += _spawnMaxIncreaseValue;
    }

    public int GetActiveCount() => SlimeSpawner.Instance.GetActiveCount();

    public List<SlimeController> GetActiveTargets() => SlimeSpawner.Instance.GetActiveTargets();
}
