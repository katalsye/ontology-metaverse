using UnityEngine;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
using System.Collections.Generic;

public class RoomSnapshotManager : MonoBehaviour
{
    public static RoomSnapshotManager Instance { get; private set; }

    private FirebaseAuth auth;
    private FirebaseFirestore db;

    void Awake()
    {
        Instance = this;
        FirebaseBootstrap.RunWhenReady(Init);
    }

    private void Init()
    {
        auth = FirebaseAuth.DefaultInstance;
        db = FirebaseFirestore.DefaultInstance;
    }

    // ───────────────────────────────────────
    // 내 스냅샷 날짜별 조회
    // ───────────────────────────────────────
    public void GetSnapshotsByMonth(string yearMonth, System.Action<List<RoomSnapshot>> onSuccess, System.Action<string> onFailure = null)
    {
        string uid = auth.CurrentUser.UserId;
        string startDate = yearMonth + "-01";
        string endDate = yearMonth + "-31";

        db.Collection("room_snapshots")
            .Document(uid)
            .Collection("snapshots")
            .WhereGreaterThanOrEqualTo("Date", startDate)
            .WhereLessThanOrEqualTo("Date", endDate)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("스냅샷 목록 읽기 실패: " + task.Exception);
                    onFailure?.Invoke(task.Exception.Message);
                    return;
                }

                List<RoomSnapshot> snapshots = new List<RoomSnapshot>();
                foreach (DocumentSnapshot doc in task.Result.Documents)
                {
                    snapshots.Add(doc.ConvertTo<RoomSnapshot>());
                }

                onSuccess?.Invoke(snapshots);
            });
    }

    // ───────────────────────────────────────
    // 특정 날짜 스냅샷 단건 읽기
    // ───────────────────────────────────────
    public void GetSnapshotByDate(string date, System.Action<RoomSnapshot> onSuccess, System.Action<string> onFailure = null)
    {
        string uid = auth.CurrentUser.UserId;
        db.Collection("room_snapshots")
            .Document(uid)
            .Collection("snapshots")
            .Document(date)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("스냅샷 읽기 실패: " + task.Exception);
                    onFailure?.Invoke(task.Exception.Message);
                    return;
                }

                if (!task.Result.Exists)
                {
                    Debug.LogWarning("해당 날짜 스냅샷 없음: " + date);
                    onFailure?.Invoke("스냅샷 없음");
                    return;
                }

                onSuccess?.Invoke(task.Result.ConvertTo<RoomSnapshot>());
            });
    }

    // ───────────────────────────────────────
    // 오늘 날짜로 스냅샷 저장
    // ───────────────────────────────────────
    public void SaveSnapshot(List<RoomObject> objects, System.Action onSuccess = null, System.Action<string> onFailure = null)
    {
        string uid = auth.CurrentUser.UserId;
        string today = System.DateTime.Now.ToString("yyyy-MM-dd");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            { "SnapshotId", today },
            { "Date", today },
            { "Objects", objects },
            { "CreatedAt", FieldValue.ServerTimestamp }
        };

        db.Collection("room_snapshots")
            .Document(uid)
            .Collection("snapshots")
            .Document(today)
            .SetAsync(data)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("스냅샷 저장 실패: " + task.Exception);
                    onFailure?.Invoke(task.Exception.Message);
                    return;
                }

                Debug.Log("스냅샷 저장 완료: " + today);
                onSuccess?.Invoke();
            });
    }

    // ───────────────────────────────────────
    // 남의 방 스냅샷 조회
    // ───────────────────────────────────────
    public void GetOtherUserSnapshot(string targetUid, string date, System.Action<RoomSnapshot> onSuccess, System.Action<string> onFailure = null)
    {
        // 상대방 공개 여부 확인 후 스냅샷 조회
        db.Collection("users")
            .Document(targetUid)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("유저 조회 실패: " + task.Exception);
                    onFailure?.Invoke(task.Exception.Message);
                    return;
                }

                if (!task.Result.Exists)
                {
                    Debug.LogWarning("존재하지 않는 유저: " + targetUid);
                    onFailure?.Invoke("유저 없음");
                    return;
                }

                bool isPublic = task.Result.GetValue<bool>("IsPublic");
                if (!isPublic)
                {
                    Debug.LogWarning("비공개 계정: " + targetUid);
                    onFailure?.Invoke("비공개 계정");
                    return;
                }

                // 공개 계정이면 스냅샷 조회
                db.Collection("room_snapshots")
                    .Document(targetUid)
                    .Collection("snapshots")
                    .Document(date)
                    .GetSnapshotAsync()
                    .ContinueWithOnMainThread(snapshotTask =>
                    {
                        if (snapshotTask.IsFaulted)
                        {
                            Debug.LogError("스냅샷 읽기 실패: " + snapshotTask.Exception);
                            onFailure?.Invoke(snapshotTask.Exception.Message);
                            return;
                        }

                        if (!snapshotTask.Result.Exists)
                        {
                            Debug.LogWarning("해당 날짜 스냅샷 없음: " + date);
                            onFailure?.Invoke("스냅샷 없음");
                            return;
                        }

                        onSuccess?.Invoke(snapshotTask.Result.ConvertTo<RoomSnapshot>());
                    });
            });
    }
}