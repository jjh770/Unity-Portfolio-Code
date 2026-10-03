using Cysharp.Threading.Tasks;

public interface IRepository<T> where T : class, ISaveData
{
    public UniTask Save(T data);

    // 미뤄 둔 쓰기가 있으면 기다리지 않고 지금 내보낸다.
    //
    // 앱이 백그라운드로 가거나 종료될 때 부른다. 즉시 저장하는 구현은 할 일이
    // 없고, 간격을 두고 모아 쓰는 구현만 실제로 무언가 한다.
    public void FlushPendingSave();

    // 읽기에 실패하면 기본값이 아니라 실패 결과를 돌려준다.
    // 실패를 기본값으로 바꾸면 호출부가 신규 계정과 구분할 수 없고,
    // 그 뒤 첫 저장이 남아 있던 데이터를 덮어쓴다.
    public UniTask<SaveLoadResult<T>> Load();
}
