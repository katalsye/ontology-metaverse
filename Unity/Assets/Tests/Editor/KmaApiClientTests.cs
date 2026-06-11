using System;
using System.Reflection;
using NUnit.Framework;
using OntologyMetaverse.DataCollection.Weather;

namespace OntologyMetaverse.Tests.Editor
{
    /// <summary>
    /// KmaApiClient의 private static JSON 파서(ParseResponse)와
    /// KmaObservation.Condition 위임을 검증. 네트워크 호출(FetchCurrentObservation)은 EditMode에서 제외.
    /// </summary>
    public class KmaApiClientTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

        private static KmaObservation ParseResponse(string json)
        {
            var method = typeof(KmaApiClient).GetMethod("ParseResponse", PrivateStatic);
            Assert.IsNotNull(method, "Method not found: ParseResponse");
            return (KmaObservation)method.Invoke(null, new object[] { json });
        }

        // ── 정상 응답 ──────────────────────────────────────────

        [Test]
        public void ParseResponse_T1HAndPty_ParsesTemperatureAndPty()
        {
            string json = "{\"response\":{\"body\":{\"items\":{\"item\":[" +
                "{\"baseDate\":\"20260606\",\"baseTime\":\"1500\",\"category\":\"T1H\",\"obsrValue\":\"23.5\"}," +
                "{\"baseDate\":\"20260606\",\"baseTime\":\"1500\",\"category\":\"PTY\",\"obsrValue\":\"0\"}" +
                "]}}}}";

            KmaObservation obs = ParseResponse(json);

            Assert.IsNotNull(obs);
            Assert.That(obs.temperature, Is.EqualTo(23.5f).Within(0.0001f));
            Assert.AreEqual(0, obs.pty);
            Assert.AreEqual(0, obs.sky);
        }

        [Test]
        public void ParseResponse_PtyRain_ParsesPtyValue()
        {
            string json = "{\"response\":{\"body\":{\"items\":{\"item\":[" +
                "{\"baseDate\":\"20260606\",\"baseTime\":\"1500\",\"category\":\"T1H\",\"obsrValue\":\"23.5\"}," +
                "{\"baseDate\":\"20260606\",\"baseTime\":\"1500\",\"category\":\"PTY\",\"obsrValue\":\"1\"}" +
                "]}}}}";

            KmaObservation obs = ParseResponse(json);

            Assert.IsNotNull(obs);
            Assert.AreEqual(1, obs.pty);
        }

        [Test]
        public void ParseResponse_FollowingItemHasBaseDateTime_ConvertsToIso8601()
        {
            string json = "{\"response\":{\"body\":{\"items\":{\"item\":[" +
                "{\"baseDate\":\"20260606\",\"baseTime\":\"1500\",\"category\":\"T1H\",\"obsrValue\":\"23.5\"}," +
                "{\"baseDate\":\"20260606\",\"baseTime\":\"1500\",\"category\":\"PTY\",\"obsrValue\":\"0\"}" +
                "]}}}}";

            KmaObservation obs = ParseResponse(json);

            Assert.AreEqual("2026-06-06T15:00:00", obs.recordedAt);
        }

        // ── baseDate/baseTime을 못 찾는 경우 (단일 item) ──────────

        [Test]
        public void ParseResponse_SingleItem_FallsBackToUtcNowForRecordedAt()
        {
            // category 뒤에 더 이상 baseDate/baseTime이 없으면 ParseResponse는
            // baseDateTime을 채우지 못하고 DateTime.UtcNow로 대체한다.
            string json = "{\"response\":{\"body\":{\"items\":{\"item\":[" +
                "{\"baseDate\":\"20260606\",\"baseTime\":\"1500\",\"category\":\"T1H\",\"obsrValue\":\"18.2\"}" +
                "]}}}}";

            DateTime before = DateTime.UtcNow;
            KmaObservation obs = ParseResponse(json);
            DateTime after = DateTime.UtcNow;

            Assert.IsNotNull(obs);
            Assert.That(obs.temperature, Is.EqualTo(18.2f).Within(0.0001f));

            DateTime recordedAt = DateTime.Parse(obs.recordedAt).ToUniversalTime();
            Assert.That(recordedAt, Is.InRange(before.AddSeconds(-1), after.AddSeconds(1)));
        }

        // ── 파싱 실패 ──────────────────────────────────────────

        [Test]
        public void ParseResponse_MissingT1H_ReturnsNull()
        {
            string json = "{\"response\":{\"body\":{\"items\":{\"item\":[" +
                "{\"baseDate\":\"20260606\",\"baseTime\":\"1500\",\"category\":\"PTY\",\"obsrValue\":\"0\"}" +
                "]}}}}";

            KmaObservation obs = ParseResponse(json);

            Assert.IsNull(obs);
        }

        [Test]
        public void ParseResponse_NonNumericT1HValue_DefaultsTemperatureToZero()
        {
            // float.TryParse는 실패 시 out 값을 0으로 설정하므로, NaN 체크를 통과해 0으로 반환됨
            string json = "{\"response\":{\"body\":{\"items\":{\"item\":[" +
                "{\"baseDate\":\"20260606\",\"baseTime\":\"1500\",\"category\":\"T1H\",\"obsrValue\":\"N/A\"}" +
                "]}}}}";

            KmaObservation obs = ParseResponse(json);

            Assert.IsNotNull(obs);
            Assert.That(obs.temperature, Is.EqualTo(0f));
        }

        // ── KmaObservation.Condition ────────────────────────────

        [TestCase(0, 0)]
        [TestCase(1, 0)]
        [TestCase(0, 4)]
        public void Condition_DelegatesToWeatherConditionMap(int pty, int sky)
        {
            var obs = new KmaObservation { pty = pty, sky = sky };

            Assert.AreEqual(WeatherCondition.Map(pty, sky), obs.Condition);
        }
    }
}
