# SaveArchitecture

게임 데이터 저장 매체를 `IRepository<T>`로 추상화하고, 로컬과 서버 저장소를 결합해 빠른 저장과 서버 요청 절감을 함께 처리합니다.

## 파일

| 파일 | 역할 |
|---|---|
| [`IRepository.cs`](./IRepository.cs) | 저장 데이터의 비동기 Save·Load 계약을 정의합니다. |
| [`ISaveData.cs`](./ISaveData.cs) | 충돌 해결에 사용할 마지막 저장 시각을 공통으로 요구합니다. |
| [`HybridRepository.cs`](./HybridRepository.cs) | 로컬 즉시 저장, Firebase 디바운스 저장, 병렬 로드와 최신 데이터 선택을 담당합니다. |
| [`PlayerPrefsSlimeStatusRepository.cs`](./PlayerPrefsSlimeStatusRepository.cs) | 슬라임 상태를 JsonUtility 호환 Wrapper로 변환해 사용자별 PlayerPrefs에 저장합니다. |
| [`FirebaseSlimeStatusRepository.cs`](./FirebaseSlimeStatusRepository.cs) | 동일한 저장 계약으로 Firestore 문서를 저장·로드합니다. |

## 저장 흐름

```text
데이터 변경
→ UTC 저장 시각 갱신
→ PlayerPrefs 즉시 저장
→ 기존 서버 예약 취소
→ 0.6초 동안 추가 변경 대기
→ 마지막 데이터만 Firebase 저장
```

## 로드 흐름

```text
PlayerPrefs Load ─┐
                  ├→ UniTask.WhenAll → LastSaveTime 비교 → 최신 데이터 선택
Firebase Load ────┘
```

## 설계 포인트

- 반복 저장을 디바운스해 서버 쓰기 횟수를 줄입니다.
- 로컬 저장은 즉시 완료되므로 앱이 갑자기 종료되어도 최신 상태를 우선 보존합니다.
- `JsonUtility`의 Dictionary 직렬화 제한은 리스트 Wrapper로 변환해 해결합니다.
