using UnityEngine;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
using System.Collections.Generic;

public class QuestManager : MonoBehaviour
{
    private FirebaseAuth auth;
    private FirebaseFirestore db;
    private RewardManager rewardManager;

    void Start()
    {
        auth = FirebaseAuth.DefaultInstance;
        db = FirebaseFirestore.DefaultInstance;
        rewardManager = GetComponent<RewardManager>();
    }

    // ───────────────────────────────────────
    // 퀘스트 목록 읽기
    // ───────────────────────────────────────
    public void GetQuests(bool completedFilter, System.Action<List<Quest>> onSuccess, System.Action<string> onFailure = null)
    {
        string uid = auth.CurrentUser.UserId;
        db.Collection("quests")
            .Document(uid)
            .Collection("userQuests")
            .WhereEqualTo("IsCompleted", completedFilter)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("퀘스트 목록 읽기 실패: " + task.Exception);
                    onFailure?.Invoke(task.Exception.Message);
                    return;
                }

                List<Quest> quests = new List<Quest>();
                foreach (DocumentSnapshot doc in task.Result.Documents)
                {
                    quests.Add(doc.ConvertTo<Quest>());
                }

                onSuccess?.Invoke(quests);
            });
    }

    // ───────────────────────────────────────
    // 퀘스트 단건 읽기 (상세 패널용)
    // ───────────────────────────────────────
    public void GetQuest(string questId, System.Action<Quest> onSuccess, System.Action<string> onFailure = null)
    {
        string uid = auth.CurrentUser.UserId;
        db.Collection("quests")
            .Document(uid)
            .Collection("userQuests")
            .Document(questId)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("퀘스트 읽기 실패: " + task.Exception);
                    onFailure?.Invoke(task.Exception.Message);
                    return;
                }

                if (!task.Result.Exists)
                {
                    Debug.LogWarning("퀘스트 없음: " + questId);
                    onFailure?.Invoke("퀘스트 없음");
                    return;
                }

                onSuccess?.Invoke(task.Result.ConvertTo<Quest>());
            });
    }

    // ───────────────────────────────────────
    // 퀘스트 완료 처리
    // ───────────────────────────────────────
    public void CompleteQuest(string questId, System.Action onSuccess = null, System.Action<string> onFailure = null)
    {
        string uid = auth.CurrentUser.UserId;
        DocumentReference questDoc = db.Collection("quests")
            .Document(uid)
            .Collection("userQuests")
            .Document(questId);

        Dictionary<string, object> updates = new Dictionary<string, object>
        {
            { "IsCompleted", true },
            { "CompletedAt", FieldValue.ServerTimestamp }
        };

        questDoc.UpdateAsync(updates).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("퀘스트 완료 처리 실패: " + task.Exception);
                onFailure?.Invoke(task.Exception.Message);
                return;
            }

            Debug.Log("퀘스트 완료 처리됨: " + questId);
            onSuccess?.Invoke();
        });
    }

    // ───────────────────────────────────────
    // 보상 수령 처리
    // ───────────────────────────────────────
    public void ClaimReward(string questId, int rewardAmount, System.Action onSuccess = null, System.Action<string> onFailure = null)
    {
        string uid = auth.CurrentUser.UserId;
        DocumentReference questDoc = db.Collection("quests")
            .Document(uid)
            .Collection("userQuests")
            .Document(questId);

        // 퀘스트 완료 여부 확인 후 보상 지급
        questDoc.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("퀘스트 조회 실패: " + task.Exception);
                onFailure?.Invoke(task.Exception.Message);
                return;
            }

            Quest quest = task.Result.ConvertTo<Quest>();

            if (!quest.IsCompleted)
            {
                Debug.LogWarning("완료되지 않은 퀘스트입니다.");
                onFailure?.Invoke("완료되지 않은 퀘스트");
                return;
            }

            // RewardManager로 재화 증가
            rewardManager.AddCurrency(rewardAmount,
                onSuccess: () =>
                {
                    Debug.Log($"보상 수령 완료: {rewardAmount}");
                    onSuccess?.Invoke();
                },
                onFailure: onFailure
            );
        });
    }
}