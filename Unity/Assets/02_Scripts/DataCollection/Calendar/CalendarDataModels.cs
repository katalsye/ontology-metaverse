using System;
using System.Collections.Generic;

namespace OntologyMetaverse.DataCollection.Calendar
{
    /// <summary>
    /// CalendarHelper.java 가 반환하는 JSON과 1:1 매칭.
    /// JsonUtility 호환: [Serializable] + public field, snake_case도 그대로 (JsonUtility는 case-sensitive).
    /// </summary>
    public static class CalendarDataModels { }

    [Serializable]
    public class CalendarEventsJson
    {
        public List<CalendarEventRaw> events;
        public string error; // 에러 시 채워짐, 정상이면 null
    }

    [Serializable]
    public class CalendarEventRaw
    {
        public long event_id;        // CalendarContract.Events._ID — URI 생성에 사용
        public string title;          // → prod:eventTitle
        public string startTime;      // ISO 8601 UTC → prod:startTime
        public string endTime;        // ISO 8601 UTC → prod:endTime
        public bool isRecurring;      // RRULE 존재 여부 → prod:isRecurring
        public string location;       // (옵션, 향후 prod:eventLocation 추가 가능)
        public string calendarName;   // 계정 이름 (디버그용)
    }
}
