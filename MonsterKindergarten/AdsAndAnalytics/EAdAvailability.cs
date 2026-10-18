// 지금 이 광고를 볼 수 있는지와, 볼 수 없다면 이유다. 버튼을 켜고 끄는 쪽이 이유에 따라 문구를 고른다.
public enum EAdAvailability
{
    Available,
    // 보여 줄 광고가 아직 없다.
    NotReady,
    // 해금 전이다.
    Locked,
    // 오늘 볼 수 있는 횟수를 다 썼다.
    DailyLimitReached,
    // 부스트 남은 시간이 이미 충분해서 더 볼 수 없다.
    BoostFull,
}
