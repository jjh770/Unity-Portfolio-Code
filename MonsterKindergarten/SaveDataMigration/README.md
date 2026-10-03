# SaveDataMigration

콘텐츠 확장 과정에서 슬라임 저장을 **등급별 Count → `InstanceId` 기반 개체 목록**으로 전환하고, 현재 `SlimeStatus` v15까지 이전 문서를 호환하는 코드입니다.

## 파일

| 파일 | 역할 |
|---|---|
| [`ISaveData.cs`](./ISaveData.cs) | 저장 도메인의 버전·시간 공통 계약 |
| [`SlimeInstanceSaveData.cs`](./SlimeInstanceSaveData.cs) | 개체 ID, 등급, 특별 여부, 위치 DTO |
| [`SlimeStatusSaveData.cs`](./SlimeStatusSaveData.cs) | 레거시 DTO, 현재 DTO, 승격과 정규화 |
| [`PlayerPrefsSlimeStatusRepository.cs`](./PlayerPrefsSlimeStatusRepository.cs) | JSON 로드에서 v15 승격·검증 |
| [`FirebaseSlimeStatusRepository.cs`](./FirebaseSlimeStatusRepository.cs) | Firestore 로드에서 같은 호환 정책 적용 |

## 승격 흐름

```text
Legacy { Grade, Count }
→ SchemaVersion 확인
→ deterministic legacy-{grade}-{index} ID 생성
→ SlimeInstance 목록으로 변환
→ 정규화·무결성 검증
→ 현재 SlimeStatus v15
```

## 버전 전략

- v2: 등급 개수 저장을 개체 저장으로 전환
- v3~v4: 일반 도감 등록·등급별 누적 기록 추가
- v5~v10: 티켓·자동 스폰·튜토리얼·장식장 놀이터·배경 상점 확장
- v11: 게임 시작·졸업 시각 추가
- v12~v13: 폐기된 특별 도감 필드를 읽기 전용 레거시로 유지
- v14: 등급별 가챠 획득 기록 추가
- v15: 최초 졸업 통계 스냅샷, 누적 가챠권·자동 합성 기록 추가

새 필드를 제거하지 않고 레거시 필드를 읽기 전용으로 남긴 이유는, 이미 배포된 Firestore 문서와 이전 로컬 JSON을 계속 읽기 위해서입니다. 새 저장은 더 이상 쓰지 않는 필드를 비워 다음 버전으로 정리합니다.

## 안전 장치

- 현재 앱보다 높은 버전은 로드를 중단해 구버전 앱의 덮어쓰기를 막습니다.
- 중복 ID, 복원할 수 없는 슬라임, 유효 범위를 벗어난 값은 변조 또는 손상으로 취급합니다.
- JSON의 채워진 리스트 초기값이 역직렬화 시 뒤에 붙는 문제를 피하기 위해 `ObjectCreationHandling.Replace`를 사용합니다.
- Firestore에 저장하는 모든 필드는 `[FirestoreProperty]`를 유지합니다.
