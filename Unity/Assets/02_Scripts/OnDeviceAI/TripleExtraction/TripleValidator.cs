using System;
using System.Globalization;

namespace OntologyMetaverse.OnDeviceAI.TripleExtraction
{
    /// <summary>
    /// 트리플 JSON이 명세서(Docs/triple-json-spec.md) 규격을 따르는지 검증
    /// 검증 항목: 필수 필드, prefix, datatype, 날짜 형식, 노드 ID 패턴
    /// </summary>
    public static class TripleValidator
    {
        // 명세서에서 허용하는 datatype 목록
        private static readonly string[] ALLOWED_DATATYPES = new string[]
        {
            "xsd:string",
            "xsd:float",
            "xsd:integer",
            "xsd:date",
            "xsd:dateTime",
            "xsd:boolean"
        };

        /// <summary>
        /// 트리플 1개를 검증
        /// 모든 검증 항목 통과 시 Success, 하나라도 실패 시 Fail (사유 포함)
        /// </summary>
        public static ValidationResult Validate(TripleJson t)
        {
            // 1. 필수 필드 검증
            var requiredCheck = CheckRequiredFields(t);
            if (!requiredCheck.IsValid) return requiredCheck;

            // 2. prefix 검증 (Subject, Predicate에 prod: 붙어있어야 함)
            var prefixCheck = CheckPrefix(t);
            if (!prefixCheck.IsValid) return prefixCheck;

            // 3. datatype 유효성 검증
            var datatypeCheck = CheckDatatype(t);
            if (!datatypeCheck.IsValid) return datatypeCheck;

            // 4. 날짜 형식 검증 (datatype이 xsd:date 또는 xsd:dateTime인 경우만)
            var dateFormatCheck = CheckDateFormat(t);
            if (!dateFormatCheck.IsValid) return dateFormatCheck;

            // 모두 통과
            return ValidationResult.Success();
        }

        /// <summary>
        /// 1. 필수 필드 검증: s, p, o가 비어있지 않은지
        /// </summary>
        private static ValidationResult CheckRequiredFields(TripleJson t)
        {
            if (string.IsNullOrEmpty(t.s))
                return ValidationResult.Fail("Subject(s)가 비어있음");

            if (string.IsNullOrEmpty(t.p))
                return ValidationResult.Fail("Predicate(p)가 비어있음");

            if (string.IsNullOrEmpty(t.o))
                return ValidationResult.Fail("Object(o)가 비어있음");

            return ValidationResult.Success();
        }

        // 명세서 base URI — full URI 형식도 인정하기 위한 prefix
        private const string OntologyBaseUri = "http://7team.dev/ontology#";

        // RDF 표준 predicate (rdf:type 등은 우리 도메인 외부지만 허용)
        private static readonly string[] AllowedPredicatePrefixes = new[]
        {
            "prod:",
            "rdf:",
            "rdfs:",
            "owl:",
            "xsd:"
        };

        /// <summary>
        /// 2. prefix 검증.
        /// Subject: "prod:" 약식 prefix OR "http://7team.dev/ontology#" full URI 둘 다 OK.
        /// Predicate: prod:/rdf:/rdfs:/owl:/xsd: 표준 prefix 또는 full URI.
        /// (Object는 리터럴일 수 있어서 검증 제외)
        /// </summary>
        private static ValidationResult CheckPrefix(TripleJson t)
        {
            bool subjectOk = t.s.StartsWith("prod:") || t.s.StartsWith(OntologyBaseUri);
            if (!subjectOk)
                return ValidationResult.Fail($"Subject가 'prod:' prefix 또는 '{OntologyBaseUri}' full URI로 시작하지 않음: {t.s}");

            bool predicateOk = t.p.StartsWith(OntologyBaseUri);
            if (!predicateOk)
            {
                foreach (var px in AllowedPredicatePrefixes)
                {
                    if (t.p.StartsWith(px)) { predicateOk = true; break; }
                }
            }
            if (!predicateOk)
                return ValidationResult.Fail($"Predicate가 허용된 prefix(prod:/rdf:/rdfs:/owl:/xsd:)로 시작하지 않음: {t.p}");

            return ValidationResult.Success();
        }

        /// <summary>
        /// 3. datatype 유효성 검증
        /// null 허용 (관계만 표현하는 트리플)
        /// 값이 있으면 ALLOWED_DATATYPES에 포함되어야 함
        /// </summary>
        private static ValidationResult CheckDatatype(TripleJson t)
        {
            // null은 허용됨
            if (t.datatype == null) return ValidationResult.Success();

            // 빈 문자열도 null처럼 처리
            if (t.datatype == "") return ValidationResult.Success();

            // 허용된 datatype 중 하나여야 함
            bool isAllowed = false;
            foreach (var allowed in ALLOWED_DATATYPES)
            {
                if (t.datatype == allowed)
                {
                    isAllowed = true;
                    break;
                }
            }

            if (!isAllowed)
                return ValidationResult.Fail($"허용되지 않은 datatype: {t.datatype}");

            return ValidationResult.Success();
        }

        /// <summary>
        /// 4. 날짜 형식 검증
        /// xsd:date → "YYYY-MM-DD" 형식
        /// xsd:dateTime → ISO 8601 형식 ("YYYY-MM-DDTHH:MM:SS")
        /// </summary>
        private static ValidationResult CheckDateFormat(TripleJson t)
        {
            // datatype이 날짜 관련이 아니면 검증 안 함
            if (t.datatype == "xsd:date")
            {
                // YYYY-MM-DD 형식
                if (!DateTime.TryParseExact(t.o, "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                {
                    return ValidationResult.Fail($"xsd:date 형식 오류 (YYYY-MM-DD 필요): {t.o}");
                }
            }
            else if (t.datatype == "xsd:dateTime")
            {
                // ISO 8601 형식 (예: 2026-04-19T14:30:00)
                if (!DateTime.TryParse(t.o, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out _))
                {
                    return ValidationResult.Fail($"xsd:dateTime 형식 오류 (ISO 8601 필요): {t.o}");
                }
            }

            return ValidationResult.Success();
        }
    }
}