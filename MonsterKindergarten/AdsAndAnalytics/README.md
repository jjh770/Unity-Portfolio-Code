# AdsAndAnalytics

보상형 광고를 불러오고 보여 준 뒤 보상을 지급하고, 그 흐름을 분석 이벤트로 남기는 코드입니다. 광고 SDK나 분석 SDK가 실패해도 게임 진행과 보상 지급이 막히지 않도록 SDK 호출, 보상 규칙, 지표 기록을 서로 다른 계층으로 나눴습니다.

## 파일

| 파일 | 역할 |
|---|---|
| [`IAdService.cs`](./IAdService.cs) | 보상형 광고 하나를 불러오고 보여 주는 쪽의 계약 |
| [`GoogleMobileAdsService.cs`](./GoogleMobileAdsService.cs) | Android AdMob 구현. 초기화, 불러오기 재시도, 만료 처리, 결과 정리 |
| [`FakeAdService.cs`](./FakeAdService.cs) | 에디터용 가짜 광고. 보상, 중도 닫기, 준비 상태 변화를 흉내 냄 |
| [`NullAdService.cs`](./NullAdService.cs) | 광고가 없는 빌드용. 항상 준비되지 않은 상태로 버튼을 숨김 |
| [`AdRewardService.cs`](./AdRewardService.cs) | 볼 수 있는지 판단, 광고 중복 방지, 보상 신호가 온 한 번만 보상 지급, 플랫폼 분기 |
| [`AdRewardTableSO.cs`](./AdRewardTableSO.cs) | 보상 수치, 하루 횟수, 개발 빌드와 릴리스 빌드의 광고 단위 ID |
| [`EAdShowResult.cs`](./EAdShowResult.cs) | 광고 한 번의 결과 |
| [`EAdAvailability.cs`](./EAdAvailability.cs) | 지금 볼 수 있는지와 볼 수 없는 이유 |
| [`EAdPlacement.cs`](./EAdPlacement.cs) | 광고 자리이자 보상 종류. 지표의 placement 값 |
| [`AdAnalytics.cs`](./AdAnalytics.cs) | 광고 흐름의 이벤트 이름과 매개변수를 한 곳에 모음 |
| [`IAnalyticsService.cs`](./IAnalyticsService.cs) | 분석 이벤트를 보내는 쪽의 계약. 실패해도 게임을 막지 않음 |
| [`Analytics.cs`](./Analytics.cs) | 분석 서비스 진입점과 플랫폼 분기 |
| [`FirebaseAnalyticsService.cs`](./FirebaseAnalyticsService.cs) | Android Firebase Analytics를 JNI로 직접 호출 |
| [`RemoveAdsInitProviderPostProcess.cs`](./RemoveAdsInitProviderPostProcess.cs) | 동의 전 광고 SDK 자동 초기화를 막는 Android 빌드 후처리 |

## 처리 순서

```text
광고 버튼 입력
→ GetAvailability: 해금, 하루 횟수, 부스트 상한, 광고 준비 여부 확인
→ ad_start 기록
→ IAdService.ShowRewardedAsync
→ 보상 신호가 온 경우에만 보상 지급
   뽑기권: 하루 횟수를 먼저 기록하고 지급, 지급 실패 시 횟수를 되돌림
   부스트: 남은 시간과 하루 횟수 기록
→ ad_reward 기록, 이번 보상으로 하루 한도에 닿으면 ad_limit_reached 기록
→ 보상 없이 닫히면 ad_close_early, 표시에 실패하면 ad_load_fail 기록
```

## 설계 포인트

- 플랫폼 분기는 `AdRewardService.CreateAdService` 한 곳에 둡니다. 에디터는 `FakeAdService`, Android는 AdMob, 그 밖의 빌드는 `NullAdService`를 사용해 광고 버튼이 자연스럽게 숨겨집니다.
- 동의 기록이 없거나 광고 단위 ID가 비어 있으면 SDK를 초기화하지 않습니다. 개발 빌드는 Google이 공개한 테스트 단위를, 릴리스 빌드는 에셋에 저장한 실제 단위를 사용해 릴리스에서 테스트 광고가 나가지 않게 했습니다. 실제 단위 ID는 저장소에 포함하지 않았습니다.
- SDK 초기화는 한 번만 하고 15초 응답 제한을 둡니다. 불러오기에 실패하면 30초 동안 다시 요청하지 않아, 네트워크가 없을 때 매 프레임 요청하지 않습니다.
- 보상형 광고는 한 번 보여 주면 버리고 새로 불러옵니다. 불러온 지 55분이 지난 광고는 1시간 만료에 대비해 준비되지 않은 것으로 봅니다.
- 광고 콜백은 Unity 메인 스레드에서 받도록 설정하고, 닫힘과 실패와 취소를 `UniTaskCompletionSource`로 모아 하나의 `EAdShowResult`로 정리합니다.
- 광고를 보는 동안에는 부스트 시간이 흐르지 않고, 광고를 닫은 직후의 앱 복귀는 오프라인 이탈로 세지 않습니다.
- 하루 횟수는 보상을 지급하는 순간의 날짜로 다시 계산합니다. 광고가 자정을 넘겨 끝나도 횟수가 다른 날에 기록되지 않습니다.
- 분석은 `IAnalyticsService` 뒤에 두고 모든 호출의 예외를 삼킵니다. 분석이 깨져도 보상 지급과 화면은 막히지 않습니다. 진행 값과 계정 식별자는 이 경로로 보내지 않습니다.
- Firebase Analytics는 Unity 패키지를 새로 들이지 않고, 이미 의존성으로 들어 있는 Android 라이브러리를 JNI로 직접 호출합니다. 사용하는 API는 `getInstance`, `logEvent`, `setAnalyticsCollectionEnabled` 세 가지뿐입니다.
- 앱이 켜질 때 광고 SDK를 스스로 초기화하는 `MobileAdsInitProvider`는 빌드 후처리에서 병합 매니페스트에 제거 표시를 넣어 막았습니다. 같은 단계의 androidlib 매니페스트에 두면 우선순위가 같아 적용되지 않는다는 것을 기기 빌드에서 확인한 뒤, Unity가 내보낸 매니페스트를 빌드할 때마다 고치는 방식으로 바꿨습니다.

## 분석 이벤트

| 이벤트 | 시점 | 매개변수 |
|---|---|---|
| `ad_start` | 광고 표시 시작 | `placement` |
| `ad_reward` | 보상 지급 | `placement`, 하루 한도가 있는 보상은 `today_count` |
| `ad_limit_reached` | 이번 보상으로 하루 한도에 닿음 | `placement` |
| `ad_close_early` | 보상 없이 닫힘 | `placement` |
| `ad_load_fail` | 미리 불러오기 실패 또는 표시 실패 | `placement` (`preload` 포함), `fail_code` |

## 포함하지 않은 것

`SlimeManager`, `CurrencyManager`, `ServerClock`, `ConsentRecord`, `AdRewardProgress` 등 연결된 타입은 포함하지 않았습니다. 뽑기권 지급에 쓰는 `EconomyTransactionService`는 [GachaTransaction](../GachaTransaction/README.md) 폴더에 있습니다.
