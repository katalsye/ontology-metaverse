using System.Reflection;
using NUnit.Framework;
using OntologyMetaverse.DataCollection.Geocoding;

namespace OntologyMetaverse.Tests.Editor
{
    /// <summary>
    /// KakaoLocalClient의 private static 간이 JSON 파서 / 검증 로직을
    /// 리플렉션으로 직접 호출해 검증. 네트워크 호출(Fetch)은 EditMode에서 제외.
    /// </summary>
    public class KakaoLocalClientTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

        private static T Invoke<T>(string methodName, params object[] args)
        {
            var method = typeof(KakaoLocalClient).GetMethod(methodName, PrivateStatic);
            Assert.IsNotNull(method, $"Method not found: {methodName}");
            return (T)method.Invoke(null, args);
        }

        // ── ExtractJsonStringField ────────────────────────────

        [Test]
        public void ExtractJsonStringField_NormalField_ReturnsValue()
        {
            string json = "{\"place_name\":\"스타벅스 강남점\",\"distance\":\"42\"}";
            string result = Invoke<string>("ExtractJsonStringField", json, "place_name");
            Assert.AreEqual("스타벅스 강남점", result);
        }

        [Test]
        public void ExtractJsonStringField_FieldMissing_ReturnsNull()
        {
            string json = "{\"place_name\":\"스타벅스\"}";
            string result = Invoke<string>("ExtractJsonStringField", json, "category_name");
            Assert.IsNull(result);
        }

        [Test]
        public void ExtractJsonStringField_NullValue_ReturnsNull()
        {
            string json = "{\"address_name\":null}";
            string result = Invoke<string>("ExtractJsonStringField", json, "address_name");
            Assert.IsNull(result);
        }

        [Test]
        public void ExtractJsonStringField_EmptyValue_ReturnsNull()
        {
            string json = "{\"place_name\":\"\"}";
            string result = Invoke<string>("ExtractJsonStringField", json, "place_name");
            Assert.IsNull(result);
        }

        [Test]
        public void ExtractJsonStringField_WhitespaceBeforeValue_ReturnsValue()
        {
            string json = "{\"place_name\":   \"스타벅스\"}";
            string result = Invoke<string>("ExtractJsonStringField", json, "place_name");
            Assert.AreEqual("스타벅스", result);
        }

        // ── ExtractDistance ────────────────────────────────────

        [Test]
        public void ExtractDistance_ValidNumber_ReturnsParsedInt()
        {
            string json = "{\"distance\":\"152\"}";
            int result = Invoke<int>("ExtractDistance", json);
            Assert.AreEqual(152, result);
        }

        [Test]
        public void ExtractDistance_FieldMissing_ReturnsMaxValue()
        {
            string json = "{\"place_name\":\"스타벅스\"}";
            int result = Invoke<int>("ExtractDistance", json);
            Assert.AreEqual(int.MaxValue, result);
        }

        [Test]
        public void ExtractDistance_NonNumericValue_ReturnsMaxValue()
        {
            string json = "{\"distance\":\"abc\"}";
            int result = Invoke<int>("ExtractDistance", json);
            Assert.AreEqual(int.MaxValue, result);
        }

        // ── PassesVerify ───────────────────────────────────────

        [Test]
        public void PassesVerify_PlaceNameContainsKeyword_ReturnsTrue()
        {
            bool result = Invoke<bool>("PassesVerify", "강남 헬스장", "체육시설", new[] { "헬스", "피트니스" });
            Assert.IsTrue(result);
        }

        [Test]
        public void PassesVerify_CategoryNameContainsKeyword_ReturnsTrue()
        {
            bool result = Invoke<bool>("PassesVerify", "강남점", "스포츠 > 피트니스", new[] { "헬스", "피트니스" });
            Assert.IsTrue(result);
        }

        [Test]
        public void PassesVerify_NoMatch_ReturnsFalse()
        {
            bool result = Invoke<bool>("PassesVerify", "강남 카페", "음식점 > 카페", new[] { "헬스", "피트니스" });
            Assert.IsFalse(result);
        }

        [Test]
        public void PassesVerify_CaseInsensitive_ReturnsTrue()
        {
            bool result = Invoke<bool>("PassesVerify", "ABC GYM", "Sports", new[] { "gym" });
            Assert.IsTrue(result);
        }

        [Test]
        public void PassesVerify_NullCategoryName_HandlesGracefully()
        {
            bool result = Invoke<bool>("PassesVerify", "강남 헬스장", null, new[] { "헬스" });
            Assert.IsTrue(result);
        }

        // ── FindMatchingBraceEnd ────────────────────────────────

        [Test]
        public void FindMatchingBraceEnd_NestedBraces_ReturnsIndexAfterClosingBrace()
        {
            string json = "XX{\"a\":{\"b\":1}}YY";
            int result = Invoke<int>("FindMatchingBraceEnd", json, 0);
            Assert.AreEqual(json.IndexOf("YY"), result);
        }

        [Test]
        public void FindMatchingBraceEnd_NoBraceAfterStart_ReturnsMinusOne()
        {
            string json = "no braces here";
            int result = Invoke<int>("FindMatchingBraceEnd", json, 0);
            Assert.AreEqual(-1, result);
        }

        [Test]
        public void FindMatchingBraceEnd_UnbalancedBraces_ReturnsMinusOne()
        {
            string json = "{\"a\":{\"b\":1}";
            int result = Invoke<int>("FindMatchingBraceEnd", json, 0);
            Assert.AreEqual(-1, result);
        }

        // ── ExtractFirstAddressName ─────────────────────────────

        [Test]
        public void ExtractFirstAddressName_RoadAddressPresent_PrefersRoadAddressName()
        {
            string json = "{\"documents\":[{\"address\":{\"address_name\":\"지번주소\"}," +
                           "\"road_address\":{\"address_name\":\"도로명주소\"}}]}";
            string result = Invoke<string>("ExtractFirstAddressName", json);
            Assert.AreEqual("도로명주소", result);
        }

        [Test]
        public void ExtractFirstAddressName_NoRoadAddress_FallsBackToAddressName()
        {
            string json = "{\"documents\":[{\"address\":{\"address_name\":\"지번주소\"}}]}";
            string result = Invoke<string>("ExtractFirstAddressName", json);
            Assert.AreEqual("지번주소", result);
        }

        [Test]
        public void ExtractFirstAddressName_NoAddressNameAnywhere_ReturnsNull()
        {
            string json = "{\"documents\":[]}";
            string result = Invoke<string>("ExtractFirstAddressName", json);
            Assert.IsNull(result);
        }
    }
}
