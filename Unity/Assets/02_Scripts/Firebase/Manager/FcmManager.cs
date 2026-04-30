using UnityEngine;
using System;
using System.Collections.Generic;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
using Firebase.Messaging;

public class FcmManager : MonoBehaviour
{
    public static FcmManager Instance { get; private set; }

    private FirebaseAuth auth;
    private FirebaseFirestore db;
    private ListenerRegistration _notificationListener;

    // UI 연결 이벤트
    public event Action<NotificationData> OnForegroundNotification;  // 포그라운드 수신 → 인앱 배너
    public event Action<NotificationData> OnNotificationTapped;      // 백그라운드 탭 → 화면 전환
    public event Action<int> OnUnreadCountChanged;                   // 미읽음 수 → 빨간 점 / 뱃지

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        auth = FirebaseAuth.DefaultInstance;
        db = FirebaseFirestore.DefaultInstance;

        FirebaseMessaging.TokenReceived += OnTokenReceived;
        FirebaseMessaging.MessageReceived += OnMessageReceived;

        FirebaseMessaging.RequestPermissionAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
                Debug.LogWarning("FCM 권한 요청 실패: " + task.Exception);
            else
                Debug.Log("FCM 권한 요청 완료");
        });
    }

    void OnDestroy()
    {
        FirebaseMessaging.TokenReceived -= OnTokenReceived;
        FirebaseMessaging.MessageReceived -= OnMessageReceived;
        StopNotificationListener();
    }

    // ───────────────────────────────────────
    // 토큰 등록
    // ───────────────────────────────────────
    private void OnTokenReceived(object sender, TokenReceivedEventArgs e)
    {
        Debug.Log("FCM 토큰 수신: " + e.Token);
        SaveTokenToFirestore(e.Token);
    }

    private void SaveTokenToFirestore(string token)
    {
        if (auth?.CurrentUser == null)
        {
            Debug.LogWarning("SaveTokenToFirestore: 로그인 상태 아님");
            return;
        }

        string uid = auth.CurrentUser.UserId;
        string platform = Application.platform == RuntimePlatform.Android ? "android"
                        : Application.platform == RuntimePlatform.IPhonePlayer ? "ios"
                        : "editor";

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            { "Token", token },
            { "Platform", platform },
            { "UpdatedAt", FieldValue.ServerTimestamp }
        };

        db.Collection("users")
            .Document(uid)
            .Collection("fcmTokens")
            .Document(token)
            .SetAsync(data)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                    Debug.LogError("FCM 토큰 저장 실패: " + task.Exception);
                else
                    Debug.Log("FCM 토큰 Firestore 저장 완료");
            });
    }

    // 로그인 후 명시적 토큰 등록 (LoginManager에서 호출)
    public void RegisterToken()
    {
        FirebaseMessaging.GetTokenAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("FCM 토큰 가져오기 실패: " + task.Exception);
                return;
            }
            SaveTokenToFirestore(task.Result);
        });
    }

    // 로그아웃 시 이 기기 토큰 제거
    public void RemoveToken()
    {
        if (auth?.CurrentUser == null) return;

        FirebaseMessaging.GetTokenAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted) return;

            string token = task.Result;
            string uid = auth.CurrentUser.UserId;

            db.Collection("users")
                .Document(uid)
                .Collection("fcmTokens")
                .Document(token)
                .DeleteAsync()
                .ContinueWithOnMainThread(deleteTask =>
                {
                    if (deleteTask.IsFaulted)
                        Debug.LogWarning("FCM 토큰 삭제 실패: " + deleteTask.Exception);
                    else
                        Debug.Log("FCM 토큰 삭제 완료");
                });
        });
    }

    // ───────────────────────────────────────
    // 메시지 수신 처리
    // ───────────────────────────────────────
    private void OnMessageReceived(object sender, MessageReceivedEventArgs e)
    {
        FirebaseMessage msg = e.Message;

        string type = "";
        string payload = "";

        if (msg.Data != null)
        {
            msg.Data.TryGetValue("type", out type);
            msg.Data.TryGetValue("payload", out payload);
        }

        NotificationData data = new NotificationData
        {
            Type    = type ?? "",
            Title   = msg.Notification?.Title ?? "",
            Body    = msg.Notification?.Body ?? "",
            Payload = payload ?? "",
            IsRead  = false
        };

        if (msg.NotificationOpened)
        {
            Debug.Log($"[FCM] 탭으로 앱 열림: type={type}");
            OnNotificationTapped?.Invoke(data);
        }
        else
        {
            Debug.Log($"[FCM] 포그라운드 수신: type={type}");
            OnForegroundNotification?.Invoke(data);
        }

        SaveNotificationToFirestore(data);
    }
    // ───────────────────────────────────────
    // 알림 Firestore 저장 (알림 내역)
    // ───────────────────────────────────────
    private void SaveNotificationToFirestore(NotificationData data)
    {
        if (auth?.CurrentUser == null) return;

        string uid = auth.CurrentUser.UserId;

        Dictionary<string, object> doc = new Dictionary<string, object>
        {
            { "Type",      data.Type },
            { "Title",     data.Title },
            { "Body",      data.Body },
            { "Payload",   data.Payload },
            { "IsRead",    false },
            { "CreatedAt", FieldValue.ServerTimestamp }
        };

        db.Collection("users")
            .Document(uid)
            .Collection("notifications")
            .AddAsync(doc)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                    Debug.LogWarning("알림 저장 실패: " + task.Exception);
            });
    }

    // ───────────────────────────────────────
    // 알림 읽음 처리
    // ───────────────────────────────────────
    public void MarkNotificationRead(string notificationId, System.Action onSuccess = null)
    {
        if (auth?.CurrentUser == null) return;

        string uid = auth.CurrentUser.UserId;

        db.Collection("users")
            .Document(uid)
            .Collection("notifications")
            .Document(notificationId)
            .UpdateAsync("IsRead", true)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                    Debug.LogWarning("알림 읽음 처리 실패: " + task.Exception);
                else
                    onSuccess?.Invoke();
            });
    }

    // ───────────────────────────────────────
    // 알림 목록 조회 (알림 UI용)
    // ───────────────────────────────────────
    public void GetNotifications(System.Action<List<NotificationData>> onSuccess, System.Action<string> onFailure = null)
    {
        if (auth?.CurrentUser == null)
        {
            onFailure?.Invoke("로그인 필요");
            return;
        }

        string uid = auth.CurrentUser.UserId;

        db.Collection("users")
            .Document(uid)
            .Collection("notifications")
            .OrderByDescending("CreatedAt")
            .Limit(50)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("알림 목록 읽기 실패: " + task.Exception);
                    onFailure?.Invoke(task.Exception.Message);
                    return;
                }

                List<NotificationData> list = new List<NotificationData>();
                foreach (DocumentSnapshot doc in task.Result.Documents)
                {
                    NotificationData n = doc.ConvertTo<NotificationData>();
                    n.NotificationId = doc.Id;
                    list.Add(n);
                }

                onSuccess?.Invoke(list);
            });
    }

    // ───────────────────────────────────────
    // 미읽음 알림 실시간 리스너 (빨간 점 / 뱃지)
    // ───────────────────────────────────────
    public void StartNotificationListener()
    {
        if (auth?.CurrentUser == null)
        {
            Debug.LogWarning("StartNotificationListener: 로그인 상태 아님");
            return;
        }

        StopNotificationListener();
        string uid = auth.CurrentUser.UserId;

        _notificationListener = db.Collection("users")
            .Document(uid)
            .Collection("notifications")
            .WhereEqualTo("IsRead", false)
            .Listen(snapshot =>
            {
                OnUnreadCountChanged?.Invoke(snapshot.Count);
            });

        Debug.Log("알림 리스너 시작: " + uid);
    }

    public void StopNotificationListener()
    {
        if (_notificationListener != null)
        {
            _notificationListener.Stop();
            _notificationListener = null;
            Debug.Log("알림 리스너 해제");
        }
    }

    void Update()
    {
        // 로그아웃 감지 → 리스너 자동 정리
        if (_notificationListener != null && auth?.CurrentUser == null)
        {
            Debug.Log("로그아웃 감지 → 알림 리스너 자동 해제");
            StopNotificationListener();
        }
    }
}
