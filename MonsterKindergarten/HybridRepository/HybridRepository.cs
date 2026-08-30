using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

public class HybridRepository<T> : IRepository<T> where T : class, ISaveData
{
    private readonly IRepository<T> _playerprefsRepository;
    private readonly IRepository<T> _firebaseRepository;
    private const float FIREBASE_INTERVAL = 0.6f;
    public HybridRepository(IRepository<T> playerprefs, IRepository<T> firebase)
    {
        _playerprefsRepository = playerprefs;
        _firebaseRepository = firebase;
    }

    private CancellationTokenSource _firebaseSaveToken;

    public async UniTask Save(T saveData)
    {
        if (GameplaySaveGate.IsResetting) return;
        int resetGeneration = GameplaySaveGate.ResetGeneration;
        // 로컬 저장 - 즉시 수행
        saveData.LastSaveTime = DateTime.UtcNow.ToString("O");
        await _playerprefsRepository.Save(saveData);
        if (GameplaySaveGate.IsResetting ||
            resetGeneration != GameplaySaveGate.ResetGeneration) return;

        // 서버 저장 - 이전 0.6초간 대기 작업이 있다면 취소 요청
        if (_firebaseSaveToken != null)
        {
            _firebaseSaveToken.Cancel();
            _firebaseSaveToken.Dispose();
        }
        // 새로운 취소 토큰 생성
        _firebaseSaveToken = new CancellationTokenSource();
        // 서버 저장 실행
        SaveToFirebase(saveData, _firebaseSaveToken.Token, resetGeneration).Forget();
    }

    private async UniTaskVoid SaveToFirebase(T saveData, CancellationToken token, int resetGeneration)
    {
        try
        {
            // 0.6초간 대기 실행
            await UniTask.Delay(TimeSpan.FromSeconds(FIREBASE_INTERVAL), cancellationToken: token);
            // 취소 요청이 떨어지지 않았다면 넘어가기
            if (token.IsCancellationRequested || GameplaySaveGate.IsResetting ||
                resetGeneration != GameplaySaveGate.ResetGeneration) return;
            // 모든 분기 통과 시 서버에 저장
            await _firebaseRepository.Save(saveData);
        }
        catch (OperationCanceledException)
        {
            // _firebaseSaveToken.Cancel()이 실행되면 여기로 들어옴 (이전 요청 폐기)
        }
        catch (Exception e)
        {
            Debug.LogWarning($"파이어베이스 저장 실패 : {e.Message}");
        }
    }

    public async UniTask<T> Load()
    {
        var playerprefsTask = _playerprefsRepository.Load();
        var firebaseTask = _firebaseRepository.Load();

        var (playerprefsData, firebaseData) = await UniTask.WhenAll(playerprefsTask, firebaseTask);

        return ResolveConflict(playerprefsData, firebaseData);
    }

    private T ResolveConflict(T playerprefs, T firebase)
    {
        if (playerprefs == null && firebase == null)
        {
            return null;
        }
        if (playerprefs == null)
        {
            return firebase;
        }
        if (firebase == null)
        {
            return playerprefs;
        }

        // LastSaveTime이 null이거나 파싱 실패 시 DateTime.MinValue 사용
        DateTime playerprefsTime = ParseSaveTime(playerprefs.LastSaveTime);
        DateTime firebaseTime = ParseSaveTime(firebase.LastSaveTime);

        if (playerprefsTime > firebaseTime)
        {
            return playerprefs;
        }
        else
        {
            _playerprefsRepository.Save(firebase).Forget();
            return firebase;
        }
    }

    private DateTime ParseSaveTime(string saveTime)
    {
        if (string.IsNullOrEmpty(saveTime))
        {
            return DateTime.MinValue;
        }

        if (DateTime.TryParse(saveTime, out DateTime result))
        {
            return result;
        }

        return DateTime.MinValue;
    }
}
