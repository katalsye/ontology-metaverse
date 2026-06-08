using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace OntologyMetaverse.DataCollection.Geocoding
{
    /// <summary>
    /// 카카오 로컬 REST API 클라이언트.
    ///
    /// 사용 endpoint:
    ///   1. /v2/local/geo/coord2address.json
    ///      → 좌표 → 도로명/지번 주소 (placeName 폴백)
    ///   2. /v2/local/search/category.json?category_group_code={CODE}
    ///      → 좌표 주변 카테고리 그룹 검색 (placeType + 정확한 상호명)
    ///
    /// 카테고리 코드 (검색 우선순위):
    ///   CE7 = 카페            → placeType = "cafe"
    ///   FD6 = 음식점          → placeType = "restaurant"
    ///   CT1 = 문화시설        → placeType = "culture"
    ///   AT4 = 관광명소        → placeType = "tourist"
    ///   HP8 = 병원            → placeType = "hospital"
    ///   SW8 = 지하철역        → placeType = "transit"
    ///
    /// 매칭 전략:
    ///   - radius=50m 안에서 카테고리 그룹 순회
    ///   - 가장 가까운 1건 발견 시 placeName/placeType 확정
    ///   - 못 찾으면 coord2address 결과로 폴백 (placeType="general")
    ///
    /// 인증:
    ///   Authorization: KakaoAK {REST_API_KEY}
    ///   (카카오 개발자 콘솔에서 발급, https://developers.kakao.com/)
    ///
    /// 무료 한도: 일 10만 건 (한도 충분 — 10분 주기 호출이면 일 144건)
    /// </summary>
    public static class KakaoLocalClient
    {
        private const string COORD2ADDRESS_URL =
            "https://dapi.kakao.com/v2/local/geo/coord2address.json";

        private const string CATEGORY_SEARCH_URL =
            "https://dapi.kakao.com/v2/local/search/category.json";

        // 검색 우선순위 (가장 가까운 카페 > 음식점 > 문화시설 > ...)
        private static readonly (string code, string type)[] CATEGORY_PRIORITY = new[]
        {
            ("CE7", "cafe"),
            ("FD6", "restaurant"),
            ("CT1", "culture"),
            ("AT4", "tourist"),
            ("HP8", "hospital"),
            ("SW8", "transit"),
        };

        /// <summary>
        /// 좌표 → GeocodeResult. 카테고리 검색 → 실패 시 행정주소 폴백.
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

            // 1단계: 카테고리 검색 (50m 반경, 거리순 정렬)
            foreach (var (code, type) in CATEGORY_PRIORITY)
            {
                bool finished = false;
                string placeName = null;
                string error = null;

                yield return SearchCategory(
                    restApiKey, code, lat, lng, 50,
                    onSuccess: name => { placeName = name; finished = true; },
                    onFailure: e => { error = e; finished = true; }
                );

                if (!finished) continue;

                if (!string.IsNullOrEmpty(placeName))
                {
                    onSuccess?.Invoke(new GeocodeResult
                    {
                        placeName = placeName,
                        placeType = type,
                        lat = lat,
                        lng = lng,
                        sourceGpsId = sourceGpsId,
                        recordedAt = nowIso
                    });
                    yield break;
                }

                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogWarning($"[KakaoLocal] category {code} 실패: {error}");
                }
            }

            // 2단계: 폴백 — coord2address (행정/도로명 주소)
            string fallbackName = null;
            string fallbackError = null;
            yield return Coord2Address(
                restApiKey, lat, lng,
                onSuccess: name => fallbackName = name,
                onFailure: e => fallbackError = e
            );

            if (!string.IsNullOrEmpty(fallbackName))
            {
                onSuccess?.Invoke(new GeocodeResult
                {
                    placeName = fallbackName,
                    placeType = "general",
                    lat = lat,
                    lng = lng,
                    sourceGpsId = sourceGpsId,
                    recordedAt = nowIso
                });
            }
            else
            {
                onFailure?.Invoke($"카테고리/주소 모두 조회 실패: {fallbackError}");
            }
        }

        /// <summary>
        /// 카테고리 그룹 검색. 가장 가까운 1건의 place_name 반환.
        /// 없으면 null.
        /// </summary>
        private static IEnumerator SearchCategory(
            string restApiKey,
            string categoryCode,
            double lat,
            double lng,
            int radiusMeters,
            Action<string> onSuccess,
            Action<string> onFailure)
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
                    onFailure?.Invoke($"HTTP 실패: {req.error}");
                    yield break;
                }

                string json = req.downloadHandler.text;
                string placeName = ExtractFirstPlaceName(json);
                onSuccess?.Invoke(placeName); // null이면 호출자가 다음 카테고리로 넘어감
            }
        }

        /// <summary>
        /// 좌표 → 도로명 주소. 도로명 우선, 없으면 지번 주소.
        /// </summary>
        private static IEnumerator Coord2Address(
            string restApiKey,
            double lat,
            double lng,
            Action<string> onSuccess,
            Action<string> onFailure)
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
        // 간이 JSON 파서 (KmaApiClient 패턴 — nested 구조에 JsonUtility 부적합)
        // ───────────────────────────────────────────

        /// <summary>
        /// 카테고리 검색 응답에서 첫 documents[].place_name 추출.
        /// 응답 예: {"meta":{...},"documents":[{"place_name":"...","x":"...","y":"...",...}]}
        /// </summary>
        private static string ExtractFirstPlaceName(string json)
        {
            return ExtractJsonStringField(json, "place_name");
        }

        /// <summary>
        /// coord2address 응답에서 첫 documents[].road_address.address_name 추출.
        /// 도로명 없으면 address.address_name (지번) 폴백.
        /// 응답 예: {"meta":{...},"documents":[{"road_address":{"address_name":"..."},"address":{"address_name":"..."}}]}
        /// </summary>
        private static string ExtractFirstAddressName(string json)
        {
            // 도로명 우선: "road_address" 블록 안의 "address_name"
            int roadIdx = json.IndexOf("\"road_address\"");
            if (roadIdx >= 0)
            {
                // road_address 객체 끝(다음 '}')까지의 부분 문자열에서 address_name 추출
                int blockEnd = FindMatchingBraceEnd(json, roadIdx);
                if (blockEnd > roadIdx)
                {
                    string roadBlock = json.Substring(roadIdx, blockEnd - roadIdx);
                    string roadName = ExtractJsonStringField(roadBlock, "address_name");
                    if (!string.IsNullOrEmpty(roadName)) return roadName;
                }
            }

            // 지번 폴백: 첫 번째 "address_name"
            return ExtractJsonStringField(json, "address_name");
        }

        /// <summary>
        /// JSON에서 "field":"value" 형태의 첫 string 필드 추출. null 또는 빈 값은 무시.
        /// </summary>
        private static string ExtractJsonStringField(string json, string field)
        {
            string needle = $"\"{field}\"";
            int idx = json.IndexOf(needle);
            if (idx < 0) return null;

            int colonIdx = json.IndexOf(':', idx + needle.Length);
            if (colonIdx < 0) return null;

            // null 체크
            int afterColon = colonIdx + 1;
            while (afterColon < json.Length && (json[afterColon] == ' ' || json[afterColon] == '\t' || json[afterColon] == '\n' || json[afterColon] == '\r'))
                afterColon++;
            if (afterColon + 4 <= json.Length && json.Substring(afterColon, 4) == "null")
                return null;

            // string 값 추출
            int start = json.IndexOf('"', colonIdx + 1);
            if (start < 0) return null;
            start++;

            // 따옴표 안의 내용 (escape 무시 — 카카오 응답에는 escape char 거의 없음)
            int end = json.IndexOf('"', start);
            if (end < 0) return null;

            string value = json.Substring(start, end - start);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        /// <summary>
        /// 시작 위치 이후의 첫 '{' 매칭 '}' 인덱스 반환.
        /// nested 객체 카운팅. 매칭 실패 시 -1.
        /// </summary>
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
