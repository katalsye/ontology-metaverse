using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Android;
using OntologyMetaverse.DataCollection.SQLite;

namespace OntologyMetaverse.DataCollection.Calendar
{
    /// <summary>
    /// 로컬 캘린더(CalendarContract) 이벤트 수집기.
    ///
    /// 동작:
    ///   1. READ_CALENDAR 권한 요청 (첫 실행 시)
    ///   2. 지난 pastDays~미래 futureDays 범위의 이벤트를 한 번에 조회
    ///   3. event_id 기준 중복 체크 — 이미 저장된 이벤트는 스킵
    ///   4. 새 이벤트만 raw_data (type=calendar) 저장
    ///        Content: {"event_id":1234,"title":"...","startTime":"...","endTime":"...","isRecurring":true,"location":"..."}
    ///
    /// RawDataToTripleConverter.ConvertCalendar 가 이 raw_data를:
    ///   - (user, prod:hasCalendarEvent, calevt_{event_id})
    ///   - (calevt_X, prod:eventTitle, "..."^^xsd:string)
    ///   - (calevt_X, prod:startTime, "..."^^xsd:dateTime)
    ///   - (calevt_X, prod:endTime, "..."^^xsd:dateTime)
    ///   - (calevt_X, prod:isRecurring, true^^xsd:boolean)
    ///
    /// 활성화 Rule:
    ///   Rule 6-F (missing_event_review): endTime < NOW + review 없음 → "어땠어?" 퀘스트
    ///   Rule 8 (Routine):                같은 hour에 3건+ → 루틴 추론 + alarm_clock 오브젝트
    ///   Rule 29 (ScheduleOverload):      같은 날짜 5건+ → "쉬어가는 건 어때요?" + calendar_wall 오브젝트
    ///   Rule P5 (Persona:routine):       Routine 노드 2개+ → lifePattern="routine" + organized_shelf
    /// </summary>
    public class CalendarCollector : MonoBehaviour
    {
        private const string PERMISSION = "android.permission.READ_CALENDAR";

        [Header("조회 범위 (일)")]
        [Tooltip("과거 N일~미래 N일 범위의 이벤트 수집. 7~14 권장 (Rule 8/29는 주간 분석).")]
        public int pastDays = 14;
        public int futureDays = 7;

        [Header("권한 요청 자동")]
        [Tooltip("권한 없을 때 자동으로 시스템 권한 요청 팝업 띄움. 거부 시 다음 호출도 자동 요청 (사용자 짜증날 수 있어서 1회 제한).")]
        public bool autoRequestPermission = true;

        private bool _permissionAlreadyRequested = false;

        /// <summary>
        /// 한 번의 수집 사이클. BatchScheduler가 주기적으로 호출.
        /// </summary>
        public IEnumerator CollectCalendarEvents()
        {
            // 1. 권한 확인
            if (!CalendarBridge.HasPermission())
            {
                if (autoRequestPermission && !_permissionAlreadyRequested)
                {
                    Debug.Log("[CalendarCollector] READ_CALENDAR 권한 요청");
                    Permission.RequestUserPermission(PERMISSION);
                    _permissionAlreadyRequested = true;
                }
                else
                {
                    Debug.LogWarning("[CalendarCollector] READ_CALENDAR 권한 없음 → 수집 스킵");
                }
                yield break;
            }

            // 2. 캘린더 이벤트 조회
            long pastMs = -(long)pastDays * 86400L * 1000L;
            long futureMs = (long)futureDays * 86400L * 1000L;
            string json = CalendarBridge.GetEventsJson(pastMs, futureMs);

            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("[CalendarCollector] 빈 응답");
                yield break;
            }

            CalendarEventsJson parsed;
            try
            {
                parsed = JsonUtility.FromJson<CalendarEventsJson>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CalendarCollector] JSON 파싱 실패: {e.Message}\nJSON: {json}");
                yield break;
            }

            if (parsed == null)
            {
                Debug.LogWarning("[CalendarCollector] 파싱 결과 null");
                yield break;
            }

            if (!string.IsNullOrEmpty(parsed.error))
            {
                Debug.LogError($"[CalendarCollector] Java 에러: {parsed.error}");
                yield break;
            }

            if (parsed.events == null || parsed.events.Count == 0)
            {
                Debug.Log("[CalendarCollector] 이벤트 없음 (해당 기간)");
                yield break;
            }

            // 3. 중복 체크 + 저장
            int newCount = 0;
            int skipCount = 0;
            foreach (var evt in parsed.events)
            {
                if (evt.event_id <= 0)
                {
                    skipCount++;
                    continue;
                }

                if (IsAlreadySaved(evt.event_id))
                {
                    skipCount++;
                    continue;
                }

                SaveEventAsRaw(evt);
                newCount++;
            }

            Debug.Log($"[CalendarCollector] 완료: 신규 {newCount}건 저장, 중복 {skipCount}건 스킵");
        }

        // ─────────────────────────────────────────────────────
        // SQLite 헬퍼
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// 같은 event_id가 이미 raw_data(type=calendar)에 저장됐는지 확인.
        /// Content JSON의 "event_id":N 패턴 검색.
        /// </summary>
        private bool IsAlreadySaved(long eventId)
        {
            try
            {
                var conn = SQLiteManager.Instance.Connection;
                string needle = $"\"event_id\":{eventId}";
                var query = conn.Table<RawData>().Where(r => r.Type == "calendar");
                foreach (var r in query)
                {
                    if (r.Content != null && r.Content.Contains(needle)) return true;
                }
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CalendarCollector] 중복 확인 실패: {e.Message}");
                return false; // 확인 실패 시 저장 진행 (중복 < 누락 위험)
            }
        }

        private void SaveEventAsRaw(CalendarEventRaw evt)
        {
            // JSON Content 재구성 (CalendarEventsJson 통째로 저장하면 너무 큼 → 이벤트 1건만)
            string content = "{" +
                $"\"event_id\":{evt.event_id}," +
                $"\"title\":\"{EscapeJson(evt.title)}\"," +
                $"\"startTime\":\"{evt.startTime}\"," +
                $"\"endTime\":\"{evt.endTime}\"," +
                $"\"isRecurring\":{(evt.isRecurring ? "true" : "false")}," +
                $"\"location\":\"{EscapeJson(evt.location ?? "")}\"" +
                "}";

            var raw = new RawData
            {
                Type = "calendar",
                Content = content,
                Timestamp = DateTime.UtcNow.ToString("o"),
                Processed = 0
            };
            SQLiteManager.Instance.Connection.Insert(raw);

            Debug.Log($"[CalendarCollector] 저장: id={evt.event_id}, title=\"{evt.title}\", recurring={evt.isRecurring}");
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", "\\n").Replace("\r", "\\r");
        }
    }
}
