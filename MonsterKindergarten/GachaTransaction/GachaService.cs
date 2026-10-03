using UnityEngine;

public enum EGachaFailure
{
    None,
    Locked,
    NoTicket,
    NoRoom,
    NoCandidate,
    SpawnFailed,
}

// 가챠권 한 장을 슬라임 한 마리로 바꾼다. 기획서 §11.2.
//
// 상태가 없어 정적으로 둔다. 필요한 것은 모두 매니저들이 들고 있고, 이 클래스는
// 그 사이의 순서만 정한다.
//
// 순서가 규칙이다. 되돌릴 수 없는 소비를 마지막 확인 뒤로 미룬다. 자리와 후보를
// 먼저 확인하지 않고 티켓부터 쓰면, 실패한 가챠가 티켓만 먹는다.
public static class GachaService
{
    public static EGachaFailure TryPull(
        out SlimeController spawned,
        out EGachaRarity rarity,
        bool isTutorialPull = false)
    {
        spawned = null;
        rarity = EGachaRarity.Common;

        SlimeManager slimeManager = SlimeManager.Instance;
        SpawnManager spawnManager = SpawnManager.Instance;
        CurrencyManager currencyManager = CurrencyManager.Instance;
        if (slimeManager == null || spawnManager == null || currencyManager == null)
        {
            return EGachaFailure.SpawnFailed;
        }

        if (!slimeManager.IsGachaUnlocked) return EGachaFailure.Locked;

        if (!currencyManager.CanAfford(ECurrencyType.GachaTicket, 1d))
        {
            return EGachaFailure.NoTicket;
        }

        // 메인 필드에 자리가 있어야 한다. 결과는 별도 보관함이 아니라 필드에
        // 실제로 태어나므로, 자리가 없으면 놓을 곳이 없다.
        if (!spawnManager.HasMainFieldRoom()) return EGachaFailure.NoRoom;

        float specialChance = slimeManager.SpecialGachaChance;
        if (!TryPick(
                slimeManager,
                specialChance,
                isTutorialPull,
                out ESlimeGrade pickedGrade,
                out EGachaRarity pickedRarity,
                out bool isSpecial))
        {
            return EGachaFailure.NoCandidate;
        }

        SlimeController pulledSlime = null;
        EEconomyTransactionResult transactionResult =
            EconomyTransactionService.TryPurchase(
                currencyManager,
                ECurrencyType.GachaTicket,
                1d,
                () =>
                {
                    pulledSlime = spawnManager.Spawn(pickedGrade, isSpecial: isSpecial);
                    return pulledSlime != null;
                });

        if (transactionResult == EEconomyTransactionResult.Success)
        {
            spawned = pulledSlime;
            rarity = pickedRarity;

            // 실패 횟수는 결과가 실제로 생긴 뒤에만 센다. 환불된 가챠가 피버를 올리면 안 된다.
            if (!isTutorialPull) slimeManager.RecordSpecialGachaResult(isSpecial);
            slimeManager.RecordGachaObtained(pickedGrade);
            LogPull(pickedGrade, isSpecial, specialChance);
            return EGachaFailure.None;
        }

        if (transactionResult == EEconomyTransactionResult.InsufficientCurrency)
        {
            return EGachaFailure.NoTicket;
        }

        Debug.LogError($"가챠 결과를 생성하지 못했습니다. : {pickedGrade}");
        return EGachaFailure.SpawnFailed;
    }

// 기획서 §15.2: 특별 여부를 가장 먼저 판정한다. 종류는 특별이든 일반이든 같은 일반 풀에서
    // 고른다. 특별한 슬라임은 외형과 포인트 배율만 다른 같은 등급이라 후보와 가중치를 나눌
    // 이유가 없다.
    private static bool TryPick(
        SlimeManager slimeManager,
        float specialChance,
        bool isTutorialPull,
        out ESlimeGrade grade,
        out EGachaRarity rarity,
        out bool isSpecial)
    {
        grade = ESlimeGrade.None;
        rarity = EGachaRarity.Common;
        isSpecial = false;

        // 튜토리얼의 무료 한 장은 특별 판정도 하지 않는다. 고정된 좋은 결과로 시작해야
        // 하고, 규칙 밖의 한 장이 피버 실패 횟수를 올리지도 않아야 한다.
        if (isTutorialPull)
        {
            if (!NormalGachaPool.TryPickTutorial(slimeManager.HighestGrade, out NormalGachaResult tutorialResult))
            {
                return false;
            }

            grade = tutorialResult.Grade;
            rarity = tutorialResult.Rarity;
            return true;
        }

        bool isSpecialRoll = Random.value < specialChance;
        if (!NormalGachaPool.TryPick(slimeManager.HighestGrade, out NormalGachaResult result))
        {
            return false;
        }

        grade = result.Grade;
        isSpecial = isSpecialRoll;
        rarity = isSpecialRoll ? EGachaRarity.Special : result.Rarity;
        return true;
    }

    // 확률과 결과를 에디터 콘솔에만 남긴다. 확률 표시가 게임에 없는 규칙(기획서 §16)이라
    // 시뮬레이션과 수동 확인은 이 로그가 유일한 근거다.
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void LogPull(ESlimeGrade grade, bool isSpecial, float specialChance)
    {
        Debug.Log($"[Gacha] {(isSpecial ? "특별" : "일반")} Lv.{(int)grade} (특별 확률 {specialChance:P1})");
    }
}
