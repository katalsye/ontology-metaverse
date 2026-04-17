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
    [SerializeField] private TextMeshProUGUI userIdTMP;
    [SerializeField] private TextMeshProUGUI userNameTMP;
    
    private FirebaseAuth auth;
    private FirebaseUser user;
    
    private void Start()
    {
        androidLoginButton.onClick.AddListener(GoogleSignInClick);

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
                Debug.Log($"Firebase Auth initialized successfully.");

                // 이미 로그인된 유저가 있으면 유저 문서 생성 체크
                if (auth.CurrentUser != null)
                {
                    user = auth.CurrentUser;
                    Debug.Log($"자동 로그인: {user.DisplayName}");
                    GetComponent<UserManager>().CreateUserIfNotExists();
                }
            }
            else
            {
                Debug.LogError($"Could not resolve Firebase dependencies: {task.Result}");
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
}