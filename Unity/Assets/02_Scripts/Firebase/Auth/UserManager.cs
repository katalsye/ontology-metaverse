using UnityEngine;
using System;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
using System.Collections.Generic;
using System.Threading.Tasks;

public class UserManager : MonoBehaviour
{
    public static UserManager Instance { get; private set; }

    private FirebaseAuth auth;
    private FirebaseFirestore db;

    public event Action<Persona> OnPersonaRestored;

    void Awake()
    {
        if (Instance != null) { Destroy(this); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        auth = FirebaseAuth.DefaultInstance;
        db = FirebaseFirestore.DefaultInstance;

        FirebaseFirestoreSettings settings = db.Settings;
        settings.PersistenceEnabled = true;
        db.Settings.PersistenceEnabled = true;
    }

    // ───────────────────────────────────────
    // 내 프로필 읽기
    // ───────────────────────────────────────
    public void GetMyProfile(Action<UserProfile> onSuccess, Action<string> onFailure = null)
    {
        if (auth?.CurrentUser == null)
        {
            Debug.LogError("GetMyProfile: 로그인 상태 아님");
            onFailure?.Invoke("로그인 상태 아님");
            return;
        }

        string uid = auth.CurrentUser.UserId;
        db.Collection("users").Document(uid).GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("프로필 읽기 실패: " + task.Exception);
                onFailure?.Invoke(task.Exception.Message);
                return;
            }

            UserProfile profile = task.Result.ConvertTo<UserProfile>();
            onSuccess?.Invoke(profile);
        });
    }

    // ───────────────────────────────────────
    // 닉네임 / 상태메시지 업데이트
    // ───────────────────────────────────────
    public void UpdateProfile(string nickname, string statusMessage, Action onSuccess = null, Action<string> onFailure = null)
    {
        if (auth?.CurrentUser == null)
        {
            Debug.LogError("UpdateProfile: 로그인 상태 아님");
            onFailure?.Invoke("로그인 상태 아님");
            return;
        }

        string uid = auth.CurrentUser.UserId;
        Dictionary<string, object> updates = new Dictionary<string, object>
        {
            { "Nickname", nickname },
            { "StatusMessage", statusMessage }
        };

        db.Collection("users").Document(uid).UpdateAsync(updates).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("프로필 업데이트 실패: " + task.Exception);
                onFailure?.Invoke(task.Exception.Message);
                return;
            }

            Debug.Log("프로필 업데이트 완료");
            onSuccess?.Invoke();
        });
    }

    // ───────────────────────────────────────
    // 페르소나 읽기
    // ───────────────────────────────────────
    public void GetPersona(Action<Persona> onSuccess, Action<string> onFailure = null)
    {
        if (auth?.CurrentUser == null)
        {
            Debug.LogError("GetPersona: 로그인 상태 아님");
            onFailure?.Invoke("로그인 상태 아님");
            return;
        }

        string uid = auth.CurrentUser.UserId;
        db.Collection("users").Document(uid).GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("페르소나 읽기 실패: " + task.Exception);
                onFailure?.Invoke(task.Exception.Message);
                return;
            }

            UserProfile profile = task.Result.ConvertTo<UserProfile>();
            onSuccess?.Invoke(profile.Persona);
        });
    }

    // ───────────────────────────────────────
    // 페르소나 쓰기 (기기 변경 시 복원용)
    // ───────────────────────────────────────
    public void UpdatePersona(Persona persona, Action onSuccess = null, Action<string> onFailure = null)
    {
        if (auth?.CurrentUser == null)
        {
            Debug.LogError("UpdatePersona: 로그인 상태 아님");
            onFailure?.Invoke("로그인 상태 아님");
            return;
        }

        string uid = auth.CurrentUser.UserId;
        Dictionary<string, object> updates = new Dictionary<string, object>
        {
            { "Persona", persona }
        };

        db.Collection("users").Document(uid).UpdateAsync(updates).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("페르소나 업데이트 실패: " + task.Exception);
                onFailure?.Invoke(task.Exception.Message);
                return;
            }

            Debug.Log("페르소나 업데이트 완료");
            onSuccess?.Invoke();
        });
    }

    // ───────────────────────────────────────
    // 다른 유저 프로필 읽기
    // ───────────────────────────────────────
    public void GetUserProfile(string uid, Action<UserProfile> onSuccess, Action<string> onFailure = null)
    {
        db.Collection("users").Document(uid).GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("유저 프로필 읽기 실패: " + task.Exception);
                onFailure?.Invoke(task.Exception.Message);
                return;
            }

            UserProfile profile = task.Result.ConvertTo<UserProfile>();

            if (!profile.IsPublic)
            {
                Debug.LogWarning("비공개 계정입니다.");
                onFailure?.Invoke("비공개 계정");
                return;
            }

            onSuccess?.Invoke(profile);
        });
    }

    // ───────────────────────────────────────
    // 팔로우 관계 기반 프로필 읽기 (비공개 계정도 허용)
    // ───────────────────────────────────────
    public void GetUserProfileForFollow(string uid, Action<UserProfile> onSuccess, Action<string> onFailure = null)
    {
        db.Collection("users").Document(uid).GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("유저 프로필 읽기 실패: " + task.Exception);
                onFailure?.Invoke(task.Exception.Message);
                return;
            }

            if (!task.Result.Exists)
            {
                onFailure?.Invoke("유저 없음");
                return;
            }

            onSuccess?.Invoke(task.Result.ConvertTo<UserProfile>());
        });
    }

    // ───────────────────────────────────────
    // 닉네임 기반 유저 검색
    // ───────────────────────────────────────
    public void SearchUserByNickname(string nickname, Action<List<UserProfile>> onSuccess, Action<string> onFailure = null)
    {
        db.Collection("users")
            .WhereEqualTo("IsPublic", true)
            .WhereEqualTo("Nickname", nickname)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("유저 검색 실패: " + task.Exception);
                    onFailure?.Invoke(task.Exception.Message);
                    return;
                }

                List<UserProfile> results = new List<UserProfile>();
                foreach (DocumentSnapshot doc in task.Result.Documents)
                {
                    results.Add(doc.ConvertTo<UserProfile>());
                }

                onSuccess?.Invoke(results);
            });
    }

    // ───────────────────────────────────────
    // 새 기기 로그인 시 페르소나 복원
    // ───────────────────────────────────────
    public void RestorePersonaOnLogin()
    {
        if (auth?.CurrentUser == null)
        {
            Debug.LogWarning("RestorePersonaOnLogin: 로그인 상태 아님");
            return;
        }

        GetPersona(
            onSuccess: persona =>
            {
                if (persona == null)
                {
                    Debug.LogWarning("RestorePersonaOnLogin: 저장된 페르소나 없음");
                    return;
                }
                Debug.Log("페르소나 복원 완료");
                OnPersonaRestored?.Invoke(persona);
            },
            onFailure: err => Debug.LogError("페르소나 복원 실패: " + err)
        );
    }

    // ───────────────────────────────────────
    // 신규 유저 문서 생성
    // ───────────────────────────────────────
    public void CreateUserIfNotExists()
    {
        FirebaseUser user = auth?.CurrentUser;
        if (user == null)
        {
            Debug.LogError("CreateUserIfNotExists: 로그인 상태 아님");
            return;
        }

        DocumentReference userDoc = db.Collection("users").Document(user.UserId);
        userDoc.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("유저 조회 실패: " + task.Exception);
                return;
            }

            if (!task.Result.Exists)
            {
                Dictionary<string, object> userData = new Dictionary<string, object>
                {
                    { "Uid", user.UserId },
                    { "Nickname", user.DisplayName ?? "새로운 유저" },
                    { "Email", user.Email ?? "" },
                    { "ProfileImageUrl", user.PhotoUrl?.ToString() ?? "" },
                    { "StatusMessage", "" },
                    { "IsPublic", true },
                    { "Persona", new Dictionary<string, object>() },
                    { "CreatedAt", FieldValue.ServerTimestamp }
                };

                userDoc.SetAsync(userData).ContinueWithOnMainThread(setTask =>
                {
                    if (setTask.IsFaulted)
                    {
                        Debug.LogError("유저 문서 생성 실패: " + setTask.Exception);
                        return;
                    }
                    Debug.Log("유저 문서 생성 완료");
                });
            }
            else
            {
                Debug.Log("이미 존재하는 유저");
            }
        });
    }
}