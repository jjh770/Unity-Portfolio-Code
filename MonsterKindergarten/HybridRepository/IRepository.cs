using Cysharp.Threading.Tasks;

public interface IRepository<T> where T : ISaveData
{
    public UniTask Save(T data);
    public UniTask<T> Load();
}
