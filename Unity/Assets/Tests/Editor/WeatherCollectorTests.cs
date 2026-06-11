using System.Reflection;
using NUnit.Framework;
using OntologyMetaverse.DataCollection.Weather;

namespace OntologyMetaverse.Tests.Editor
{
    /// <summary>
    /// WeatherCollector의 private static 간이 JSON 숫자 파서(ExtractDouble)를
    /// 리플렉션으로 직접 호출해 검증. SQLite/네트워크 의존 로직은 EditMode에서 제외.
    /// </summary>
    public class WeatherCollectorTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

        private static T Invoke<T>(string methodName, params object[] args)
        {
            var method = typeof(WeatherCollector).GetMethod(methodName, PrivateStatic);
            Assert.IsNotNull(method, $"Method not found: {methodName}");
            return (T)method.Invoke(null, args);
        }

        [Test]
        public void ExtractDouble_KeyPresent_ReturnsParsedValue()
        {
            string json = "{\"lat\":37.5665,\"lng\":126.9780}";
            double result = Invoke<double>("ExtractDouble", json, "lng");
            Assert.That(result, Is.EqualTo(126.9780).Within(0.0001));
        }

        [Test]
        public void ExtractDouble_NegativeValue_ReturnsParsedValue()
        {
            string json = "{\"lat\":-12.34}";
            double result = Invoke<double>("ExtractDouble", json, "lat");
            Assert.That(result, Is.EqualTo(-12.34).Within(0.0001));
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
    }
}
