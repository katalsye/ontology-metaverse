using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;

/// <summary>
/// 퀘스트 관리자.
/// 온톨로지 엔진이 quests/{uid} 단일 문서의 "quests" 배열 필드(camelCase)에
/// 추론 결과를 기록하므로, Unity도 같은 경로/형식을 그대로 읽고 쓴다.
/// 배열 항목에는 문서 ID가 없어 배열 내 위치(index)로 개별 퀘스트를 식별한다.
/// </summary>
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    private FirebaseAuth auth;
    private FirebaseFirestore db;
    private RewardManager rewardManager;

    private ListenerRegistration _questListener;

    // false → true 전환이 감지된 퀘스트에 대해 발생 (자동 완료 추론 결과)
    public event Action<Quest> OnQuestAutoCompleted;

    private Dictionary<int, bool> _previousStates = new Dictionary<int, bool>();
    private bool _questListenerInitialized;

    void Awake()
    {
        if (Instance != null) { Destroy(this); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public event Action<List<Quest>> OnQuestsChanged;
    public event Action<int> OnUnreadQuestCountChanged;
    // public event Action<string> OnQuestListenerError; // 리스너 에러 알림

    void Start()
    {
        auth = FirebaseAuth.DefaultInstance;
        db = FirebaseFirestore.DefaultInstance;
        rewardManager = RewardManager.Instance;

        if (rewardManager == null)
        {
            Debug.LogError("QuestManager: RewardManager.Instance가 없음. ClaimReward가 작동하지 않습니다.");
        }
    }

    void Update()
    {
        // 로그아웃 감지: 리스너 돌고 있는데 CurrentUser가 null이면 자동 정리
        if (_questListener != null && auth?.CurrentUser == null)
        {
            Debug.Log("로그아웃 감지 → 퀘스트 리스너 자동 해제");
            StopQuestListener();
        }
    }

    // ───────────────────────────────────────
    // quests/{uid} 문서 파싱
    // ───────────────────────────────────────
    private static List<Quest> ParseQuests(DocumentSnapshot doc)
    {
        var quests = new List<Quest>();
        if (doc == null || !doc.Exists || !doc.ContainsField("quests")) return quests;

        var raw = doc.GetValue<List<object>>("quests");
        if (raw == null) return quests;

        for (int i = 0; i < raw.Count; i++)
        {
            if (raw[i] is Dictionary<string, object> map)
                quests.Add(Quest.FromMap(map, i));
        }
        return quests;
    }

    // ───────────────────────────────────────
    // 퀘스트 목록 읽기
    // ───────────────────────────────────────
    public void GetQuests(bool completedFilter, System.Action<List<Quest>> onSuccess, System.Action<string> onFailure = null)
    {
        if (auth?.CurrentUser == null)
        {
            Debug.LogWarning("GetQuests: 로그인 상태 아님");
            onFailure?.Invoke("로그인 필요");
            return;
        }

        string uid = auth.CurrentUser.UserId;
        db.Collection("quests")
            .Document(uid)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("퀘스트 목록 읽기 실패: " + task.Exception);
                    onFailure?.Invoke(task.Exception.Message);
                    return;
                }

                var quests = ParseQuests(task.Result)
                    .Where(q => q.IsCompleted == completedFilter)
                    .ToList();

                onSuccess?.Invoke(quests);
            });
    }

    // ───────────────────────────────────────
    // 퀘스트 단건 읽기 (상세 패널용) — index = quests 배열 내 위치
    // ───────────────────────────────────────
    public void GetQuest(int index, System.Action<Quest> onSuccess, System.Action<string> onFailure = null)
    {
        if (auth?.CurrentUser == null)
        {
            Debug.LogWarning("GetQuest: 로그인 상태 아님");
            onFailure?.Invoke("로그인 필요");
            return;
        }

        string uid = auth.CurrentUser.UserId;
        db.Collection("quests")
            .Document(uid)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("퀘스트 읽기 실패: " + task.Exception);
                    onFailure?.Invoke(task.Exception.Message);
                    return;
                }

                var quests = ParseQuests(task.Result);
                if (index < 0 || index >= quests.Count)
                {
                    Debug.LogWarning("퀘스트 없음: index=" + index);
                    onFailure?.Invoke("퀘스트 없음");
                    return;
                }

                onSuccess?.Invoke(quests[index]);
            });
    }

    // ───────────────────────────────────────
    // 퀘스트 완료 처리 (DEBUG 전용)
    // 운영 시에는 ontology_engine.py가 자동으로 isCompleted=true를 기록한다.
    // ───────────────────────────────────────
    public void CompleteQuest(int index, System.Action onSuccess = null, System.Action<string> onFailure = null)
    {
        if (auth?.CurrentUser == null)
        {
            Debug.LogWarning("CompleteQuest: 로그인 상태 아님");
            onFailure?.Invoke("로그인 필요");
            return;
        }

        string uid = auth.CurrentUser.UserId;
        DocumentReference questDoc = db.Collection("quests").Document(uid);

        db.RunTransactionAsync(transaction =>
        {
            return transaction.GetSnapshotAsync(questDoc).ContinueWith(snapshotTask =>
            {
                var quests = ParseQuests(snapshotTask.Result);
                if (index < 0 || index >= quests.Count)
                    throw new InvalidOperationException("퀘스트 없음");

                quests[index].IsCompleted = true;
                transaction.Update(questDoc, "quests", quests.Select(q => q.ToMap()).ToList());
            });
        }).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("퀘스트 완료 처리 실패: " + task.Exception);
                onFailure?.Invoke(task.Exception.Message);
                return;
            }

            Debug.Log("퀘스트 완료 처리됨: index=" + index);
            onSuccess?.Invoke();
        });
    }

    // ───────────────────────────────────────
    // 보상 수령 처리 — 완료 + 미수령 상태에서만 코인 지급 후 claimed 표시
    // ───────────────────────────────────────
    public void ClaimReward(int index, int rewardAmount, System.Action onSuccess = null, System.Action<string> onFailure = null)
    {
        if (auth?.CurrentUser == null)
        {
            Debug.LogWarning("ClaimReward: 로그인 상태 아님");
            onFailure?.Invoke("로그인 필요");
            return;
        }

        if (rewardManager == null)
        {
            Debug.LogError("ClaimReward: RewardManager 없음");
            onFailure?.Invoke("RewardManager 미연결");
            return;
        }

        string uid = auth.CurrentUser.UserId;
        DocumentReference questDoc = db.Collection("quests").Document(uid);

        questDoc.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("퀘스트 조회 실패: " + task.Exception);
                onFailure?.Invoke(task.Exception.Message);
                return;
            }

            var quests = ParseQuests(task.Result);
            if (index < 0 || index >= quests.Count)
            {
                Debug.LogWarning("퀘스트 없음: index=" + index);
                onFailure?.Invoke("퀘스트 없음");
                return;
            }

            Quest quest = quests[index];

            if (!quest.IsCompleted)
            {
                Debug.LogWarning("완료되지 않은 퀘스트입니다.");
                onFailure?.Invoke("완료되지 않은 퀘스트");
                return;
            }

            if (quest.Claimed)
            {
                Debug.LogWarning("이미 보상을 수령한 퀘스트입니다.");
                onFailure?.Invoke("이미 수령한 보상");
                return;
            }

            // RewardManager로 재화 증가
            rewardManager.AddCurrency(rewardAmount,
                onSuccess: () =>
                {
                    // claimed 표시 — 배열 전체를 다시 써서 중복 수령 방지
                    quest.Claimed = true;
                    questDoc.UpdateAsync("quests", quests.Select(q => q.ToMap()).ToList())
                        .ContinueWithOnMainThread(updateTask =>
                        {
                            if (updateTask.IsFaulted)
                                Debug.LogError("claimed 갱신 실패: " + updateTask.Exception);
                        });

                    Debug.Log($"보상 수령 완료: {rewardAmount}");
                    onSuccess?.Invoke();
                },
                onFailure: onFailure
            );
        });
    }

    // ───────────────────────────────────────
    // 퀘스트 실시간 리스너
    // ───────────────────────────────────────
    public void StartQuestListener()
    {
        if (auth?.CurrentUser == null)
        {
            Debug.LogWarning("StartQuestListener: 로그인 상태 아님");
            return;
        }

        StopQuestListener();
        string uid = auth.CurrentUser.UserId;
        _questListenerInitialized = false;
        _previousStates.Clear();

        _questListener = db.Collection("quests")
            .Document(uid)
            .Listen(snapshot =>
            {
                List<Quest> quests = ParseQuests(snapshot);
                int unread = quests.Count(q => !q.IsCompleted);

                // 초기 로드 이후부터 false → true 전환 감지
                if (_questListenerInitialized)
                {
                    foreach (var quest in quests)
                    {
                        bool wasDone = _previousStates.TryGetValue(quest.Index, out var prev) && prev;
                        if (!wasDone && quest.IsCompleted)
                            OnQuestAutoCompleted?.Invoke(quest);
                    }
                }
                _questListenerInitialized = true;
                _previousStates = quests.ToDictionary(q => q.Index, q => q.IsCompleted);

                OnQuestsChanged?.Invoke(quests);
                OnUnreadQuestCountChanged?.Invoke(unread);  // 뱃지용
            });

        Debug.Log("퀘스트 리스너 시작: " + uid);
    }

    public void StopQuestListener()
    {
        if (_questListener != null)
        {
            _questListener.Stop();
            _questListener = null;
            _questListenerInitialized = false;
            _previousStates.Clear();
            Debug.Log("퀘스트 리스너 해제");
        }
    }

    void OnDestroy() => StopQuestListener();
}
