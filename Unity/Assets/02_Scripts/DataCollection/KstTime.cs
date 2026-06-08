using System;
using System.Globalization;

namespace OntologyMetaverse.DataCollection
{
    /// <summary>
    /// 시각 트리플을 무성님 추론 규칙과 맞추기 위한 KST(naive) 변환 유틸.
    ///
    /// 왜 필요한가:
    ///   무성님 inference_rules.sparql의 다수 규칙이 HOURS(?t)로 시각을 추출해 판단함
    ///   (Rule 1 카페인 주/야간, Rule 5/10 오후6시+, Rule 27 야간음악, Rule P6 야행성, Rule 8 루틴).
    ///   RDFLib HOURS()는 lexical 시각을 반환 → 타임존 표기대로.
    ///
    ///   무성님 test_rules.py는 시각을 "2026-04-17T19:00:00"처럼 타임존 없는
    ///   naive 문자열로 넣고, 그 19를 "오후 7시(KST)"로 가정함.
    ///   → 즉 무성님 기준은 naive KST.
    ///
    ///   반면 우리 수집기 다수가 UTC("...Z")로 올려서 HOURS()가 9시간 어긋남.
    ///   (한국 오후 7시 방문 → UTC 10:00 → HOURS=10 → Rule 5 ?h>=18 미발동)
    ///
    /// 해결:
    ///   모든 시각 트리플 값을 ToKstNaive()로 통과 →
    ///   - UTC("...Z" / "+offset") 입력: KST(+9)로 변환 후 naive 포맷
    ///   - 이미 naive(타임존 표기 없음, 예: 기상청 weather, KST로 보낸 calendar): 그대로 유지
    ///
    /// 결과 포맷: "yyyy-MM-ddTHH:mm:ss" (타임존 표기 없음 = 무성님 test와 동일)
    /// </summary>
    public static class KstTime
    {
        private static readonly TimeSpan KstOffset = TimeSpan.FromHours(9);

        /// <summary>
        /// ISO 8601 시각 문자열 → naive KST 문자열.
        /// 타임존 표기(Z 또는 +/-offset)가 있으면 KST로 변환, 없으면(naive) 그대로.
        /// </summary>
        public static string ToKstNaive(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return NowKstNaive();

            // 이미 naive(타임존 표기 없음)면 변환하지 않고 19자리(yyyy-MM-ddTHH:mm:ss)로만 정리.
            // (weather recordedAt = 기상청 KST, calendar = Java가 KST로 보냄)
            if (!HasTimezone(iso))
            {
                return iso.Length >= 19 ? iso.Substring(0, 19) : iso;
            }

            // 타임존 있는 UTC/offset → KST(+9)로 변환 후 naive 출력
            if (DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out DateTimeOffset dto))
            {
                return dto.ToOffset(KstOffset).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            }

            // 파싱 실패 시 원본 보존 (validator가 거를 수 있음)
            return iso;
        }

        /// <summary>현재 시각을 naive KST로.</summary>
        public static string NowKstNaive()
        {
            return DateTimeOffset.UtcNow.ToOffset(KstOffset)
                .ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 타임존 표기 존재 여부. 'Z' 또는 'T' 이후의 '+'/'-' offset.
        /// </summary>
        private static bool HasTimezone(string iso)
        {
            if (iso.EndsWith("Z") || iso.EndsWith("z")) return true;
            int tIdx = iso.IndexOf('T');
            if (tIdx < 0) return false;
            // T 이후 구간에서 +/- 발견 시 offset (날짜의 - 와 구분)
            string afterT = iso.Substring(tIdx + 1);
            return afterT.Contains("+") || afterT.Contains("-");
        }
    }
}
