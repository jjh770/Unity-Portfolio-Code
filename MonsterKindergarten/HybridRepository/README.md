# HybridRepository

PlayerPrefs와 Firestore를 결합해 같은 기기에서는 즉시 보존하고, 기기 변경·재설치에 필요한 클라우드 사본은 쓰기 한도를 고려해 전송하는 저장 계층입니다.

## 파일

| 파일 | 역할 |
|---|---|
| [`IRepository.cs`](./IRepository.cs) | 저장·즉시 flush·실패를 보존하는 로드 계약 |
| [`ISaveData.cs`](./ISaveData.cs) | 도메인별 스키마 버전과 `LastSaveTime` 공통 계약 |
| [`SaveLoadResult.cs`](./SaveLoadResult.cs) | `Loaded`·`NotFound`·`Failed` 및 실패 원인 표현 |
| [`HybridRepository.cs`](./HybridRepository.cs) | 로컬 저장, 5초 throttle, 병렬 로드와 충돌 해결 |
| [`CloudSaveGuard.cs`](./CloudSaveGuard.cs) | 영구적인 클라우드 쓰기 실패를 세션 중 안내 |

## 저장 정책

```text
데이터 변경
→ ServerClock 기준 LastSaveTime 갱신
→ PlayerPrefs 즉시 저장 시도
→ 예약된 클라우드 전송이 있으면 payload만 최신 값으로 교체
→ 최대 5초마다 최신 전체 문서를 Firestore에 전송
→ Pause / Quit 시 예약분을 즉시 전송 요청
```

`debounce`는 매 요청마다 시간을 다시 재기 때문에 자동 생산처럼 변경이 계속되면 클라우드 저장이 영원히 실행되지 않을 수 있습니다. 이 구현은 예약을 하나만 유지하는 throttle이라 지속적으로 변경되어도 일정 간격마다 최신 값이 전송됩니다.

## 로드 정책

```text
PlayerPrefs Load ─┐
                  ├→ UniTask.WhenAll → 상태 검증 → LastSaveTime 비교
Firestore Load ───┘
```

- `NotFound`만 신규 계정으로 시작할 수 있습니다.
- 둘 중 하나를 읽지 못하면 그 이유를 호출부에 전달해, 보이지 않은 원본을 새 저장으로 덮어쓰지 않습니다.
- 로컬이 손상됐지만 클라우드가 정상이라면 클라우드를 채택하고 로컬 사본 갱신은 best-effort로 수행합니다.
- 같은 시각 또는 잘못된 시각은 Firestore를 우선합니다.

## 설계 포인트

- 로컬 저장 실패가 클라우드 전송까지 막지 않고, 클라우드 실패도 같은 기기의 플레이를 잠그지 않습니다.
- 네트워크 단절은 Firestore SDK가 큐잉·재시도하므로, 예외로 들어오는 규칙 거부 같은 영구 실패만 안내합니다.
- 초기화 중 저장이 재실행되지 않도록 reset generation을 확인합니다.
