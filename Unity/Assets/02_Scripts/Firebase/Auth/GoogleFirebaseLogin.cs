using System;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using Google;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GoogleFirebaseLogin : MonoBehaviour
{
    [SerializeField] private Button androidLoginButton;
    [SerializeField] private Button logoutButton;
    [SerializeField] private TextMeshProUGUI userIdTMP;
    [SerializeField] private TextMeshProUGUI userNameTMP;
    [SerializeField] private AuthConfig authConfig;
    
    private FirebaseAuth auth;
    private FirebaseUser user;
    private bool _initialized = false;

    private void OnEnable()
    {
        if (_initialized) return;

        if (authConfig == null)
        {
            Debug.LogError("AuthConfig가 연결되지 않았습니다. 인스펙터에서 AuthConfig.asset을 드래그하세요.");
            return;
        }

        androidLoginButton.interactable = false;
        androidLoginButton.onClick.AddListener(GoogleSignInClick);
        logoutButton.onClick.AddListener(SignOut);

        try
        {
            GoogleSignIn.Configuration = new GoogleSignInConfiguration()
            {
                WebClientId = authConfig.webClientId,
                RequestIdToken = true,
                UseGameSignIn = false,
                RequestEmail = true,
            };
            Debug.Log("[Login] GoogleSignIn Configuration 완료");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Login] GoogleSignIn Configuration 실패: {e.Message}");
            return;
        }

        FirebaseBootstrap.RunWhenReady(OnFirebaseReady);
        _initialized = true;
    }

    private void OnFirebaseReady()
    {
        auth = FirebaseAuth.DefaultInstance;
        Debug.Log("[Login] Firebase Auth 초기화 완료");

        androidLoginButton.interactable = true;

        if (auth.CurrentUser == null) return;

        user = auth.CurrentUser;
        Debug.Log("자동 로그인: " + user.DisplayName);

        userIdTMP.text = "Google UserId: " + user.UserId;
        userNameTMP.text = "User Name: " + user.DisplayName;

        GetComponent<UserManager>().CreateUserIfNotExists();
        GetComponent<UserManager>().RestorePersonaOnLogin();
        FcmManager.Instance?.RegisterToken();
        FcmManager.Instance?.StartNotificationListener();
    }
    
    private void GoogleSignInClick()
    {
        if (auth == null)
        {
            Debug.LogError("[Login] auth가 null입니다. Firebase 초기화 대기 중.");
            return;
        }

        if (GoogleSignIn.Configuration == null)
        {
            Debug.LogError("[Login] GoogleSignIn.Configuration이 null입니다.");
            return;
        }

        try
        {
            GoogleSignIn.DefaultInstance.SignIn().ContinueWith(task =>
            {
                if (task.IsFaulted)
                    Debug.LogError($"SignIn Error: {task.Exception}");
                else if (task.IsCanceled)
                    Debug.LogError("SignIn Canceled");
                else
                    OnGoogleAuthenticatedFinished(task);
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"GoogleSignInClick Exception: {e.Message}");
        }
    }
    
    private void OnGoogleAuthenticatedFinished(Task<GoogleSignInUser> task)
    {
        if (task.IsFaulted)
        {
            Debug.LogError("Faulted");
        }
        else if (task.IsCanceled)
        {
            Debug.LogError("Cancelled");
        }
        else
        {
            Credential credential = GoogleAuthProvider.GetCredential(task.Result.IdToken, null);
    
            auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(continuation =>
            {
                if (continuation.IsCanceled) return;
    
                if (continuation.IsFaulted)
                {
                    Debug.LogError($"SignInWithCredentialAsync encountered an error: {continuation.Exception}");
                    return;
                }
    
                user = auth.CurrentUser;

                Debug.Log($"UserName: {user.DisplayName}");
                Debug.Log($"UserEmail: {user.Email}");

                userIdTMP.text = $"Google UserId: {user.UserId}";
                userNameTMP.text = $"User Name: {user.DisplayName}";

                GetComponent<UserManager>().CreateUserIfNotExists();
                GetComponent<UserManager>().RestorePersonaOnLogin();
                FcmManager.Instance?.RegisterToken();
                FcmManager.Instance?.StartNotificationListener();
            });
        }
    }

    public void SignOut()
    {
        StopAllManagerListeners();

        GoogleSignIn.DefaultInstance.SignOut();
        auth.SignOut();
        user = null;

        userIdTMP.text = "";
        userNameTMP.text = "";

        Debug.Log("로그아웃 완료");
    }

    private void StopAllManagerListeners()
    {
        var roomManager = GetComponent<RoomObjectManager>();
        if (roomManager != null) roomManager.StopRoomListener();

        var questManager = GetComponent<QuestManager>();
        if (questManager != null) questManager.StopQuestListener();

        FcmManager.Instance?.RemoveToken();
        FcmManager.Instance?.StopNotificationListener();

        Debug.Log("모든 리스너 정리 완료");
    }
}