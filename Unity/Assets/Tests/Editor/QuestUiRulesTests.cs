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

        [TestCase("오늘 테스트카페 누구랑 갔어?", QuestStatus.Active)]
        [TestCase("오늘 테스트카페 누구랑 갔어?", QuestStatus.Done)]
        public void DataFill_NeverShowsCompleteButton(string title, QuestStatus status)
        {
            Assert.IsFalse(QuestUiRules.ShouldShowCompleteButton(title, status));
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

        // A1 — burnout_warning / late_caffeine_sleep_quality / schedule_overload
        [TestCase("가벼운 스트레칭 10분")]
        [TestCase("오후엔 디카페인 어때요?")]
        [TestCase("오늘 일정이 빡빡해 보여요. 잠깐 쉬어가는 건 어때요?")]
        public void LifeImprove_A1Title_Active_ShowsCompleteButton(string title)
        {
            Assert.IsTrue(QuestUiRules.ShouldShowCompleteButton(title, QuestStatus.Active));
        }

        [TestCase("가벼운 스트레칭 10분")]
        [TestCase("오후엔 디카페인 어때요?")]
        [TestCase("오늘 일정이 빡빡해 보여요. 잠깐 쉬어가는 건 어때요?")]
        public void LifeImprove_A1Title_Done_HidesCompleteButton(string title)
        {
            Assert.IsFalse(QuestUiRules.ShouldShowCompleteButton(title, QuestStatus.Done));
        }

        // A2 — 센서 추론으로 자동 완료되는 삶 개선형 퀘스트는 완료 버튼이 없어야 함
        [TestCase("30분 산책하기")]
        [TestCase("이번 주 활동량이 많이 줄었어요. 짧은 산책부터 시작해볼까요?")]
        [TestCase("날씨가 맑아요! 지금 딱 산책하기 좋아요")]
        [TestCase("오늘 심박수가 높네요. 잠깐 쉬어볼까요?")]
        public void LifeImprove_A2AutoCompleteTitle_NeverShowsCompleteButton(string title)
        {
            Assert.IsFalse(QuestUiRules.ShouldShowCompleteButton(title, QuestStatus.Active));
            Assert.IsFalse(QuestUiRules.ShouldShowCompleteButton(title, QuestStatus.Done));
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
