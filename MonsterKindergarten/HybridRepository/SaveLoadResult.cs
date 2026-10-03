using System;

// 저장 데이터를 읽은 결과.
//
// "읽기 실패"와 "저장된 데이터 없음"을 반드시 구분한다. 둘을 같은 값으로 뭉개면
// 읽기에 실패한 세션이 신규 계정으로 시작하고, 그 뒤 첫 저장이 남아 있던 원본을
// 덮어써 복구할 수 없다. 실패는 실패로 올려 보내고 판단은 호출부가 한다.
public enum ESaveLoadStatus
{
    // 저장된 데이터를 읽었다.
    Loaded,
    // 저장된 데이터가 없다. 신규 계정이므로 기본값으로 시작해도 된다.
    NotFound,
    // 읽지 못했다. 저장된 데이터의 유무를 알 수 없으므로 덮어쓰면 안 된다.
    Failed,
}

// 실패 원인. 안내 문구와 복구 방법이 달라서 구분한다.
public enum ESaveLoadFailure
{
    None,
    // 데이터가 있으나 해석할 수 없다.
    Unreadable,
    // 현재 앱이 지원하는 것보다 높은 저장 버전이다.
    UnsupportedVersion,
    // 저장소에 닿지 못했다.
    Unreachable,
    // 저장은 읽었지만 그 뒤의 초기화 코드가 예외를 던졌다. 저장된 진행도는 멀쩡하므로
    // 초기화 수단을 열지 않는다. 앱의 결함이라 재시도가 답이다.
    InitializationFailed,
}

public readonly struct SaveLoadResult<T> where T : class, ISaveData
{
    public ESaveLoadStatus Status { get; }
    public ESaveLoadFailure Failure { get; }
    public string FailureMessage { get; }

    // Loaded일 때만 값이 있다. 다른 상태에서 접근하면 예외를 던져
    // 상태 확인을 건너뛴 호출부를 개발 중에 드러낸다.
    public T Data
    {
        get
        {
            if (Status != ESaveLoadStatus.Loaded)
            {
                throw new InvalidOperationException(
                    $"읽은 데이터가 없는 결과입니다. : {Status}");
            }

            return _data;
        }
    }

    private readonly T _data;

    public bool IsLoaded => Status == ESaveLoadStatus.Loaded;
    public bool IsNotFound => Status == ESaveLoadStatus.NotFound;
    public bool IsFailed => Status == ESaveLoadStatus.Failed;

    private SaveLoadResult(
        ESaveLoadStatus status,
        T data,
        ESaveLoadFailure failure,
        string failureMessage)
    {
        Status = status;
        _data = data;
        Failure = failure;
        FailureMessage = failureMessage;
    }

    public static SaveLoadResult<T> Loaded(T data)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        return new SaveLoadResult<T>(
            ESaveLoadStatus.Loaded,
            data,
            ESaveLoadFailure.None,
            null);
    }

    public static SaveLoadResult<T> NotFound()
    {
        return new SaveLoadResult<T>(
            ESaveLoadStatus.NotFound,
            null,
            ESaveLoadFailure.None,
            null);
    }

    public static SaveLoadResult<T> Failed(
        ESaveLoadFailure failure,
        string failureMessage)
    {
        if (failure == ESaveLoadFailure.None)
        {
            throw new ArgumentException(
                "실패 결과에는 원인이 필요합니다.", nameof(failure));
        }

        return new SaveLoadResult<T>(
            ESaveLoadStatus.Failed,
            null,
            failure,
            failureMessage);
    }
}
