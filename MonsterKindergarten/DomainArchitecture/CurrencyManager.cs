/*
 * 역할: 재화 조회·추가·소모, 변경 이벤트와 Repository 저장을 조정합니다.
 * 핵심 설계: 재화 값의 규칙은 Currency에 두고 Unity 생명주기와 다른 시스템 협력만 담당합니다.
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

    // ECurrencyType의 정수 인덱스를 사용해 재화를 상수 시간에 조회합니다.
    private Currency[] _currencies = new Currency[(int)ECurrencyType.Count];

    // 플랫폼별 저장 구현체는 IRepository 계약 뒤에서 교체합니다.
    private IRepository<CurrencySaveData> _repository;

    public Currency Point => Get(ECurrencyType.Point);

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

#if !UNITY_WEBGL || UNITY_EDITOR
        _repository = new HybridRepository<CurrencySaveData>(new LocalCurrencyRepository(AccountManager.Instance.Email), new FirebaseCurrencyRepository());
#else
        _repository = new LocalCurrencyRepository(AccountManager.Instance.Email);
#endif

        CurrencySaveData saveData = await _repository.Load();
        double[] currencyValues = saveData.Currencies;
        for (int i = 0; i < _currencies.Length; i++)
        {
            _currencies[i] = currencyValues[i];
        }

        OnDataInitialized?.Invoke();
    }

    /// <summary>
    /// 지정한 종류의 현재 재화 값을 조회합니다.
    /// </summary>
    public Currency Get(ECurrencyType currencyType)
    {
        return _currencies[(int)currencyType];
    }

    /// <summary>
    /// 재화를 증가시키고 변경 이벤트 발행 후 저장합니다.
    /// </summary>
    public void Add(ECurrencyType type, Currency amount)
    {
        _currencies[(int)type] += amount;
        OnDataChanged?.Invoke(type, _currencies[(int)type]);
        Save();
    }

    /// <summary>
    /// 잔액이 충분할 때만 차감·이벤트·저장을 하나의 성공 흐름으로 수행합니다.
    /// </summary>
    public bool TrySpend(ECurrencyType type, Currency amount)
    {
        if (_currencies[(int)type] >= amount)
        {
            _currencies[(int)type] -= amount;
            OnDataChanged?.Invoke(type, _currencies[(int)type]);
            Save();
            return true;
        }
        return false;
    }

    /// <summary>
    /// 런타임 Currency 배열을 저장 DTO로 변환해 Repository에 위임합니다.
    /// </summary>
    private void Save()
    {
        _repository.Save(new CurrencySaveData()
        {
            Currencies = ToSaveData(),
            LastSaveTime = DateTime.UtcNow.ToString("O")
        });
    }

    // 저장 경계에서만 값 객체를 원시 double 배열로 변환합니다.
    private double[] ToSaveData()
    {
        double[] result = new double[_currencies.Length];
        for (int i = 0; i < _currencies.Length; i++)
        {
            result[i] = (double)_currencies[i];
        }
        return result;
    }

    /// <summary>
    /// 상태를 변경하지 않고 구매 가능 여부만 확인합니다.
    /// </summary>
    public bool CanAfford(ECurrencyType type, Currency amount)
    {
        return _currencies[(int)type] >= amount;
    }
}
