using System;
using System.Collections.Generic;
using UnityEngine;
using OntologyMetaverse.DataCollection.SQLite;
using OntologyMetaverse.OnDeviceAI.TripleExtraction;

namespace OntologyMetaverse.DataCollection
{
    /// <summary>
    /// SQLite raw_data → SQLite triples 변환 매니저 (자동화 흐름의 심장).
    ///
    /// 설계 원칙:
    /// - structured 센서 데이터 (gps/step/app_usage/sleep)는 코드로 직접 변환
    ///   (Gemma 호출 비용/시간 절약 + 결과 안정성).
    /// - 자연어 일기는 TextTripleExtractor가 별도로 Gemma 호출 (P3a).
    /// - 이미지 EXIF는 P3b에서 ImageTripleExtractor가 처리.
    ///
    /// raw_data 처리 흐름:
    ///   1. Processed=0인 raw_data를 batch로 가져옴 (batchSize 만큼)
    ///   2. type 별로 분기하여 triples 생성
    ///   3. URI 정규화 + Validator 통과한 트리플만 SQLite triples에 저장
    ///      (Synced=0 상태 → 추후 TempTripleManager가 Firestore로 sync)
    ///   4. raw_data.Processed=1 마크 → 다음 batch에서 제외
    ///
    /// 호출 방법:
    ///   var converter = GetComponent&lt;RawDataToTripleConverter&gt;();
    ///   int saved = converter.ConvertBatch();
    ///
    /// 또는 BatchScheduler가 주기적으로 자동 호출.
    /// </summary>
    public class RawDataToTripleConverter : MonoBehaviour
    {
        [Header("Batch 크기 (한 번에 처리할 raw_data 최대 개수)")]
        [Tooltip("크기 너무 크면 SQLite 잠금 길어짐. 5~20 권장.")]
        public int batchSize = 10;

        // 무성님 온톨로지 명세 base URI
        private const string OntologyBaseUri = "http://7team.dev/ontology#";

        // ─────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// 미처리 raw_data를 batch로 처리. 처리된 트리플 수 반환.
        /// </summary>
        public int ConvertBatch()
        {
            var pending = GetUnprocessedRawData(batchSize);
            if (pending.Count == 0)
            {
                Debug.Log("[RawConverter] 처리할 raw_data 없음");
                return 0;
            }

            Debug.Log($"[RawConverter] {pending.Count}건 처리 시작");

            int totalTriples = 0;
            int processedRaws = 0;
            int failedRaws = 0;

            foreach (var raw in pending)
            {
                try
                {
                    List<TripleJson> triples = ConvertSingle(raw);
                    int savedCount = SaveTriples(triples, raw);
                    totalTriples += savedCount;
                    MarkProcessed(raw);
                    processedRaws++;
                    Debug.Log($"[RawConverter] raw[{raw.Id}] type={raw.Type} → {savedCount} triples");
                }
                catch (Exception e)
                {
                    failedRaws++;
                    Debug.LogError($"[RawConverter] raw[{raw.Id}] 처리 실패: {e.Message}\n{e.StackTrace}");
                    // 실패해도 다음 raw는 시도. Processed 마크는 하지 않음 (재시도 가능).
                }
            }

            Debug.Log($"[RawConverter] 완료: raw {processedRaws}/{pending.Count} 처리, " +
                      $"실패 {failedRaws}건, 트리플 {totalTriples}건 신규 저장");
            return totalTriples;
        }

        // ─────────────────────────────────────────────────────
        // SQLite I/O
        // ─────────────────────────────────────────────────────

        private List<RawData> GetUnprocessedRawData(int max)
        {
            var conn = SQLiteManager.Instance.Connection;
            var result = new List<RawData>();
            // Processed=0 미처리 데이터를 오래된 순으로 가져옴 (FIFO)
            var query = conn.Table<RawData>().Where(r => r.Processed == 0).OrderBy(r => r.Id);
            int count = 0;
            foreach (var r in query)
            {
                result.Add(r);
                count++;
                if (count >= max) break;
            }
            return result;
        }

        private int SaveTriples(List<TripleJson> triples, RawData source)
        {
            int saved = 0;
            var conn = SQLiteManager.Instance.Connection;

            foreach (var t in triples)
            {
                NormalizeUri(t);

                var validation = TripleValidator.Validate(t);
                if (!validation.IsValid)
                {
                    Debug.LogWarning($"[RawConverter] 검증 실패 (사유: {validation.ErrorReason}): {t}");
                    continue;
                }

                var sqliteRow = new Triple
                {
                    Subject = t.s,
                    Predicate = t.p,
                    Object = t.o,
                    Datatype = t.datatype,
                    Source = $"raw_data:{source.Type}:{source.Id}",
                    Timestamp = DateTime.UtcNow.ToString("o"),
                    Synced = 0  // TempTripleManager가 Firestore 업로드 후 1로 변경
                };
                conn.Insert(sqliteRow);
                saved++;
            }
            return saved;
        }

        private void MarkProcessed(RawData raw)
        {
            raw.Processed = 1;
            SQLiteManager.Instance.Connection.Update(raw);
        }

        // ─────────────────────────────────────────────────────
        // type 별 변환 로직
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// raw_data 1건 → triples list. type에 따라 다른 변환기 호출.
        /// </summary>
        private List<TripleJson> ConvertSingle(RawData raw)
        {
            switch (raw.Type)
            {
                case "gps":       return ConvertGps(raw);
                case "step":      return ConvertStep(raw);
                case "app_usage": return ConvertAppUsage(raw);
                case "sleep":     return ConvertSleep(raw);
                case "exif":      return ConvertExif(raw);
                case "weather":   return ConvertWeather(raw);
                default:
                    Debug.LogWarning($"[RawConverter] 알 수 없는 type: {raw.Type}");
                    return new List<TripleJson>();
            }
        }

        /// <summary>
        /// GPS 좌표 → Location 노드. placeName/placeType은 비워둠 (보완형 퀘스트 유도).
        /// JSON 형식: {"lat":35.88,"lng":128.60,"alt":50,"accuracy":10}
        /// </summary>
        private List<TripleJson> ConvertGps(RawData raw)
        {
            var json = JsonUtility.FromJson<GpsContent>(raw.Content);
            string locId = $"loc_{raw.Id}";
            string locUri = OntologyBaseUri + locId;
            string userUri = OntologyBaseUri + "user_001"; // TempTripleManager가 실제 uid로 정규화함

            return new List<TripleJson>
            {
                new TripleJson { s = userUri, p = OntologyBaseUri + "hasLocation", o = locUri, datatype = null },
                new TripleJson { s = locUri,  p = OntologyBaseUri + "latitude",    o = json.lat.ToString("F6"), datatype = "xsd:float" },
                new TripleJson { s = locUri,  p = OntologyBaseUri + "longitude",   o = json.lng.ToString("F6"), datatype = "xsd:float" },
                new TripleJson { s = locUri,  p = OntologyBaseUri + "visitTime",   o = raw.Timestamp, datatype = "xsd:dateTime" },
                // placeName, placeType 의도적으로 누락 → 무성님 시스템이 보완형 퀘스트 자동 생성
            };
        }

        /// <summary>
        /// 걸음수 → StepCount 노드.
        /// JSON 형식: {"count":2800,"date":"2026-06-06"}
        /// </summary>
        private List<TripleJson> ConvertStep(RawData raw)
        {
            var json = JsonUtility.FromJson<StepContent>(raw.Content);
            string stepId = $"step_{raw.Id}";
            string stepUri = OntologyBaseUri + stepId;
            string userUri = OntologyBaseUri + "user_001";

            return new List<TripleJson>
            {
                new TripleJson { s = userUri, p = OntologyBaseUri + "hasStepCount", o = stepUri, datatype = null },
                new TripleJson { s = stepUri, p = OntologyBaseUri + "count",        o = json.count.ToString(), datatype = "xsd:integer" },
                new TripleJson { s = stepUri, p = OntologyBaseUri + "date",         o = json.date, datatype = "xsd:date" },
                new TripleJson { s = stepUri, p = OntologyBaseUri + "timestamp",    o = raw.Timestamp, datatype = "xsd:dateTime" },
            };
        }

        /// <summary>
        /// 앱 사용 → AppUsage 노드.
        /// JSON 형식: {"appName":"YouTube","usageDuration":90,"date":"2026-06-06"}
        /// </summary>
        private List<TripleJson> ConvertAppUsage(RawData raw)
        {
            var json = JsonUtility.FromJson<AppUsageContent>(raw.Content);
            string usageId = $"app_{raw.Id}";
            string usageUri = OntologyBaseUri + usageId;
            string userUri = OntologyBaseUri + "user_001";

            return new List<TripleJson>
            {
                new TripleJson { s = userUri,  p = OntologyBaseUri + "hasAppUsage",   o = usageUri, datatype = null },
                new TripleJson { s = usageUri, p = OntologyBaseUri + "appName",       o = json.appName ?? "unknown", datatype = "xsd:string" },
                new TripleJson { s = usageUri, p = OntologyBaseUri + "usageDuration", o = json.usageDuration.ToString(), datatype = "xsd:integer" },
                new TripleJson { s = usageUri, p = OntologyBaseUri + "date",          o = json.date, datatype = "xsd:date" },
            };
        }

        /// <summary>
        /// 수면 → SleepData 노드.
        /// JSON 형식: {"duration":7.5,"quality":80,"deepSleepRatio":0.25,"timestamp":"..."}
        /// </summary>
        private List<TripleJson> ConvertSleep(RawData raw)
        {
            var json = JsonUtility.FromJson<SleepContent>(raw.Content);
            string sleepId = $"sleep_{raw.Id}";
            string sleepUri = OntologyBaseUri + sleepId;
            string userUri = OntologyBaseUri + "user_001";

            return new List<TripleJson>
            {
                new TripleJson { s = userUri,  p = OntologyBaseUri + "hasSleepData",   o = sleepUri, datatype = null },
                new TripleJson { s = sleepUri, p = OntologyBaseUri + "duration",       o = json.duration.ToString("F2"), datatype = "xsd:float" },
                new TripleJson { s = sleepUri, p = OntologyBaseUri + "quality",        o = json.quality.ToString(), datatype = "xsd:integer" },
                new TripleJson { s = sleepUri, p = OntologyBaseUri + "deepSleepRatio", o = json.deepSleepRatio.ToString("F2"), datatype = "xsd:float" },
                new TripleJson { s = sleepUri, p = OntologyBaseUri + "timestamp",      o = raw.Timestamp, datatype = "xsd:dateTime" },
            };
        }

        /// <summary>
        /// EXIF (갤러리 사진) → GalleryPhoto 노드.
        /// JSON 형식: {"image_path":"...","lat":35.88,"lng":128.60,"capture_time":"..."}
        ///
        /// 주의: foodType/placeType은 사진 분석 필요 (Gemma multimodal).
        /// 여기서는 위치/시간만 추출하고 multimodal 추론은 P3b에서.
        /// </summary>
        private List<TripleJson> ConvertExif(RawData raw)
        {
            var json = JsonUtility.FromJson<ExifContent>(raw.Content);
            string photoId = $"photo_{raw.Id}";
            string photoUri = OntologyBaseUri + photoId;
            string userUri = OntologyBaseUri + "user_001";

            return new List<TripleJson>
            {
                new TripleJson { s = userUri,  p = OntologyBaseUri + "hasGalleryPhoto", o = photoUri, datatype = null },
                new TripleJson { s = photoUri, p = OntologyBaseUri + "latitude",        o = json.lat.ToString("F6"), datatype = "xsd:float" },
                new TripleJson { s = photoUri, p = OntologyBaseUri + "longitude",       o = json.lng.ToString("F6"), datatype = "xsd:float" },
                new TripleJson { s = photoUri, p = OntologyBaseUri + "timestamp",       o = json.capture_time ?? raw.Timestamp, datatype = "xsd:dateTime" },
                // foodType, placeType, analyzedBy 는 multimodal Gemma 처리 후 추가 (P3b)
            };
        }

        /// <summary>
        /// Weather (기상청 API) → Weather 노드.
        /// JSON 형식: {"temperature":23.5,"condition":"clear","recordedAt":"2026-06-06T15:00:00"}
        ///
        /// 무성님 명세 (core.ttl):
        ///   prod:Weather, prod:hasWeather, prod:temperature(float), prod:condition(string), prod:recordedAt(dateTime)
        ///
        /// 사용 추론 규칙:
        ///   Rule 7 (IndoorDayPattern): condition CONTAINS "rain" 또는 겨울(12/1/2월)
        ///                              + 방문 장소 2곳 미만 → 실내 중심 패턴 추론
        /// </summary>
        private List<TripleJson> ConvertWeather(RawData raw)
        {
            var json = JsonUtility.FromJson<WeatherContent>(raw.Content);
            string weatherId = $"weather_{raw.Id}";
            string weatherUri = OntologyBaseUri + weatherId;
            string userUri = OntologyBaseUri + "user_001";

            return new List<TripleJson>
            {
                new TripleJson { s = userUri,    p = OntologyBaseUri + "hasWeather",  o = weatherUri, datatype = null },
                new TripleJson { s = weatherUri, p = OntologyBaseUri + "temperature", o = json.temperature.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), datatype = "xsd:float" },
                new TripleJson { s = weatherUri, p = OntologyBaseUri + "condition",   o = json.condition ?? "unknown", datatype = "xsd:string" },
                new TripleJson { s = weatherUri, p = OntologyBaseUri + "recordedAt",  o = json.recordedAt ?? raw.Timestamp, datatype = "xsd:dateTime" },
            };
        }

        // ─────────────────────────────────────────────────────
        // URI 정규화 (TextTripleExtractor와 동일 로직 — LLM 노이즈 흡수용이지만
        // 코드 생성 URI에도 안전하게 한 번 통과시킴)
        // ─────────────────────────────────────────────────────

        private void NormalizeUri(TripleJson t)
        {
            t.s = NormalizeUriString(t.s);
            t.p = NormalizeUriString(t.p);
            t.o = NormalizeUriString(t.o);
        }

        private string NormalizeUriString(string uri)
        {
            if (string.IsNullOrEmpty(uri)) return uri;

            // 슬래시 누락 보정 (LLM 출력 보호 — 코드에는 영향 없음)
            if (uri.Contains("7team.devontology"))
                uri = uri.Replace("7team.devontology", "7team.dev/ontology");

            // prod: prefix → full URI expand (혹시 모를 케이스 대비)
            if (uri.StartsWith("prod:"))
                uri = OntologyBaseUri + uri.Substring("prod:".Length);

            return uri;
        }

        // ─────────────────────────────────────────────────────
        // type별 JSON 스키마 (JsonUtility 호환 — public field, [Serializable])
        // ─────────────────────────────────────────────────────

        [Serializable] private class GpsContent      { public float lat; public float lng; public float alt; public float accuracy; }
        [Serializable] private class StepContent     { public int count; public string date; }
        [Serializable] private class AppUsageContent { public string appName; public int usageDuration; public string date; }
        [Serializable] private class SleepContent    { public float duration; public int quality; public float deepSleepRatio; public string timestamp; }
        [Serializable] private class ExifContent     { public string image_path; public float lat; public float lng; public string capture_time; }
        [Serializable] private class WeatherContent  { public float temperature; public string condition; public string recordedAt; }
    }
}
