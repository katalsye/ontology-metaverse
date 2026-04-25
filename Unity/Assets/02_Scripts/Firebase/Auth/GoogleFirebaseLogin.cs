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
    
    private FirebaseAuth auth;
    private FirebaseUser user;
    
    private void Start()
    {
        androidLoginButton.onClick.AddListener(GoogleSignInClick);
        logoutButton.onClick.AddListener(SignOut);

        GoogleSignIn.Configuration = new GoogleSignInConfiguration()
        {
            WebClientId = "616295277126-o19i1e2jblr5otfdkh8b843olvo4rv04.apps.googleusercontent.com",
            RequestIdToken = true,
            UseGameSignIn = false,
            RequestEmail = true,
        };

        InitFirebase();
    }

    private void InitFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                auth = FirebaseAuth.DefaultInstance;
                Debug.Log("Firebase Auth initialized successfully.");

                if (auth.CurrentUser != null)
                {
                    user = auth.CurrentUser;
                    Debug.Log("자동 로그인: " + user.DisplayName);

                    userIdTMP.text = "Google UserId: " + user.UserId;
                    userNameTMP.text = "User Name: " + user.DisplayName;

                    GetComponent<UserManager>().CreateUserIfNotExists();
                }
            }
            else
            {
                Debug.LogError("Could not resolve Firebase dependencies: " + task.Result);
            }
        });
    }
    
    private void GoogleSignInClick()
    {
        try
        {
            GoogleSignIn.DefaultInstance.SignIn().ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError($"SignIn Error: {task.Exception}");
                }
                else if (task.IsCanceled)
                {
                    Debug.LogError($"SignIn Canceled: ");
                }
                else
                {
                    OnGoogleAuthenticatedFinished(task);
                }
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
                if(continuation.IsCanceled) { return; }
    
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
            });
        }
    }

    public void SignOut()
    {
        // 1. 리스너 먼저 정리 (CurrentUser가 null 되기 전에)
        StopAllManagerListeners();

        // 2. 로그아웃 처리
        GoogleSignIn.DefaultInstance.SignOut();
        auth.SignOut();
        user = null;

        userIdTMP.text = "";
        userNameTMP.text = "";

        Debug.Log("로그아웃 완료");
    }

    private void StopAllManagerListeners()
    {
        // 같은 GameObject에 붙어있는 Manager들의 리스너 정리
        var roomManager = GetComponent<RoomObjectManager>();
        if (roomManager != null) roomManager.StopRoomListener();

        var questManager = GetComponent<QuestManager>();
        if (questManager != null) questManager.StopQuestListener();

        Debug.Log("모든 리스너 정리 완료");
    }
}