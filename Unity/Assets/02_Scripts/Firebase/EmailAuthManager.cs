using UnityEngine;
using Firebase.Auth;
using Firebase.Extensions;

public class EmailAuthManager : MonoBehaviour
{
    private FirebaseAuth auth;

    void Start()
    {
        auth = FirebaseAuth.DefaultInstance;
    }

    public void Register(string email, string password)
    {
        auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("회원가입 실패: " + task.Exception);
                return;
            }

            FirebaseUser user = task.Result.User;
            Debug.Log("회원가입 성공! UID: " + user.UserId);
        });
    }

    public void Login(string email, string password)
    {
        auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("로그인 실패: " + task.Exception);
                return;
            }

            FirebaseUser user = task.Result.User;
            Debug.Log("로그인 성공! UID: " + user.UserId);
        });
    }

    public void Logout()
    {
        auth.SignOut();
        Debug.Log("로그아웃 완료");
    }

    public bool IsLoggedIn()
    {
        return auth.CurrentUser != null;
    }
}