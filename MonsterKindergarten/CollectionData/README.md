# CollectionData

도감의 최초 등록과 등급별 플레이 기록을 도메인에서 집계하고, 최초 졸업 순간에는 이후 플레이와 분리된 통계 스냅샷을 남기는 코드입니다.

## 파일

| 파일 | 역할 |
|---|---|
| [`NormalSlimeCollectionStats.cs`](./NormalSlimeCollectionStats.cs) | 최초 등록·자연 생성·머지·터치·생산·가챠 획득의 등급별 통계 |
| [`SlimeManager.cs`](./SlimeManager.cs) | 게임 이벤트 연결, 상태 검증, Dirty 저장과 앱 생명주기 flush |

## 기록 흐름

```text
Spawn / Merge / Manual touch / Production / Gacha
                         ↓
             NormalSlimeCollectionStats
                         ↓
          SlimeStatus + GraduationStatistics
```

일반 도감 20종을 장식장에 전시해 졸업식이 시작되면, 그 시점의 누적 기록을 한 번만 고정합니다. 이후 플레이 기록은 계속 증가하지만 졸업 통계는 바뀌지 않아 첫 완주 순간을 보존합니다.

## 저장 정책

- 잦은 통계 변경은 `Dirty`로 표시하고 30초 주기로 저장합니다.
- 앱 pause·quit에서는 `GameManager`가 모든 도메인을 순회해 저장과 보류 중인 클라우드 전송을 요청합니다.
- `long` 카운트는 최대값에서 증가를 멈추고, 생산 포인트는 유효성 검사를 거칩니다.
- UI는 누적값을 직접 계산하지 않고 도메인 상태를 조회합니다.
