using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using OntologyMetaverse.DataCollection.Spotify;

namespace OntologyMetaverse.Tests.Editor
{
    /// <summary>
    /// LastFmGenreClient의 HasKey 및 private static PickGenre(태그 우선순위 매칭)를 검증.
    /// 네트워크 호출(GetArtistGenre)은 EditMode에서 제외.
    /// </summary>
    public class LastFmGenreClientTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

        private static Type TagType =>
            typeof(LastFmGenreClient).GetNestedType("LastFmTag", BindingFlags.NonPublic);

        private static object MakeTagList(params (string name, int count)[] tags)
        {
            Type tagType = TagType;
            Assert.IsNotNull(tagType, "Nested type not found: LastFmTag");

            Type listType = typeof(List<>).MakeGenericType(tagType);
            object list = Activator.CreateInstance(listType);
            MethodInfo add = listType.GetMethod("Add");

            foreach (var (name, count) in tags)
            {
                object tag = Activator.CreateInstance(tagType, nonPublic: true);
                tagType.GetField("name").SetValue(tag, name);
                tagType.GetField("count").SetValue(tag, count);
                add.Invoke(list, new object[] { tag });
            }

            return list;
        }

        private static string PickGenre(object tagList)
        {
            var method = typeof(LastFmGenreClient).GetMethod("PickGenre", PrivateStatic);
            Assert.IsNotNull(method, "Method not found: PickGenre");
            return (string)method.Invoke(null, new object[] { tagList });
        }

        // ── HasKey ─────────────────────────────────────────────

        [TestCase(null, false)]
        [TestCase("", false)]
        [TestCase("   ", false)]
        [TestCase("abc123", true)]
        public void HasKey_ReflectsApiKeyPresence(string apiKey, bool expected)
        {
            var client = new LastFmGenreClient(apiKey);

            Assert.AreEqual(expected, client.HasKey);
        }

        // ── PickGenre: 빈 입력 ────────────────────────────────

        [Test]
        public void PickGenre_NullList_ReturnsEmptyString()
        {
            Assert.AreEqual("", PickGenre(null));
        }

        [Test]
        public void PickGenre_EmptyList_ReturnsEmptyString()
        {
            Assert.AreEqual("", PickGenre(MakeTagList()));
        }

        // ── PickGenre: 무성님 규칙 키워드 우선 매칭 ──────────────

        [Test]
        public void PickGenre_RuleKeywordTag_ReturnsThatTagOverMorePopularNonMatch()
        {
            // "seen live"가 가장 인기 있어도 규칙 키워드(pop)를 포함하는 "j-pop"을 우선 선택
            object tags = MakeTagList(("seen live", 200), ("j-pop", 100), ("vocaloid", 50));

            Assert.AreEqual("j-pop", PickGenre(tags));
        }

        [Test]
        public void PickGenre_KeywordMatchIsCaseInsensitive_ButPreservesOriginalCasing()
        {
            object tags = MakeTagList(("Hard Rock", 100));

            Assert.AreEqual("Hard Rock", PickGenre(tags));
        }

        // ── PickGenre: 키워드 미매칭 시 노이즈 아닌 1순위 ────────

        [Test]
        public void PickGenre_NoKeywordMatch_ReturnsFirstNonNoiseTag()
        {
            object tags = MakeTagList(("seen live", 200), ("vocaloid", 100), ("favorite", 50));

            Assert.AreEqual("vocaloid", PickGenre(tags));
        }

        [Test]
        public void PickGenre_NoiseTagComparisonIsCaseInsensitive()
        {
            object tags = MakeTagList(("Seen Live", 200), ("vocaloid", 100));

            Assert.AreEqual("vocaloid", PickGenre(tags));
        }

        [Test]
        public void PickGenre_AllNoiseTags_ReturnsEmptyString()
        {
            object tags = MakeTagList(("seen live", 200), ("favorite", 100));

            Assert.AreEqual("", PickGenre(tags));
        }

        [Test]
        public void PickGenre_SkipsTagsWithNullOrWhitespaceName()
        {
            object tags = MakeTagList((null, 200), ("   ", 150), ("vocaloid", 100));

            Assert.AreEqual("vocaloid", PickGenre(tags));
        }
    }
}
