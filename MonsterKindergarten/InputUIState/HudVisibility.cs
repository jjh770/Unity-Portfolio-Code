using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[Flags]
public enum EHudParts
{
    None = 0,
    Top = 1 << 0,
    Bottom = 1 << 1,
    All = Top | Bottom,
}

// 상단·하단 HUD 루트를 화면 밖으로 밀어내는 일을 한 곳에서 처리한다.
//
// 시작 위치를 아는 곳이 하나뿐이어야 한다. 연출마다 각자 위치를 기억하면
// 이미 밀려 있는 좌표를 시작 위치로 잘못 저장했다가 그 자리로 되돌려서
// HUD가 영영 돌아오지 않는다.
//
// 요청은 소유자별로 쌓고 남은 요청의 합집합만큼 숨긴다. 하나라도 남아 있으면
// 숨긴 채로 두므로 두 연출이 겹쳐도 안전하다. Clicker의 입력 모드와 같은 방식이다.
//
// 좌측 업그레이드 서랍은 여기서 다루지 않는다. 자기 폭과 세이프에어리어로
// 숨김 위치를 계산하므로 좌표를 옮기는 대신 UpgradeUI에 위임해야 한다.
public sealed class HudVisibility : MonoBehaviour
{
    [SerializeField] private RectTransform _topRoot;
    [SerializeField] private RectTransform _bottomRoot;
    [SerializeField, Min(0f)] private float _duration = 0.3f;

    private readonly List<HideRequest> _requests = new();
    private Vector2 _topStartPosition;
    private Vector2 _bottomStartPosition;
    private Tween _topTween;
    private Tween _bottomTween;
    private bool _isTopHidden;
    private bool _isBottomHidden;
    private bool _isInitialized;

    // 시작 위치는 다른 컴포넌트의 Start보다 먼저 확정되어야 한다.
    private void Awake()
    {
        if (_topRoot == null || _bottomRoot == null)
        {
            Debug.LogError("HUD 숨김의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _topStartPosition = _topRoot.anchoredPosition;
        _bottomStartPosition = _bottomRoot.anchoredPosition;
        _isInitialized = true;
    }

    private void OnDestroy()
    {
        _topTween?.Kill();
        _bottomTween?.Kill();
    }

    // 같은 소유자가 다시 요청하면 범위만 갱신한다.
    public void PushHide(object owner, EHudParts parts)
    {
        if (!_isInitialized || owner == null) return;

        int index = FindRequestIndex(owner);
        if (index >= 0)
        {
            _requests[index] = new HideRequest(owner, parts);
        }
        else
        {
            _requests.Add(new HideRequest(owner, parts));
        }

        Apply(animated: true);
    }

    // 연출 없이 즉시 되돌려야 하는 정리 경로에서는 animated를 끈다.
    public void Release(object owner, bool animated = true)
    {
        if (!_isInitialized || owner == null) return;

        int index = FindRequestIndex(owner);
        if (index < 0) return;

        _requests.RemoveAt(index);
        Apply(animated);
    }

    private int FindRequestIndex(object owner)
    {
        for (int i = 0; i < _requests.Count; ++i)
        {
            if (ReferenceEquals(_requests[i].Owner, owner)) return i;
        }

        return -1;
    }

    private void Apply(bool animated)
    {
        EHudParts parts = EHudParts.None;
        for (int i = 0; i < _requests.Count; ++i)
        {
            parts |= _requests[i].Parts;
        }

        bool shouldHideTop = (parts & EHudParts.Top) != 0;
        bool shouldHideBottom = (parts & EHudParts.Bottom) != 0;

        // 상태가 그대로면 진행 중인 연출을 다시 시작하지 않는다.
        if (shouldHideTop != _isTopHidden || !animated)
        {
            _isTopHidden = shouldHideTop;
            Move(
                _topRoot,
                ref _topTween,
                _topStartPosition,
                Vector2.up,
                shouldHideTop,
                animated);
        }

        if (shouldHideBottom != _isBottomHidden || !animated)
        {
            _isBottomHidden = shouldHideBottom;
            Move(
                _bottomRoot,
                ref _bottomTween,
                _bottomStartPosition,
                Vector2.down,
                shouldHideBottom,
                animated);
        }
    }

    private void Move(
        RectTransform root,
        ref Tween tween,
        Vector2 startPosition,
        Vector2 direction,
        bool shouldHide,
        bool animated)
    {
        tween?.Kill();
        tween = null;

        Vector2 destination = shouldHide
            ? startPosition + direction * root.rect.height
            : startPosition;

        if (!animated)
        {
            root.anchoredPosition = destination;
            return;
        }

        tween = root.DOAnchorPos(destination, _duration);
    }

    private readonly struct HideRequest
    {
        public object Owner { get; }
        public EHudParts Parts { get; }

        public HideRequest(object owner, EHudParts parts)
        {
            Owner = owner;
            Parts = parts;
        }
    }

}
