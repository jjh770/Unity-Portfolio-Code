using UnityEngine;

// 보상형 광고의 보상 수치다. 기획서 §21.2의 초안 값이며 플레이 테스트로 조정한다.
[CreateAssetMenu(fileName = "AdRewardTable", menuName = "Monster Kindergarten/Ad Reward Table")]
public sealed class AdRewardTableSO : ScriptableObject
{
    [Header("Offline Double")]
    [Tooltip("오프라인 보상 포인트에 곱하는 배율입니다. 오프라인 뽑기권에는 곱하지 않습니다.")]
    [SerializeField, Min(1f)] private float _offlineRewardMultiplier = 2f;

    [Header("Point Boost")]
    [Tooltip("부스트 동안 수동 터치와 자동 생산 포인트에 곱하는 배율입니다.")]
    [SerializeField, Min(1f)] private float _pointBoostMultiplier = 2f;
    [Tooltip("광고 한 번으로 늘어나는 부스트 시간(초)입니다.")]
    [SerializeField, Min(1f)] private float _pointBoostSecondsPerAd = 600f;
    [Tooltip("부스트 남은 시간(초)의 상한입니다. 광고를 보면 이 값을 넘게 되는 상태에서는 볼 수 없습니다.")]
    [SerializeField, Min(1f)] private float _pointBoostMaxRemainingSeconds = 540f;
    [Tooltip("하루에 부스트 광고를 보상까지 볼 수 있는 횟수입니다.")]
    [SerializeField, Min(1)] private int _pointBoostDailyLimit = 6;

    [Header("Ticket")]
    [Tooltip("광고 한 번으로 받는 뽑기권 장수입니다.")]
    [SerializeField, Min(1)] private int _ticketReward = 1;
    [Tooltip("하루에 뽑기권 광고를 보상까지 볼 수 있는 횟수입니다.")]
    [SerializeField, Min(1)] private int _ticketDailyLimit = 3;

    [Header("Ad Unit")]
    [Tooltip("개발 빌드(Development Build)가 쓰는 보상형 광고 단위 ID입니다. 값은 Google이 공개한 테스트 단위입니다.")]
    [SerializeField] private string _rewardedAdUnitIdTest = "ca-app-pub-3940256099942544/5224354917";
    [Tooltip("릴리스 빌드가 쓰는 실제 보상형 광고 단위 ID입니다. 비어 있으면 릴리스 빌드에서는 광고가 없는 것으로 다룹니다.")]
    [SerializeField] private string _rewardedAdUnitIdRelease = string.Empty;

    public float OfflineRewardMultiplier => _offlineRewardMultiplier;
    public float PointBoostMultiplier => _pointBoostMultiplier;
    public float PointBoostSecondsPerAd => _pointBoostSecondsPerAd;
    public float PointBoostMaxRemainingSeconds => _pointBoostMaxRemainingSeconds;
    public int PointBoostDailyLimit => _pointBoostDailyLimit;
    public int TicketReward => _ticketReward;
    public int TicketDailyLimit => _ticketDailyLimit;

    // 개발 빌드는 테스트 단위, 릴리스 빌드는 실제 단위다. 릴리스에서 테스트 광고가 나가지 않게 하려는 것이다.
    public string RewardedAdUnitId => Debug.isDebugBuild ? _rewardedAdUnitIdTest : _rewardedAdUnitIdRelease;

    private void OnValidate()
    {
        // 한 번에 늘어나는 시간이 상한보다 크면 부스트 광고를 볼 수 있는 때가 없다.
        _pointBoostMaxRemainingSeconds = Mathf.Max(_pointBoostMaxRemainingSeconds, _pointBoostSecondsPerAd);
    }
}
