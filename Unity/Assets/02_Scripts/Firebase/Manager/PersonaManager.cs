using UnityEngine;
using System;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;

public class PersonaManager : MonoBehaviour
{
    private FirebaseAuth auth;
    private FirebaseFirestore db;

    void Start()
    {
        auth = FirebaseAuth.DefaultInstance;
        db = FirebaseFirestore.DefaultInstance;
    }

    // ───────────────────────────────────────
    // 페르소나 읽기
    // ───────────────────────────────────────
    public void GetPersona(Action<Persona> onSuccess, Action<string> onFailure = null)
    {
        string uid = auth.CurrentUser.UserId;
        db.Collection("personas")
            .Document(uid)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("페르소나 읽기 실패: " + task.Exception);
                    onFailure?.Invoke(task.Exception.Message);
                    return;
                }

                if (!task.Result.Exists)
                {
                    onFailure?.Invoke("페르소나 없음");
                    return;
                }

                onSuccess?.Invoke(task.Result.ConvertTo<Persona>());
            });
    }

    // ───────────────────────────────────────
    // 페르소나 저장 / 수정
    // ───────────────────────────────────────
    public void SavePersona(Persona persona, Action onSuccess = null, Action<string> onFailure = null)
    {
        string uid = auth.CurrentUser.UserId;
        db.Collection("personas")
            .Document(uid)
            .SetAsync(persona)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("페르소나 저장 실패: " + task.Exception);
                    onFailure?.Invoke(task.Exception.Message);
                    return;
                }

                Debug.Log("페르소나 저장 완료");
                onSuccess?.Invoke();
            });
    }

    // ───────────────────────────────────────
    // 페르소나 삭제
    // ───────────────────────────────────────
    public void DeletePersona(Action onSuccess = null, Action<string> onFailure = null)
    {
        string uid = auth.CurrentUser.UserId;
        db.Collection("personas")
            .Document(uid)
            .DeleteAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("페르소나 삭제 실패: " + task.Exception);
                    onFailure?.Invoke(task.Exception.Message);
                    return;
                }

                Debug.Log("페르소나 삭제 완료");
                onSuccess?.Invoke();
            });
    }
}
