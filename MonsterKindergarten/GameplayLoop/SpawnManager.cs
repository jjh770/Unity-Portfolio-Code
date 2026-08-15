/*
 * 역할: 저장된 활성 슬라임을 복원하고 일정 간격 자동 생성과 스폰 관련 업그레이드를 적용합니다.
 * 핵심 설계: 데이터 초기화 후 복원 상태를 기준으로 최초 슬라임 생성 여부를 결정하고 Gameplay 정지 상태를 반영합니다.
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

    [Header("Spawn Settings")]
    [SerializeField] private float _spawnInterval = 3f;
    [SerializeField] private int _maxActiveCount = 10;

    [Header("Spawn Area")]
    [SerializeField] private Vector2 _spawnAreaMin = new(-3f, -2f);
    [SerializeField] private Vector2 _spawnAreaMax = new(3f, 2f);

    [Header("Interval Area")]
    [SerializeField] private float _spawnIntervalDecreaseValue = 0.1f;
    [SerializeField] private int _spawnMaxIncreaseValue = 1;
    [SerializeField] private float _minSpawnInterval = 0.5f;

    private float _timer;
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

    private void OnAllDataInitialized()
    {
        _isInitialized = true;
        ApplySavedUpgrades();
        InitSlimeSpawns();

        // 신규 데이터처럼 복원할 슬라임이 없을 때만 최초 Grade1을 생성합니다.
        if (SlimeSpawner.Instance.GetActiveCount() == 0)
        {
            Spawn(ESlimeGrade.Grade1);
        }
    }

    private void ApplySavedUpgrades()
    {
        Upgrade intervalUpgrade =
            UpgradeManager.Instance.Get(EUpgradeType.SpawnTimeSub, ESlimeGrade.None);

        if (intervalUpgrade != null)
        {
            SpawnInterval -= _spawnIntervalDecreaseValue * intervalUpgrade.Level;
        }

        Upgrade maxCountUpgrade =
            UpgradeManager.Instance.Get(EUpgradeType.MaxCountAdd, ESlimeGrade.None);

        if (maxCountUpgrade != null)
        {
            MaxActiveCount += _spawnMaxIncreaseValue * maxCountUpgrade.Level;
        }
    }

    private void InitSlimeSpawns()
    {
        SlimeStatus status = SlimeManager.Instance.Status;

        foreach (KeyValuePair<ESlimeGrade, int> item in status.ActiveSlimes)
        {
            for (int i = 0; i < item.Value; ++i)
            {
                Spawn(item.Key, shouldSave: false);
            }
        }
    }

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
        if (GameManager.Instance == null || !GameManager.Instance.IsGameplayActive) return;

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

        if (Keyboard.current?.f1Key.wasPressedThisFrame == true)
        {
            Spawn(ESlimeGrade.Grade1);
        }
    }

    public SlimeController Spawn(ESlimeGrade grade, bool shouldSave = true)
    {
        if (SlimeSpawner.Instance == null) return null;

        Vector2 randomPos = new(
            UnityEngine.Random.Range(_spawnAreaMin.x, _spawnAreaMax.x),
            UnityEngine.Random.Range(_spawnAreaMin.y, _spawnAreaMax.y));

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

    public int GetActiveCount()
    {
        return SlimeSpawner.Instance.GetActiveCount();
    }

    public List<SlimeController> GetActiveTargets()
    {
        return SlimeSpawner.Instance.GetActiveTargets();
    }
}
