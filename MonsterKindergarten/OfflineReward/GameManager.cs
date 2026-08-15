/*
 * 역할: 데이터 초기화 이후 오프라인 보상 계산과 수령 상태를 관리합니다.
 * 핵심 설계: 마지막 저장 시각과 보유 슬라임 자동 생산량으로 보상을 산출하고,
 *           보상 수령·연출 동안 Gameplay를 잠가 재화 흐름이 섞이지 않도록 합니다.
 */
using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public static event Action OnAllDataInitialized;
    public event Action OnOfflineRewardReady;

    [Header("Offline Reward")]
    [SerializeField] private float _minimumOfflineSeconds = 60f;
    [SerializeField] private float _maximumOfflineHours = 8f;
    [SerializeField, Range(0f, 1f)] private float _offlineRewardEfficiency = 0.5f;

    private bool _isUpgradeInitialized;
    private bool _isSlimeInitialized;
    private bool _isCurrencyInitialized;
    private bool _isAllInitialized;

    private OfflineRewardResult? _pendingOfflineReward;
    private bool _isOfflineRewardConsumed;
    private bool _isOfflineRewardClaimed;

    public bool IsAllDataInitialized => _isAllInitialized;
    public bool IsGameplayActive { get; private set; }

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
    }

    private void Start()
    {
        UpgradeManager.OnDataInitialized += OnUpgradeDataInitialized;
        SlimeManager.OnDataInitialized += OnSlimeDataInitialized;
        CurrencyManager.Instance.OnDataInitialized += OnCurrencyDataInitialized;
    }

    private void OnDestroy()
    {
        UpgradeManager.OnDataInitialized -= OnUpgradeDataInitialized;
        SlimeManager.OnDataInitialized -= OnSlimeDataInitialized;
        CurrencyManager.Instance.OnDataInitialized -= OnCurrencyDataInitialized;
    }

    private void OnUpgradeDataInitialized()
    {
        _isUpgradeInitialized = true;
        TryInvokeAllInitialized();
    }

    private void OnSlimeDataInitialized()
    {
        _isSlimeInitialized = true;
        TryInvokeAllInitialized();
    }

    private void OnCurrencyDataInitialized()
    {
        _isCurrencyInitialized = true;
        TryInvokeAllInitialized();
    }

    /// <summary>
    /// 재화·업그레이드·슬라임 데이터가 모두 준비된 뒤에만
    /// 오프라인 보상 계산과 Gameplay 초기화를 시작합니다.
    /// </summary>
    private void TryInvokeAllInitialized()
    {
        if (_isAllInitialized) return;

        if (_isUpgradeInitialized && _isSlimeInitialized && _isCurrencyInitialized)
        {
            _isAllInitialized = true;
            GrantOfflineReward();
            OnAllDataInitialized?.Invoke();
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!_isAllInitialized) return;

        if (pauseStatus)
        {
            // 수령 대기 중인 보상이 있으면 기준 시각을 덮어쓰지 않습니다.
            if (!_pendingOfflineReward.HasValue)
            {
                CurrencyManager.Instance.SaveCurrent();
            }
        }
        else if (!_pendingOfflineReward.HasValue || !_isOfflineRewardClaimed)
        {
            GrantOfflineReward();
        }
    }

    private void OnApplicationQuit()
    {
        if (_isAllInitialized && !_pendingOfflineReward.HasValue)
        {
            CurrencyManager.Instance.SaveCurrent();
        }
    }

    private void GrantOfflineReward()
    {
        DateTime lastSaveTime = CurrencyManager.Instance.LastSaveTime;
        DateTime currentTime = DateTime.UtcNow;

        if (lastSaveTime == DateTime.MinValue || currentTime <= lastSaveTime)
        {
            CurrencyManager.Instance.SaveCurrent();
            ActivateGameplayIfNoPendingReward();
            return;
        }

        double elapsedSeconds = (currentTime - lastSaveTime).TotalSeconds;

        if (elapsedSeconds < _minimumOfflineSeconds)
        {
            CurrencyManager.Instance.SaveCurrent();
            ActivateGameplayIfNoPendingReward();
            return;
        }

        double maximumSeconds = Math.Max(0f, _maximumOfflineHours) * 60d * 60d;
        elapsedSeconds = Math.Min(elapsedSeconds, maximumSeconds);

        double pointPerSecond = CalculateAutoPointPerSecond();
        double reward = Math.Floor(
            pointPerSecond *
            elapsedSeconds *
            _offlineRewardEfficiency);

        if (reward <= 0d)
        {
            CurrencyManager.Instance.SaveCurrent();
            ActivateGameplayIfNoPendingReward();
            return;
        }

        Currency pointBeforeReward = CurrencyManager.Instance.Point;
        Currency pointAfterReward = pointBeforeReward + (Currency)reward;

        // 팝업 수령 전 자동 생산과 스폰을 차단합니다.
        IsGameplayActive = false;
        _isOfflineRewardConsumed = false;
        _isOfflineRewardClaimed = false;

        _pendingOfflineReward = new OfflineRewardResult(
            TimeSpan.FromSeconds(elapsedSeconds),
            reward,
            pointBeforeReward,
            pointAfterReward);

        OnOfflineRewardReady?.Invoke();
    }

    /// <summary>
    /// 보유 중인 모든 활성 슬라임의 초당 자동 생산량을 합산합니다.
    /// 온라인 자동 생산과 동일한 PointCalculator 규칙을 재사용합니다.
    /// </summary>
    private static double CalculateAutoPointPerSecond()
    {
        double total = 0d;

        foreach (var pair in SlimeManager.Instance.Status.ActiveSlimes)
        {
            Slime slime = SlimeManager.Instance.Get(pair.Key);

            if (slime == null ||
                pair.Value <= 0 ||
                slime.SpecData.AutoClickInterval <= 0f)
            {
                continue;
            }

            double point = PointCalculator.Calculate(
                slime.SpecData.Point,
                pair.Key,
                EClickType.Auto);

            total += point / slime.SpecData.AutoClickInterval * pair.Value;
        }

        return total;
    }

    /// <summary>
    /// UI가 대기 중인 보상을 한 번만 가져가도록 합니다.
    /// </summary>
    public bool TryConsumeOfflineReward(out OfflineRewardResult result)
    {
        if (!_pendingOfflineReward.HasValue || _isOfflineRewardConsumed)
        {
            result = default;
            return false;
        }

        result = _pendingOfflineReward.Value;
        _isOfflineRewardConsumed = true;
        return true;
    }

    public bool TryGetCurrentOfflineReward(out OfflineRewardResult result)
    {
        if (!_pendingOfflineReward.HasValue)
        {
            result = default;
            return false;
        }

        result = _pendingOfflineReward.Value;
        return true;
    }

    /// <summary>
    /// 보상 버튼 입력 시 실제 재화를 한 번만 지급합니다.
    /// </summary>
    public bool TryClaimOfflineReward()
    {
        if (!_pendingOfflineReward.HasValue || _isOfflineRewardClaimed)
        {
            return false;
        }

        OfflineRewardResult result = _pendingOfflineReward.Value;
        CurrencyManager.Instance.Add(ECurrencyType.Point, result.Reward);
        _isOfflineRewardClaimed = true;
        return true;
    }

    /// <summary>
    /// 팝업과 보상 연출까지 모두 끝난 뒤 Gameplay를 다시 활성화합니다.
    /// </summary>
    public void CompleteOfflineRewardPresentation()
    {
        if (!_isOfflineRewardClaimed) return;

        _pendingOfflineReward = null;
        _isOfflineRewardConsumed = false;
        _isOfflineRewardClaimed = false;
        IsGameplayActive = true;
    }

    private void ActivateGameplayIfNoPendingReward()
    {
        if (!_pendingOfflineReward.HasValue)
        {
            IsGameplayActive = true;
        }
    }
}

public readonly struct OfflineRewardResult
{
    public TimeSpan ElapsedTime { get; }
    public Currency Reward { get; }
    public Currency PointBeforeReward { get; }
    public Currency PointAfterReward { get; }

    public OfflineRewardResult(
        TimeSpan elapsedTime,
        Currency reward,
        Currency pointBeforeReward,
        Currency pointAfterReward)
    {
        ElapsedTime = elapsedTime;
        Reward = reward;
        PointBeforeReward = pointBeforeReward;
        PointAfterReward = pointAfterReward;
    }
}
