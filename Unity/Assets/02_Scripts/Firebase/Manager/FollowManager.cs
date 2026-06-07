using UnityEngine;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
using System.Collections.Generic;

public class FollowManager : MonoBehaviour
{
    public static FollowManager Instance { get; private set; }

    private FirebaseAuth auth;
    private FirebaseFirestore db;

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
    // [STUB] CheckIsFollowing — UI에서 호출하지만 구현 누락
    // TODO: 서윤님 follow 컬렉션 구조 확정 후 정식 구현
    // ───────────────────────────────────────
    public void CheckIsFollowing(string userId, System.Action<bool> onResult, System.Action<string> onFailure = null)
    {
        Debug.LogWarning($"[FollowManager] CheckIsFollowing stub 호출 (userId={userId}) — 추후 서윤님 구현");
        onResult?.Invoke(false);
    }

    // ───────────────────────────────────────
    // 팔로우 요청
    // ───────────────────────────────────────
    public void SendFollowRequest(string targetUid, System.Action onSuccess = null, System.Action<string> onFailure = null)
    {
        string uid = auth.CurrentUser.UserId;
        string docId = uid + "_" + targetUid;

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            { "FromUid", uid },
            { "ToUid", targetUid },
            { "Status", "pending" },
            { "CreatedAt", FieldValue.ServerTimestamp }
        };

        db.Collection("follows")
            .Document(docId)
            .SetAsync(data)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("팔로우 요청 실패: " + task.Exception);
                    onFailure?.Invoke(task.Exception.Message);
                    return;
                }

                Debug.Log("팔로우 요청 완료: " + targetUid);
                onSuccess?.Invoke();
            });
    }

    // ───────────────────────────────────────
    // 팔로우 요청 수락
    // ───────────────────────────────────────
    public void AcceptFollowRequest(string fromUid, System.Action onSuccess = null, System.Action<string> onFailure = null)
    {
        string uid = auth.CurrentUser.UserId;
        string docId = fromUid + "_" + uid;

        db.Collection("follows")
            .Document(docId)
            .UpdateAsync("Status", "accepted")
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("팔로우 수락 실패: " + task.Exception);
                    onFailure?.Invoke(task.Exception.Message);
                    return;
                }

                Debug.Log("팔로우 수락 완료: " + fromUid);
                onSuccess?.Invoke();
            });
    }

    // ───────────────────────────────────────
    // 팔로우 요청 거절
    // ───────────────────────────────────────
    public void RejectFollowRequest(string fromUid, System.Action onSuccess = null, System.Action<string> onFailure = null)
    {
        string uid = auth.CurrentUser.UserId;
        string docId = fromUid + "_" + uid;

        db.Collection("follows")
            .Document(docId)
            .UpdateAsync("Status", "rejected")
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("팔로우 거절 실패: " + task.Exception);
                    onFailure?.Invoke(task.Exception.Message);
                    return;
                }

                Debug.Log("팔로우 거절 완료: " + fromUid);
                onSuccess?.Invoke();
            });
    }

    // ───────────────────────────────────────
    // 언팔로우 / 삭제
    // ───────────────────────────────────────
    public void Unfollow(string targetUid, System.Action onSuccess = null, System.Action<string> onFailure = null)
    {
        string uid = auth.CurrentUser.UserId;
        string docId = uid + "_" + targetUid;

        db.Collection("follows")
            .Document(docId)
            .DeleteAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("언팔로우 실패: " + task.Exception);
                    onFailure?.Invoke(task.Exception.Message);
                    return;
                }

                Debug.Log("언팔로우 완료: " + targetUid);
                onSuccess?.Invoke();
            });
    }

    // ───────────────────────────────────────
    // 팔로잉 목록 읽기 (내가 팔로우하는 사람)
    // ───────────────────────────────────────
    public void GetFollowings(System.Action<List<FollowRelation>> onSuccess, System.Action<string> onFailure = null)
    {
        string uid = auth.CurrentUser.UserId;
        db.Collection("follows")
            .WhereEqualTo("FromUid", uid)
            .WhereEqualTo("Status", "accepted")
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("팔로잉 목록 읽기 실패: " + task.Exception);
                    onFailure?.Invoke(task.Exception.Message);
                    return;
                }

                List<FollowRelation> followings = new List<FollowRelation>();
                foreach (DocumentSnapshot doc in task.Result.Documents)
                {
                    followings.Add(doc.ConvertTo<FollowRelation>());
                }

                onSuccess?.Invoke(followings);
            });
    }

    // ───────────────────────────────────────
    // 팔로워 목록 읽기 (나를 팔로우하는 사람)
    // ───────────────────────────────────────
    public void GetFollowers(System.Action<List<FollowRelation>> onSuccess, System.Action<string> onFailure = null)
    {
        string uid = auth.CurrentUser.UserId;
        db.Collection("follows")
            .WhereEqualTo("ToUid", uid)
            .WhereEqualTo("Status", "accepted")
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("팔로워 목록 읽기 실패: " + task.Exception);
                    onFailure?.Invoke(task.Exception.Message);
                    return;
                }

                List<FollowRelation> followers = new List<FollowRelation>();
                foreach (DocumentSnapshot doc in task.Result.Documents)
                {
                    followers.Add(doc.ConvertTo<FollowRelation>());
                }

                onSuccess?.Invoke(followers);
            });
    }

    // ───────────────────────────────────────
    // 받은 팔로우 요청 목록 (pending)
    // ───────────────────────────────────────
    public void GetPendingRequests(System.Action<List<FollowRelation>> onSuccess, System.Action<string> onFailure = null)
    {
        string uid = auth.CurrentUser.UserId;
        db.Collection("follows")
            .WhereEqualTo("ToUid", uid)
            .WhereEqualTo("Status", "pending")
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("팔로우 요청 목록 읽기 실패: " + task.Exception);
                    onFailure?.Invoke(task.Exception.Message);
                    return;
                }

                List<FollowRelation> requests = new List<FollowRelation>();
                foreach (DocumentSnapshot doc in task.Result.Documents)
                {
                    requests.Add(doc.ConvertTo<FollowRelation>());
                }

                onSuccess?.Invoke(requests);
            });
    }

    // ───────────────────────────────────────
    // 팔로워/팔로잉 수 조회 (프로필 표시용)
    // ───────────────────────────────────────
    public void GetFollowCounts(string targetUid, System.Action<int, int> onSuccess, System.Action<string> onFailure = null)
    {
        var followerTask = db.Collection("follows")
            .WhereEqualTo("ToUid", targetUid)
            .WhereEqualTo("Status", "accepted")
            .GetSnapshotAsync();

        var followingTask = db.Collection("follows")
            .WhereEqualTo("FromUid", targetUid)
            .WhereEqualTo("Status", "accepted")
            .GetSnapshotAsync();

        followerTask.ContinueWithOnMainThread(fTask =>
        {
            if (fTask.IsFaulted)
            {
                Debug.LogError("팔로워 수 조회 실패: " + fTask.Exception);
                onFailure?.Invoke(fTask.Exception.Message);
                return;
            }

            int followerCount = fTask.Result.Count;

            followingTask.ContinueWithOnMainThread(gTask =>
            {
                if (gTask.IsFaulted)
                {
                    Debug.LogError("팔로잉 수 조회 실패: " + gTask.Exception);
                    onFailure?.Invoke(gTask.Exception.Message);
                    return;
                }

                onSuccess?.Invoke(followerCount, gTask.Result.Count);
            });
        });
    }

    // ───────────────────────────────────────
    // 다른 유저 팔로워 목록 (공개 계정 여부 분기)
    // ───────────────────────────────────────
    public void GetOtherUserFollowers(string targetUid, System.Action<List<FollowRelation>> onSuccess, System.Action<string> onFailure = null)
    {
        CheckFollowAccess(targetUid,
            onAllowed: () =>
            {
                db.Collection("follows")
                    .WhereEqualTo("ToUid", targetUid)
                    .WhereEqualTo("Status", "accepted")
                    .GetSnapshotAsync()
                    .ContinueWithOnMainThread(task =>
                    {
                        if (task.IsFaulted)
                        {
                            Debug.LogError("팔로워 목록 읽기 실패: " + task.Exception);
                            onFailure?.Invoke(task.Exception.Message);
                            return;
                        }

                        List<FollowRelation> followers = new List<FollowRelation>();
                        foreach (DocumentSnapshot doc in task.Result.Documents)
                            followers.Add(doc.ConvertTo<FollowRelation>());

                        onSuccess?.Invoke(followers);
                    });
            },
            onDenied: () => onFailure?.Invoke("비공개 계정")
        );
    }

    // ───────────────────────────────────────
    // 다른 유저 팔로잉 목록 (공개 계정 여부 분기)
    // ───────────────────────────────────────
    public void GetOtherUserFollowings(string targetUid, System.Action<List<FollowRelation>> onSuccess, System.Action<string> onFailure = null)
    {
        CheckFollowAccess(targetUid,
            onAllowed: () =>
            {
                db.Collection("follows")
                    .WhereEqualTo("FromUid", targetUid)
                    .WhereEqualTo("Status", "accepted")
                    .GetSnapshotAsync()
                    .ContinueWithOnMainThread(task =>
                    {
                        if (task.IsFaulted)
                        {
                            Debug.LogError("팔로잉 목록 읽기 실패: " + task.Exception);
                            onFailure?.Invoke(task.Exception.Message);
                            return;
                        }

                        List<FollowRelation> followings = new List<FollowRelation>();
                        foreach (DocumentSnapshot doc in task.Result.Documents)
                            followings.Add(doc.ConvertTo<FollowRelation>());

                        onSuccess?.Invoke(followings);
                    });
            },
            onDenied: () => onFailure?.Invoke("비공개 계정")
        );
    }

    // ───────────────────────────────────────
    // 접근 권한 확인 (내부 헬퍼)
    //   공개 계정 or 나 자신 → onAllowed
    //   비공개 + 팔로우 accepted → onAllowed
    //   그 외 → onDenied
    // ───────────────────────────────────────
    private void CheckFollowAccess(string targetUid, System.Action onAllowed, System.Action onDenied)
    {
        string myUid = auth.CurrentUser.UserId;

        if (myUid == targetUid) { onAllowed?.Invoke(); return; }

        db.Collection("users").Document(targetUid).GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || !task.Result.Exists) { onDenied?.Invoke(); return; }

                bool isPublic = task.Result.GetValue<bool>("IsPublic");
                if (isPublic) { onAllowed?.Invoke(); return; }

                // 비공개 계정 → 내가 팔로우(accepted) 중인지 확인
                string docId = myUid + "_" + targetUid;
                db.Collection("follows").Document(docId).GetSnapshotAsync()
                    .ContinueWithOnMainThread(followTask =>
                    {
                        if (followTask.IsFaulted || !followTask.Result.Exists) { onDenied?.Invoke(); return; }

                        string status = followTask.Result.GetValue<string>("Status");
                        if (status == "accepted") onAllowed?.Invoke();
                        else onDenied?.Invoke();
                    });
            });
    }
}