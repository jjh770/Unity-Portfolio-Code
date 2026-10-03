# InputUIState

장식장 선택·튜토리얼·모달·엔딩 연출이 동시에 월드 입력과 HUD를 요구해도, 먼저 끝난 기능이 다른 기능의 상태를 실수로 복원하지 않도록 owner 기반 요청으로 분리했습니다.

## 파일

| 파일 | 역할 |
|---|---|
| [`ClickerInputMode.cs`](./ClickerInputMode.cs) | 입력 상태와 `Space < Selection < Tutorial < Modal` 우선순위 정의 |
| [`Clicker.cs`](./Clicker.cs) | owner별 입력 요청, 터치 보정, 드래그·합성 대상 관리 |
| [`HudVisibility.cs`](./HudVisibility.cs) | owner별 HUD 숨김 요청을 합집합으로 적용 |

## 입력 흐름

```text
각 기능 → PushMode(owner, mode, priority)
        → 현재 요청 중 가장 높은 priority 선택
        → Clicker 입력 상태 적용
각 기능 종료 → ReleaseMode(owner)
          → 남은 요청으로 다시 계산
```

같은 우선순위에서는 나중 요청이 우선하지만, 같은 owner의 갱신은 기존 순서를 바꾸지 않습니다. 따라서 화면이 겹쳐도 어떤 기능이 먼저 닫히는지에 상태 복원이 의존하지 않습니다.

## 설계 포인트

- 월드 선택과 UI 입력은 별도 경로로 두고, `Clicker`는 슬라임 터치·드래그만 담당합니다.
- 작은 슬라임 콜라이더와 손가락 오차의 요구를 분리해, 정확한 hit가 없을 때만 가까운 슬라임을 보정 선택합니다.
- 튜토리얼은 지정한 한두 대상만 선택·드래그하도록 제한할 수 있습니다.
- HUD는 남은 숨김 요청의 합집합만 적용하므로, 한 연출이 끝나도 다른 연출의 숨김 상태가 유지됩니다.
