/// <summary>
/// 퀘스트 카드/상세 화면의 유형별 표시 규칙 (라벨, 버튼/입력 노출 여부).
/// QuestScreenController에서 분리해 유닛 테스트로 검증한다.
/// </summary>
public static class QuestUiRules
{
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

    // 완료 버튼 — 센서 측정 불가로 엔진이 completable=true로 표시한 퀘스트 + 미완료일 때만 (#187).
    // (기존: 하드코딩된 title 집합과 대조 → 엔진 문구 변경 시 조용히 깨짐. 엔진 필드 기반으로 전환.)
    public static bool ShouldShowCompleteButton(bool completable, QuestStatus status) =>
        status == QuestStatus.Active && completable;

    // 답변 입력 (데이터 보완형 + 미완료일 때만)
    public static bool ShouldShowAnswerSection(QuestType type, QuestStatus status) =>
        type == QuestType.DataFill && status == QuestStatus.Active;

    // 보상 수령 버튼 (완료 + 미수령일 때만)
    public static bool ShouldShowClaimButton(QuestStatus status, bool claimed) =>
        status == QuestStatus.Done && !claimed;
}
