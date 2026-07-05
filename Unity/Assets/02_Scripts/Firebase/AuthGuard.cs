using System;
using UnityEngine;
using Firebase.Auth;

/// <summary>
/// 로그인 상태 확인 + uid 취득 보일러플레이트 단일화 (#160).
/// 각 매니저에 반복되던
///   if (auth?.CurrentUser == null) { [Debug.LogWarning(...);] [onFailure?.Invoke("로그인 필요");] return; }
///   string uid = auth.CurrentUser.UserId;
/// 를 대체한다.
/// </summary>
public static class AuthGuard
{
    /// <summary>
    /// 로그인 상태면 uid를 out으로 반환하고 true.
    /// 아니면 context가 있을 때 "{context}: 로그인 상태 아님" 경고,
    /// onFailure가 있으면 "로그인 필요"로 호출한 뒤 false.
    /// </summary>
    public static bool TryGetUid(FirebaseAuth auth, out string uid,
                                 string context = null, Action<string> onFailure = null)
    {
        if (auth?.CurrentUser != null)
        {
            uid = auth.CurrentUser.UserId;
            return true;
        }

        if (!string.IsNullOrEmpty(context))
            Debug.LogWarning($"{context}: 로그인 상태 아님");
        onFailure?.Invoke("로그인 필요");
        uid = null;
        return false;
    }

    /// <summary>uid가 필요 없는 로그인 가드. TryGetUid와 동일 동작.</summary>
    public static bool RequireLogin(FirebaseAuth auth,
                                    string context = null, Action<string> onFailure = null)
        => TryGetUid(auth, out _, context, onFailure);
}
