using UnityEngine;

namespace OntologyMetaverse.OnDeviceAI.TripleExtraction
{
    /// <summary>
    /// TripleValidator 단위 테스트
    /// 통과 케이스 + 다양한 실패 케이스를 검증
    /// 빈 GameObject에 부착해서 Play 시 자동 실행
    /// </summary>
    public class TripleValidatorTest : MonoBehaviour
    {
        // 통과 / 실패 카운트
        private int passedCount = 0;
        private int failedCount = 0;

        void Start()
        {
            Debug.Log("[TripleValidatorTest] === 검증기 테스트 시작 ===");

            // 1. 통과해야 하는 케이스들
            TestValid_RelationTriple();
            TestValid_StringTriple();
            TestValid_FloatTriple();
            TestValid_DateTriple();
            TestValid_DateTimeTriple();

            // 2. 실패해야 하는 케이스들
            TestInvalid_EmptySubject();
            TestInvalid_EmptyPredicate();
            TestInvalid_EmptyObject();
            TestInvalid_NoPrefixOnSubject();
            TestInvalid_NoPrefixOnPredicate();
            TestInvalid_UnknownDatatype();
            TestInvalid_WrongDateFormat();
            TestInvalid_WrongDateTimeFormat();

            // 3. 결과 요약
            Debug.Log($"[TripleValidatorTest] === 테스트 완료: {passedCount}개 통과 / {failedCount}개 실패 ===");
        }

        // ─────────────────────────────────────────────────────
        // 통과해야 하는 케이스 (Should Pass)
        // ─────────────────────────────────────────────────────

        private void TestValid_RelationTriple()
        {
            // 관계 트리플 (datatype = null)
            var t = new TripleJson
            {
                s = "prod:user_001",
                p = "prod:visited",
                o = "prod:loc_001_xyz",
                datatype = null
            };
            AssertPass(t, "관계 트리플 (datatype null)");
        }

        private void TestValid_StringTriple()
        {
            var t = new TripleJson
            {
                s = "prod:loc_001_xyz",
                p = "prod:placeName",
                o = "스타벅스 강남점",
                datatype = "xsd:string"
            };
            AssertPass(t, "문자열 트리플");
        }

        private void TestValid_FloatTriple()
        {
            var t = new TripleJson
            {
                s = "prod:sleep_001_20260419",
                p = "prod:duration",
                o = "6.5",
                datatype = "xsd:float"
            };
            AssertPass(t, "실수 트리플");
        }

        private void TestValid_DateTriple()
        {
            var t = new TripleJson
            {
                s = "prod:step_001_20260419",
                p = "prod:date",
                o = "2026-04-19",  // YYYY-MM-DD 형식
                datatype = "xsd:date"
            };
            AssertPass(t, "날짜 트리플 (xsd:date)");
        }

        private void TestValid_DateTimeTriple()
        {
            var t = new TripleJson
            {
                s = "prod:loc_001_xyz",
                p = "prod:visitTime",
                o = "2026-04-19T14:30:00",  // ISO 8601
                datatype = "xsd:dateTime"
            };
            AssertPass(t, "일시 트리플 (xsd:dateTime)");
        }

        // ─────────────────────────────────────────────────────
        // 실패해야 하는 케이스 (Should Fail)
        // ─────────────────────────────────────────────────────

        private void TestInvalid_EmptySubject()
        {
            var t = new TripleJson
            {
                s = "",  // Subject 비어있음
                p = "prod:visited",
                o = "prod:loc_001",
                datatype = null
            };
            AssertFail(t, "빈 Subject");
        }

        private void TestInvalid_EmptyPredicate()
        {
            var t = new TripleJson
            {
                s = "prod:user_001",
                p = "",  // Predicate 비어있음
                o = "prod:loc_001",
                datatype = null
            };
            AssertFail(t, "빈 Predicate");
        }

        private void TestInvalid_EmptyObject()
        {
            var t = new TripleJson
            {
                s = "prod:user_001",
                p = "prod:visited",
                o = "",  // Object 비어있음
                datatype = null
            };
            AssertFail(t, "빈 Object");
        }

        private void TestInvalid_NoPrefixOnSubject()
        {
            var t = new TripleJson
            {
                s = "user_001",  // prod: prefix 없음
                p = "prod:visited",
                o = "prod:loc_001",
                datatype = null
            };
            AssertFail(t, "Subject에 prod: prefix 없음");
        }

        private void TestInvalid_NoPrefixOnPredicate()
        {
            var t = new TripleJson
            {
                s = "prod:user_001",
                p = "visited",  // prod: prefix 없음
                o = "prod:loc_001",
                datatype = null
            };
            AssertFail(t, "Predicate에 prod: prefix 없음");
        }

        private void TestInvalid_UnknownDatatype()
        {
            var t = new TripleJson
            {
                s = "prod:user_001",
                p = "prod:age",
                o = "25",
                datatype = "xsd:double"  // 명세서에 없는 datatype
            };
            AssertFail(t, "허용되지 않은 datatype");
        }

        private void TestInvalid_WrongDateFormat()
        {
            var t = new TripleJson
            {
                s = "prod:step_001_20260419",
                p = "prod:date",
                o = "2026/04/19",  // 슬래시 → 잘못된 형식 (대시여야 함)
                datatype = "xsd:date"
            };
            AssertFail(t, "잘못된 xsd:date 형식");
        }

        private void TestInvalid_WrongDateTimeFormat()
        {
            var t = new TripleJson
            {
                s = "prod:loc_001",
                p = "prod:visitTime",
                o = "오늘 오후 2시",  // ISO 8601 아님
                datatype = "xsd:dateTime"
            };
            AssertFail(t, "잘못된 xsd:dateTime 형식");
        }

        // ─────────────────────────────────────────────────────
        // 헬퍼 메서드: 통과/실패 자동 검사 및 로그 출력
        // ─────────────────────────────────────────────────────

        /// <summary>통과해야 하는 케이스 검사</summary>
        private void AssertPass(TripleJson t, string caseName)
        {
            ValidationResult result = TripleValidator.Validate(t);
            if (result.IsValid)
            {
                Debug.Log($"[TripleValidatorTest] ✓ PASS: {caseName}");
                passedCount++;
            }
            else
            {
                Debug.LogError($"[TripleValidatorTest] ✗ FAIL: {caseName} (예상: 통과, 실제: 실패 - {result.ErrorReason})");
                failedCount++;
            }
        }

        /// <summary>실패해야 하는 케이스 검사</summary>
        private void AssertFail(TripleJson t, string caseName)
        {
            ValidationResult result = TripleValidator.Validate(t);
            if (!result.IsValid)
            {
                Debug.Log($"[TripleValidatorTest] ✓ PASS: {caseName} (실패 사유: {result.ErrorReason})");
                passedCount++;
            }
            else
            {
                Debug.LogError($"[TripleValidatorTest] ✗ FAIL: {caseName} (예상: 실패, 실제: 통과)");
                failedCount++;
            }
        }
    }
}