using NUnit.Framework;
using OntologyMetaverse.OnDeviceAI.TripleExtraction;

namespace OntologyMetaverse.Tests.Editor
{
    public class TripleValidatorTests
    {
        private static TripleJson MakeTriple(string s, string p, string o, string datatype = null)
        {
            return new TripleJson { s = s, p = p, o = o, datatype = datatype };
        }

        [Test]
        public void Validate_ValidTripleWithProdPrefix_Succeeds()
        {
            var t = MakeTriple("prod:user_001", "prod:visited", "prod:cafe_gangnam");
            var result = TripleValidator.Validate(t);
            Assert.IsTrue(result.IsValid, result.ErrorReason);
        }

        [TestCase(null, "prod:visited", "prod:cafe")]
        [TestCase("", "prod:visited", "prod:cafe")]
        [TestCase("prod:user_001", null, "prod:cafe")]
        [TestCase("prod:user_001", "", "prod:cafe")]
        [TestCase("prod:user_001", "prod:visited", null)]
        [TestCase("prod:user_001", "prod:visited", "")]
        public void Validate_MissingRequiredField_Fails(string s, string p, string o)
        {
            var t = MakeTriple(s, p, o);
            var result = TripleValidator.Validate(t);
            Assert.IsFalse(result.IsValid);
        }

        [Test]
        public void Validate_SubjectWithoutProdPrefixOrFullUri_Fails()
        {
            var t = MakeTriple("user_001", "prod:visited", "prod:cafe");
            var result = TripleValidator.Validate(t);
            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("Subject", result.ErrorReason);
        }

        [Test]
        public void Validate_SubjectWithFullOntologyUri_Succeeds()
        {
            var t = MakeTriple("http://7team.dev/ontology#user_001", "prod:visited", "prod:cafe");
            var result = TripleValidator.Validate(t);
            Assert.IsTrue(result.IsValid, result.ErrorReason);
        }

        [TestCase("rdf:type")]
        [TestCase("rdfs:label")]
        [TestCase("owl:sameAs")]
        [TestCase("xsd:string")]
        [TestCase("http://7team.dev/ontology#visited")]
        public void Validate_PredicateWithAllowedPrefix_Succeeds(string predicate)
        {
            var t = MakeTriple("prod:user_001", predicate, "prod:cafe");
            var result = TripleValidator.Validate(t);
            Assert.IsTrue(result.IsValid, result.ErrorReason);
        }

        [Test]
        public void Validate_PredicateWithDisallowedPrefix_Fails()
        {
            var t = MakeTriple("prod:user_001", "foo:visited", "prod:cafe");
            var result = TripleValidator.Validate(t);
            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("Predicate", result.ErrorReason);
        }

        [Test]
        public void Validate_NullDatatype_SkipsDatatypeAndDateChecks()
        {
            var t = MakeTriple("prod:user_001", "prod:visited", "프로그래밍 카페", null);
            var result = TripleValidator.Validate(t);
            Assert.IsTrue(result.IsValid, result.ErrorReason);
        }

        [TestCase("xsd:string")]
        [TestCase("xsd:float")]
        [TestCase("xsd:integer")]
        [TestCase("xsd:date")]
        [TestCase("xsd:dateTime")]
        [TestCase("xsd:boolean")]
        public void Validate_AllowedDatatypes_DoNotFailDatatypeCheck(string datatype)
        {
            string value = datatype switch
            {
                "xsd:date" => "2026-04-17",
                "xsd:dateTime" => "2026-04-17T19:00:00",
                _ => "value",
            };
            var t = MakeTriple("prod:user_001", "prod:walked", value, datatype);
            var result = TripleValidator.Validate(t);
            Assert.IsTrue(result.IsValid, result.ErrorReason);
        }

        [Test]
        public void Validate_DisallowedDatatype_Fails()
        {
            var t = MakeTriple("prod:user_001", "prod:walked", "2800", "xsd:double");
            var result = TripleValidator.Validate(t);
            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("datatype", result.ErrorReason);
        }

        [Test]
        public void Validate_XsdDateWithCorrectFormat_Succeeds()
        {
            var t = MakeTriple("prod:step_1", "prod:date", "2026-04-17", "xsd:date");
            var result = TripleValidator.Validate(t);
            Assert.IsTrue(result.IsValid, result.ErrorReason);
        }

        [Test]
        public void Validate_XsdDateWithWrongFormat_Fails()
        {
            var t = MakeTriple("prod:step_1", "prod:date", "2026/04/17", "xsd:date");
            var result = TripleValidator.Validate(t);
            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("xsd:date", result.ErrorReason);
        }

        [Test]
        public void Validate_XsdDateTimeWithCorrectFormat_Succeeds()
        {
            var t = MakeTriple("prod:loc_1", "prod:visitTime", "2026-04-17T19:00:00", "xsd:dateTime");
            var result = TripleValidator.Validate(t);
            Assert.IsTrue(result.IsValid, result.ErrorReason);
        }

        [Test]
        public void Validate_XsdDateTimeWithInvalidValue_Fails()
        {
            var t = MakeTriple("prod:loc_1", "prod:visitTime", "not-a-datetime", "xsd:dateTime");
            var result = TripleValidator.Validate(t);
            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("xsd:dateTime", result.ErrorReason);
        }
    }
}
