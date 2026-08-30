# SaveDataMigration

콘텐츠 확장에 대응해 슬라임 저장 구조를 **등급별 개수 기반 저장 → InstanceId 기반 개체 저장**으로 전환하고,
기존 진행 데이터는 `SchemaVersion`을 기준으로 현재 구조까지 승격합니다.

## 파일

| 파일 | 역할 |
|---|---|
| [`ISaveData.cs`](./ISaveData.cs) | 도메인별 `SchemaVersion`과 `LastSaveTime` 저장 계약을 정의합니다. |
| [`SlimeInstanceSaveData.cs`](./SlimeInstanceSaveData.cs) | `InstanceId`, `Grade`, `IsSpecial`, `Location`을 가진 슬라임 개체 저장 DTO입니다. |
| [`SlimeStatusSaveData.cs`](./SlimeStatusSaveData.cs) | Legacy Count DTO, 현재 SaveData, Count → Entity 마이그레이션과 도감 데이터 정규화를 담당합니다. |
| [`PlayerPrefsSlimeStatusRepository.cs`](./PlayerPrefsSlimeStatusRepository.cs) | 로컬 저장 데이터의 버전을 판별하고 필요한 마이그레이션·정규화를 적용합니다. |
| [`FirebaseSlimeStatusRepository.cs`](./FirebaseSlimeStatusRepository.cs) | Firestore에서도 동일한 버전 판별과 마이그레이션 정책을 적용합니다. |

## 마이그레이션 흐름

```text
Legacy Save
{ Grade, Count }
      ↓
SchemaVersion 확인
      ↓
Count → SlimeInstance List
      ↓
Validate / Normalize
      ↓
Current Save
{ InstanceId, Grade, IsSpecial, Location }
```

예를 들어 `Grade 3 × 2`는 다음처럼 개체 목록으로 승격합니다.

```text
legacy-3-0 / Grade 3 / MainStage
legacy-3-1 / Grade 3 / MainStage
```

## 버전 정책

| Version | 변경 내용 |
|---|---|
| v0/v1 | 등급별 Count 기반 저장 |
| v2 | `SlimeInstance` 기반 개체 저장으로 전환 |
| v3 | 일반 슬라임 도감 등록 상태 추가 |
| v4 | 등급별 도감 누적 통계 추가 |

현재 앱보다 높은 버전의 저장 데이터는 이전 앱이 잘못 덮어쓰지 않도록 로드를 중단합니다.

## 설계 포인트

- 같은 등급의 슬라임을 개별적으로 구분할 수 있도록 영구 `InstanceId`를 저장합니다.
- 레거시 Count 데이터는 `legacy-{grade}-{index}` 형식으로 안정적인 ID를 생성합니다.
- 잘못된 Grade와 0 이하 Count는 마이그레이션 과정에서 제외합니다.
- 새 필드가 없는 이전 세이브는 현재 도감 크기에 맞춰 기본값을 생성합니다.
- 음수 Count, null 문자열, NaN·Infinity 등 잘못된 누적 통계 값을 정규화합니다.
- Migration이 발생한 데이터는 현재 구조로 다시 저장해 이후 실행부터 최신 스키마를 사용합니다.
