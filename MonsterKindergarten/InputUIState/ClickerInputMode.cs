// 등록 순서와 무관한 우선순위. 팝업·연출 차단은 튜토리얼 선택보다 우선한다.
public enum ClickerInputPriority
{
    Space,
    Selection,
    Tutorial,
    Modal,
}

// Clicker의 월드 입력 상태 한 벌.
// 값 타입이라 소유자별로 쌓아 두었다가 그대로 꺼내 쓸 수 있다.
public readonly struct ClickerInputMode
{
    public bool ClickEnabled { get; }
    public bool DragEnabled { get; }
    public bool InvokeClickAction { get; }
    public SlimeController RestrictedTarget { get; }
    public SlimeController SecondaryRestrictedTarget { get; }

    public ClickerInputMode(
        bool clickEnabled,
        bool dragEnabled,
        SlimeController restrictedTarget = null,
        SlimeController secondaryRestrictedTarget = null,
        bool invokeClickAction = true)
    {
        ClickEnabled = clickEnabled;
        DragEnabled = dragEnabled;
        RestrictedTarget = restrictedTarget;
        SecondaryRestrictedTarget = secondaryRestrictedTarget;
        InvokeClickAction = invokeClickAction;
    }

    // 대사, 팝업, 연출 중 월드 입력을 완전히 막는다.
    public static ClickerInputMode Blocked => new(false, false);

    // 메인 스테이지 평상시. 터치 포인트와 드래그 합성을 모두 허용한다.
    public static ClickerInputMode Free => new(true, true);

    // 선택만 허용한다. 클릭 포인트는 지급하지 않는다.
    // 장식장(기획서 §7.2)과 장식장 이동 선택 모드(§7.4)가 쓴다.
    public static ClickerInputMode SelectOnly(SlimeController restrictedTarget = null)
    {
        return new ClickerInputMode(
            clickEnabled: true,
            dragEnabled: false,
            restrictedTarget: restrictedTarget,
            invokeClickAction: false);
    }

    // 지정한 슬라임만 클릭할 수 있다. 튜토리얼 안내용.
    public static ClickerInputMode ClickOnly(SlimeController target)
    {
        return new ClickerInputMode(true, false, target);
    }

    // 지정한 슬라임만 드래그할 수 있다. 합성 안내는 두 번째 대상까지 허용한다.
    public static ClickerInputMode DragOnly(
        SlimeController target,
        SlimeController secondaryTarget = null)
    {
        return new ClickerInputMode(false, true, target, secondaryTarget);
    }
}
