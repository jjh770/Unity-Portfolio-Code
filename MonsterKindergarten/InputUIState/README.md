# InputUIState

장식장 선택·관찰, 튜토리얼, 모달 UI가 같은 입력과 HUD를 공유하면서
각 기능이 상태를 직접 변경할 때 발생하던 종료 순서 의존성을 owner 기반 요청 구조로 분리합니다.

## 파일

| 파일 | 역할 |
|---|---|
| [`ClickerInputMode.cs`](./ClickerInputMode.cs) | 입력 상태 한 벌과 `Space < Selection < Tutorial < Modal` 우선순위를 정의합니다. |
| [`Clicker.cs`](./Clicker.cs) | owner별 입력 모드 요청을 등록·해제하고 가장 높은 우선순위의 모드를 적용합니다. |
| [`HudVisibility.cs`](./HudVisibility.cs) | owner별 HUD 숨김 요청을 누적하고 남아 있는 요청의 합집합만큼 HUD를 숨깁니다. |

## 입력 흐름

```text
Selection ─┐
Tutorial  ─┼→ PushMode(owner, mode, priority)
Modal     ─┘
                  ↓
         가장 높은 우선순위 선택
                  ↓
            현재 입력 모드 적용
```

owner가 종료되면 자신의 요청만 `ReleaseMode`로 제거하고,
남은 요청을 다시 평가해 이전 상태를 복원합니다.

## HUD 흐름

```text
장식장 / 튜토리얼 / 모달
          ↓
PushHide(owner, parts)
          ↓
남아 있는 Hide 요청 합집합 계산
          ↓
Top / Bottom HUD 상태 적용
```

## 설계 포인트

- 각 기능이 전역 입력 상태를 직접 덮어쓰지 않고 자신이 필요한 상태만 요청합니다.
- 입력은 등록 순서와 별개로 명시적 priority를 사용합니다.
- 같은 priority에서는 나중에 등록된 요청이 우선하며, 같은 owner의 갱신은 기존 위치를 유지합니다.
- owner가 해제를 빠뜨리면 입력이 잠긴 채 남을 수 있어 Editor에서 요청 스택 깊이를 검사합니다.
- HUD는 입력과 별도의 책임으로 분리하고, 다른 owner의 숨김 요청이 남아 있으면 숨김 상태를 유지합니다.
- HUD 시작 위치와 이동 처리는 `HudVisibility` 한 곳에서 관리해 연출마다 좌표를 따로 기억하지 않도록 했습니다.
