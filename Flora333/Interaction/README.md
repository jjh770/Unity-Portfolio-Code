# Interaction

플레이어가 주변 오브젝트의 구체 타입을 몰라도 공통 인터페이스를 통해 상호작용하고, 들기·던지기를 수행하도록 구성했습니다.

## 파일

| 파일 | 역할 |
|---|---|
| `IInteractable.cs` | 상호작용 가능 여부, 타입, Transform과 실행 계약을 정의합니다. |
| `IPickable.cs` | 들기와 던지기 동작을 추가한 상호작용 확장 인터페이스입니다. |
| `InteractionType.cs` | 대상 선택 우선순위에 사용하는 상호작용 종류입니다. |
| `PlayerInteraction.cs` | 주변 대상을 수집하고 타입 우선순위와 거리로 현재 대상을 선택합니다. |
| `PlayerPickUpThrow.cs` | 애니메이션 이벤트에 맞춰 복수 오브젝트를 들고 FIFO 순서로 던집니다. |

## 설계 포인트

- `GetComponents<IInteractable>()`로 하나의 Collider에 연결된 여러 상호작용 구현을 수집합니다.
- 거리 계산은 `sqrMagnitude`를 사용하고 대상 갱신 주기를 제한합니다.
- 파괴된 Unity Object가 인터페이스 참조에 남는 경우를 정리합니다.
