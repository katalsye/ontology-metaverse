using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace OntologyMetaverse.DataCollection.Weather
{
    /// <summary>
    /// 기상청 단기예보 조회서비스 - 초단기실황(getUltraSrtNcst).
    ///
    /// API:
    ///   GET https://apis.data.go.kr/1360000/VilageFcstInfoService_2.0/getUltraSrtNcst
    ///   파라미터:
    ///     serviceKey   : 공공데이터포털 발급 인증키 (URL-encoded)
    ///     pageNo       : 1
    ///     numOfRows    : 10
    ///     dataType     : JSON
    ///     base_date    : YYYYMMDD (조회 기준 일자)
    ///     base_time    : HHMM (정시. 매 시각 40분 이후 갱신됨)
    ///     nx, ny       : 격자 좌표 (정수)
    ///
    /// 응답 카테고리 (초단기실황):
    ///   T1H = 기온 (°C, float)
    ///   PTY = 강수 형태 (0=없음, 1=비, 2=비/눈, 3=눈, 4=소나기, ...)
    ///   REH = 습도 (%)
    ///   RN1 = 1시간 강수량 (mm)
    ///   ...
    ///
    /// 우리는 T1H + PTY 만 추출하면 무성님 명세 충족.
    /// </summary>
    public static class KmaApiClient
    {
        private const string BASE_URL = "https://apis.data.go.kr/1360000/VilageFcstInfoService_2.0/getUltraSrtNcst";

        /// <summary>
        /// 초단기실황 조회. UnityWebRequest 기반 코루틴.
        /// 발표 시각: 매 시각 40분 후. 안전하게 1시간 전 정시로 호출.
        /// </summary>
        /// <param name="serviceKey">공공데이터포털 인증키 (URL encode 된 상태가 아니라 디코딩 키 그대로)</param>
        /// <param name="nx">격자 X</param>
        /// <param name="ny">격자 Y</param>
        /// <param name="onSuccess">파싱된 KmaObservation 콜백</param>
        /// <param name="onFailure">에러 메시지 콜백</param>
        public static IEnumerator FetchCurrentObservation(
            string serviceKey,
            int nx,
            int ny,
            Action<KmaObservation> onSuccess,
            Action<string> onFailure)
        {
            // 발표 시각: 지금에서 1시간 전 정시 (예: 15:37 호출 → 14:00 데이터 조회)
            DateTime now = DateTime.Now.AddHours(-1);
            string baseDate = now.ToString("yyyyMMdd");
            string baseTime = now.ToString("HH") + "00";

            // serviceKey는 인코딩되지 않은 원본을 받아서 여기서 URL encoding
            string url = $"{BASE_URL}" +
                $"?serviceKey={UnityWebRequest.EscapeURL(serviceKey)}" +
                $"&pageNo=1&numOfRows=10&dataType=JSON" +
                $"&base_date={baseDate}&base_time={baseTime}" +
                $"&nx={nx}&ny={ny}";

            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    onFailure?.Invoke($"HTTP 실패: {req.error}");
                    yield break;
                }

                string json = req.downloadHandler.text;
                try
                {
                    KmaObservation obs = ParseResponse(json);
                    if (obs == null)
                    {
                        onFailure?.Invoke("응답 파싱 실패 (item 비어있음)");
                        yield break;
                    }
                    onSuccess?.Invoke(obs);
                }
                catch (Exception e)
                {
                    onFailure?.Invoke($"파싱 예외: {e.Message}\n원본: {json}");
                }
            }
        }

        /// <summary>
        /// 기상청 JSON 응답 → KmaObservation 객체.
        /// 응답 구조 깊어서 직접 파싱 (JsonUtility 한계 회피).
        /// </summary>
        private static KmaObservation ParseResponse(string json)
        {
            // 기상청 응답 구조:
            // { "response": { "header": {...}, "body": { "items": { "item": [...] } } } }
            // 각 item: { "category": "T1H", "obsrValue": "23.5", "baseDate":"...", "baseTime":"..." }

            // 간단 파싱 (정규식 없이 substring 검색):
            // category와 obsrValue 쌍만 뽑음.
            float temperature = float.NaN;
            int pty = 0;
            int sky = 0; // 초단기실황은 SKY 없음, 기본 0
            string baseDateTime = "";

            int idx = 0;
            while (true)
            {
                int catIdx = json.IndexOf("\"category\"", idx);
                if (catIdx < 0) break;

                int catStart = json.IndexOf('"', catIdx + 10) + 1;
                int catEnd = json.IndexOf('"', catStart);
                string category = json.Substring(catStart, catEnd - catStart);

                int valIdx = json.IndexOf("\"obsrValue\"", catEnd);
                if (valIdx < 0) break;
                int valStart = json.IndexOf('"', valIdx + 11) + 1;
                int valEnd = json.IndexOf('"', valStart);
                string value = json.Substring(valStart, valEnd - valStart);

                // baseDate / baseTime — 첫 item에서만 추출
                if (string.IsNullOrEmpty(baseDateTime))
                {
                    int bdIdx = json.IndexOf("\"baseDate\"", catEnd);
                    int btIdx = json.IndexOf("\"baseTime\"", catEnd);
                    if (bdIdx > 0 && btIdx > 0)
                    {
                        int bdS = json.IndexOf('"', bdIdx + 10) + 1;
                        int bdE = json.IndexOf('"', bdS);
                        int btS = json.IndexOf('"', btIdx + 10) + 1;
                        int btE = json.IndexOf('"', btS);
                        string bd = json.Substring(bdS, bdE - bdS); // 20260606
                        string bt = json.Substring(btS, btE - btS); // 1500
                        // ISO 8601 변환
                        if (bd.Length == 8 && bt.Length == 4)
                        {
                            baseDateTime = $"{bd.Substring(0, 4)}-{bd.Substring(4, 2)}-{bd.Substring(6, 2)}T{bt.Substring(0, 2)}:{bt.Substring(2, 2)}:00";
                        }
                    }
                }

                switch (category)
                {
                    case "T1H": float.TryParse(value, out temperature); break;
                    case "PTY": int.TryParse(value, out pty); break;
                    // SKY는 초단기실황에 없음
                }

                idx = valEnd;
            }

            if (float.IsNaN(temperature))
            {
                Debug.LogWarning($"[KmaApiClient] T1H(기온) 파싱 실패. 응답 일부: {json.Substring(0, Math.Min(json.Length, 300))}");
                return null;
            }

            return new KmaObservation
            {
                temperature = temperature,
                pty = pty,
                sky = sky,
                recordedAt = string.IsNullOrEmpty(baseDateTime) ? DateTime.UtcNow.ToString("o") : baseDateTime
            };
        }
    }

    /// <summary>
    /// 기상청 응답 → 우리 도메인 데이터 모델.
    /// </summary>
    [Serializable]
    public class KmaObservation
    {
        public float temperature;    // T1H (°C)
        public int pty;              // 강수 형태 코드
        public int sky;              // 하늘 상태 (초단기실황은 0 고정)
        public string recordedAt;    // ISO 8601 — 기상청 발표 시각

        public string Condition => WeatherCondition.Map(pty, sky);
    }
}
