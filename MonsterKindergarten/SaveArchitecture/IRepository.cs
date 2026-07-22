/*
 * 역할: 저장 매체와 무관하게 게임 데이터의 비동기 저장·로드 기능을 사용하는 공통 계약입니다.
 * 핵심 설계: Manager가 PlayerPrefs, Firebase 또는 Hybrid 구현체의 구체 타입에 의존하지 않게 합니다.
 */
using Cysharp.Threading.Tasks;

/// <summary>
/// ISaveData를 구현한 저장 DTO에 대한 Save·Load 추상화입니다.
/// </summary>
public interface IRepository<T> where T : ISaveData
{
    public UniTask Save(T data);
    public UniTask<T> Load();
}
