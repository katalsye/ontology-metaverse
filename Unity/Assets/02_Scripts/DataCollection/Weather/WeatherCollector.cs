using System;
using System.Collections;
using UnityEngine;
using OntologyMetaverse.DataCollection.SQLite;

namespace OntologyMetaverse.DataCollection.Weather
{
    /// <summary>
    /// 기상청 단기예보 API로부터 현재 위치 날씨 수집기.
    ///
    /// 동작:
    ///   1. SQLite raw_data 에서 가장 최근 GPS 좌표 가져옴 (LocationCollector 가 수집한 것)
    ///   2. lat/lng → 기상청 격자 X/Y 변환 (LCC 투영)
    ///   3. 기상청 초단기실황 API 호출
    ///   4. T1H(기온) + PTY(강수) 추출 → condition 매핑
    ///   5. raw_data (type=weather) 저장
    ///        Content: {"temperature":23.5,"condition":"clear","recordedAt":"2026-06-06T15:00:00"}
    ///
    /// RawDataToTripleConverter.ConvertWeather() 가 이 raw_data를:
    ///   - (weather_X, rdf:type, prod:Weather)
    ///   - (user, prod:hasWeather, weather_X)
    ///   - (weather_X, prod:temperature, "23.5"^^xsd:float)
    ///   - (weather_X, prod:condition, "clear")
    ///   - (weather_X, prod:recordedAt, "2026-06-06T15:00:00"^^xsd:dateTime)
    ///
    /// 무성님 Rule 7 (IndoorDayPattern) 이 condition=rain 또는 12/1/2월에 발동.
    ///
    /// API 키 발급:
    ///   https://www.data.go.kr → 기상청_단기예보 조회서비스 신청 (즉시 승인)
    ///   Inspector의 serviceKey 필드에 입력. 일반 인증키(디코딩) 사용.
    /// </summary>
    public class WeatherCollector : MonoBehaviour
    {
        [Header("기상청 공공데이터포털 인증키")]
        [Tooltip("https://www.data.go.kr 에서 기상청_단기예보 조회서비스 신청 후 받은 일반 인증키(Decoding) 입력")]
        public string serviceKey = "";

        [Header("기본 좌표 (GPS 미수집 상태일 때 폴백)")]
        [Tooltip("대구광역시 시청 위치. 한국 영역 안의 어떤 좌표든 OK.")]
        public double fallbackLat = 35.8714;
        public double fallbackLng = 128.6014;

        /// <summary>
        /// 한 번 수집 사이클. BatchScheduler가 주기적으로 호출.
        /// </summary>
        public IEnumerator CollectCurrentWeather()
        {
            if (string.IsNullOrWhiteSpace(serviceKey))
            {
                Debug.LogWarning("[WeatherCollector] serviceKey 미설정 → 수집 스킵. Inspector에서 입력하세요.");
                yield break;
            }

            // 1. 최근 GPS 좌표 가져오기 (없으면 fallback)
            (double lat, double lng) = GetLatestGps();

            // 2. 격자 좌표 변환
            (int nx, int ny) = GridConverter.ToGrid(lat, lng);
            Debug.Log($"[WeatherCollector] 좌표 변환: ({lat:F4}, {lng:F4}) → 격자({nx}, {ny})");

            // 3. 기상청 API 호출
            KmaObservation obs = null;
            string error = null;
            yield return StartCoroutine(KmaApiClient.FetchCurrentObservation(
                serviceKey, nx, ny,
                onSuccess: o => obs = o,
                onFailure: e => error = e
            ));

            if (obs == null)
            {
                Debug.LogError($"[WeatherCollector] 날씨 조회 실패: {error}");
                yield break;
            }

            // 4. SQLite 저장
            string content = $"{{\"temperature\":{obs.temperature.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"condition\":\"{obs.Condition}\",\"recordedAt\":\"{obs.recordedAt}\"}}";
            var raw = new RawData
            {
                Type = "weather",
                Content = content,
                Timestamp = DateTime.UtcNow.ToString("o"),
                Processed = 0
            };
            SQLiteManager.Instance.Connection.Insert(raw);
            Debug.Log($"[WeatherCollector] 저장 완료: {obs.Condition}, {obs.temperature}°C @ {obs.recordedAt}");
        }

        /// <summary>
        /// raw_data 테이블에서 가장 최근 type=gps 1건의 lat/lng 추출.
        /// 없으면 fallback 좌표.
        ///
        /// sqlite-net-pcl LINQ는 Take/FirstOrDefault 체이닝이 불완전해서
        /// foreach + break 패턴 사용 (RawDataToTripleConverter 와 동일 스타일).
        /// </summary>
        private (double lat, double lng) GetLatestGps()
        {
            try
            {
                var conn = SQLiteManager.Instance.Connection;
                var query = conn.Table<RawData>().Where(r => r.Type == "gps").OrderByDescending(r => r.Id);
                RawData latestGps = null;
                foreach (var r in query)
                {
                    latestGps = r;
                    break; // 가장 최신 1건만 필요
                }

                if (latestGps == null)
                {
                    Debug.LogWarning($"[WeatherCollector] GPS raw_data 없음 → fallback ({fallbackLat}, {fallbackLng})");
                    return (fallbackLat, fallbackLng);
                }

                // Content 형식: {"lat":35.83912,"lng":128.5416,...}
                double lat = ExtractDouble(latestGps.Content, "lat");
                double lng = ExtractDouble(latestGps.Content, "lng");

                if (lat == 0 && lng == 0)
                {
                    Debug.LogWarning($"[WeatherCollector] GPS Content 파싱 실패 → fallback. raw: {latestGps.Content}");
                    return (fallbackLat, fallbackLng);
                }
                return (lat, lng);
            }
            catch (Exception e)
            {
                Debug.LogError($"[WeatherCollector] GPS 조회 실패: {e.Message} → fallback");
                return (fallbackLat, fallbackLng);
            }
        }

        /// <summary>
        /// 간이 JSON 파서. {"key":value, ...} 형식에서 숫자 값 추출.
        /// 정식 JSON 파서 안 쓰는 이유: RawData.Content 가 단순 평탄 객체뿐이라.
        /// </summary>
        private static double ExtractDouble(string json, string key)
        {
            string needle = $"\"{key}\":";
            int idx = json.IndexOf(needle);
            if (idx < 0) return 0;
            int start = idx + needle.Length;
            int end = start;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '.' || json[end] == '-' || json[end] == '+' || json[end] == 'e' || json[end] == 'E'))
            {
                end++;
            }
            if (end == start) return 0;
            double.TryParse(json.Substring(start, end - start), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double val);
            return val;
        }
    }
}
