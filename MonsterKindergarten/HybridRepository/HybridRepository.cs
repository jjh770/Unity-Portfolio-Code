using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

public class HybridRepository<T> : IRepository<T> where T : class, ISaveData
{
    private readonly IRepository<T> _playerprefsRepository;
    private readonly IRepository<T> _firebaseRepository;
    // 클라우드 쓰기를 이 간격에 한 번으로 제한한다.
    //
    // 재화는 클릭과 자동 생산마다 바뀌어 저장 요청이 초당 몇 번씩 들어온다.
    // 그대로 내보내면 무료 플랜의 하루 문서 쓰기 한도에 금방 닿는다. 한도는
    // 계정이 아니라 프로젝트 전체 기준이라 테스터가 몇 명만 붙어도 넘는다.
    //
    // 로컬 저장은 즉시라서 이 지연이 화면에 보이지 않는다. 뒤처지는 것은
    // 클라우드 사본뿐이고, 그 차이는 재설치나 기기 변경 때만 드러난다.
    private const float FIREBASE_INTERVAL = 5f;
    public HybridRepository(IRepository<T> playerprefs, IRepository<T> firebase)
    {
        _playerprefsRepository = playerprefs;
        _firebaseRepository = firebase;
    }

    private T _pendingSaveData;
    private bool _isFirebaseSaveScheduled;
    private int _scheduleGeneration;

    public async UniTask Save(T saveData)
    {
        if (GameplaySaveGate.IsResetting) return;
        int resetGeneration = GameplaySaveGate.ResetGeneration;
        // 로컬 저장 - 즉시 수행
        saveData.LastSaveTime = ServerClock.TrustedUtcNow.ToString("O");
        // 로컬이 실패해도 클라우드 저장은 계속한다.
        //
        // 여기서 예외가 새어 나가면 아래 예약까지 건너뛰어 어디에도 남지 않는다.
        // 클라우드가 더 오래 남는 사본이므로 한쪽이 실패했다고 다른 쪽을 포기하지 않는다.
        try
        {
            await _playerprefsRepository.Save(saveData);
        }
        catch (Exception e)
        {
            Debug.LogError($"로컬 저장 실패 : {typeof(T).Name} : {e.Message}");
        }

        if (GameplaySaveGate.IsResetting ||
            resetGeneration != GameplaySaveGate.ResetGeneration) return;

        // 예약이 이미 있으면 실을 내용만 최신으로 바꾼다.
        //
        // 예전에는 저장할 때마다 대기 중이던 쓰기를 취소하고 타이머를 처음부터 다시
        // 돌렸다. 그러면 "요청이 간격만큼 끊긴 순간"에만 올라가는데, 슬라임이 늘어
        // 재화가 쉼 없이 오르면 그런 순간이 오지 않는다. 간격을 늘릴수록 쓰기가 주는
        // 것이 아니라 아예 멈춘다. 그래서 간격마다 한 번은 내보내는 방식으로 바꿨다.
        _pendingSaveData = saveData;
        if (_isFirebaseSaveScheduled) return;

        _isFirebaseSaveScheduled = true;
        SaveToFirebase(resetGeneration, ++_scheduleGeneration).Forget();
    }

    // 앱이 내려가기 전에 미뤄 둔 쓰기를 지금 내보낸다.
    //
    // 네트워크 왕복을 기다리지 않는다. Firestore SDK가 변경을 로컬 큐에 적어 두고
    // 다음 실행이나 연결 복구 때 올리므로, 발사만 해 두면 프로세스가 죽어도 남는다.
    //
    // 로컬 저장은 이미 즉시 끝나 있으므로 같은 기기에서는 이것이 없어도 잃지 않는다.
    // 차이가 나는 것은 재설치나 기기 변경 때뿐이다.
    public void FlushPendingSave()
    {
        if (!_isFirebaseSaveScheduled) return;

        // 대기 중인 예약을 무효로 만들어 같은 내용이 두 번 올라가지 않게 한다.
        _scheduleGeneration++;
        _isFirebaseSaveScheduled = false;
        SendPendingSave(GameplaySaveGate.ResetGeneration).Forget();
    }

    // 예약해 둔 쓰기를 간격이 지난 뒤 내보낸다.
    //
    // 그 사이 들어온 저장은 _pendingSaveData만 갈아치우므로 마지막 상태가 올라간다.
    // 문서 전체를 덮어쓰는 방식이라 중간 값을 건너뛰어도 잃는 것이 없다.
    private async UniTaskVoid SaveToFirebase(int resetGeneration, int scheduleGeneration)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(FIREBASE_INTERVAL));

        // 기다리는 동안 FlushPendingSave가 먼저 보냈으면 여기서 물러난다.
        if (scheduleGeneration != _scheduleGeneration) return;

        _isFirebaseSaveScheduled = false;
        await SendPendingSave(resetGeneration);
    }

    private async UniTask SendPendingSave(int resetGeneration)
    {
        try
        {
            // 초기화가 시작됐으면 이 쓰기는 폐기한다.
            if (GameplaySaveGate.IsResetting ||
                resetGeneration != GameplaySaveGate.ResetGeneration) return;

            await _firebaseRepository.Save(_pendingSaveData);

            // 한 번이라도 성공하면 이전 실패는 끝났다. 문서 전체를 덮어쓰므로
            // 그동안 올라가지 못한 값도 이 저장으로 한꺼번에 맞춰진다.
            CloudSaveGuard.ReportSuccess();
        }
        catch (Exception e)
        {
            // 여기 오는 예외는 일시적인 연결 문제가 아니다. 그건 SDK가 큐에 담아
            // 재시도하며 예외를 내보내지 않는다. 남는 것은 규칙 거부처럼 재시도해도
            // 성공하지 않는 실패뿐이라, 세는 대신 곧바로 신고한다.
            CloudSaveGuard.Report(typeof(T).Name, e);
        }
    }


    public async UniTask<SaveLoadResult<T>> Load()
    {
        var playerprefsTask = _playerprefsRepository.Load();
        var firebaseTask = _firebaseRepository.Load();

        var (playerprefsResult, firebaseResult) = await UniTask.WhenAll(playerprefsTask, firebaseTask);

        return ResolveConflict(playerprefsResult, firebaseResult);
    }

    // 어느 쪽이든 읽지 못했으면 진행하지 않는다.
    //
    // 읽지 못한 저장소에 무엇이 들어 있는지 알 수 없는데 게임을 시작하면,
    // 첫 저장이 확인하지 못한 원본을 덮어써 복구할 수 없게 만든다.
    // 로그인이 이미 네트워크를 요구하므로 클라우드 실패는 드문 상태다.
    private SaveLoadResult<T> ResolveConflict(
        SaveLoadResult<T> playerprefs,
        SaveLoadResult<T> firebase)
    {
        // 로컬에 현재 앱보다 높은 버전이 있으면 앱 업데이트가 먼저다.
        // 클라우드의 낮은 버전을 채택하면 이 기기의 최신 진행도를 되돌린다.
        if (playerprefs.IsFailed &&
            playerprefs.Failure == ESaveLoadFailure.UnsupportedVersion)
        {
            return playerprefs;
        }

        if (firebase.IsFailed)
        {
            return firebase;
        }

        if (playerprefs.IsFailed)
        {
            // 로컬이 손상돼도 클라우드를 읽었다면 그것으로 복구한다.
            if (firebase.IsLoaded)
            {
                return AdoptFirebase(firebase);
            }

            // 클라우드가 비어 있으면 복구할 원본이 없다. 새 게임으로 덮지 않는다.
            return playerprefs;
        }

        if (playerprefs.IsNotFound && firebase.IsNotFound)
        {
            return SaveLoadResult<T>.NotFound();
        }

        if (playerprefs.IsNotFound)
        {
            return AdoptFirebase(firebase);
        }

        if (firebase.IsNotFound)
        {
            return playerprefs;
        }

        // LastSaveTime이 null이거나 파싱 실패 시 DateTime.MinValue 사용
        DateTime playerprefsTime = ParseSaveTime(playerprefs.Data.LastSaveTime);
        DateTime firebaseTime = ParseSaveTime(firebase.Data.LastSaveTime);

        if (playerprefsTime > firebaseTime)
        {
            return playerprefs;
        }

        return AdoptFirebase(firebase);
    }

    // 클라우드를 채택하면 로컬 사본도 같은 내용으로 맞춘다.
    //
    // 미러 쓰기는 로드 결과가 아니라 로컬 캐시 갱신이다. 여기서 예외가 새어 나가면
    // 로드 전체가 중단돼 호출부가 실패 결과조차 받지 못하고 초기화가 멈춘다.
    // 저장소 구현이 동기라 예외는 Forget()에 닿기 전에 호출 지점에서 터진다.
    private SaveLoadResult<T> AdoptFirebase(SaveLoadResult<T> firebase)
    {
        try
        {
            _playerprefsRepository.Save(firebase.Data).Forget();
        }
        catch (Exception e)
        {
            // 채택한 값은 그대로 돌려준다. 해석할 수 없는 내용이면 호출부가 판정한다.
            Debug.LogWarning($"로컬 사본 갱신 실패 : {e.Message}");
        }

        return firebase;
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
