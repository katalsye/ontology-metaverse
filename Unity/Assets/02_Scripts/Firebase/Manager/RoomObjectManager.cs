using UnityEngine;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
using System.Collections.Generic;

public class RoomObjectManager : MonoBehaviour
{
    private FirebaseAuth auth;
    private FirebaseFirestore db;

    void Start()
    {
        auth = FirebaseAuth.DefaultInstance;
        db = FirebaseFirestore.DefaultInstance;
    }

    // ───────────────────────────────────────
    // 내 room_objects 전체 읽기
    // ───────────────────────────────────────
    public void GetRoomObjects(System.Action<List<RoomObject>> onSuccess, System.Action<string> onFailure = null)
    {
        string uid = auth.CurrentUser.UserId;
        db.Collection("room_objects")
            .Document(uid)
            .Collection("objects")
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("room_objects 읽기 실패: " + task.Exception);
                    onFailure?.Invoke(task.Exception.Message);
                    return;
                }

                List<RoomObject> objects = new List<RoomObject>();
                foreach (DocumentSnapshot doc in task.Result.Documents)
                {
                    objects.Add(doc.ConvertTo<RoomObject>());
                }

                onSuccess?.Invoke(objects);
            });
    }

    // ───────────────────────────────────────
    // 오브젝트 단건 쓰기 (추가 / 수정)
    // ───────────────────────────────────────
    public void SetRoomObject(RoomObject roomObject, System.Action onSuccess = null, System.Action<string> onFailure = null)
    {
        string uid = auth.CurrentUser.UserId;
        DocumentReference objectDoc = db.Collection("room_objects")
            .Document(uid)
            .Collection("objects")
            .Document(roomObject.ObjectId);

        objectDoc.SetAsync(roomObject).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("오브젝트 쓰기 실패: " + task.Exception);
                onFailure?.Invoke(task.Exception.Message);
                return;
            }

            Debug.Log("오브젝트 저장 완료: " + roomObject.ObjectId);
            onSuccess?.Invoke();
        });
    }

    // ───────────────────────────────────────
    // 오브젝트 단건 삭제
    // ───────────────────────────────────────
    public void DeleteRoomObject(string objectId, System.Action onSuccess = null, System.Action<string> onFailure = null)
    {
        string uid = auth.CurrentUser.UserId;
        db.Collection("room_objects")
            .Document(uid)
            .Collection("objects")
            .Document(objectId)
            .DeleteAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("오브젝트 삭제 실패: " + task.Exception);
                    onFailure?.Invoke(task.Exception.Message);
                    return;
                }

                Debug.Log("오브젝트 삭제 완료: " + objectId);
                onSuccess?.Invoke();
            });
    }

    // ───────────────────────────────────────
    // 커스텀 모드 저장 (배치 일괄 저장)
    // ───────────────────────────────────────
    public void SaveCustomLayout(List<RoomObject> roomObjects, System.Action onSuccess = null, System.Action<string> onFailure = null)
    {
        string uid = auth.CurrentUser.UserId;
        WriteBatch batch = db.StartBatch();

        foreach (RoomObject obj in roomObjects)
        {
            DocumentReference objectDoc = db.Collection("room_objects")
                .Document(uid)
                .Collection("objects")
                .Document(obj.ObjectId);

            batch.Set(objectDoc, obj);
        }

        batch.CommitAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("커스텀 레이아웃 저장 실패: " + task.Exception);
                onFailure?.Invoke(task.Exception.Message);
                return;
            }

            Debug.Log($"커스텀 레이아웃 저장 완료: {roomObjects.Count}개");
            onSuccess?.Invoke();
        });
    }
}