using System.Collections.Generic;

/// <summary>
/// 퀘스트 카드/상세 화면의 유형별 표시 규칙 (라벨, 버튼/입력 노출 여부).
/// QuestScreenController에서 분리해 유닛 테스트로 검증한다.
/// </summary>
public static class QuestUiRules
{
    // A1 — 센서로 측정 불가능해 "완료하기" 버튼으로 직접 완료 처리하는 삶 개선형 퀘스트.
    // inference_rules.sparql의 burnout_warning / late_caffeine_sleep_quality / schedule_overload prod:title과 1:1 대응.
    public static readonly HashSet<string> ManuallyCompletableTitles = new HashSet<string>
    {
        "가벼운 스트레칭 10분",
        "오후엔 디카페인 어때요?",
        "오늘 일정이 빡빡해 보여요. 잠깐 쉬어가는 건 어때요?",
    };

    // 엔진 questType 값(camelCase questType 필드) → 화면 표시용 QuestType
    public static QuestType MapQuestType(string questType) => questType switch
    {
        "데이터 보완형" => QuestType.DataFill,
        "삶 개선형" => QuestType.LifeImprove,
        "운동" => QuestType.LifeImprove,
        "일일" => QuestType.Daily,
        _ => QuestType.DataFill,
    };

    public static string GetTypeLabel(QuestType type) => type switch
    {
        QuestType.DataFill => "보완형",
        QuestType.LifeImprove => "개선형",
        QuestType.Daily => "일일",
        _ => "",
    };

    public static string GetTypeStyleClass(QuestType type) => type switch
    {
        QuestType.DataFill => "quest-type--data",
        QuestType.LifeImprove => "quest-type--life",
        QuestType.Daily => "quest-type--daily",
        _ => "",
    };

    // 완료 버튼 (A1: 측정 불가능한 삶 개선형 퀘스트 + 미완료일 때만)
    public static bool ShouldShowCompleteButton(string title, QuestStatus status) =>
        status == QuestStatus.Active && ManuallyCompletableTitles.Contains(title);

    // 답변 입력 (데이터 보완형 + 미완료일 때만)
    public static bool ShouldShowAnswerSection(QuestType type, QuestStatus status) =>
        type == QuestType.DataFill && status == QuestStatus.Active;

    // 보상 수령 버튼 (완료 + 미수령일 때만)
    public static bool ShouldShowClaimButton(QuestStatus status, bool claimed) =>
        status == QuestStatus.Done && !claimed;
}
