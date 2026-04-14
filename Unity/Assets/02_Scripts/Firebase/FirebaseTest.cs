using UnityEngine;
using Firebase;
using Firebase.Firestore;
using Firebase.Extensions;

public class FirebaseTest : MonoBehaviour
{
    void Start()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                Debug.Log("Firebase 초기화 성공!");
                TestFirestore();
            }
            else
            {
                Debug.LogError("Firebase 초기화 실패: " + task.Result);
            }
        });
    }

    void TestFirestore()
    {
        var db = FirebaseFirestore.DefaultInstance;
        var testDoc = db.Collection("test").Document("hello");

        testDoc.SetAsync(new { message = "Firebase 연결 테스트", timestamp = FieldValue.ServerTimestamp })
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsCompletedSuccessfully)
                    Debug.Log("Firestore 쓰기 성공!");
                else
                    Debug.LogError("Firestore 쓰기 실패: " + task.Exception);
            });
    }
}