using NUnit.Framework;

namespace OntologyMetaverse.Tests.Editor
{
    /// <summary>
    /// QuestScreenController의 유형별 표시 규칙(QuestUiRules) 검증.
    /// "데이터 보완형"과 "삶 개선형" 퀘스트가 각각 올바른 UI 요소(타입 라벨,
    /// 답변 입력란, 완료 버튼, 보상 수령 버튼)를 노출하는지 확인한다.
    /// </summary>
    public class QuestUiRulesTests
    {
        // ═══════════════════════════════════════════════════════════════
        // 데이터 보완형 (DataFill)
        // ═══════════════════════════════════════════════════════════════
        [Test]
        public void MapQuestType_DataFill_ReturnsDataFill()
        {
            Assert.AreEqual(QuestType.DataFill, QuestUiRules.MapQuestType("데이터 보완형"));
        }

        [Test]
        public void DataFill_TypeLabelAndStyle_AreCorrect()
        {
            Assert.AreEqual("보완형", QuestUiRules.GetTypeLabel(QuestType.DataFill));
            Assert.AreEqual("quest-type--data", QuestUiRules.GetTypeStyleClass(QuestType.DataFill));
        }

        [Test]
        public void DataFill_Active_ShowsAnswerSection()
        {
            Assert.IsTrue(QuestUiRules.ShouldShowAnswerSection(QuestType.DataFill, QuestStatus.Active));
        }

        [Test]
        public void DataFill_Done_HidesAnswerSection()
        {
            Assert.IsFalse(QuestUiRules.ShouldShowAnswerSection(QuestType.DataFill, QuestStatus.Done));
        }

        // ═══════════════════════════════════════════════════════════════
        // 삶 개선형 (LifeImprove)
        // ═══════════════════════════════════════════════════════════════
        [Test]
        public void MapQuestType_LifeImprove_ReturnsLifeImprove()
        {
            Assert.AreEqual(QuestType.LifeImprove, QuestUiRules.MapQuestType("삶 개선형"));
        }

        [Test]
        public void MapQuestType_Exercise_ReturnsLifeImprove()
        {
            Assert.AreEqual(QuestType.LifeImprove, QuestUiRules.MapQuestType("운동"));
        }

        [Test]
        public void LifeImprove_TypeLabelAndStyle_AreCorrect()
        {
            Assert.AreEqual("개선형", QuestUiRules.GetTypeLabel(QuestType.LifeImprove));
            Assert.AreEqual("quest-type--life", QuestUiRules.GetTypeStyleClass(QuestType.LifeImprove));
        }

        [Test]
        public void LifeImprove_NeverShowsAnswerSection()
        {
            Assert.IsFalse(QuestUiRules.ShouldShowAnswerSection(QuestType.LifeImprove, QuestStatus.Active));
            Assert.IsFalse(QuestUiRules.ShouldShowAnswerSection(QuestType.LifeImprove, QuestStatus.Done));
        }

        // 완료 버튼 — 엔진 completable 플래그 + 미완료(Active)일 때만 노출 (#187).
        // 어느 퀘스트가 completable인지는 엔진 규칙이 결정하며
        // (Functions/test_rules.py::test_quest_completable_field에서 검증),
        // Unity는 그 플래그를 받아 버튼 노출만 판단한다.
        [TestCase(true,  QuestStatus.Active, true)]   // 측정 불가(completable) + 미완료 → 노출
        [TestCase(true,  QuestStatus.Done,   false)]  // completable + 완료 → 숨김
        [TestCase(false, QuestStatus.Active, false)]  // 측정 가능(자동완료 대상) → 숨김
        [TestCase(false, QuestStatus.Done,   false)]
        public void ShouldShowCompleteButton_OnlyWhenCompletableAndActive(
            bool completable, QuestStatus status, bool expected)
        {
            Assert.AreEqual(expected, QuestUiRules.ShouldShowCompleteButton(completable, status));
        }

        // ═══════════════════════════════════════════════════════════════
        // 일일 / 알 수 없는 유형
        // ═══════════════════════════════════════════════════════════════
        [Test]
        public void MapQuestType_Daily_ReturnsDaily()
        {
            Assert.AreEqual(QuestType.Daily, QuestUiRules.MapQuestType("일일"));
        }

        [Test]
        public void Daily_TypeLabelAndStyle_AreCorrect()
        {
            Assert.AreEqual("일일", QuestUiRules.GetTypeLabel(QuestType.Daily));
            Assert.AreEqual("quest-type--daily", QuestUiRules.GetTypeStyleClass(QuestType.Daily));
        }

        [Test]
        public void MapQuestType_UnknownString_DefaultsToDataFill()
        {
            Assert.AreEqual(QuestType.DataFill, QuestUiRules.MapQuestType("알수없음"));
            Assert.AreEqual(QuestType.DataFill, QuestUiRules.MapQuestType(""));
        }

        // ═══════════════════════════════════════════════════════════════
        // 보상 수령 버튼 — 유형과 무관하게 완료 + 미수령일 때만 노출
        // ═══════════════════════════════════════════════════════════════
        [TestCase(QuestStatus.Done, false, true)]
        [TestCase(QuestStatus.Done, true, false)]
        [TestCase(QuestStatus.Active, false, false)]
        [TestCase(QuestStatus.Active, true, false)]
        public void ShouldShowClaimButton_OnlyWhenDoneAndUnclaimed(QuestStatus status, bool claimed, bool expected)
        {
            Assert.AreEqual(expected, QuestUiRules.ShouldShowClaimButton(status, claimed));
        }
    }
}
