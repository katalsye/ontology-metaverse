using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using OntologyMetaverse.DataCollection;

namespace OntologyMetaverse.Tests.Editor
{
    /// <summary>
    /// RawDataToTripleConverter의 private 인스턴스 메서드 NormalizeUriString을
    /// 리플렉션으로 직접 호출해 검증. SQLite/Firestore I/O는 EditMode에서 제외.
    /// </summary>
    public class RawDataToTripleConverterTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        private static string Normalize(string uri)
        {
            var go = new GameObject("RawDataToTripleConverterTest");
            try
            {
                var converter = go.AddComponent<RawDataToTripleConverter>();
                var method = typeof(RawDataToTripleConverter).GetMethod("NormalizeUriString", PrivateInstance);
                Assert.IsNotNull(method, "Method not found: NormalizeUriString");
                return (string)method.Invoke(converter, new object[] { uri });
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void NormalizeUriString_MissingSlash_InsertsSlash()
        {
            string result = Normalize("http://7team.devontology#user_001");
            Assert.AreEqual("http://7team.dev/ontology#user_001", result);
        }

        [Test]
        public void NormalizeUriString_ProdPrefix_ExpandsToFullUri()
        {
            string result = Normalize("prod:hasLocation");
            Assert.AreEqual("http://7team.dev/ontology#hasLocation", result);
        }

        [Test]
        public void NormalizeUriString_AlreadyNormalized_Unchanged()
        {
            string result = Normalize("http://7team.dev/ontology#user_001");
            Assert.AreEqual("http://7team.dev/ontology#user_001", result);
        }

        [Test]
        public void NormalizeUriString_PlainLiteralValue_Unchanged()
        {
            // 트리플의 object는 URI가 아닌 일반 리터럴(예: "7.50")일 수 있음 → 그대로 통과
            string result = Normalize("7.50");
            Assert.AreEqual("7.50", result);
        }

        [TestCase(null)]
        [TestCase("")]
        public void NormalizeUriString_NullOrEmpty_ReturnsAsIs(string input)
        {
            string result = Normalize(input);
            Assert.AreEqual(input, result);
        }
    }
}
