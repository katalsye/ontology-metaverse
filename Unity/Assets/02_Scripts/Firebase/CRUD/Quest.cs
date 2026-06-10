using System;
using System.Collections.Generic;

/// <summary>
/// 퀘스트 데이터 모델.
/// 온톨로지 엔진(ontology_engine.py)이 quests/{uid} 단일 문서의
/// "quests" 배열 필드(camelCase)에 기록하는 형식과 1:1로 대응한다.
/// 배열 항목이라 문서 ID(questId)가 없으므로, 배열 내 위치(Index)로 식별한다.
/// </summary>
public class Quest
{
    public int Index { get; set; }          // 배열 내 위치 (Firestore에는 저장되지 않음)

    public string Title { get; set; }
    public string QuestType { get; set; }    // 보완형 / 개선형
    public int RewardAmount { get; set; }
    public bool IsCompleted { get; set; }
    public string CreatedAt { get; set; }

    // 자동 완료 추론 시 엔진이 기록하는 필드
    public string CompletedAt { get; set; }       // ISO 8601, 완료 시각
    public string TargetEntityUri { get; set; }   // 완료 판정에 쓰인 엔티티 URI
    public string TargetValue { get; set; }        // 완료 판정에 쓰인 값

    // 엔진 스키마에는 없는 Unity 측 부가 필드 — 보상 중복 수령 방지용
    public bool Claimed { get; set; }

    public static Quest FromMap(Dictionary<string, object> map, int index)
    {
        return new Quest
        {
            Index           = index,
            Title           = map.TryGetValue("title", out var title) ? title as string ?? "" : "",
            QuestType       = map.TryGetValue("questType", out var questType) ? questType as string ?? "" : "",
            RewardAmount    = map.TryGetValue("rewardAmount", out var reward) ? Convert.ToInt32(reward) : 0,
            IsCompleted     = map.TryGetValue("isCompleted", out var completed) && completed is bool b1 && b1,
            CreatedAt       = map.TryGetValue("createdAt", out var createdAt) ? createdAt?.ToString() ?? "" : "",
            CompletedAt     = map.TryGetValue("completedAt", out var completedAt) ? completedAt?.ToString() ?? "" : "",
            TargetEntityUri = map.TryGetValue("targetEntityUri", out var uri) ? uri as string ?? "" : "",
            TargetValue     = map.TryGetValue("targetValue", out var val) ? val as string ?? "" : "",
            Claimed         = map.TryGetValue("claimed", out var claimed) && claimed is bool b2 && b2,
        };
    }

    public Dictionary<string, object> ToMap()
    {
        return new Dictionary<string, object>
        {
            { "title", Title },
            { "questType", QuestType },
            { "rewardAmount", RewardAmount },
            { "isCompleted", IsCompleted },
            { "createdAt", CreatedAt },
            { "completedAt", CompletedAt ?? "" },
            { "targetEntityUri", TargetEntityUri ?? "" },
            { "targetValue", TargetValue ?? "" },
            { "claimed", Claimed },
        };
    }
}
