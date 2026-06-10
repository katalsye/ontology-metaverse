using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace OntologyMetaverse.DataCollection.Geocoding
{
    /// <summary>
    /// OpenStreetMap 기반 역지오코딩 클라이언트 (API 키 불필요).
    ///
    /// 카카오 로컬 API가 비즈니스 심사를 요구해서 OSM으로 대체.
    /// (카카오 심사 통과 시 KakaoLocalClient로 교체 가능 — ReverseGeocoder 한 줄만 변경)
    ///
    /// 두 단계:
    ///   1. Overpass API: 좌표 반경 내 POI를 OSM 태그로 검색
    ///      - amenity=cafe/restaurant/library/gym, leisure=park/fitness_centre
    ///      → 무성님 Rule 4 전용 오브젝트(cafe→coffee_cup, gym→dumbbell,
    ///        library→bookshelf, park→tree_pot)와 정확히 매핑
    ///      → 가장 가까운 POI 채택 (Haversine 거리)
    ///   2. Nominatim reverse: POI 못 찾으면 좌표→주소 (placeType="general")
    ///
    /// 무료 / 키 불필요. rate limit:
    ///   - Nominatim 1req/s (User-Agent 헤더 필수 — 정책)
    ///   - Overpass 적당히 (10분 주기면 무관)
    /// </summary>
    public static class OsmGeocodingClient
    {
        // Overpass 공개 서버는 가끔 504(과부하) → 여러 엔드포인트 순회로 안정성 확보
        private static readonly string[] OVERPASS_ENDPOINTS = new[]
        {
            "https://overpass-api.de/api/interpreter",
            "https://maps.mail.ru/osm/tools/overpass/api/interpreter",
        };
        private const string NOMINATIM_URL = "https://nominatim.openstreetmap.org/reverse";

        // Nominatim 정책상 식별 가능한 User-Agent 필수
        private const string USER_AGENT = "ontology-metaverse/1.0 (KNU graduation project)";

        /// <summary>
        /// 좌표 → GeocodeResult. POI 검색 → 실패 시 주소 폴백.
        /// </summary>
        public static IEnumerator Fetch(
            double lat,
            double lng,
            int sourceGpsId,
            Action<GeocodeResult> onSuccess,
            Action<string> onFailure)
        {
            string nowIso = DateTime.UtcNow.ToString("o");

            // 1. Overpass로 주변 POI 검색
            string bestName = null;
            string bestType = null;
            string error = null;
            yield return SearchPoi(lat, lng,
                onResult: (name, type) => { bestName = name; bestType = type; },
                onError: e => error = e);

            if (!string.IsNullOrEmpty(bestType))
            {
                Debug.Log($"[OSM] POI 채택: \"{bestName}\" ({bestType})");
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

            // 2. 폴백: Nominatim reverse → placeType="general"
            string addr = null;
            string addrError = null;
            yield return ReverseAddress(lat, lng,
                onSuccess: a => addr = a,
                onFailure: e => addrError = e);

            if (!string.IsNullOrEmpty(addr))
            {
                onSuccess?.Invoke(new GeocodeResult
                {
                    placeName = addr,
                    placeType = "general",   // 무성님 Rule 4 → generic_marker
                    lat = lat,
                    lng = lng,
                    sourceGpsId = sourceGpsId,
                    recordedAt = nowIso
                });
            }
            else
            {
                onFailure?.Invoke($"POI/주소 모두 조회 실패 (overpass: {error}, nominatim: {addrError})");
            }
        }

        // ───────────────────────────────────────────
        // Overpass POI 검색
        // ───────────────────────────────────────────

        private static IEnumerator SearchPoi(
            double lat, double lng,
            Action<string, string> onResult,
            Action<string> onError)
        {
            // amenity(cafe/restaurant/library/gym) + leisure(park/fitness_centre) 한 번에
            // nwr = node/way/relation, out center tags = way/relation도 중심좌표+태그 반환
            string latS = lat.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
            string lngS = lng.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
            string query =
                "[out:json][timeout:15];(" +
                $"nwr(around:150,{latS},{lngS})[amenity=cafe];" +
                $"nwr(around:150,{latS},{lngS})[amenity=restaurant];" +
                $"nwr(around:150,{latS},{lngS})[amenity=library];" +
                $"nwr(around:150,{latS},{lngS})[amenity=gym];" +
                $"nwr(around:200,{latS},{lngS})[leisure=fitness_centre];" +
                $"nwr(around:200,{latS},{lngS})[leisure=park];" +
                ");out center tags;";

            string data = "?data=" + UnityWebRequest.EscapeURL(query);

            // 엔드포인트 순회: HTTP 실패(504/timeout)면 다음, HTTP 성공이면 그 결과 사용
            string lastError = "(없음)";
            foreach (var endpoint in OVERPASS_ENDPOINTS)
            {
                using (UnityWebRequest req = UnityWebRequest.Get(endpoint + data))
                {
                    req.SetRequestHeader("User-Agent", USER_AGENT);
                    req.timeout = 25;
                    yield return req.SendWebRequest();

                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        lastError = $"{endpoint}: {req.error}";
                        continue; // 다음 엔드포인트 시도
                    }

                    string json = req.downloadHandler.text;
                    OverpassResponse parsed;
                    try
                    {
                        parsed = JsonUtility.FromJson<OverpassResponse>(json);
                    }
                    catch (Exception e)
                    {
                        lastError = $"파싱 예외: {e.Message}";
                        continue;
                    }

                    // 가장 가까운 유효 POI 선택 (Haversine)
                    double bestDist = double.MaxValue;
                    string bestName = null;
                    string bestType = null;
                    if (parsed?.elements != null)
                    {
                        foreach (var el in parsed.elements)
                        {
                            if (el.tags == null) continue;
                            string type = DetermineType(el.tags);
                            if (type == null) continue;

                            double elat = el.lat != 0 ? el.lat : (el.center != null ? el.center.lat : 0);
                            double elng = el.lon != 0 ? el.lon : (el.center != null ? el.center.lon : 0);
                            if (elat == 0 && elng == 0) continue;

                            double dist = HaversineMeters(lat, lng, elat, elng);
                            if (dist < bestDist)
                            {
                                bestDist = dist;
                                bestType = type;
                                bestName = !string.IsNullOrWhiteSpace(el.tags.name) ? el.tags.name : DefaultName(type);
                            }
                        }
                    }

                    // HTTP 성공 — POI 있으면 채택, 없으면 (null,null)로 Nominatim 폴백 유도.
                    // 다른 엔드포인트도 OSM 데이터 동일하니 더 순회 안 함.
                    onResult?.Invoke(bestName, bestType);
                    yield break;
                }
            }

            // 모든 엔드포인트 HTTP 실패
            onError?.Invoke(lastError);
            onResult?.Invoke(null, null);
        }

        /// <summary>OSM 태그 → 무성님 placeType.</summary>
        private static string DetermineType(OverpassTags t)
        {
            if (t.amenity == "cafe") return "cafe";
            if (t.amenity == "restaurant") return "restaurant";
            if (t.amenity == "library") return "library";
            if (t.amenity == "gym") return "gym";
            if (t.leisure == "fitness_centre") return "gym";
            if (t.leisure == "park") return "park";
            return null;
        }

        /// <summary>name 태그 없는 POI의 기본 이름 (Rule 6 placeName용).</summary>
        private static string DefaultName(string type)
        {
            switch (type)
            {
                case "cafe": return "카페";
                case "restaurant": return "음식점";
                case "library": return "도서관";
                case "gym": return "헬스장";
                case "park": return "공원";
                default: return "장소";
            }
        }

        // ───────────────────────────────────────────
        // Nominatim reverse (폴백)
        // ───────────────────────────────────────────

        private static IEnumerator ReverseAddress(
            double lat, double lng,
            Action<string> onSuccess, Action<string> onFailure)
        {
            string latS = lat.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
            string lngS = lng.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
            string url = $"{NOMINATIM_URL}?format=json&lat={latS}&lon={lngS}&zoom=18&accept-language=ko";

            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.SetRequestHeader("User-Agent", USER_AGENT);
                req.timeout = 15;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    onFailure?.Invoke($"Nominatim HTTP 실패: {req.error}");
                    yield break;
                }

                string json = req.downloadHandler.text;
                try
                {
                    var parsed = JsonUtility.FromJson<NominatimResponse>(json);
                    string name = !string.IsNullOrWhiteSpace(parsed?.name)
                        ? parsed.name
                        : FirstSegment(parsed?.display_name);
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        onFailure?.Invoke("주소 파싱 실패");
                        yield break;
                    }
                    onSuccess?.Invoke(name);
                }
                catch (Exception e)
                {
                    onFailure?.Invoke($"Nominatim 파싱 예외: {e.Message}");
                }
            }
        }

        private static string FirstSegment(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return null;
            int comma = displayName.IndexOf(',');
            return comma > 0 ? displayName.Substring(0, comma).Trim() : displayName.Trim();
        }

        // ───────────────────────────────────────────
        // Haversine 거리 (미터)
        // ───────────────────────────────────────────

        private static double HaversineMeters(double lat1, double lng1, double lat2, double lng2)
        {
            const double R = 6371000.0;
            double dLat = (lat2 - lat1) * Math.PI / 180.0;
            double dLng = (lng2 - lng1) * Math.PI / 180.0;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                       Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        // ───────────────────────────────────────────
        // JSON 모델 (JsonUtility — 정의 안 한 필드는 무시됨)
        // ───────────────────────────────────────────

        [Serializable]
        private class OverpassResponse
        {
            public List<OverpassElement> elements;
        }

        [Serializable]
        private class OverpassElement
        {
            public string type;
            public double lat;          // node
            public double lon;          // node
            public OverpassCenter center; // way/relation 중심
            public OverpassTags tags;
        }

        [Serializable]
        private class OverpassCenter
        {
            public double lat;
            public double lon;
        }

        [Serializable]
        private class OverpassTags
        {
            public string amenity;
            public string leisure;
            public string name;
        }

        [Serializable]
        private class NominatimResponse
        {
            public string name;
            public string display_name;
        }
    }
}
