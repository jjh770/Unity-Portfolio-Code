# OfflineReward

마지막 저장 시각과 현재 보유 슬라임의 자동 생산량을 기준으로 미접속 보상을 계산하고, 보상 수령·연출이 완료될 때까지 Gameplay를 잠그는 흐름입니다.

## 핵심 흐름

```text
LastSaveTime
    ↓
미접속 시간 계산
    ↓
보유 슬라임별 초당 자동 생산량 합산
    ↓
자동 생산량 × 미접속 시간 × 보상 효율
    ↓
보상 대기 → 수령 → 연출 완료
    ↓
Gameplay 재개
```

## 파일

| 파일 | 역할 |
|---|---|
| [`GameManager.cs`](./GameManager.cs) | 데이터 초기화 완료 확인, 오프라인 보상 계산, 중복 수령 방지, Gameplay 정지·재개를 관리합니다. |
| [`../DomainArchitecture/CurrencyManager.cs`](../DomainArchitecture/CurrencyManager.cs) | UTC 기준 마지막 저장 시각을 보관하고 Pause/Quit 시 현재 상태를 저장할 수 있게 합니다. |
| [`../GameplayLoop/PointCalculator.cs`](../GameplayLoop/PointCalculator.cs) | 온라인 자동 생산과 동일한 업그레이드 규칙으로 슬라임별 생산량을 계산합니다. |
| [`../GameplayLoop/AutoClicker.cs`](../GameplayLoop/AutoClicker.cs) | 보상 대기 중 자동 생산을 정지합니다. |
| [`../GameplayLoop/SpawnManager.cs`](../GameplayLoop/SpawnManager.cs) | 보상 대기 중 자동 스폰을 정지합니다. |

## 보상 계산

```text
Reward
= floor(
    TotalAutoPointPerSecond
    × OfflineSeconds
    × OfflineRewardEfficiency
  )
```

- 최소 미접속 시간: 60초
- 최대 인정 시간: 8시간
- 기본 보상 효율: 50%
- 시간 계산은 UTC 기준
- `PointCalculator`를 재사용해 현재 자동 생산 업그레이드가 오프라인 생산량에도 동일하게 반영됩니다.

## 상태 제어

보상이 계산되면 즉시 재화를 지급하지 않고 `OfflineRewardResult`를 대기 상태로 보관합니다.

```text
보상 계산
→ IsGameplayActive = false
→ 팝업에서 TryClaimOfflineReward()
→ 재화 지급
→ 보상 연출 완료
→ CompleteOfflineRewardPresentation()
→ IsGameplayActive = true
```

이렇게 수령 전 자동 생산·스폰과 보상 재화가 동시에 변하는 것을 막고, 중복 수령도 차단합니다.

## 앱 생명주기

- 앱이 Pause/Quit 될 때 마지막 저장 시각을 갱신합니다.
- 이미 수령 대기 중인 보상이 있다면 저장 시각을 덮어쓰지 않아 다음 실행에서 보상이 사라지지 않도록 합니다.
- 복귀 시 현재 상태를 기준으로 다시 오프라인 보상 여부를 확인합니다.
