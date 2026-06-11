using System.Text.RegularExpressions;
using NUnit.Framework;
using OntologyMetaverse.DataCollection.Spotify;

namespace OntologyMetaverse.Tests.Editor
{
    public class SpotifyPKCETests
    {
        private static readonly Regex Base64Url = new Regex(@"^[A-Za-z0-9\-_]+$");

        [Test]
        public void GenerateCodeVerifier_Has43UrlSafeChars()
        {
            string verifier = SpotifyPKCE.GenerateCodeVerifier();
            Assert.AreEqual(43, verifier.Length);
            Assert.IsTrue(Base64Url.IsMatch(verifier), $"Not URL-safe base64: {verifier}");
        }

        [Test]
        public void GenerateCodeVerifier_IsRandomAcrossCalls()
        {
            string a = SpotifyPKCE.GenerateCodeVerifier();
            string b = SpotifyPKCE.GenerateCodeVerifier();
            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void GenerateCodeChallenge_MatchesKnownSha256Base64Url()
        {
            string challenge = SpotifyPKCE.GenerateCodeChallenge("test-code-verifier-1234567890");
            Assert.AreEqual("zP9WD-aC0tyxQ8j2yFOsEM4jCFnCcGRqEPmHx5qiO70", challenge);
        }

        [Test]
        public void GenerateCodeChallenge_IsDeterministicForSameVerifier()
        {
            string verifier = SpotifyPKCE.GenerateCodeVerifier();
            string challengeA = SpotifyPKCE.GenerateCodeChallenge(verifier);
            string challengeB = SpotifyPKCE.GenerateCodeChallenge(verifier);
            Assert.AreEqual(challengeA, challengeB);
            Assert.IsTrue(Base64Url.IsMatch(challengeA), $"Not URL-safe base64: {challengeA}");
        }

        [Test]
        public void GenerateState_Has22UrlSafeChars()
        {
            string state = SpotifyPKCE.GenerateState();
            Assert.AreEqual(22, state.Length);
            Assert.IsTrue(Base64Url.IsMatch(state), $"Not URL-safe base64: {state}");
        }

        [Test]
        public void GenerateState_IsRandomAcrossCalls()
        {
            string a = SpotifyPKCE.GenerateState();
            string b = SpotifyPKCE.GenerateState();
            Assert.AreNotEqual(a, b);
        }
    }
}
