using UnityEngine;
using Firebase.Auth;
using Firebase.Extensions;
using Google;
using System;

public class AuthService : MonoBehaviour
{
    public static AuthService Instance { get; private set; }

    [SerializeField] private AuthConfig authConfig;

    private FirebaseAuth auth;

    public FirebaseUser CurrentUser => auth?.CurrentUser;
    public bool IsLoggedIn => auth?.CurrentUser != null;

    void Awake()
    {
        if (Instance != null) { Destroy(this); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        auth = FirebaseAuth.DefaultInstance;

        if (authConfig == null)
        {
            Debug.LogError("[AuthService] AuthConfig가 연결되지 않았습니다. 인스펙터에서 AuthConfig.asset을 드래그하세요.");
            return;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        GoogleSignIn.Configuration = new GoogleSignInConfiguration
        {
            WebClientId = authConfig.webClientId,
            RequestIdToken = true,
            UseGameSignIn = false,
            RequestEmail = true,
        };
#endif
        Debug.Log("[AuthService] 초기화 완료");
    }

    // ───────────────────────────────────────
    // Google 로그인
    // ───────────────────────────────────────
    public void SignInWithGoogle(Action onSuccess, Action<string> onFailure = null)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        GoogleSignIn.DefaultInstance.SignIn().ContinueWith(task =>
        {
            if (task.IsCanceled)
            {
                onFailure?.Invoke("로그인 취소됨");
                return;
            }
            if (task.IsFaulted)
            {
                Debug.LogError($"[AuthService] Google 로그인 실패: {task.Exception}");
                onFailure?.Invoke(task.Exception?.Message ?? "Google 로그인 실패");
                return;
            }

            Credential credential = GoogleAuthProvider.GetCredential(task.Result.IdToken, null);
            auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(continuation =>
            {
                if (continuation.IsCanceled || continuation.IsFaulted)
                {
                    string err = continuation.Exception?.Message ?? "Firebase 인증 실패";
                    Debug.LogError($"[AuthService] Firebase 인증 실패: {err}");
                    onFailure?.Invoke(err);
                    return;
                }

                Debug.Log($"[AuthService] 로그인 완료: {auth.CurrentUser.DisplayName}");
                UserManager.Instance.CreateUserIfNotExists();
                onSuccess?.Invoke();
            });
        });
#else
        Debug.LogWarning("[AuthService] Google 로그인은 Android 빌드에서만 작동합니다. 에디터에서는 로그인 단계를 스킵합니다.");
        UserManager.Instance.CreateUserIfNotExists();
        onSuccess?.Invoke();
#endif
    }

    // ───────────────────────────────────────
    // 로그아웃
    // ───────────────────────────────────────
    public void SignOut(Action onComplete = null)
    {
        QuestManager.Instance?.StopQuestListener();
#if UNITY_ANDROID && !UNITY_EDITOR
        GoogleSignIn.DefaultInstance.SignOut();
#endif
        auth.SignOut();
        Debug.Log("[AuthService] 로그아웃 완료");
        onComplete?.Invoke();
    }

    // ───────────────────────────────────────
    // 계정 삭제
    // ───────────────────────────────────────
    public void DeleteAccount(Action onSuccess = null, Action<string> onFailure = null)
    {
        if (auth.CurrentUser == null)
        {
            onFailure?.Invoke("로그인 상태 아님");
            return;
        }

        QuestManager.Instance?.StopQuestListener();

        auth.CurrentUser.DeleteAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError($"[AuthService] 계정 삭제 실패: {task.Exception}");
                onFailure?.Invoke(task.Exception?.Message ?? "계정 삭제 실패");
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            GoogleSignIn.DefaultInstance.SignOut();
#endif
            Debug.Log("[AuthService] 계정 삭제 완료");
            onSuccess?.Invoke();
        });
    }
}
