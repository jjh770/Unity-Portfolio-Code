# PlayerArchitecture

입력 감지, 상태 전환, 행동 실행과 특수 이동을 각각의 컴포넌트로 분리한 플레이어 구조입니다.

## 파일

| 파일 | 역할 |
|---|---|
| [`PlayerStateManager.cs`](./PlayerStateManager.cs) | 현재 상태와 상태 전환 규칙, 행동 가능 여부를 중앙 관리합니다. |
| [`PlayerInputHandler.cs`](./PlayerInputHandler.cs) | Unity 입력을 행동 이벤트로 변환하고 이동 입력을 보관합니다. |
| [`TweenMovement.cs`](./TweenMovement.cs) | 공격·대시의 선형 이동과 스킬의 포물선 이동을 공통 처리합니다. |
| [`PlayerAttack.cs`](./PlayerAttack.cs) | 콤보, 공격 취소, 애니메이션 이벤트와 판정 구간을 연결합니다. |
| [`PlayerDash.cs`](./PlayerDash.cs) | 상태와 쿨타임을 확인한 뒤 TweenMovement로 대시를 실행합니다. |

## 흐름

```text
Unity Input
→ PlayerInputHandler 이벤트
→ PlayerStateManager 전환 검증
→ PlayerAttack / PlayerDash
→ TweenMovement
→ CharacterController.Move
```

## 설계 포인트

- 입력을 각 행동 컴포넌트에서 제거해 입력 장치 변경과 상태별 입력 해석을 분리했습니다.
- 공격과 대시가 공유하는 비일반 이동을 하나의 컴포넌트로 모았습니다.
- 애니메이션 이벤트가 실제 공격 판정 시작·종료 시점을 결정합니다.
