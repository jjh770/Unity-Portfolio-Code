/*
 * 역할: 로컬 Repository와 Firebase Repository를 결합해 즉시 저장, 서버 디바운스와 최신 데이터 선택을 제공합니다.
 * 핵심 설계: 빈번한 클리커 데이터 변경은 로컬에 즉시 기록하고 마지막 변경만 서버에 전송합니다.
 */
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// 동일한 IRepository 계약을 구현하는 두 저장소의 정책을 조합합니다.
/// </summary>
public class HybridRepository<T> : IRepository<T> where T : class, ISaveData
{
    private readonly IRepository<T> _playerprefsRepository;
    private readonly IRepository<T> _firebaseRepository;
    // 연속 변경을 하나의 서버 쓰기로 묶기 위한 디바운스 시간입니다.
    private const float FIREBASE_INTERVAL = 0.6f;
    public HybridRepository(IRepository<T> playerprefs, IRepository<T> firebase)
    {
        _playerprefsRepository = playerprefs;
        _firebaseRepository = firebase;
    }

    // 새 저장 요청이 들어오면 이전 예약 서버 저장을 취소하기 위해 보관합니다.
    private CancellationTokenSource _firebaseSaveToken;

    /// <summary>
    /// UTC 저장 시각을 갱신하고 로컬 저장을 완료한 뒤 서버 저장을 예약합니다.
    /// </summary>
    public async UniTask Save(T saveData)
    {
        // 로컬 저장 - 즉시 수행
        saveData.LastSaveTime = DateTime.UtcNow.ToString("O");
        await _playerprefsRepository.Save(saveData);

        // 서버 저장 - 이전 0.6초간 대기 작업이 있다면 취소 요청
        if (_firebaseSaveToken != null)
        {
            _firebaseSaveToken.Cancel();
            _firebaseSaveToken.Dispose();
        }
        // 새로운 취소 토큰 생성
        _firebaseSaveToken = new CancellationTokenSource();
        // 서버 저장 실행
        SaveToFirebase(saveData, _firebaseSaveToken.Token).Forget();
    }

    /// <summary>
    /// 디바운스 시간 동안 추가 요청이 없을 때만 Firebase 저장을 실행합니다.
    /// </summary>
    private async UniTaskVoid SaveToFirebase(T saveData, CancellationToken token)
    {
        try
        {
            // 0.6초간 대기 실행
            await UniTask.Delay(TimeSpan.FromSeconds(FIREBASE_INTERVAL), cancellationToken: token);
            // 취소 요청이 떨어지지 않았다면 넘어가기
            if (token.IsCancellationRequested) return;
            // 모든 분기 통과 시 서버에 저장
            await _firebaseRepository.Save(saveData);
        }
        catch (OperationCanceledException)
        {
            // _firebaseSaveToken.Cancel()이 실행되면 여기로 들어옴 (이전 요청 폐기)
        }
        catch (Exception e)
        {
            Debug.Log($"파이어베이스 저장 실패 : {e.Message}");
        }
    }


    /// <summary>
    /// 로컬과 서버를 병렬로 읽어 전체 로드 시간을 줄입니다.
    /// </summary>
    public async UniTask<T> Load()
    {
        var playerprefsTask = _playerprefsRepository.Load();
        var firebaseTask = _firebaseRepository.Load();

        var (playerprefsData, firebaseData) = await UniTask.WhenAll(playerprefsTask, firebaseTask);

        return ResolveConflict(playerprefsData, firebaseData);
    }

    /// <summary>
    /// 두 데이터의 LastSaveTime을 비교해 최신 버전을 선택합니다.
    /// Firebase 데이터가 더 최신이면 로컬 저장소도 해당 데이터로 갱신합니다.
    /// </summary>
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

        if (playerprefsTime >= firebaseTime)
        {
            return playerprefs;
        }
        else
        {
            _playerprefsRepository.Save(firebase).Forget();
            return firebase;
        }
    }

    /// <summary>
    /// 누락되거나 손상된 저장 시각을 최소값으로 처리해 비교 로직을 안전하게 유지합니다.
    /// </summary>
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
