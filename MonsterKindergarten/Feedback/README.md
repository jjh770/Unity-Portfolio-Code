# Component Feedback

슬라임 클릭 결과를 여러 피드백 컴포넌트로 분리한 코드 샘플입니다.

## 실행 흐름

```text
Clicker
  → ClickInfo 생성
  → SlimeController.OnClick()
  → 재화 지급
  → IFeedback 구현체 실행
```

`SlimeController`는 특정 효과 클래스를 직접 참조하지 않고,
자식 오브젝트에 부착된 `IFeedback` 구현체를 조회해 실행합니다.

```csharp
IFeedback[] feedbacks = GetComponentsInChildren<IFeedback>();

foreach (IFeedback feedback in feedbacks)
{
    feedback.Play(clickInfo);
}
```

## 파일

| 파일 | 역할 |
|---|---|
| [`ClickInfo.cs`](./ClickInfo.cs) | 클릭 종류, 위치, 획득량과 슬라임 등급을 전달합니다. |
| [`IFeedback.cs`](./IFeedback.cs) | 피드백 컴포넌트의 공통 실행 계약입니다. |
| [`SlimeController.cs`](./SlimeController.cs) | 클릭 보상 처리 후 조립된 피드백을 일괄 실행합니다. |
| [`ScaleTweeningFeedback.cs`](./ScaleTweeningFeedback.cs) | DOTween 기반 크기 반응과 풀 반환 시 Tween 정리를 보여주는 대표 구현체입니다. |

## 설계 포인트

- 새로운 효과는 `IFeedback` 구현 컴포넌트를 추가하는 방식으로 확장합니다.
- 클릭 처리 코드는 색상, 사운드, 플로팅 텍스트 같은 구체 효과를 알지 않습니다.
- Tween은 오브젝트 비활성화 시 정리해 풀 재사용 후 이전 효과가 남지 않게 했습니다.

실제 프로젝트에는 색상 변화, 효과음, 포인트 플로팅 피드백 구현체도 포함되어 있지만,
포트폴리오 저장소에는 구조를 이해하는 데 필요한 대표 코드만 선별했습니다.
