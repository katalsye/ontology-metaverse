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
    }

    void Start()
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
        DocumentReference diaryDoc = db.Collection("diary_entries")
            .Document(uid)
            .Collection("entries")
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
        db.Collection("diary_entries")
            .Document(uid)
            .Collection("entries")
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

        db.Collection("diary_entries")
            .Document(uid)
            .Collection("entries")
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
        DocumentReference diaryDoc = db.Collection("diary_entries")
            .Document(uid)
            .Collection("entries")
            .Document(date);

        diaryDoc.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("일기 조회 실패: " + task.Exception);
                onFailure?.Invoke(task.Exception.Message);
                return;
            }

            if (!task.Result.Exists)
            {
                Debug.LogWarning("해당 날짜 일기 없음: " + date);
                onFailure?.Invoke("일기 없음");
                return;
            }

            bool rewardClaimed = task.Result.GetValue<bool>("RewardClaimed");
            if (rewardClaimed)
            {
                Debug.LogWarning("이미 보상을 수령했습니다.");
                onFailure?.Invoke("이미 수령한 보상");
                return;
            }

            // 보상 플래그 업데이트
            diaryDoc.UpdateAsync("RewardClaimed", true).ContinueWithOnMainThread(updateTask =>
            {
                if (updateTask.IsFaulted)
                {
                    Debug.LogError("보상 플래그 업데이트 실패: " + updateTask.Exception);
                    onFailure?.Invoke(updateTask.Exception.Message);
                    return;
                }

                // RewardManager로 재화 증가
                rewardManager.AddCurrency(DiaryRewardAmount,
                    onSuccess: () =>
                    {
                        Debug.Log($"일기 보상 수령 완료: +{DiaryRewardAmount}");
                        onSuccess?.Invoke();
                    },
                    onFailure: onFailure
                );
            });
        });
    }
}