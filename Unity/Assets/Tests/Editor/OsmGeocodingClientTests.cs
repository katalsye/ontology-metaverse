using System;
using System.Reflection;
using NUnit.Framework;
using OntologyMetaverse.DataCollection.Geocoding;

namespace OntologyMetaverse.Tests.Editor
{
    /// <summary>
    /// OsmGeocodingClient의 private static 순수 로직(거리 계산, 문자열 파싱, 태그 매핑)을
    /// 리플렉션으로 직접 호출해 검증. 네트워크 호출(Fetch)은 EditMode에서 제외.
    /// </summary>
    public class OsmGeocodingClientTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

        private static T Invoke<T>(string methodName, params object[] args)
        {
            var method = typeof(OsmGeocodingClient).GetMethod(methodName, PrivateStatic);
            Assert.IsNotNull(method, $"Method not found: {methodName}");
            return (T)method.Invoke(null, args);
        }

        // ── HaversineMeters ──────────────────────────────────

        [Test]
        public void HaversineMeters_SamePoint_ReturnsZero()
        {
            double dist = Invoke<double>("HaversineMeters", 37.5665, 126.9780, 37.5665, 126.9780);
            Assert.That(dist, Is.EqualTo(0.0).Within(0.001));
        }

        [Test]
        public void HaversineMeters_OneDegreeLatitude_MatchesKnownDistance()
        {
            double dist = Invoke<double>("HaversineMeters", 37.0, 127.0, 38.0, 127.0);
            Assert.That(dist, Is.EqualTo(111194.93).Within(0.1));
        }

        [Test]
        public void HaversineMeters_ShortDistance_MatchesKnownDistance()
        {
            double dist = Invoke<double>("HaversineMeters", 37.4979, 127.0276, 37.4990, 127.0276);
            Assert.That(dist, Is.EqualTo(122.31).Within(0.1));
        }

        // ── FirstSegment ──────────────────────────────────────

        [Test]
        public void FirstSegment_WithComma_ReturnsTrimmedFirstPart()
        {
            string result = Invoke<string>("FirstSegment", "서울 강남구 테헤란로 152, 대한민국");
            Assert.AreEqual("서울 강남구 테헤란로 152", result);
        }

        [Test]
        public void FirstSegment_WithoutComma_ReturnsTrimmedWhole()
        {
            string result = Invoke<string>("FirstSegment", "  대한민국  ");
            Assert.AreEqual("대한민국", result);
        }

        [TestCase(null)]
        [TestCase("")]
        public void FirstSegment_NullOrEmpty_ReturnsNull(string input)
        {
            string result = Invoke<string>("FirstSegment", new object[] { input });
            Assert.IsNull(result);
        }

        // ── DefaultName ──────────────────────────────────────

        [TestCase("cafe", "카페")]
        [TestCase("restaurant", "음식점")]
        [TestCase("library", "도서관")]
        [TestCase("gym", "헬스장")]
        [TestCase("park", "공원")]
        [TestCase("unknown_type", "장소")]
        public void DefaultName_MapsTypeToKoreanLabel(string type, string expected)
        {
            string result = Invoke<string>("DefaultName", type);
            Assert.AreEqual(expected, result);
        }

        // ── DetermineType (private nested OverpassTags via reflection) ──

        private static object MakeTags(string amenity, string leisure, string name = null)
        {
            Type tagsType = typeof(OsmGeocodingClient).GetNestedType("OverpassTags", BindingFlags.NonPublic);
            Assert.IsNotNull(tagsType, "OverpassTags nested type not found");
            object tags = Activator.CreateInstance(tagsType, nonPublic: true);
            tagsType.GetField("amenity").SetValue(tags, amenity);
            tagsType.GetField("leisure").SetValue(tags, leisure);
            tagsType.GetField("name").SetValue(tags, name);
            return tags;
        }

        [TestCase("cafe", null, "cafe")]
        [TestCase("restaurant", null, "restaurant")]
        [TestCase("library", null, "library")]
        [TestCase("gym", null, "gym")]
        [TestCase(null, "fitness_centre", "gym")]
        [TestCase(null, "park", "park")]
        [TestCase("shop", null, null)]
        [TestCase(null, null, null)]
        public void DetermineType_MapsOsmTagsToPlaceType(string amenity, string leisure, string expected)
        {
            object tags = MakeTags(amenity, leisure);
            string result = Invoke<string>("DetermineType", tags);
            Assert.AreEqual(expected, result);
        }

        [Test]
        public void DetermineType_AmenityTakesPriorityOverLeisure()
        {
            object tags = MakeTags("gym", "park");
            string result = Invoke<string>("DetermineType", tags);
            Assert.AreEqual("gym", result);
        }
    }
}
