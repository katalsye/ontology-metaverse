using System;
using System.Linq;
using OntologyMetaverse.DataCollection.SQLite;

/// <summary>
/// "데이터 보완형" 퀘스트의 텍스트 답변을 트리플로 변환해 SQLite에 저장하고 동기화한다.
/// 퀘스트 title 패턴은 Functions/ontology/rules/inference_rules.sparql의
/// missing_* / complete_missing_* 규칙 쌍과 1:1로 대응시킨 것이므로,
/// 두 파일을 함께 수정해야 한다.
/// </summary>
public static class QuestAnswerSubmitter
{
    private const string OntologyBaseUri = "http://7team.dev/ontology#";

    private struct AnswerMapping
    {
        // 새 트리플에 기록할 predicate (companion/emotion/purpose/mood/cause/review)
        public string Predicate;

        // null이면 quest.TargetEntityUri를 subject로 사용.
        // 값이 있으면 quest.TargetValue == 이 predicate의 값을 가진 트리플을 찾아 그 subject를 사용.
        public string LookupPredicate;
    }

    public static bool TrySubmit(Quest quest, string answerText, out string message)
    {
        answerText = answerText?.Trim();
        if (string.IsNullOrEmpty(answerText))
        {
            message = "답변을 입력해 주세요";
            return false;
        }

        var mapping = ResolveMapping(quest.Title);
        if (mapping == null)
        {
            message = "이 퀘스트는 텍스트 답변으로 처리할 수 없어요";
            return false;
        }

        string subjectUri;
        if (mapping.Value.LookupPredicate == null)
        {
            if (string.IsNullOrEmpty(quest.TargetEntityUri))
            {
                message = "퀘스트 정보가 부족해 답변을 반영할 수 없어요";
                return false;
            }
            subjectUri = quest.TargetEntityUri;
        }
        else
        {
            if (string.IsNullOrEmpty(quest.TargetValue))
            {
                message = "퀘스트 정보가 부족해 답변을 반영할 수 없어요";
                return false;
            }
            subjectUri = FindSubjectByValue(mapping.Value.LookupPredicate, quest.TargetValue);
            if (subjectUri == null)
            {
                message = "지금은 반영할 수 없어요. 나중에 다시 시도해 주세요";
                return false;
            }
        }

        var triple = new Triple
        {
            Subject = subjectUri,
            Predicate = OntologyBaseUri + mapping.Value.Predicate,
            Object = answerText,
            Datatype = "xsd:string",
            Source = "quest_answer:" + quest.Title,
            Timestamp = DateTime.UtcNow.ToString("o"),
            Synced = 0,
        };

        SQLiteManager.Instance.Connection.Insert(triple);
        TempTripleSyncManager.Instance?.SyncUnsyncedTriples();

        message = "답변이 저장됐어요! 잠시 후 퀘스트가 자동으로 완료될 수 있어요.";
        return true;
    }

    // missing_* 규칙(inference_rules.sparql)의 prod:title 문구와 매칭
    private static AnswerMapping? ResolveMapping(string title)
    {
        if (string.IsNullOrEmpty(title)) return null;

        // missing_companion: "오늘 [장소] 누구랑 갔어?" → targetEntity = Location
        if (title.EndsWith("누구랑 갔어?"))
            return new AnswerMapping { Predicate = "companion", LookupPredicate = null };

        // missing_sleep_cause: "어젯밤 잠이 잘 안 왔어? 이유가 있었어?" → targetEntity = SleepData
        if (title == "어젯밤 잠이 잘 안 왔어? 이유가 있었어?")
            return new AnswerMapping { Predicate = "cause", LookupPredicate = null };

        // missing_event_review: "[eventTitle] 어땠어?" → targetEntity = CalendarEvent
        if (title.EndsWith("어땠어?"))
            return new AnswerMapping { Predicate = "review", LookupPredicate = null };

        // stress_music_pattern / complete_stress_reason: "오늘 힘든 일 있었어?" → targetEntity = Activity/MusicListening
        if (title == "오늘 힘든 일 있었어?")
            return new AnswerMapping { Predicate = "reason", LookupPredicate = null };

        // missing_emotion: "오늘 [activityType] 어떤 기분이었어?" → targetValue = activityType
        if (title.EndsWith("어떤 기분이었어?"))
            return new AnswerMapping { Predicate = "emotion", LookupPredicate = "activityType" };

        // missing_purpose: "[placeName]에 자주 가는 이유가 있어?" → targetValue = placeName
        if (title.EndsWith("자주 가는 이유가 있어?"))
            return new AnswerMapping { Predicate = "purpose", LookupPredicate = "placeName" };

        // missing_music_mood: "요즘 [genre] 음악 자주 듣네, 어떤 기분일 때 들어?" → targetValue = genre
        if (title.EndsWith("어떤 기분일 때 들어?"))
            return new AnswerMapping { Predicate = "mood", LookupPredicate = "genre" };

        return null;
    }

    // 로컬 SQLite에서 (?, lookupPredicate, value)인 트리플의 subject를 찾는다.
    private static string FindSubjectByValue(string lookupPredicate, string value)
    {
        string predicateUri = OntologyBaseUri + lookupPredicate;
        var match = SQLiteManager.Instance.Connection
            .Table<Triple>()
            .FirstOrDefault(t => t.Predicate == predicateUri && t.Object == value);
        return match?.Subject;
    }
}
