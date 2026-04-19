using UnityEngine;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
using System.Collections.Generic;

public class FollowManager : MonoBehaviour
{
    private FirebaseAuth auth;
    private FirebaseFirestore db;

    void Start()
    {
        auth = FirebaseAuth.DefaultInstance;
        db = FirebaseFirestore.DefaultInstance;
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
}