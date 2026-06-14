using System.Collections.Generic;
using NUnit.Framework;

namespace OntologyMetaverse.Tests.Editor
{
    /// <summary>
    /// Quest 데이터 모델(FromMap/ToMap)의 Firestore 직렬화 검증.
    /// ontology_engine.py가 quests/{uid} 문서의 "quests" 배열에 기록하는
    /// camelCase 필드 형식과 Unity Quest 모델 간 매핑이 깨지지 않는지 확인한다.
    /// </summary>
    public class QuestTests
    {
        private static Dictionary<string, object> FullMap() => new Dictionary<string, object>
        {
            { "title", "30분 산책하기" },
            { "questType", "삶 개선형" },
            { "rewardAmount", 50L }, // Firestore 숫자 필드는 long으로 반환됨
            { "isCompleted", true },
            { "createdAt", "2026-06-10T09:00:00Z" },
            { "completedAt", "2026-06-12T08:30:00Z" },
            { "targetEntityUri", "http://7team.dev/ontology#stepcount_123" },
            { "targetValue", "2800" },
            { "claimed", false },
        };

        private static Quest MakeQuest() => new Quest
        {
            Index = 2,
            Title = "오후엔 디카페인 어때요?",
            QuestType = "삶 개선형",
            RewardAmount = 30,
            IsCompleted = false,
            CreatedAt = "2026-06-14T10:00:00Z",
            CompletedAt = "",
            TargetEntityUri = "",
            TargetValue = "",
            Claimed = false,
        };

        // ═══════════════════════════════════════════════════════════════
        // FromMap
        // ═══════════════════════════════════════════════════════════════
        [Test]
        public void FromMap_AllFieldsPresent_MapsAllPropertiesCorrectly()
        {
            var quest = Quest.FromMap(FullMap(), index: 3);

            Assert.AreEqual(3, quest.Index);
            Assert.AreEqual("30분 산책하기", quest.Title);
            Assert.AreEqual("삶 개선형", quest.QuestType);
            Assert.AreEqual(50, quest.RewardAmount);
            Assert.IsTrue(quest.IsCompleted);
            Assert.AreEqual("2026-06-10T09:00:00Z", quest.CreatedAt);
            Assert.AreEqual("2026-06-12T08:30:00Z", quest.CompletedAt);
            Assert.AreEqual("http://7team.dev/ontology#stepcount_123", quest.TargetEntityUri);
            Assert.AreEqual("2800", quest.TargetValue);
            Assert.IsFalse(quest.Claimed);
        }

        [Test]
        public void FromMap_EmptyMap_DefaultsToEmptyValues()
        {
            var quest = Quest.FromMap(new Dictionary<string, object>(), index: 0);

            Assert.AreEqual(0, quest.Index);
            Assert.AreEqual("", quest.Title);
            Assert.AreEqual("", quest.QuestType);
            Assert.AreEqual(0, quest.RewardAmount);
            Assert.IsFalse(quest.IsCompleted);
            Assert.AreEqual("", quest.CreatedAt);
            Assert.AreEqual("", quest.CompletedAt);
            Assert.AreEqual("", quest.TargetEntityUri);
            Assert.AreEqual("", quest.TargetValue);
            Assert.IsFalse(quest.Claimed);
        }

        [Test]
        public void FromMap_RewardAmountAsLong_ConvertsToInt()
        {
            var map = new Dictionary<string, object> { { "rewardAmount", 120L } };

            var quest = Quest.FromMap(map, index: 0);

            Assert.AreEqual(120, quest.RewardAmount);
        }

        [TestCase(true, true)]
        [TestCase(false, false)]
        public void FromMap_IsCompleted_ParsesBoolValue(bool stored, bool expected)
        {
            var map = new Dictionary<string, object> { { "isCompleted", stored } };

            var quest = Quest.FromMap(map, index: 0);

            Assert.AreEqual(expected, quest.IsCompleted);
        }

        [Test]
        public void FromMap_ClaimedTrue_ParsesTrue()
        {
            var map = new Dictionary<string, object> { { "claimed", true } };

            var quest = Quest.FromMap(map, index: 0);

            Assert.IsTrue(quest.Claimed);
        }

        // ═══════════════════════════════════════════════════════════════
        // ToMap
        // ═══════════════════════════════════════════════════════════════
        [Test]
        public void ToMap_DoesNotIncludeIndex()
        {
            var quest = MakeQuest();

            var map = quest.ToMap();

            Assert.IsFalse(map.ContainsKey("index"), "Index는 배열 내 위치이므로 Firestore에 저장되지 않아야 함");
        }

        [Test]
        public void ToMap_NullOptionalFields_BecomeEmptyStrings()
        {
            var quest = MakeQuest();
            quest.CompletedAt = null;
            quest.TargetEntityUri = null;
            quest.TargetValue = null;

            var map = quest.ToMap();

            Assert.AreEqual("", map["completedAt"]);
            Assert.AreEqual("", map["targetEntityUri"]);
            Assert.AreEqual("", map["targetValue"]);
        }

        // ═══════════════════════════════════════════════════════════════
        // 라운드트립 (FromMap ↔ ToMap)
        // ═══════════════════════════════════════════════════════════════
        [Test]
        public void ToMap_FromMap_RoundTrip_PreservesFields()
        {
            var original = MakeQuest();

            var roundTripped = Quest.FromMap(original.ToMap(), index: original.Index);

            Assert.AreEqual(original.Title, roundTripped.Title);
            Assert.AreEqual(original.QuestType, roundTripped.QuestType);
            Assert.AreEqual(original.RewardAmount, roundTripped.RewardAmount);
            Assert.AreEqual(original.IsCompleted, roundTripped.IsCompleted);
            Assert.AreEqual(original.CreatedAt, roundTripped.CreatedAt);
            Assert.AreEqual(original.CompletedAt, roundTripped.CompletedAt);
            Assert.AreEqual(original.TargetEntityUri, roundTripped.TargetEntityUri);
            Assert.AreEqual(original.TargetValue, roundTripped.TargetValue);
            Assert.AreEqual(original.Claimed, roundTripped.Claimed);
        }

        [Test]
        public void CompleteQuest_TogglesIsCompleted_WithoutAffectingOtherFields()
        {
            var quest = MakeQuest();

            // QuestManager.CompleteQuest가 하는 작업과 동일: isCompleted만 true로 바꿔 ToMap → FromMap
            quest.IsCompleted = true;
            var reloaded = Quest.FromMap(quest.ToMap(), quest.Index);

            Assert.IsTrue(reloaded.IsCompleted);
            Assert.AreEqual(quest.Title, reloaded.Title);
            Assert.AreEqual(quest.RewardAmount, reloaded.RewardAmount);
        }
    }
}
