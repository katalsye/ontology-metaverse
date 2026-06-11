using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OntologyMetaverse.DataCollection;

namespace OntologyMetaverse.Tests.Editor
{
    public class KstTimeTests
    {
        [Test]
        public void ToKstNaive_UtcZ_ConvertsToKstPlus9()
        {
            Assert.AreEqual("2026-04-17T19:00:00", KstTime.ToKstNaive("2026-04-17T10:00:00Z"));
        }

        [Test]
        public void ToKstNaive_UtcZ_RollsOverToNextDay()
        {
            Assert.AreEqual("2026-04-18T01:30:00", KstTime.ToKstNaive("2026-04-17T16:30:00Z"));
        }

        [Test]
        public void ToKstNaive_NegativeOffset_ConvertsToKst()
        {
            // 2026-04-17T01:00:00-05:00 == 2026-04-17T06:00:00Z == 2026-04-17T15:00:00 KST
            Assert.AreEqual("2026-04-17T15:00:00", KstTime.ToKstNaive("2026-04-17T01:00:00-05:00"));
        }

        [Test]
        public void ToKstNaive_AlreadyKstOffset_Unchanged()
        {
            Assert.AreEqual("2026-04-17T19:00:00", KstTime.ToKstNaive("2026-04-17T19:00:00+09:00"));
        }

        [Test]
        public void ToKstNaive_NaiveString_PassedThroughUnchanged()
        {
            Assert.AreEqual("2026-04-17T19:00:00", KstTime.ToKstNaive("2026-04-17T19:00:00"));
        }

        [Test]
        public void ToKstNaive_NaiveStringWithFraction_TruncatedTo19Chars()
        {
            Assert.AreEqual("2026-04-17T19:00:00", KstTime.ToKstNaive("2026-04-17T19:00:00.123456"));
        }

        [TestCase(null)]
        [TestCase("")]
        public void ToKstNaive_NullOrEmpty_ReturnsNowKstNaiveFormat(string input)
        {
            string result = KstTime.ToKstNaive(input);
            Assert.IsTrue(Regex.IsMatch(result, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}$"),
                $"Expected naive KST format but got: {result}");
        }

        [Test]
        public void NowKstNaive_MatchesExpectedFormatAndIsCloseToUtcPlus9()
        {
            string result = KstTime.NowKstNaive();
            Assert.IsTrue(Regex.IsMatch(result, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}$"),
                $"Expected naive KST format but got: {result}");

            DateTime parsed = DateTime.ParseExact(result, "yyyy-MM-ddTHH:mm:ss", null);
            DateTime expectedKst = DateTime.UtcNow.AddHours(9);
            Assert.That((parsed - expectedKst).TotalSeconds, Is.LessThan(5).And.GreaterThan(-5));
        }
    }
}
