using UnityEngine;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
using System.Collections.Generic;

public class RewardManager : MonoBehaviour
{
    public static RewardManager Instance { get; private set; }

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
    // 재화 잔액 읽기
    // ───────────────────────────────────────
    public void GetCurrency(System.Action<int> onSuccess, System.Action<string> onFailure = null)
    {
        string uid = auth.CurrentUser.UserId;
        db.Collection("rewards").Document(uid).GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("재화 읽기 실패: " + task.Exception);
                onFailure?.Invoke(task.Exception.Message);
                return;
            }

            if (!task.Result.Exists)
            {
                onSuccess?.Invoke(0);
                return;
            }

            int currency = task.Result.GetValue<int>("Amount");
            onSuccess?.Invoke(currency);
        });
    }

    // ───────────────────────────────────────
    // 재화 증가 (퀘스트 보상, 일기 보상)
    // ───────────────────────────────────────
    public void AddCurrency(int amount, System.Action onSuccess = null, System.Action<string> onFailure = null)
    {
        string uid = auth.CurrentUser.UserId;
        DocumentReference rewardDoc = db.Collection("rewards").Document(uid);

        db.RunTransactionAsync(transaction =>
        {
            return transaction.GetSnapshotAsync(rewardDoc).ContinueWithOnMainThread(task =>
            {
                int current = task.Result.Exists ? task.Result.GetValue<int>("Amount") : 0;
                Dictionary<string, object> data = new Dictionary<string, object> { { "Amount", current + amount } };
                if (task.Result.Exists) transaction.Update(rewardDoc, data);
                else transaction.Set(rewardDoc, data);
                return true;
            });
        }).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("재화 증가 실패: " + task.Exception);
                onFailure?.Invoke(task.Exception.Message);
                return;
            }

            Debug.Log($"재화 +{amount} 완료");
            onSuccess?.Invoke();
        });
    }

    // ───────────────────────────────────────
    // 아이템 구매 (재화 차감 + 아이템 추가)
    // ───────────────────────────────────────
    public void PurchaseItem(string itemId, int price, System.Action onSuccess = null, System.Action<string> onFailure = null)
    {
        string uid = auth.CurrentUser.UserId;
        DocumentReference rewardDoc = db.Collection("rewards").Document(uid);

        db.RunTransactionAsync(transaction =>
        {
            return transaction.GetSnapshotAsync(rewardDoc).ContinueWithOnMainThread(task =>
            {
                if (!task.Result.Exists)
                {
                    throw new System.Exception("재화 문서 없음");
                }

                int current = task.Result.GetValue<int>("Amount");

                if (current < price)
                {
                    throw new System.Exception("재화 부족");
                }

                List<string> items = task.Result.TryGetValue("Items", out List<string> existingItems)
                    ? existingItems
                    : new List<string>();

                items.Add(itemId);

                transaction.Update(rewardDoc, new Dictionary<string, object>
                {
                    { "Amount", current - price },
                    { "Items", items }
                });

                return true;
            });
        }).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                string reason = task.Exception?.InnerException?.Message ?? task.Exception?.Message;
                Debug.LogError("아이템 구매 실패: " + reason);
                onFailure?.Invoke(reason);
                return;
            }

            Debug.Log($"아이템 구매 완료: {itemId}");
            onSuccess?.Invoke();
        });
    }

    // ───────────────────────────────────────
    // 보유 아이템 목록 읽기
    // ───────────────────────────────────────
    public void GetItems(System.Action<List<string>> onSuccess, System.Action<string> onFailure = null)
    {
        string uid = auth.CurrentUser.UserId;
        db.Collection("rewards").Document(uid).GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("아이템 목록 읽기 실패: " + task.Exception);
                onFailure?.Invoke(task.Exception.Message);
                return;
            }

            if (!task.Result.Exists)
            {
                onSuccess?.Invoke(new List<string>());
                return;
            }

            List<string> items = task.Result.TryGetValue("Items", out List<string> existingItems)
                ? existingItems
                : new List<string>();

            onSuccess?.Invoke(items);
        });
    }
}