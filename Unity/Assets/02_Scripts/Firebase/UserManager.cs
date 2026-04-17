using UnityEngine;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
using System.Collections.Generic;
using System;

public class UserManager : MonoBehaviour
{
    public void CreateUserIfNotExists()
    {
        Debug.Log("=== CreateUserIfNotExists 호출됨 ===");

        FirebaseAuth auth = FirebaseAuth.DefaultInstance;
        FirebaseFirestore db = FirebaseFirestore.DefaultInstance;

        if (auth == null)
        {
            Debug.LogError("auth가 null");
            return;
        }

        FirebaseUser user = auth.CurrentUser;
        if (user == null)
        {
            Debug.LogError("user가 null");
            return;
        }

        if (db == null)
        {
            Debug.LogError("db가 null");
            return;
        }

        Debug.Log("유저 UID: " + user.UserId);

        try
        {
            DocumentReference userDoc = db.Collection("users").Document(user.UserId);

            userDoc.GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                Debug.Log("GetSnapshotAsync 콜백 도착");

                if (task.IsFaulted)
                {
                    Debug.LogError("유저 조회 실패: " + task.Exception);
                    return;
                }

                if (task.IsCanceled)
                {
                    Debug.LogError("유저 조회 취소됨");
                    return;
                }

                DocumentSnapshot snapshot = task.Result;
                Debug.Log("문서 존재 여부: " + snapshot.Exists);

                if (!snapshot.Exists)
                {
                    Dictionary<string, object> userData = new Dictionary<string, object>
                    {
                        { "nickname", user.DisplayName ?? "새로운 유저" },
                        { "email", user.Email ?? "" },
                        { "avatarUrl", user.PhotoUrl != null ? user.PhotoUrl.ToString() : "" },
                        { "statusMessage", "" },
                        { "coin", 0 },
                        { "createdAt", FieldValue.ServerTimestamp },
                        { "updatedAt", FieldValue.ServerTimestamp }
                    };

                    userDoc.SetAsync(userData).ContinueWithOnMainThread(setTask =>
                    {
                        if (setTask.IsFaulted)
                        {
                            Debug.LogError("유저 문서 생성 실패: " + setTask.Exception);
                            return;
                        }

                        Debug.Log("유저 문서 생성 완료! UID: " + user.UserId);
                    });
                }
                else
                {
                    Debug.Log("이미 존재하는 유저: " + user.UserId);
                }
            });
        }
        catch (Exception e)
        {
            Debug.LogError("CreateUserIfNotExists 예외: " + e.Message);
        }
    }

    public void UpdateNickname(string newNickname)
    {
        FirebaseAuth auth = FirebaseAuth.DefaultInstance;
        FirebaseFirestore db = FirebaseFirestore.DefaultInstance;
        FirebaseUser user = auth.CurrentUser;
        if (user == null) return;

        DocumentReference userDoc = db.Collection("users").Document(user.UserId);

        Dictionary<string, object> updates = new Dictionary<string, object>
        {
            { "nickname", newNickname },
            { "updatedAt", FieldValue.ServerTimestamp }
        };

        userDoc.UpdateAsync(updates).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("닉네임 업데이트 실패: " + task.Exception);
                return;
            }

            Debug.Log("닉네임 변경 완료: " + newNickname);
        });
    }

    public void UpdateStatusMessage(string message)
    {
        FirebaseAuth auth = FirebaseAuth.DefaultInstance;
        FirebaseFirestore db = FirebaseFirestore.DefaultInstance;
        FirebaseUser user = auth.CurrentUser;
        if (user == null) return;

        DocumentReference userDoc = db.Collection("users").Document(user.UserId);

        Dictionary<string, object> updates = new Dictionary<string, object>
        {
            { "statusMessage", message },
            { "updatedAt", FieldValue.ServerTimestamp }
        };

        userDoc.UpdateAsync(updates).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("상태메시지 업데이트 실패: " + task.Exception);
                return;
            }

            Debug.Log("상태메시지 변경 완료: " + message);
        });
    }

    public void DeleteAccount()
    {
        FirebaseAuth auth = FirebaseAuth.DefaultInstance;
        FirebaseFirestore db = FirebaseFirestore.DefaultInstance;
        FirebaseUser user = auth.CurrentUser;
        if (user == null) return;

        DocumentReference userDoc = db.Collection("users").Document(user.UserId);

        userDoc.DeleteAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("유저 문서 삭제 실패: " + task.Exception);
                return;
            }

            user.DeleteAsync().ContinueWithOnMainThread(authTask =>
            {
                if (authTask.IsFaulted)
                {
                    Debug.LogError("계정 삭제 실패: " + authTask.Exception);
                    return;
                }

                Debug.Log("계정 삭제 완료");
            });
        });
    }
}