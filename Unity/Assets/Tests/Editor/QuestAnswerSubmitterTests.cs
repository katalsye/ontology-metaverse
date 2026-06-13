using System;
using System.Linq;
using NUnit.Framework;
using OntologyMetaverse.DataCollection.SQLite;

namespace OntologyMetaverse.Tests.Editor
{
    /// <summary>
    /// "데이터 보완형" 퀘스트 답변 제출(QuestAnswerSubmitter) 검증.
    /// missing_*/complete_missing_* 규칙(inference_rules.sparql)이 기대하는
    /// predicate 이름과 lookup 경로를 1:1로 검증한다.
    /// SQLiteManager.Instance는 실제 파일 DB(싱글톤)이므로,
    /// Subject/Object에 TestTag를 포함시켜 TearDown에서만 정리한다.
    /// </summary>
    public class QuestAnswerSubmitterTests
    {
        private const string Base = "http://7team.dev/ontology#";
        private const string TestTag = "qas_test_";

        [TearDown]
        public void Cleanup()
        {
            var conn = SQLiteManager.Instance.Connection;
            var rows = conn.Table<Triple>()
                .Where(t => t.Subject.Contains(TestTag) || t.Object.Contains(TestTag))
                .ToList();
            foreach (var row in rows)
                conn.Delete(row);
        }

        private static Quest MakeQuest(string title, string targetEntityUri = "", string targetValue = "")
        {
            return new Quest
            {
                Title = title,
                QuestType = "데이터 보완형",
                RewardAmount = 30,
                IsCompleted = false,
                TargetEntityUri = targetEntityUri,
                TargetValue = targetValue,
            };
        }

        private static void SeedTriple(string subject, string predicate, string obj)
        {
            SQLiteManager.Instance.Connection.Insert(new Triple
            {
                Subject = subject,
                Predicate = predicate,
                Object = obj,
                Datatype = "xsd:string",
                Source = "qas_test_seed",
                Timestamp = DateTime.UtcNow.ToString("o"),
                Synced = 0,
            });
        }

        private static Triple FindTriple(string subject, string predicate)
        {
            return SQLiteManager.Instance.Connection.Table<Triple>()
                .FirstOrDefault(t => t.Subject == subject && t.Predicate == predicate);
        }

        // ═══════════════════════════════════════════════════════════════
        // companion — missing_companion (targetEntityUri 경로)
        // ═══════════════════════════════════════════════════════════════
        [Test]
        public void TrySubmit_Companion_InsertsTripleOnTargetEntity()
        {
            string locUri = Base + TestTag + "loc_companion";
            var quest = MakeQuest("오늘 테스트카페 누구랑 갔어?", targetEntityUri: locUri);

            bool ok = QuestAnswerSubmitter.TrySubmit(quest, "친구랑", out string message);

            Assert.IsTrue(ok, message);
            var triple = FindTriple(locUri, Base + "companion");
            Assert.IsNotNull(triple, "companion 트리플이 생성되어야 함");
            Assert.AreEqual("친구랑", triple.Object);
            Assert.AreEqual("xsd:string", triple.Datatype);
            Assert.AreEqual(0, triple.Synced);
        }

        // ═══════════════════════════════════════════════════════════════
        // cause — missing_sleep_cause (targetEntityUri 경로, 제목 완전 일치)
        // ═══════════════════════════════════════════════════════════════
        [Test]
        public void TrySubmit_SleepCause_InsertsTripleOnTargetEntity()
        {
            string sleepUri = Base + TestTag + "sleep_cause";
            var quest = MakeQuest("어젯밤 잠이 잘 안 왔어? 이유가 있었어?", targetEntityUri: sleepUri);

            bool ok = QuestAnswerSubmitter.TrySubmit(quest, "카페인 때문에", out string message);

            Assert.IsTrue(ok, message);
            var triple = FindTriple(sleepUri, Base + "cause");
            Assert.IsNotNull(triple, "cause 트리플이 생성되어야 함");
            Assert.AreEqual("카페인 때문에", triple.Object);
        }

        // ═══════════════════════════════════════════════════════════════
        // review — missing_event_review (targetEntityUri 경로, "어땠어?" 접미사)
        // ═══════════════════════════════════════════════════════════════
        [Test]
        public void TrySubmit_EventReview_InsertsTripleOnTargetEntity()
        {
            string evtUri = Base + TestTag + "evt_review";
            var quest = MakeQuest("팀 미팅 어땠어?", targetEntityUri: evtUri);

            bool ok = QuestAnswerSubmitter.TrySubmit(quest, "유익했음", out string message);

            Assert.IsTrue(ok, message);
            var triple = FindTriple(evtUri, Base + "review");
            Assert.IsNotNull(triple, "review 트리플이 생성되어야 함");
            Assert.AreEqual("유익했음", triple.Object);
        }

        // ═══════════════════════════════════════════════════════════════
        // reason — stress_music_pattern / complete_stress_reason (targetEntityUri 경로, 제목 완전 일치)
        // ═══════════════════════════════════════════════════════════════
        [Test]
        public void TrySubmit_StressReason_InsertsTripleOnTargetEntity()
        {
            string actUri = Base + TestTag + "act_stress";
            var quest = MakeQuest("오늘 힘든 일 있었어?", targetEntityUri: actUri);

            bool ok = QuestAnswerSubmitter.TrySubmit(quest, "발표 망쳤어", out string message);

            Assert.IsTrue(ok, message);
            var triple = FindTriple(actUri, Base + "reason");
            Assert.IsNotNull(triple, "reason 트리플이 생성되어야 함");
            Assert.AreEqual("발표 망쳤어", triple.Object);
        }

        // ═══════════════════════════════════════════════════════════════
        // emotion — missing_emotion (targetValue + activityType lookup 경로)
        // ═══════════════════════════════════════════════════════════════
        [Test]
        public void TrySubmit_Emotion_FindsSubjectByActivityTypeAndInsertsTriple()
        {
            string actUri = Base + TestTag + "act_emotion";
            string activityType = TestTag + "walk";
            SeedTriple(actUri, Base + "activityType", activityType);

            var quest = MakeQuest($"오늘 {activityType} 어떤 기분이었어?", targetValue: activityType);

            bool ok = QuestAnswerSubmitter.TrySubmit(quest, "평온함", out string message);

            Assert.IsTrue(ok, message);
            var triple = FindTriple(actUri, Base + "emotion");
            Assert.IsNotNull(triple, "lookup으로 찾은 Activity subject에 emotion 트리플이 생성되어야 함");
            Assert.AreEqual("평온함", triple.Object);
        }

        // ═══════════════════════════════════════════════════════════════
        // purpose — missing_purpose (targetValue + placeName lookup 경로)
        // ═══════════════════════════════════════════════════════════════
        [Test]
        public void TrySubmit_Purpose_FindsSubjectByPlaceNameAndInsertsTriple()
        {
            string locUri = Base + TestTag + "loc_purpose";
            string placeName = TestTag + "library";
            SeedTriple(locUri, Base + "placeName", placeName);

            var quest = MakeQuest($"{placeName}에 자주 가는 이유가 있어?", targetValue: placeName);

            bool ok = QuestAnswerSubmitter.TrySubmit(quest, "공부하러", out string message);

            Assert.IsTrue(ok, message);
            var triple = FindTriple(locUri, Base + "purpose");
            Assert.IsNotNull(triple, "lookup으로 찾은 Location subject에 purpose 트리플이 생성되어야 함");
            Assert.AreEqual("공부하러", triple.Object);
        }

        // ═══════════════════════════════════════════════════════════════
        // mood — missing_music_mood (targetValue + genre lookup 경로)
        // ═══════════════════════════════════════════════════════════════
        [Test]
        public void TrySubmit_MusicMood_FindsSubjectByGenreAndInsertsTriple()
        {
            string mlUri = Base + TestTag + "ml_mood";
            string genre = TestTag + "jazz";
            SeedTriple(mlUri, Base + "genre", genre);

            var quest = MakeQuest($"요즘 {genre} 음악 자주 듣네, 어떤 기분일 때 들어?", targetValue: genre);

            bool ok = QuestAnswerSubmitter.TrySubmit(quest, "잠들기 전", out string message);

            Assert.IsTrue(ok, message);
            var triple = FindTriple(mlUri, Base + "mood");
            Assert.IsNotNull(triple, "lookup으로 찾은 MusicListening subject에 mood 트리플이 생성되어야 함");
            Assert.AreEqual("잠들기 전", triple.Object);
        }

        // ═══════════════════════════════════════════════════════════════
        // 실패 케이스
        // ═══════════════════════════════════════════════════════════════
        [Test]
        public void TrySubmit_EmptyAnswer_Fails()
        {
            var quest = MakeQuest("오늘 테스트카페 누구랑 갔어?", targetEntityUri: Base + TestTag + "x");

            bool ok = QuestAnswerSubmitter.TrySubmit(quest, "   ", out string message);

            Assert.IsFalse(ok);
            Assert.AreEqual("답변을 입력해 주세요", message);
        }

        [Test]
        public void TrySubmit_UnmappedTitle_Fails()
        {
            var quest = MakeQuest("오늘 날씨 어떻게 생각해");

            bool ok = QuestAnswerSubmitter.TrySubmit(quest, "좋아요", out string message);

            Assert.IsFalse(ok);
            Assert.AreEqual("이 퀘스트는 텍스트 답변으로 처리할 수 없어요", message);
        }

        [Test]
        public void TrySubmit_CompanionWithoutTargetEntityUri_Fails()
        {
            var quest = MakeQuest("오늘 테스트카페 누구랑 갔어?", targetEntityUri: "");

            bool ok = QuestAnswerSubmitter.TrySubmit(quest, "친구랑", out string message);

            Assert.IsFalse(ok);
            Assert.AreEqual("퀘스트 정보가 부족해 답변을 반영할 수 없어요", message);
        }

        [Test]
        public void TrySubmit_EmotionWithoutTargetValue_Fails()
        {
            var quest = MakeQuest("오늘 산책 어떤 기분이었어?", targetValue: "");

            bool ok = QuestAnswerSubmitter.TrySubmit(quest, "좋음", out string message);

            Assert.IsFalse(ok);
            Assert.AreEqual("퀘스트 정보가 부족해 답변을 반영할 수 없어요", message);
        }

        [Test]
        public void TrySubmit_EmotionLookupNotFound_Fails()
        {
            string activityType = TestTag + "nonexistent_activity";
            var quest = MakeQuest($"오늘 {activityType} 어떤 기분이었어?", targetValue: activityType);

            bool ok = QuestAnswerSubmitter.TrySubmit(quest, "좋음", out string message);

            Assert.IsFalse(ok);
            Assert.AreEqual("지금은 반영할 수 없어요. 나중에 다시 시도해 주세요", message);
        }
    }
}
