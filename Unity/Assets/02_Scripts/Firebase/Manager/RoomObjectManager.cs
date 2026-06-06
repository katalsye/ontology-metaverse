using System;
using UnityEngine;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
using System.Collections.Generic;

public class RoomObjectManager : MonoBehaviour
{
    private FirebaseAuth auth;
    private FirebaseFirestore db;
    private ListenerRegistration _roomListener;
    private string _listeningUid; // 현재 리스닝 중인 uid 추적 (로그아웃 감지용)

    public event Action<List<RoomObject>> OnRoomObjectsChanged;
    // public event Action<string> OnRoomListenerError; // 리스너 에러 알림

    void Awake()
    {
        FirebaseBootstrap.RunWhenReady(Init);
    }

    private void Init()
    {
        auth = FirebaseAuth.DefaultInstance;
        db = FirebaseFirestore.DefaultInstance;
    }

    void Update()
    {
        // 로그아웃 감지: 리스너 돌고 있는데 CurrentUser가 null이면 자동 정리
        if (_roomListener != null && auth?.CurrentUser == null)
        {
            Debug.Log("로그아웃 감지 → 룸 리스너 자동 해제");
            StopRoomListener();
        }
    }

    // ───────────────────────────────────────
    // 내 room_objects 전체 읽기
    // ───────────────────────────────────────
    public void GetRoomObjects(System.Action<List<RoomObject>> onSuccess, System.Action<string> onFailure = null)
    {
        if (auth?.CurrentUser == null)
        {
            Debug.LogWarning("GetRoomObjects: 로그인 상태 아님");
            onFailure?.Invoke("로그인 필요");
            return;
        }

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
        if (auth?.CurrentUser == null)
        {
            Debug.LogWarning("SetRoomObject: 로그인 상태 아님");
            onFailure?.Invoke("로그인 필요");
            return;
        }

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
        if (auth?.CurrentUser == null)
        {
            Debug.LogWarning("DeleteRoomObject: 로그인 상태 아님");
            onFailure?.Invoke("로그인 필요");
            return;
        }

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
        if (auth?.CurrentUser == null)
        {
            Debug.LogWarning("SaveCustomLayout: 로그인 상태 아님");
            onFailure?.Invoke("로그인 필요");
            return;
        }

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

    // ───────────────────────────────────────
    // 내 방 실시간 리스너 시작
    // ───────────────────────────────────────
    public void StartRoomListener()
    {
        if (auth?.CurrentUser == null)
        {
            Debug.LogWarning("StartRoomListener: 로그인 상태 아님");
            return;
        }

        StopRoomListener();
        string uid = auth.CurrentUser.UserId;
        _listeningUid = uid;

        _roomListener = db.Collection("room_objects")
            .Document(uid)
            .Collection("objects")
            .Listen(
                snapshot =>
                {
                    List<RoomObject> objects = new List<RoomObject>();
                    foreach (DocumentSnapshot doc in snapshot.Documents)
                        objects.Add(doc.ConvertTo<RoomObject>());
                    OnRoomObjectsChanged?.Invoke(objects);
                });

        Debug.Log("내 방 리스너 시작: " + uid);
    }

    // 남의 방 진입 시
    public void StartVisitingRoomListener(string targetUid)
    {
        if (auth?.CurrentUser == null)
        {
            Debug.LogWarning("StartVisitingRoomListener: 로그인 상태 아님");
            return;
        }

        if (string.IsNullOrEmpty(targetUid))
        {
            Debug.LogWarning("StartVisitingRoomListener: targetUid가 비어있음");
            return;
        }

        StopRoomListener();
        _listeningUid = targetUid;

        _roomListener = db.Collection("room_objects")
            .Document(targetUid)
            .Collection("objects")
            .Listen(
                snapshot =>
                {
                    List<RoomObject> objects = new List<RoomObject>();
                    foreach (DocumentSnapshot doc in snapshot.Documents)
                        objects.Add(doc.ConvertTo<RoomObject>());
                    OnRoomObjectsChanged?.Invoke(objects);
                });

        Debug.Log("남의 방 리스너 시작: " + targetUid);
    }

    public void StopRoomListener()
    {
        if (_roomListener != null)
        {
            _roomListener.Stop();
            _roomListener = null;
            Debug.Log("룸 리스너 해제: " + _listeningUid);
            _listeningUid = null;
        }
    }

    void OnDestroy() => StopRoomListener();
}