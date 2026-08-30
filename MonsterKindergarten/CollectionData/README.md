# CollectionData

도감에 일반 슬라임의 최초 등록 여부뿐 아니라 자연 생성, 머지 생성, 직접 터치,
누적 생산 포인트를 등급별로 기록하고 저장 시점을 묶어 관리합니다.

## 파일

| 파일 | 역할 |
|---|---|
| [`NormalSlimeCollectionStats.cs`](./NormalSlimeCollectionStats.cs) | 등급별 최초 등록 시각과 자연 생성·머지·터치·생산량 누적 기록을 관리합니다. |
| [`SlimeManager.cs`](./SlimeManager.cs) | 게임 이벤트를 누적 기록에 연결하고 Dirty 상태, 30초 주기, Pause·Quit 저장을 관리합니다. |

저장 DTO와 v4 스키마 정의는 [`../SaveDataMigration/SlimeStatusSaveData.cs`](../SaveDataMigration/SlimeStatusSaveData.cs)를 함께 참고합니다.

## 기록 흐름

```text
자연 생성 ──────┐
머지 생성 ──────┤
직접 터치 ──────┼→ NormalSlimeCollectionStats
포인트 생산 ────┘
                       ↓
                  등급별 누적 기록
```

장식장에 일반 슬라임을 처음 배치하면 도감 등록과 최초 등록 시각도 함께 기록합니다.

## 저장 흐름

```text
통계 데이터 변경
→ Dirty = true
→ 30초 주기 저장

또는

Application Pause / Quit
→ Dirty 확인
→ 저장
```

## 설계 포인트

- 도감 통계는 UI가 직접 계산하지 않고 별도 `NormalSlimeCollectionStats`에서 관리합니다.
- 최초 등록 시각은 한 번 기록된 뒤 다시 덮어쓰지 않습니다.
- 자연 생성, 머지 생성, 직접 터치, 생산 포인트를 각각 명시적인 기록 메서드로 분리합니다.
- 이벤트마다 저장하지 않고 Dirty 상태로 표시해 잦은 저장 요청을 줄입니다.
- 기본 저장 주기는 30초이며 앱 Pause·Quit 시 변경 데이터가 남아 있으면 저장합니다.
- `long` Count는 `long.MaxValue`에서 증가를 멈추고 생산 포인트 Infinity는 `double.MaxValue`로 제한합니다.
