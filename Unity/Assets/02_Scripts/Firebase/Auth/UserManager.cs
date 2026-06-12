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

        FirebaseBootstrap.RunWhenReady(Init);
    }

    private void Init()
    {
        auth = FirebaseAuth.DefaultInstance;
        db = FirebaseFirestore.DefaultInstance;
    }

    // ───────────────────────────────────────
    // [STUB] GetUserProfileForFollow — UI에서 호출하지만 구현 누락
    // TODO: 서윤님 영역 확정 후 정식 구현 (GetMyProfile과 유사 패턴 가능)
    // ───────────────────────────────────────
    public void GetUserProfileForFollow(string userId, System.Action<UserProfile> onSuccess, System.Action<string> onFailure = null)
    {
        Debug.LogWarning($"[UserManager] GetUserProfileForFollow stub 호출 (userId={userId}) — 추후 서윤님 구현");
        // 간이 구현: users/{userId} 문서를 그대로 반환
        if (db == null) { onFailure?.Invoke("Firestore not ready"); return; }
        db.Collection("users").Document(userId).GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || !task.Result.Exists)
            {
                onFailure?.Invoke(task.Exception?.Message ?? "user not found");
                return;
            }
            onSuccess?.Invoke(task.Result.ConvertTo<UserProfile>());
        });
    }

    // ───────────────────────────────────────
    // 내 프로필 읽기
    // ───────────────────────────────────────
    public void GetMyProfile(System.Action<UserProfile> onSuccess, System.Action<string> onFailure = null)
    {
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
    public void UpdateProfile(string nickname, string statusMessage, System.Action onSuccess = null, System.Action<string> onFailure = null)
    {
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
    public void GetPersona(System.Action<Persona> onSuccess, System.Action<string> onFailure = null)
    {
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
    public void UpdatePersona(Persona persona, System.Action onSuccess = null, System.Action<string> onFailure = null)
    {
        string uid = auth.CurrentUser.UserId;
        // ontology_engine.py와 동일한 소문자 "persona" 필드에 기록 (필드 분기 방지)
        Dictionary<string, object> updates = new Dictionary<string, object>
        {
            { "persona", persona }
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
    public void GetUserProfile(string uid, System.Action<UserProfile> onSuccess, System.Action<string> onFailure = null)
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
    // 닉네임 기반 유저 검색
    // ───────────────────────────────────────
    public void SearchUserByNickname(string nickname, System.Action<List<UserProfile>> onSuccess, System.Action<string> onFailure = null)
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
    //   GetPersona로 읽은 뒤 OnPersonaRestored 이벤트 발행
    //   로그인 완료 콜백에서 호출할 것
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
    // 신규 유저 문서 생성 (1단계에서 이어짐)
    // ───────────────────────────────────────
    public void CreateUserIfNotExists()
    {
        FirebaseUser user = auth.CurrentUser;
        if (user == null)
        {
            Debug.LogError("로그인되지 않은 상태");
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
                    { "persona", new Dictionary<string, object>() },
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