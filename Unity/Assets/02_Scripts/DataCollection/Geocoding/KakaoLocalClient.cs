using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace OntologyMetaverse.DataCollection.Geocoding
{
    /// <summary>
    /// 카카오 로컬 REST API 클라이언트.
    ///
    /// 무성님 Rule 4 (PlaceHabit) 전용 오브젝트 매핑에 정확히 맞춘 placeType 생성:
    ///   cafe → coffee_cup | gym → dumbbell | library → bookshelf | park → tree_pot | 그 외 → generic_marker
    ///
    /// 핵심: gym/library/park는 카카오 "카테고리 그룹 코드"에 없음 →
    ///   - cafe(CE7) / restaurant(FD6): 카테고리 그룹 검색 (정확)
    ///   - gym / library / park: 키워드 검색 (헬스장/도서관/공원) + category_name 검증
    ///   - 모든 후보를 거리(distance)로 비교 → 가장 가까운 장소 채택
    ///   - 다 못 찾으면 coord2address 폴백 (placeType="general" → Rule 4 generic_marker)
    ///
    /// 사용 endpoint:
    ///   - /v2/local/search/category.json?category_group_code=CODE  (cafe/restaurant)
    ///   - /v2/local/search/keyword.json?query=검색어                (gym/library/park)
    ///   - /v2/local/geo/coord2address.json                         (폴백 주소)
    ///
    /// 인증: Authorization: KakaoAK {REST_API_KEY}
    /// 무료 한도: 일 10만 건 (10분 주기 × 5종 검색 = 일 ~720건, 무관)
    /// </summary>
    public static class KakaoLocalClient
    {
        private const string CATEGORY_SEARCH_URL = "https://dapi.kakao.com/v2/local/search/category.json";
        private const string KEYWORD_SEARCH_URL  = "https://dapi.kakao.com/v2/local/search/keyword.json";
        private const string COORD2ADDRESS_URL   = "https://dapi.kakao.com/v2/local/geo/coord2address.json";

        // 카테고리 그룹 검색 (정확) — (code, placeType, radius)
        private static readonly (string code, string type, int radius)[] CATEGORY_CANDIDATES = new[]
        {
            ("CE7", "cafe",       50),   // 카페
            ("FD6", "restaurant", 50),   // 음식점
        };

        // 키워드 검색 (카테고리 그룹 없는 것) — (query, placeType, radius, verifyKeywords)
        // verifyKeywords: 결과 category_name/place_name에 이 중 하나가 포함돼야 오탐 방지
        private static readonly (string query, string type, int radius, string[] verify)[] KEYWORD_CANDIDATES = new[]
        {
            ("헬스장",  "gym",     120, new[]{"헬스", "피트니스", "gym", "fitness", "스포츠"}),
            ("도서관",  "library", 120, new[]{"도서관", "library"}),
            ("공원",    "park",    150, new[]{"공원", "park"}),
        };

        /// <summary>
        /// 좌표 → GeocodeResult. 5종 후보를 거리 비교 → 최단 채택 → 실패 시 주소 폴백.
        /// </summary>
        public static IEnumerator Fetch(
            string restApiKey,
            double lat,
            double lng,
            int sourceGpsId,
            Action<GeocodeResult> onSuccess,
            Action<string> onFailure)
        {
            string nowIso = DateTime.UtcNow.ToString("o");

            string bestName = null;
            string bestType = null;
            int bestDistance = int.MaxValue;

            // 1. 카테고리 후보 (cafe, restaurant)
            foreach (var (code, type, radius) in CATEGORY_CANDIDATES)
            {
                string name = null;
                int dist = int.MaxValue;
                yield return CategorySearch(restApiKey, code, lat, lng, radius,
                    onResult: (n, d) => { name = n; dist = d; });

                if (!string.IsNullOrEmpty(name) && dist < bestDistance)
                {
                    bestName = name;
                    bestType = type;
                    bestDistance = dist;
                }
            }

            // 2. 키워드 후보 (gym, library, park)
            foreach (var (query, type, radius, verify) in KEYWORD_CANDIDATES)
            {
                string name = null;
                int dist = int.MaxValue;
                yield return KeywordSearch(restApiKey, query, lat, lng, radius, verify,
                    onResult: (n, d) => { name = n; dist = d; });

                if (!string.IsNullOrEmpty(name) && dist < bestDistance)
                {
                    bestName = name;
                    bestType = type;
                    bestDistance = dist;
                }
            }

            // 3. 최단 후보 채택
            if (!string.IsNullOrEmpty(bestName))
            {
                Debug.Log($"[KakaoLocal] 채택: \"{bestName}\" ({bestType}, {bestDistance}m)");
                onSuccess?.Invoke(new GeocodeResult
                {
                    placeName = bestName,
                    placeType = bestType,
                    lat = lat,
                    lng = lng,
                    sourceGpsId = sourceGpsId,
                    recordedAt = nowIso
                });
                yield break;
            }

            // 4. 폴백: coord2address (행정/도로명 주소) → placeType="general"
            string fallbackName = null;
            string fallbackError = null;
            yield return Coord2Address(restApiKey, lat, lng,
                onSuccess: n => fallbackName = n,
                onFailure: e => fallbackError = e);

            if (!string.IsNullOrEmpty(fallbackName))
            {
                onSuccess?.Invoke(new GeocodeResult
                {
                    placeName = fallbackName,
                    placeType = "general",   // 무성님 Rule 4 → generic_marker
                    lat = lat,
                    lng = lng,
                    sourceGpsId = sourceGpsId,
                    recordedAt = nowIso
                });
            }
            else
            {
                onFailure?.Invoke($"카테고리/키워드/주소 모두 조회 실패: {fallbackError}");
            }
        }

        // ───────────────────────────────────────────
        // 카테고리 그룹 검색 (cafe / restaurant)
        // ───────────────────────────────────────────

        private static IEnumerator CategorySearch(
            string restApiKey, string categoryCode, double lat, double lng, int radiusMeters,
            Action<string, int> onResult)
        {
            string url = $"{CATEGORY_SEARCH_URL}" +
                $"?category_group_code={categoryCode}" +
                $"&x={lng.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}" +
                $"&y={lat.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}" +
                $"&radius={radiusMeters}" +
                $"&sort=distance" +
                $"&size=1";

            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.SetRequestHeader("Authorization", $"KakaoAK {restApiKey}");
                req.timeout = 10;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[KakaoLocal] category {categoryCode} HTTP 실패: {req.error}");
                    onResult?.Invoke(null, int.MaxValue);
                    yield break;
                }

                string json = req.downloadHandler.text;
                string placeName = ExtractJsonStringField(json, "place_name");
                int distance = ExtractDistance(json);
                onResult?.Invoke(placeName, distance);
            }
        }

        // ───────────────────────────────────────────
        // 키워드 검색 (gym / library / park)
        // ───────────────────────────────────────────

        private static IEnumerator KeywordSearch(
            string restApiKey, string query, double lat, double lng, int radiusMeters, string[] verify,
            Action<string, int> onResult)
        {
            string url = $"{KEYWORD_SEARCH_URL}" +
                $"?query={UnityWebRequest.EscapeURL(query)}" +
                $"&x={lng.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}" +
                $"&y={lat.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}" +
                $"&radius={radiusMeters}" +
                $"&sort=distance" +
                $"&size=1";

            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.SetRequestHeader("Authorization", $"KakaoAK {restApiKey}");
                req.timeout = 10;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[KakaoLocal] keyword '{query}' HTTP 실패: {req.error}");
                    onResult?.Invoke(null, int.MaxValue);
                    yield break;
                }

                string json = req.downloadHandler.text;
                string placeName = ExtractJsonStringField(json, "place_name");
                string categoryName = ExtractJsonStringField(json, "category_name");
                int distance = ExtractDistance(json);

                // 오탐 방지: category_name 또는 place_name에 verify 키워드 포함 확인
                if (!string.IsNullOrEmpty(placeName) && PassesVerify(placeName, categoryName, verify))
                {
                    onResult?.Invoke(placeName, distance);
                }
                else
                {
                    onResult?.Invoke(null, int.MaxValue);
                }
            }
        }

        private static bool PassesVerify(string placeName, string categoryName, string[] verify)
        {
            string hay = ((placeName ?? "") + " " + (categoryName ?? "")).ToLowerInvariant();
            foreach (var v in verify)
            {
                if (hay.Contains(v.ToLowerInvariant())) return true;
            }
            return false;
        }

        // ───────────────────────────────────────────
        // 좌표 → 주소 (폴백)
        // ───────────────────────────────────────────

        private static IEnumerator Coord2Address(
            string restApiKey, double lat, double lng,
            Action<string> onSuccess, Action<string> onFailure)
        {
            string url = $"{COORD2ADDRESS_URL}" +
                $"?x={lng.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}" +
                $"&y={lat.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}";

            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.SetRequestHeader("Authorization", $"KakaoAK {restApiKey}");
                req.timeout = 10;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    onFailure?.Invoke($"HTTP 실패: {req.error}");
                    yield break;
                }

                string json = req.downloadHandler.text;
                string addressName = ExtractFirstAddressName(json);
                if (string.IsNullOrEmpty(addressName))
                {
                    onFailure?.Invoke("address_name 파싱 실패");
                    yield break;
                }
                onSuccess?.Invoke(addressName);
            }
        }

        // ───────────────────────────────────────────
        // 간이 JSON 파서 (nested 구조에 JsonUtility 부적합)
        // ───────────────────────────────────────────

        /// <summary>documents[0].distance 추출 (미터, 정수). 없으면 int.MaxValue.</summary>
        private static int ExtractDistance(string json)
        {
            string distStr = ExtractJsonStringField(json, "distance");
            if (string.IsNullOrEmpty(distStr)) return int.MaxValue;
            return int.TryParse(distStr, out int d) ? d : int.MaxValue;
        }

        /// <summary>
        /// coord2address 응답에서 도로명 우선, 없으면 지번 주소.
        /// </summary>
        private static string ExtractFirstAddressName(string json)
        {
            int roadIdx = json.IndexOf("\"road_address\"");
            if (roadIdx >= 0)
            {
                int blockEnd = FindMatchingBraceEnd(json, roadIdx);
                if (blockEnd > roadIdx)
                {
                    string roadBlock = json.Substring(roadIdx, blockEnd - roadIdx);
                    string roadName = ExtractJsonStringField(roadBlock, "address_name");
                    if (!string.IsNullOrEmpty(roadName)) return roadName;
                }
            }
            return ExtractJsonStringField(json, "address_name");
        }

        /// <summary>
        /// JSON에서 "field":"value" 형태의 첫 string 필드 추출. null/빈 값은 무시.
        /// </summary>
        private static string ExtractJsonStringField(string json, string field)
        {
            string needle = $"\"{field}\"";
            int idx = json.IndexOf(needle);
            if (idx < 0) return null;

            int colonIdx = json.IndexOf(':', idx + needle.Length);
            if (colonIdx < 0) return null;

            int afterColon = colonIdx + 1;
            while (afterColon < json.Length && (json[afterColon] == ' ' || json[afterColon] == '\t' || json[afterColon] == '\n' || json[afterColon] == '\r'))
                afterColon++;
            if (afterColon + 4 <= json.Length && json.Substring(afterColon, 4) == "null")
                return null;

            int start = json.IndexOf('"', colonIdx + 1);
            if (start < 0) return null;
            start++;

            int end = json.IndexOf('"', start);
            if (end < 0) return null;

            string value = json.Substring(start, end - start);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static int FindMatchingBraceEnd(string json, int startIdx)
        {
            int braceStart = json.IndexOf('{', startIdx);
            if (braceStart < 0) return -1;

            int depth = 0;
            for (int i = braceStart; i < json.Length; i++)
            {
                if (json[i] == '{') depth++;
                else if (json[i] == '}')
                {
                    depth--;
                    if (depth == 0) return i + 1;
                }
            }
            return -1;
        }
    }
}
