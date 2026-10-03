using System;

public enum EEconomyTransactionResult
{
    Success,
    InsufficientCurrency,
    Rejected,
}

// 서로 다른 게임 도메인이 참여하는 경제 동작의 검증, 차감, 실패 환불 순서를
// 한곳에서 유지한다. 각 도메인의 실제 저장 책임은 기존 매니저가 그대로 가진다.
public static class EconomyTransactionService
{
    public static EEconomyTransactionResult TryPurchase(
        CurrencyManager currencyManager,
        ECurrencyType currencyType,
        Currency cost,
        Func<bool> applyPurchase)
    {
        if (currencyManager == null)
        {
            throw new ArgumentNullException(nameof(currencyManager));
        }
        if (applyPurchase == null)
        {
            throw new ArgumentNullException(nameof(applyPurchase));
        }

        if (!currencyManager.TrySpend(currencyType, cost))
        {
            return EEconomyTransactionResult.InsufficientCurrency;
        }

        try
        {
            if (applyPurchase())
            {
                return EEconomyTransactionResult.Success;
            }
        }
        catch
        {
            currencyManager.Add(currencyType, cost);
            throw;
        }

        currencyManager.Add(currencyType, cost);
        return EEconomyTransactionResult.Rejected;
    }

    public static bool TryGrantAfterConsume(
        CurrencyManager currencyManager,
        ECurrencyType currencyType,
        Currency reward,
        Func<bool> consumeSource,
        Action rollbackSource)
    {
        if (currencyManager == null)
        {
            throw new ArgumentNullException(nameof(currencyManager));
        }
        if (consumeSource == null)
        {
            throw new ArgumentNullException(nameof(consumeSource));
        }
        if (rollbackSource == null)
        {
            throw new ArgumentNullException(nameof(rollbackSource));
        }

        if (!consumeSource()) return false;

        try
        {
            currencyManager.Add(currencyType, reward);
            return true;
        }
        catch
        {
            rollbackSource();
            throw;
        }
    }
}
