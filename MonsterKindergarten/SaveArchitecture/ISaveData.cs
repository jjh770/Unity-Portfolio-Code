/*
 * 역할: 로컬·서버 데이터 충돌 해결에 필요한 마지막 저장 시각을 공통 저장 DTO에 요구합니다.
 */
/// <summary>
/// HybridRepository가 저장본의 최신 여부를 비교하기 위한 공통 계약입니다.
/// </summary>
public interface ISaveData
{
    string LastSaveTime { get; set; }
}
