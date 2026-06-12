using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
using OntologyMetaverse.DataCollection.SQLite;

namespace OntologyMetaverse.Testing
{
    /// <summary>
    /// [개발자 테스트용] "데이터 보완형" 퀘스트 답변 흐름(QuestAnswerSubmitter) 검증용
    /// 임시 퀘스트를 quests/{uid}에 추가한다.
    ///
    /// 사용법:
    ///   1. 씬에 빈 GameObject 추가 (e.g. "_QuestAnswerTest"), 이 컴포넌트 부착
    ///   2. 로그인 상태로 Play
    ///   3. Inspector에서 컴포넌트 우클릭 → 원하는 ContextMenu 실행
    ///      - "1. companion 테스트 퀘스트 추가": targetEntityUri 경로 테스트
    ///        (퀘스트 상세 → 답변 제출 시 targetEntityUri를 subject로 바로 사용)
    ///      - "2. emotion 테스트 퀘스트 추가": targetValue 경로 테스트
    ///        (SQLite에 activityType="산책" 트리플을 먼저 심어두고,
    ///         답변 제출 시 그 트리플의 subject를 찾아 prod:emotion을 붙임)
    ///   4. 앱에서 퀘스트 화면 → 진행 중 탭 → 새로 추가된 퀘스트 열어서 답변 제출
    ///   5. 확인:
    ///      - answer-feedback에 성공 메시지 표시
    ///      - SQLite triples 테이블에 새 행 (Synced 0→1)
    ///      - Firestore temp_triples/{uid}/items에 새 문서
    ///   6. "3. 테스트 퀘스트 정리"로 quests/{uid}에서 시드 데이터 제거
    ///
    /// 주의: temp_triples에 올라간 데이터는 다음 batch 추론에서 실제 그래프에 반영됨.
    /// 운영 계정이 아닌 테스트 계정으로 실행 권장.
    /// </summary>
    public class QuestAnswerTestSeeder : MonoBehaviour
    {
        private const string OntologyBaseUri = "http://7team.dev/ontology#";
        private const string TestTitlePrefix = "[테스트] ";
        private const string SeedSource = "quest_answer_test_seed";

        [ContextMenu("1. companion 테스트 퀘스트 추가 (targetEntityUri)")]
        public void SeedCompanionQuest()
        {
            string locUri = OntologyBaseUri + "loc_test_companion";

            AddQuest(new Dictionary<string, object>
            {
                { "title", TestTitlePrefix + "오늘 테스트카페 누구랑 갔어?" },
                { "questType", "데이터 보완형" },
                { "rewardAmount", 30 },
                { "isCompleted", false },
                { "createdAt", DateTime.UtcNow.ToString("o") },
                { "completedAt", "" },
                { "targetEntityUri", locUri },
                { "targetValue", "" },
                { "claimed", false },
            });
        }

        [ContextMenu("2. emotion 테스트 퀘스트 추가 (targetValue + SQLite 트리플)")]
        public void SeedEmotionQuest()
        {
            string actUri = OntologyBaseUri + "act_test_emotion";

            // complete_missing_emotion이 매칭할 activityType 트리플을 로컬에 미리 심어둔다.
            SQLiteManager.Instance.Connection.Insert(new Triple
            {
                Subject = actUri,
                Predicate = OntologyBaseUri + "activityType",
                Object = "산책",
                Datatype = "xsd:string",
                Source = SeedSource,
                Timestamp = DateTime.UtcNow.ToString("o"),
                Synced = 0,
            });
            Debug.Log($"[QuestAnswerTestSeeder] SQLite 시드: ({actUri}, activityType, 산책)");

            AddQuest(new Dictionary<string, object>
            {
                { "title", TestTitlePrefix + "오늘 산책 어떤 기분이었어?" },
                { "questType", "데이터 보완형" },
                { "rewardAmount", 30 },
                { "isCompleted", false },
                { "createdAt", DateTime.UtcNow.ToString("o") },
                { "completedAt", "" },
                { "targetEntityUri", "" },
                { "targetValue", "산책" },
                { "claimed", false },
            });
        }

        [ContextMenu("3. 테스트 퀘스트 정리 (Firestore + SQLite)")]
        public void CleanupSeedData()
        {
            string uid = FirebaseAuth.DefaultInstance.CurrentUser?.UserId;
            if (uid == null)
            {
                Debug.LogWarning("[QuestAnswerTestSeeder] 로그인 상태 아님");
                return;
            }

            var doc = FirebaseFirestore.DefaultInstance.Collection("quests").Document(uid);
            doc.GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || !task.Result.Exists || !task.Result.ContainsField("quests"))
                    return;

                var quests = task.Result.GetValue<List<object>>("quests");
                int before = quests.Count;
                quests = quests
                    .Where(q => !(q is Dictionary<string, object> map
                                   && map.TryGetValue("title", out var t)
                                   && t is string title
                                   && title.StartsWith(TestTitlePrefix)))
                    .ToList();

                doc.SetAsync(new Dictionary<string, object> { { "quests", quests } }, SetOptions.MergeAll)
                    .ContinueWithOnMainThread(t =>
                    {
                        if (t.IsFaulted)
                            Debug.LogError("[QuestAnswerTestSeeder] 정리 실패: " + t.Exception);
                        else
                            Debug.Log($"[QuestAnswerTestSeeder] 테스트 퀘스트 {before - quests.Count}건 제거");
                    });
            });

            int deleted = SQLiteManager.Instance.Connection.Table<Triple>()
                .Where(t => t.Source == SeedSource)
                .ToList()
                .Sum(t => SQLiteManager.Instance.Connection.Delete(t));
            Debug.Log($"[QuestAnswerTestSeeder] SQLite 시드 트리플 {deleted}건 삭제");
        }

        private void AddQuest(Dictionary<string, object> quest)
        {
            string uid = FirebaseAuth.DefaultInstance.CurrentUser?.UserId;
            if (uid == null)
            {
                Debug.LogWarning("[QuestAnswerTestSeeder] 로그인 상태 아님");
                return;
            }

            var doc = FirebaseFirestore.DefaultInstance.Collection("quests").Document(uid);
            doc.GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("[QuestAnswerTestSeeder] 조회 실패: " + task.Exception);
                    return;
                }

                var quests = (task.Result.Exists && task.Result.ContainsField("quests"))
                    ? task.Result.GetValue<List<object>>("quests")
                    : new List<object>();

                quests.Add(quest);

                doc.SetAsync(new Dictionary<string, object> { { "quests", quests } }, SetOptions.MergeAll)
                    .ContinueWithOnMainThread(t =>
                    {
                        if (t.IsFaulted)
                            Debug.LogError("[QuestAnswerTestSeeder] 추가 실패: " + t.Exception);
                        else
                            Debug.Log("[QuestAnswerTestSeeder] 테스트 퀘스트 추가 완료: " + quest["title"]);
                    });
            });
        }
    }
}
