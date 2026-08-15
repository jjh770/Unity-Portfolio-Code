/*
 * 역할: 재화 조회·추가·소모, 변경 이벤트와 Repository 저장을 조정합니다.
 * 핵심 설계: 재화 규칙은 Currency에 두고, 마지막 저장 시각과 저장소 연결은 Manager가 담당합니다.
 */
using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

/// <summary>
/// 재화 도메인 컬렉션과 저장소를 연결하고 다른 시스템에 변경 이벤트를 제공합니다.
/// </summary>
public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance;

    private Currency[] _currencies = new Currency[(int)ECurrencyType.Count];
    private IRepository<CurrencySaveData> _repository;

    public Currency Point => Get(ECurrencyType.Point);
    public DateTime LastSaveTime { get; private set; } = DateTime.MinValue;

    public event Action<ECurrencyType, Currency> OnDataChanged;
    public event Action OnDataInitialized;

    private async void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        await UniTask.Yield();

#if UNITY_ANDROID && !UNITY_EDITOR
        _repository = new HybridRepository<CurrencySaveData>(
            new LocalCurrencyRepository(AccountManager.Instance.UserId),
            new FirebaseCurrencyRepository());
#else
        _repository = new LocalCurrencyRepository(AccountManager.Instance.UserId);
#endif

        CurrencySaveData saveData = await _repository.Load();
        LastSaveTime = ParseSaveTime(saveData.LastSaveTime);

        double[] currencyValues = saveData.Currencies;
        for (int i = 0; i < _currencies.Length; i++)
        {
            _currencies[i] = currencyValues[i];
        }

        OnDataInitialized?.Invoke();
    }

    public Currency Get(ECurrencyType currencyType)
    {
        return _currencies[(int)currencyType];
    }

    public void Add(ECurrencyType type, Currency amount)
    {
        _currencies[(int)type] += amount;
        OnDataChanged?.Invoke(type, _currencies[(int)type]);
        Save();
    }

    public bool TrySpend(ECurrencyType type, Currency amount)
    {
        if (_currencies[(int)type] < amount)
        {
            return false;
        }

        _currencies[(int)type] -= amount;
        OnDataChanged?.Invoke(type, _currencies[(int)type]);
        Save();
        return true;
    }

    /// <summary>
    /// 현재 시각을 UTC로 기록한 뒤 재화 데이터를 Repository에 저장합니다.
    /// </summary>
    private void Save()
    {
        LastSaveTime = DateTime.UtcNow;

        _repository.Save(new CurrencySaveData
        {
            Currencies = ToSaveData(),
            LastSaveTime = LastSaveTime.ToString("O")
        });
    }

    /// <summary>
    /// 앱 Pause/Quit 등 외부 저장 시점에서 현재 상태를 기록합니다.
    /// </summary>
    public void SaveCurrent()
    {
        Save();
    }

    private static DateTime ParseSaveTime(string saveTime)
    {
        if (DateTime.TryParse(saveTime, out DateTime result))
        {
            return result.ToUniversalTime();
        }

        return DateTime.MinValue;
    }

    private double[] ToSaveData()
    {
        double[] result = new double[_currencies.Length];
        for (int i = 0; i < _currencies.Length; i++)
        {
            result[i] = (double)_currencies[i];
        }

        return result;
    }

    public bool CanAfford(ECurrencyType type, Currency amount)
    {
        return _currencies[(int)type] >= amount;
    }
}
