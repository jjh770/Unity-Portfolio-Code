using System;
using UnityEngine;

// 클라우드 저장이 막힌 세션을 표시한다.
//
// SaveDataLoadGuard와 같은 모양이지만 세션을 막지 않는다. 읽기 실패는 확인하지 못한
// 원본을 첫 저장이 덮어쓸 위험이라 진입을 막아야 하지만, 쓰기 실패는 아무것도 파괴하지
// 않는다. PlayerPrefs 저장은 계속 성공하고 게임도 정상이다. 클라우드만 조용히 낡아갈
// 뿐이므로, 알리되 플레이는 이어간다.
//
// 여기까지 올라온 예외는 이미 영구 실패다. 네트워크가 끊긴 동안의 쓰기는 Firestore
// SDK가 로컬 큐에 담아 두었다가 연결되면 밀어 올리며, C# 쪽으로 예외를 던지지 않는다.
// 2026-09-07 실기기에서 비행기 모드로 확인했다. 그래서 실패 횟수를 세거나 임계값을
// 두지 않고, 한 번이라도 올라오면 곧바로 실패로 본다.
//
// 리포지토리가 셋이라 각자 표시하면 같은 경고가 세 번 뜬다. 신고는 여기로 모은다.
public static class CloudSaveGuard
{
    public static bool HasFailed { get; private set; }
    public static string FailureMessage { get; private set; }

    public static void Report(string context, Exception exception)
    {
        // 같은 실패가 초당 한 번꼴로 반복된다. 매번 찍으면 진짜 원인이 로그에 묻힌다.
        if (HasFailed) return;

        HasFailed = true;
        FailureMessage = exception?.Message ?? string.Empty;
        Debug.LogError(
            $"[CloudSaveGuard] 클라우드 저장에 실패했습니다. : {context} : {FailureMessage}");
    }

    // 저장이 다시 성공하면 스스로 풀린다.
    //
    // 이게 없으면 알림이 영영 멎지 않는다. 규칙을 고쳐 배포하거나 인증이 되살아나면
    // 저장은 곧바로 재개되는데, 게임은 여전히 실패 상태로 알고 계속 경고하게 된다.
    //
    // 성공은 매 저장마다 들어오므로 상태가 바뀔 때만 일한다.
    public static void ReportSuccess()
    {
        if (!HasFailed) return;

        HasFailed = false;
        FailureMessage = null;
        Debug.Log("[CloudSaveGuard] 클라우드 저장이 복구되었습니다.");
    }

    // 로그인 화면에서만 호출한다. 계정이 바뀌면 이전 세션의 실패는 의미가 없다.
    public static void Clear()
    {
        HasFailed = false;
        FailureMessage = null;
    }
}
