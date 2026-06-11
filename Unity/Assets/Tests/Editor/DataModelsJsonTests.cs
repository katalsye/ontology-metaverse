using NUnit.Framework;
using UnityEngine;
using OntologyMetaverse.DataCollection.Health;
using OntologyMetaverse.DataCollection.Calendar;

namespace OntologyMetaverse.Tests.Editor
{
    /// <summary>
    /// HealthDataModels / CalendarDataModels의 JsonUtility 역직렬화 검증.
    /// Java/Kotlin 쪽 JSON과 필드명이 어긋나면(특히 case-sensitive) 여기서 0/null로 드러남.
    /// </summary>
    public class DataModelsJsonTests
    {
        // ── Health: Sleep ──────────────────────────────────────

        [Test]
        public void SleepRecordsJson_ParsesRecordsArray()
        {
            string json = "{\"records\":[{\"durationHours\":7.5,\"quality\":80,\"deepSleepRatio\":0.25," +
                           "\"startTime\":\"2026-06-06T22:00:00Z\",\"endTime\":\"2026-06-07T06:00:00Z\"}]}";

            var result = JsonUtility.FromJson<SleepRecordsJson>(json);

            Assert.AreEqual(1, result.records.Count);
            var record = result.records[0];
            Assert.AreEqual(7.5f, record.durationHours);
            Assert.AreEqual(80, record.quality);
            Assert.AreEqual(0.25f, record.deepSleepRatio);
            Assert.AreEqual("2026-06-06T22:00:00Z", record.startTime);
            Assert.AreEqual("2026-06-07T06:00:00Z", record.endTime);
        }

        [Test]
        public void SleepRecordsJson_EmptyRecords_ReturnsEmptyList()
        {
            string json = "{\"records\":[]}";
            var result = JsonUtility.FromJson<SleepRecordsJson>(json);
            Assert.AreEqual(0, result.records.Count);
        }

        // ── Health: Steps ──────────────────────────────────────

        [Test]
        public void StepsResultJson_ParsesTotalSteps()
        {
            string json = "{\"totalSteps\":2800,\"startTime\":\"2026-06-06T00:00:00Z\",\"endTime\":\"2026-06-06T23:59:59Z\"}";

            var result = JsonUtility.FromJson<StepsResultJson>(json);

            Assert.AreEqual(2800L, result.totalSteps);
            Assert.AreEqual("2026-06-06T00:00:00Z", result.startTime);
            Assert.AreEqual("2026-06-06T23:59:59Z", result.endTime);
        }

        // ── Health: HeartRate / HRV ─────────────────────────────

        [Test]
        public void HeartRateResultJson_ParsesAvgBpmAndSampleCount()
        {
            string json = "{\"avgBpm\":72.5,\"sampleCount\":42}";

            var result = JsonUtility.FromJson<HeartRateResultJson>(json);

            Assert.AreEqual(72.5f, result.avgBpm);
            Assert.AreEqual(42, result.sampleCount);
        }

        [Test]
        public void HrvResultJson_ParsesAvgRmssdAndSampleCount()
        {
            string json = "{\"avgRmssd\":42.5,\"sampleCount\":12}";

            var result = JsonUtility.FromJson<HrvResultJson>(json);

            Assert.AreEqual(42.5f, result.avgRmssd);
            Assert.AreEqual(12, result.sampleCount);
        }

        // ── Calendar ────────────────────────────────────────────

        [Test]
        public void CalendarEventsJson_ParsesEventsArray()
        {
            string json = "{\"events\":[{\"event_id\":1234,\"title\":\"팀 회의\"," +
                           "\"startTime\":\"2026-06-08T10:00:00Z\",\"endTime\":\"2026-06-08T11:00:00Z\"," +
                           "\"isRecurring\":true,\"location\":\"회의실 A\",\"calendarName\":\"업무\"}],\"error\":null}";

            var result = JsonUtility.FromJson<CalendarEventsJson>(json);

            Assert.AreEqual(1, result.events.Count);
            var evt = result.events[0];
            Assert.AreEqual(1234L, evt.event_id);
            Assert.AreEqual("팀 회의", evt.title);
            Assert.AreEqual("2026-06-08T10:00:00Z", evt.startTime);
            Assert.AreEqual("2026-06-08T11:00:00Z", evt.endTime);
            Assert.IsTrue(evt.isRecurring);
            Assert.AreEqual("회의실 A", evt.location);
            Assert.AreEqual("업무", evt.calendarName);
            // JsonUtility는 "error":null을 C# null이 아닌 빈 문자열로 역직렬화함
            Assert.IsTrue(string.IsNullOrEmpty(result.error));
        }

        [Test]
        public void CalendarEventsJson_ErrorResponse_EventsNullErrorSet()
        {
            string json = "{\"error\":\"권한 없음\"}";

            var result = JsonUtility.FromJson<CalendarEventsJson>(json);

            // JsonUtility는 누락된 List 필드를 null이 아닌 빈 리스트로 초기화함
            Assert.AreEqual(0, result.events.Count);
            Assert.AreEqual("권한 없음", result.error);
        }

        [Test]
        public void CalendarEventRaw_IsRecurringFalse_ParsesCorrectly()
        {
            string json = "{\"event_id\":5,\"title\":\"단발 일정\"," +
                           "\"startTime\":\"2026-06-09T09:00:00Z\",\"endTime\":\"2026-06-09T10:00:00Z\"," +
                           "\"isRecurring\":false,\"location\":\"\",\"calendarName\":\"개인\"}";

            var result = JsonUtility.FromJson<CalendarEventRaw>(json);

            Assert.IsFalse(result.isRecurring);
            Assert.AreEqual(5L, result.event_id);
        }
    }
}
