# HybridRepository

게임 데이터 저장 매체를 `IRepository<T>`로 추상화하고,
PlayerPrefs와 Firestore를 결합해 빠른 로컬 저장과 서버 쓰기 요청 절감을 함께 처리합니다.

## 파일

| 파일 | 역할 |
|---|---|
| [`IRepository.cs`](./IRepository.cs) | 저장 데이터의 비동기 Save·Load 계약을 정의합니다. |
| [`ISaveData.cs`](./ISaveData.cs) | 최신 데이터 선택에 사용할 `LastSaveTime`과 저장 버전을 공통으로 요구합니다. |
| [`HybridRepository.cs`](./HybridRepository.cs) | 로컬 즉시 저장, Firestore 디바운스 저장, 병렬 로드와 최신 데이터 선택을 담당합니다. |

## 저장 흐름

```text
데이터 변경
→ UTC LastSaveTime 갱신
→ PlayerPrefs 즉시 저장
→ 기존 Firestore 예약 취소
→ 0.6초 동안 추가 변경 대기
→ 마지막 데이터만 Firestore 저장
```

## 로드 흐름

```text
PlayerPrefs Load ─┐
                  ├→ UniTask.WhenAll → LastSaveTime 비교 → 최신 데이터 선택
Firestore Load ───┘
```

## 설계 포인트

- 반복 저장을 디바운스해 Firestore 쓰기 요청을 줄입니다.
- 로컬 저장은 즉시 수행해 네트워크 상태와 관계없이 최신 진행도를 우선 보존합니다.
- 로컬과 클라우드 저장 시점이 다르면 `LastSaveTime`을 기준으로 최신 데이터를 선택합니다.
- Firestore 데이터가 더 최신이면 선택 후 PlayerPrefs에도 다시 반영합니다.
- 진행 데이터 초기화 중 예약된 서버 저장이 다시 실행되지 않도록 ResetGeneration을 함께 확인합니다.
