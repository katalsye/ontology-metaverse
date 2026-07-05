#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Firestore;
using Firebase.Extensions;

// 이슈 #103 검증용 — 머지 후 삭제
public class RoomObjectVerifyTest : MonoBehaviour
{
    [ContextMenu("Run Verify Test")]
    public void RunTest()
    {
        const string testUid = "Po0np5gin2aU0nJoDU7CvsfGSta2";

        FirebaseFirestore.DefaultInstance
            .Collection("room_objects")
            .Document(testUid)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("[Verify] Firestore 읽기 실패: " + task.Exception);
                    return;
                }

                var objects = ParseObjects(task.Result);
                Debug.Log($"[Verify] 파싱 결과: {objects.Count}개");

                foreach (var o in objects)
                    Debug.Log($"[Verify] objectType={o.ObjectType}, placementZone={o.PlacementZone}, inferredFrom={o.InferredFrom}");
            });
    }

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
                    result.Add(RoomObject.FromMap(dict));
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[Verify] 파싱 오류: " + e.Message);
        }

        return result;
    }
}
#endif
