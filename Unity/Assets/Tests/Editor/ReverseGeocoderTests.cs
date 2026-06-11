using System.Reflection;
using NUnit.Framework;
using OntologyMetaverse.DataCollection.Geocoding;

namespace OntologyMetaverse.Tests.Editor
{
    /// <summary>
    /// ReverseGeocoder의 private static 간이 JSON 숫자 파서(ExtractDouble)와
    /// JSON escape(EscapeJson)를 리플렉션으로 직접 호출해 검증.
    /// SQLite/네트워크 의존 로직(GeocodeLatestLocation)은 EditMode에서 제외.
    /// </summary>
    public class ReverseGeocoderTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

        private static T Invoke<T>(string methodName, params object[] args)
        {
            var method = typeof(ReverseGeocoder).GetMethod(methodName, PrivateStatic);
            Assert.IsNotNull(method, $"Method not found: {methodName}");
            return (T)method.Invoke(null, args);
        }

        // ── ExtractDouble ──────────────────────────────────────

        [Test]
        public void ExtractDouble_KeyPresent_ReturnsParsedValue()
        {
            string json = "{\"lat\":37.5665,\"lng\":126.9780}";
            double result = Invoke<double>("ExtractDouble", json, "lat");
            Assert.That(result, Is.EqualTo(37.5665).Within(0.0001));
        }

        [Test]
        public void ExtractDouble_NegativeValue_ReturnsParsedValue()
        {
            string json = "{\"lat\":-12.34}";
            double result = Invoke<double>("ExtractDouble", json, "lat");
            Assert.That(result, Is.EqualTo(-12.34).Within(0.0001));
        }

        [Test]
        public void ExtractDouble_IntegerValue_ReturnsParsedValue()
        {
            string json = "{\"source_gps_id\":12}";
            double result = Invoke<double>("ExtractDouble", json, "source_gps_id");
            Assert.That(result, Is.EqualTo(12.0));
        }

        [Test]
        public void ExtractDouble_ScientificNotation_ReturnsParsedValue()
        {
            string json = "{\"val\":1.5e2}";
            double result = Invoke<double>("ExtractDouble", json, "val");
            Assert.That(result, Is.EqualTo(150.0));
        }

        [Test]
        public void ExtractDouble_KeyMissing_ReturnsZero()
        {
            string json = "{\"lat\":37.5665}";
            double result = Invoke<double>("ExtractDouble", json, "lng");
            Assert.That(result, Is.EqualTo(0.0));
        }

        [Test]
        public void ExtractDouble_ValueIsNotNumeric_ReturnsZero()
        {
            string json = "{\"lat\":\"abc\"}";
            double result = Invoke<double>("ExtractDouble", json, "lat");
            Assert.That(result, Is.EqualTo(0.0));
        }

        // ── EscapeJson ─────────────────────────────────────────

        [Test]
        public void EscapeJson_PlainString_ReturnsUnchanged()
        {
            string result = Invoke<string>("EscapeJson", "스타벅스 강남점");
            Assert.AreEqual("스타벅스 강남점", result);
        }

        [Test]
        public void EscapeJson_ContainsQuote_EscapesQuote()
        {
            string result = Invoke<string>("EscapeJson", "12\" 카페");
            Assert.AreEqual("12\\\" 카페", result);
        }

        [Test]
        public void EscapeJson_ContainsBackslash_EscapesBackslash()
        {
            string result = Invoke<string>("EscapeJson", "C:\\place");
            Assert.AreEqual("C:\\\\place", result);
        }

        [TestCase(null)]
        [TestCase("")]
        public void EscapeJson_NullOrEmpty_ReturnsEmptyString(string input)
        {
            string result = Invoke<string>("EscapeJson", new object[] { input });
            Assert.AreEqual("", result);
        }
    }
}
