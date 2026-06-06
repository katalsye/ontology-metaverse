using System;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;

public class RoomObjectManager : MonoBehaviour
{
    private FirebaseAuth auth;
    private FirebaseFirestore db;
    private ListenerRegistration _roomListener;
    private string _listeningUid;

    public event Action<List<RoomObject>> OnRoomObjectsChanged;

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
        if (_roomListener != null && auth?.CurrentUser == null)
        {
            Debug.Log("로그아웃 감지 → 룸 리스너 자동 해제");
            StopRoomListener();
        }
    }

    // ───────────────────────────────────────
    // 내 room_objects 전체 읽기
    // ───────────────────────────────────────
    public void GetRoomObjects(Action<List<RoomObject>> onSuccess, Action<string> onFailure = null)
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
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("room_objects 읽기 실패: " + task.Exception);
                    onFailure?.Invoke(task.Exception.Message);
                    return;
                }
                onSuccess?.Invoke(ParseObjects(task.Result));
            });
    }

    // ───────────────────────────────────────
    // 오브젝트 단건 쓰기 (추가 / 수정)
    // ───────────────────────────────────────
    public void SetRoomObject(RoomObject roomObject, Action onSuccess = null, Action<string> onFailure = null)
    {
        if (auth?.CurrentUser == null)
        {
            Debug.LogWarning("SetRoomObject: 로그인 상태 아님");
            onFailure?.Invoke("로그인 필요");
            return;
        }

        GetRoomObjects(objects =>
        {
            int idx = objects.FindIndex(o => o.ObjectId == roomObject.ObjectId);
            if (idx >= 0)
                objects[idx] = roomObject;
            else
                objects.Add(roomObject);
            SaveCustomLayout(objects, onSuccess, onFailure);
        }, onFailure);
    }

    // ───────────────────────────────────────
    // 오브젝트 단건 삭제
    // ───────────────────────────────────────
    public void DeleteRoomObject(string objectId, Action onSuccess = null, Action<string> onFailure = null)
    {
        if (auth?.CurrentUser == null)
        {
            Debug.LogWarning("DeleteRoomObject: 로그인 상태 아님");
            onFailure?.Invoke("로그인 필요");
            return;
        }

        GetRoomObjects(objects =>
        {
            objects.RemoveAll(o => o.ObjectId == objectId);
            SaveCustomLayout(objects, onSuccess, onFailure);
        }, onFailure);
    }

    // ───────────────────────────────────────
    // 커스텀 모드 저장 (배열 전체 덮어쓰기)
    // ───────────────────────────────────────
    public void SaveCustomLayout(List<RoomObject> roomObjects, Action onSuccess = null, Action<string> onFailure = null)
    {
        if (auth?.CurrentUser == null)
        {
            Debug.LogWarning("SaveCustomLayout: 로그인 상태 아님");
            onFailure?.Invoke("로그인 필요");
            return;
        }

        string uid = auth.CurrentUser.UserId;
        var docData = new Dictionary<string, object>
        {
            { "objects", SerializeObjects(roomObjects) }
        };

        db.Collection("room_objects")
            .Document(uid)
            .SetAsync(docData, SetOptions.MergeAll)
            .ContinueWithOnMainThread(task =>
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
            .Listen(snapshot =>
            {
                OnRoomObjectsChanged?.Invoke(ParseObjects(snapshot));
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
            .Listen(snapshot =>
            {
                OnRoomObjectsChanged?.Invoke(ParseObjects(snapshot));
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

    // ───────────────────────────────────────
    // 파싱 / 직렬화 헬퍼
    // ───────────────────────────────────────

    private List<RoomObject> ParseObjects(DocumentSnapshot doc)
    {
        var result = new List<RoomObject>();
        if (!doc.Exists || !doc.ContainsField("objects")) return result;

        try
        {
            var rawList = doc.GetValue<List<object>>("objects");
            if (rawList == null) return result;

            foreach (var item in rawList)
            {
                if (item is Dictionary<string, object> dict)
                    result.Add(ParseRoomObject(dict));
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("objects 파싱 오류: " + e.Message);
        }

        return result;
    }

    private RoomObject ParseRoomObject(Dictionary<string, object> d)
    {
        return new RoomObject
        {
            ObjectId            = GetStr(d, "objectId"),
            ObjectType          = GetStr(d, "objectType"),
            PlacementZone       = GetStr(d, "placementZone"),
            PositionX           = GetFloat(d, "positionX"),
            PositionY           = GetFloat(d, "positionY"),
            PositionZ           = GetFloat(d, "positionZ"),
            InferredFrom        = GetStr(d, "inferredFrom"),
            InferredFromConcept = GetStr(d, "inferredFromConcept"),
        };
    }

    private List<object> SerializeObjects(List<RoomObject> objects)
    {
        var list = new List<object>(objects.Count);
        foreach (var o in objects)
        {
            var d = new Dictionary<string, object>
            {
                { "objectType",          o.ObjectType },
                { "placementZone",       o.PlacementZone },
                { "positionX",           o.PositionX },
                { "positionY",           o.PositionY },
                { "positionZ",           o.PositionZ },
                { "inferredFrom",        o.InferredFrom },
                { "inferredFromConcept", o.InferredFromConcept },
            };
            if (!string.IsNullOrEmpty(o.ObjectId))
                d["objectId"] = o.ObjectId;
            list.Add(d);
        }
        return list;
    }

    private string GetStr(Dictionary<string, object> d, string key)
        => d.TryGetValue(key, out var v) ? v?.ToString() : null;

    private float GetFloat(Dictionary<string, object> d, string key)
        => d.TryGetValue(key, out var v) && v != null ? Convert.ToSingle(v) : 0f;
}
