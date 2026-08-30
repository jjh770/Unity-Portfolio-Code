public interface ISaveData
{
    int SchemaVersion { get; set; }
    string LastSaveTime { get; set; }
}

public static class SaveSchema
{
    // 저장 구조를 변경할 때 해당 도메인의 버전을 올리고, 이전 버전의 승격 로직을 함께 추가한다.
    // 각 저장소는 CurrentVersion보다 높은 데이터를 로드하거나 덮어쓰지 않도록 차단한다.
    // SchemaVersion 필드가 없는 기존 저장 데이터는 LegacyVersion으로 로드된다.
    public const int LegacyVersion = 0;
    public const int CurrencyCurrentVersion = 1;
    // v2: ActiveSlimes를 등급별 개수에서 SlimeInstance 목록으로 전환했다.
    public const int SlimeInstanceVersion = 2;
    // v3: 일반 슬라임 도감 등록 상태를 추가했다.
    // v4: 도감에 표시할 등급별 누적 통계를 추가했다.
    public const int SlimeCurrentVersion = 4;
    public const int UpgradeCurrentVersion = 1;
}
