using UnityEngine;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
using System.Collections.Generic;
using OntologyMetaverse.OnDeviceAI.TripleExtraction;

public class DiaryManager : MonoBehaviour
{
    public static DiaryManager Instance { get; private set; }

    private FirebaseAuth auth;
    private FirebaseFirestore db;
    private RewardManager rewardManager;

    // 지연 탐색 — Gemma 미준비 시 ExtractAndSaveTriples가 0 반환하고 조용히 실패
    private TextTripleExtractor _tripleExtractor;
    private TextTripleExtractor TripleExtractor
    {
        get
        {
            if (_tripleExtractor == null)
                _tripleExtractor = FindObjectOfType<TextTripleExtractor>();
            return _tripleExtractor;
        }
    }

    private const int DiaryRewardAmount = 10;

    void Awake()
    {
        if (Instance != null) { Destroy(this); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        rewardManager = GetComponent<RewardManager>();
        FirebaseBootstrap.RunWhenReady(Init);
    }

    private void Init()
    {
        auth = FirebaseAuth.DefaultInstance;
        db = FirebaseFirestore.DefaultInstance;
        rewardManager = RewardManager.Instance;
    }

    // ───────────────────────────────────────
    // 오늘 일기 쓰기 / 수정
    // ───────────────────────────────────────
    public void SaveDiary(string content, System.Action onSuccess = null, System.Action<string> onFailure = null)
    {
        string uid = auth.CurrentUser.UserId;
        string today = System.DateTime.Now.ToString("yyyy-MM-dd");
        DocumentReference diaryDoc = db.Collection(FirestoreCollections.DiaryEntries)
            .Document(uid)
            .Collection(FirestoreCollections.Entries)
            .Document(today);

        diaryDoc.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("일기 조회 실패: " + task.Exception);
                onFailure?.Invoke(task.Exception.Message);
                return;
            }

            bool alreadyExists = task.Result.Exists;
            bool rewardClaimed = alreadyExists && task.Result.GetValue<bool>("RewardClaimed");

            Dictionary<string, object> data = new Dictionary<string, object>
            {
                { "EntryId", today },
                { "Date", today },
                { "Content", content },
                { "RewardClaimed", rewardClaimed },
                { "CreatedAt", alreadyExists
                    ? task.Result.GetValue<Firebase.Firestore.Timestamp>("CreatedAt")
                    : FieldValue.ServerTimestamp }
            };

            diaryDoc.SetAsync(data).ContinueWithOnMainThread(setTask =>
            {
                if (setTask.IsFaulted)
                {
                    Debug.LogError("일기 저장 실패: " + setTask.Exception);
                    onFailure?.Invoke(setTask.Exception.Message);
                    return;
                }

                Debug.Log("일기 저장 완료");
                TripleExtractor?.ExtractAndSaveTriples(content);
                TempTripleSyncManager.Instance?.SyncUnsyncedTriples();
                onSuccess?.Invoke();
            });
        });
    }

    // ───────────────────────────────────────
    // 날짜별 일기 단건 조회
    // ───────────────────────────────────────
    public void GetDiary(string date, System.Action<DiaryEntry> onSuccess, System.Action<string> onFailure = null)
    {
        string uid = auth.CurrentUser.UserId;
        db.Collection(FirestoreCollections.DiaryEntries)
            .Document(uid)
            .Collection(FirestoreCollections.Entries)
            .Document(date)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("일기 읽기 실패: " + task.Exception);
                    onFailure?.Invoke(task.Exception.Message);
                    return;
                }

                if (!task.Result.Exists)
                {
                    Debug.LogWarning("해당 날짜 일기 없음: " + date);
                    onFailure?.Invoke("일기 없음");
                    return;
                }

                onSuccess?.Invoke(task.Result.ConvertTo<DiaryEntry>());
            });
    }

    // ───────────────────────────────────────
    // 월간 일기 목록 읽기 (달력 UI용)
    // ───────────────────────────────────────
    public void GetMonthlyDiaries(string yearMonth, System.Action<List<DiaryEntry>> onSuccess, System.Action<string> onFailure = null)
    {
        string uid = auth.CurrentUser.UserId;
        string startDate = yearMonth + "-01";
        string endDate = yearMonth + "-31";

        db.Collection(FirestoreCollections.DiaryEntries)
            .Document(uid)
            .Collection(FirestoreCollections.Entries)
            .WhereGreaterThanOrEqualTo("Date", startDate)
            .WhereLessThanOrEqualTo("Date", endDate)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("월간 일기 읽기 실패: " + task.Exception);
                    onFailure?.Invoke(task.Exception.Message);
                    return;
                }

                List<DiaryEntry> entries = new List<DiaryEntry>();
                foreach (DocumentSnapshot doc in task.Result.Documents)
                {
                    entries.Add(doc.ConvertTo<DiaryEntry>());
                }

                onSuccess?.Invoke(entries);
            });
    }

    // ───────────────────────────────────────
    // 보상 플래그 업데이트 + 재화 지급
    // ───────────────────────────────────────
    public void ClaimDiaryReward(string date, System.Action onSuccess = null, System.Action<string> onFailure = null)
    {
        string uid = auth.CurrentUser.UserId;
        DocumentReference diaryDoc = db.Collection(FirestoreCollections.DiaryEntries)
            .Document(uid)
            .Collection(FirestoreCollections.Entries)
            .Document(date);

        // 존재/미수령 확인과 RewardClaimed=true 세팅을 트랜잭션으로 원자화(중복 수령 차단).
        // (기존: 플래그 먼저 커밋 → 이후 지급 실패 시 플래그가 true로 남아 보상 영구 유실)
        db.RunTransactionAsync(transaction =>
        {
            return transaction.GetSnapshotAsync(diaryDoc).ContinueWith(snapTask =>
            {
                var snap = snapTask.Result;
                if (!snap.Exists)
                    throw new System.InvalidOperationException("일기 없음");
                if (snap.GetValue<bool>("RewardClaimed"))
                    throw new System.InvalidOperationException("이미 수령한 보상");

                transaction.Update(diaryDoc, "RewardClaimed", true);
            });
        }).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                var ex = task.Exception?.Flatten().InnerException ?? task.Exception;
                string msg = ex?.Message ?? "보상 수령 실패";
                Debug.LogWarning("일기 보상 수령 실패: " + msg);
                onFailure?.Invoke(msg);
                return;
            }

            // RewardClaimed 확정 후 코인 지급.
            rewardManager.AddCurrency(DiaryRewardAmount,
                onSuccess: () =>
                {
                    Debug.Log($"일기 보상 수령 완료: +{DiaryRewardAmount}");
                    onSuccess?.Invoke();
                },
                onFailure: err =>
                {
                    // 지급 실패 → RewardClaimed 롤백해 재수령 허용(영구 유실 방지).
                    diaryDoc.UpdateAsync("RewardClaimed", false)
                        .ContinueWithOnMainThread(rb =>
                        {
                            if (rb.IsFaulted)
                                Debug.LogError("RewardClaimed 롤백 실패(수동 확인 필요): " + rb.Exception);
                        });
                    Debug.LogError("일기 코인 지급 실패, RewardClaimed 롤백 시도: " + err);
                    onFailure?.Invoke(err);
                });
        });
    }
}