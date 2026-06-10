using System;
using System.Collections;
using UnityEngine;
using OntologyMetaverse.DataCollection.SQLite;

namespace OntologyMetaverse.DataCollection.Geocoding
{
    /// <summary>
    /// 가장 최근 GPS 좌표 → 카카오 로컬 API → placeName/placeType 추론 → raw_data 저장.
    ///
    /// 동작:
    ///   1. SQLite raw_data 에서 type=gps 최신 1건 가져옴
    ///   2. lat/lng 추출
    ///   3. 카카오 카테고리 검색 (CE7→FD6→CT1→AT4→HP8→SW8 우선순위)
    ///   4. 못 찾으면 coord2address 폴백
    ///   5. raw_data (type=geocode) 저장
    ///      Content: {"placeName":"...","placeType":"cafe","lat":...,"lng":...,"source_gps_id":12,"recordedAt":"..."}
    ///
    /// RawDataToTripleConverter.ConvertGeocode 가 이 raw_data를:
    ///   - (loc_{source_gps_id}, prod:placeName, "..."^^xsd:string)
    ///   - (loc_{source_gps_id}, prod:placeType, "cafe"^^xsd:string)
    ///   같은 Location URI를 보강 → 무성님 Rule 4 (PlaceHabit), Rule 5 (카페인 인과),
    ///   Rule 6 (보완형 퀘스트) 활성화.
    ///
    /// 호출 주기:
    ///   - BatchScheduler가 geocodeIntervalMinutes 마다 1회 호출 (기본 10분)
    ///   - 일 호출 ≈ 144건 << 카카오 무료 한도 10만건
    ///
    /// API 키 발급:
    ///   https://developers.kakao.com/ → 내 애플리케이션 → REST API 키
    ///   Inspector의 restApiKey 필드에 입력 (KakaoAK 접두어 제외)
    /// </summary>
    public class ReverseGeocoder : MonoBehaviour
    {
        [Header("카카오 REST API 키")]
        [Tooltip("https://developers.kakao.com 내 애플리케이션 → 앱 키 → REST API 키 (KakaoAK 접두어 제외)")]
        public string restApiKey = "";

        /// <summary>
        /// 한 번의 geocoding 사이클. BatchScheduler가 주기적으로 호출.
        /// </summary>
        public IEnumerator GeocodeLatestLocation()
        {
            // OSM(Overpass+Nominatim)은 API 키 불필요 — 키 체크 없이 바로 진행.
            // (restApiKey 필드는 카카오 심사 통과 시 KakaoLocalClient 복귀용으로 남겨둠)

            // 1. 최근 GPS raw_data 1건 가져오기
            RawData latestGps = GetLatestGpsRaw();
            if (latestGps == null)
            {
                Debug.Log("[ReverseGeocoder] GPS raw_data 없음 → 스킵");
                yield break;
            }

            // 2. lat/lng 추출
            double lat = ExtractDouble(latestGps.Content, "lat");
            double lng = ExtractDouble(latestGps.Content, "lng");
            if (lat == 0 && lng == 0)
            {
                Debug.LogWarning($"[ReverseGeocoder] GPS Content 파싱 실패. raw[{latestGps.Id}]: {latestGps.Content}");
                yield break;
            }

            // 3. 같은 source_gps_id로 이미 geocoded됐는지 확인 (API 호출 절약)
            if (IsAlreadyGeocoded(latestGps.Id))
            {
                Debug.Log($"[ReverseGeocoder] raw[{latestGps.Id}] 이미 geocoded → 스킵");
                yield break;
            }

            // 4. OSM 역지오코딩 (Overpass POI → Nominatim 주소 폴백)
            GeocodeResult result = null;
            string error = null;
            yield return StartCoroutine(OsmGeocodingClient.Fetch(
                lat, lng, latestGps.Id,
                onSuccess: r => result = r,
                onFailure: e => error = e
            ));

            if (result == null)
            {
                Debug.LogError($"[ReverseGeocoder] geocoding 실패: {error}");
                yield break;
            }

            // 5. SQLite 저장
            string content = $"{{\"placeName\":\"{EscapeJson(result.placeName)}\"," +
                             $"\"placeType\":\"{result.placeType}\"," +
                             $"\"lat\":{result.lat.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}," +
                             $"\"lng\":{result.lng.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}," +
                             $"\"source_gps_id\":{result.sourceGpsId}," +
                             $"\"recordedAt\":\"{result.recordedAt}\"}}";

            var raw = new RawData
            {
                Type = "geocode",
                Content = content,
                Timestamp = DateTime.UtcNow.ToString("o"),
                Processed = 0
            };
            SQLiteManager.Instance.Connection.Insert(raw);

            Debug.Log($"[ReverseGeocoder] 저장 완료: \"{result.placeName}\" ({result.placeType}) ← gps_id={latestGps.Id}");
        }

        // ───────────────────────────────────────────
        // SQLite 조회 헬퍼
        // ───────────────────────────────────────────

        /// <summary>
        /// raw_data에서 type=gps 가장 최신 1건. 없으면 null.
        /// </summary>
        private RawData GetLatestGpsRaw()
        {
            try
            {
                var conn = SQLiteManager.Instance.Connection;
                var query = conn.Table<RawData>().Where(r => r.Type == "gps").OrderByDescending(r => r.Id);
                foreach (var r in query)
                {
                    return r; // 가장 최신 1건만
                }
                return null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ReverseGeocoder] GPS 조회 실패: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 특정 GPS raw.Id가 이미 geocode raw_data에 source로 사용됐는지 확인.
        /// Content JSON에 "source_gps_id":N 패턴 검색 (정식 파싱 대신 substring).
        /// </summary>
        private bool IsAlreadyGeocoded(int gpsId)
        {
            try
            {
                var conn = SQLiteManager.Instance.Connection;
                string needle = $"\"source_gps_id\":{gpsId}";
                var query = conn.Table<RawData>().Where(r => r.Type == "geocode");
                foreach (var r in query)
                {
                    if (r.Content != null && r.Content.Contains(needle)) return true;
                }
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ReverseGeocoder] geocode 중복 확인 실패: {e.Message}");
                return false; // 확인 실패 시 호출은 진행 (중복 위험 < 누락 위험)
            }
        }

        // ───────────────────────────────────────────
        // 유틸 (WeatherCollector와 동일 패턴)
        // ───────────────────────────────────────────

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

        /// <summary>
        /// placeName에 큰따옴표/백슬래시 들어가면 SQLite Content JSON 깨짐. 최소한의 escape.
        /// </summary>
        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
