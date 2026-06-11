using NUnit.Framework;
using OntologyMetaverse.DataCollection.Weather;

namespace OntologyMetaverse.Tests.Editor
{
    public class WeatherConditionTests
    {
        [TestCase(1, 0, "rain")]
        [TestCase(2, 0, "sleet")]
        [TestCase(3, 0, "snow")]
        [TestCase(4, 0, "rain")]
        [TestCase(5, 0, "drizzle")]
        [TestCase(6, 0, "sleet")]
        [TestCase(7, 0, "snow")]
        public void Map_PrecipitationType_TakesPriorityOverSky(int pty, int sky, string expected)
        {
            // 강수가 있으면 SKY 값과 무관하게 강수 형태가 우선
            Assert.AreEqual(expected, WeatherCondition.Map(pty, sky));
            Assert.AreEqual(expected, WeatherCondition.Map(pty, sky: 4));
        }

        [TestCase(0, 1, "clear")]
        [TestCase(0, 3, "cloudy")]
        [TestCase(0, 4, "overcast")]
        public void Map_NoPrecipitation_FallsBackToSky(int pty, int sky, string expected)
        {
            Assert.AreEqual(expected, WeatherCondition.Map(pty, sky));
        }

        [Test]
        public void Map_NoPrecipitationAndNoSkyInfo_DefaultsToClear()
        {
            // 초단기실황 API는 SKY 미제공 → PTY=0이면 "clear"
            Assert.AreEqual("clear", WeatherCondition.Map(pty: 0, sky: 0));
        }

        [Test]
        public void Map_NoPrecipitationAndUnmappedSky_DefaultsToClear()
        {
            // PTY=0이면 SKY 값과 무관하게 "clear" (Rule 7은 "rain"만 검사하므로 안전)
            Assert.AreEqual("clear", WeatherCondition.Map(pty: 0, sky: 99));
        }

        [Test]
        public void Map_UnmappedPtyAndSky_ReturnsUnknown()
        {
            // PTY가 0~7 범위를 벗어난 미정의 값이고 SKY도 매핑 안 되면 "unknown"
            Assert.AreEqual("unknown", WeatherCondition.Map(pty: 99, sky: 99));
        }
    }
}
