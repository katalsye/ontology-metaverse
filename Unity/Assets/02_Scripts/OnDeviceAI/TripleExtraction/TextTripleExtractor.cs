using System;
using UnityEngine;
using OntologyMetaverse.OnDeviceAI.Gemma;
using OntologyMetaverse.DataCollection.SQLite;

namespace OntologyMetaverse.OnDeviceAI.TripleExtraction
{
    /// <summary>
    /// 사용자 텍스트 입력을 Gemma 3n으로 분석하여 트리플(S, P, O)로 변환하는 추출기
    /// 명세서: Docs/triple-json-spec.md 규격 준수
    /// 무성님 명세 반영: full URI, prod:hasLocation predicate 사용
    /// </summary>
    public class TextTripleExtractor : MonoBehaviour
    {
        [Header("Gemma 매니저 참조")]
        [Tooltip("Inspector에서 GemmaOnDeviceManager가 붙은 GameObject 드래그")]
        public GemmaOnDeviceManager gemmaManager;

        // 온톨로지 base URI (무성님 명세)
        private const string OntologyBaseUri = "http://7team.dev/ontology#";

        // 테스트용 임시 user_uid (나중에 Firebase Auth uid로 대체)
        private string testUserUid = "user_001";

        /// <summary>
        /// 텍스트 입력 → 트리플 추출 → SQLite 저장
        /// </summary>
        /// <param name="userInput">사용자가 입력한 자연어 (예: "오늘 강남 카페 갔다")</param>
        /// <returns>저장된 트리플 개수</returns>
        public int ExtractAndSaveTriples(string userInput)
        {
            // 1. Gemma 준비 상태 확인
            if (gemmaManager == null || !gemmaManager.isModelLoaded)
            {
                Debug.LogWarning("[TextTripleExtractor] Gemma 매니저 미준비");
                return 0;
            }

            // 2. 입력 확인
            if (string.IsNullOrWhiteSpace(userInput))
            {
                Debug.LogWarning("[TextTripleExtractor] 입력 텍스트 비어있음");
                return 0;
            }

            Debug.Log($"[TextTripleExtractor] 입력: {userInput}");

            // 3. 프롬프트 생성
            string prompt = BuildPrompt(userInput);

            // 4. Gemma 호출
            string response = gemmaManager.GenerateResponse(prompt);
            Debug.Log($"[TextTripleExtractor] Gemma 응답:\n{response}");

            // 5. JSON 파싱
            TripleJson[] triples = ParseTriples(response);
            if (triples == null || triples.Length == 0)
            {
                Debug.LogWarning("[TextTripleExtractor] 추출된 트리플 없음");
                return 0;
            }

            Debug.Log($"[TextTripleExtractor] {triples.Length}개 트리플 추출 성공");

            // 6. 검증 + SQLite 저장 (TripleValidator 재사용)
            int savedCount = 0;
            foreach (var t in triples)
            {
                ValidationResult result = TripleValidator.Validate(t);

                if (result.IsValid)
                {
                    SaveToSQLite(t, sourceInput: userInput);
                    savedCount++;
                }
                else
                {
                    Debug.LogWarning($"[TextTripleExtractor] 검증 실패 (사유: {result.ErrorReason}): {t}");
                }
            }

            Debug.Log($"[TextTripleExtractor] {savedCount}개 트리플 SQLite 저장 완료");
            return savedCount;
        }

        /// <summary>
        /// 텍스트 분석용 프롬프트 생성 (Few-shot 예시 포함)
        /// 무성님 명세: full URI 형식 + prod:hasLocation 사용
        /// </summary>
        private string BuildPrompt(string userInput)
        {
            string baseUri = OntologyBaseUri;
            string prompt = @"다음 사용자 입력을 RDF 트리플(Subject, Predicate, Object)로 변환하세요.

규칙:
1. 응답은 반드시 다음 JSON 형식만 포함하세요: {""triples"": [...]}
2. Subject와 Object의 URI는 ""http://7team.dev/ontology#"" 로 시작하는 full URI 형식을 사용하세요.
3. Predicate는 ""prod:"" 약식을 사용하세요. (예: prod:hasLocation)
4. 문자열은 datatype을 ""xsd:string"", 숫자는 ""xsd:float"" 또는 ""xsd:integer""로 표시하세요.
5. 관계만 표현하는 트리플은 datatype을 null로 두세요.

사용할 수 있는 Predicate (무성님 온톨로지 명세):
- prod:hasLocation: 사용자가 방문한 장소
- prod:placeName: 장소 이름 (문자열)
- prod:placeType: 장소 유형 (cafe, restaurant, park 등)
- prod:visitTime: 방문 시각 (ISO 8601 datetime)

예시 1) ""오늘 강남 카페 갔다""
{""triples"": [
  {""s"": """ + baseUri + @"user_" + testUserUid + @""", ""p"": ""prod:hasLocation"", ""o"": """ + baseUri + @"loc_001"", ""datatype"": null},
  {""s"": """ + baseUri + @"loc_001"", ""p"": ""prod:placeName"", ""o"": ""강남 카페"", ""datatype"": ""xsd:string""},
  {""s"": """ + baseUri + @"loc_001"", ""p"": ""prod:placeType"", ""o"": ""cafe"", ""datatype"": ""xsd:string""}
]}

예시 2) ""홍대 공원에서 산책했다""
{""triples"": [
  {""s"": """ + baseUri + @"user_" + testUserUid + @""", ""p"": ""prod:hasLocation"", ""o"": """ + baseUri + @"loc_002"", ""datatype"": null},
  {""s"": """ + baseUri + @"loc_002"", ""p"": ""prod:placeName"", ""o"": ""홍대 공원"", ""datatype"": ""xsd:string""},
  {""s"": """ + baseUri + @"loc_002"", ""p"": ""prod:placeType"", ""o"": ""park"", ""datatype"": ""xsd:string""}
]}

이제 다음 입력을 변환하세요.
입력: " + userInput + @"
출력:";

            return prompt;
        }

        /// <summary>
        /// Gemma 응답 문자열에서 JSON 부분을 찾아 TripleJson 배열로 파싱
        /// </summary>
        private TripleJson[] ParseTriples(string response)
        {
            if (string.IsNullOrEmpty(response))
            {
                return null;
            }

            // 응답 안에서 "{" 부터 "}"까지 JSON 부분만 추출
            int startIdx = response.IndexOf('{');
            int endIdx = response.LastIndexOf('}');

            if (startIdx == -1 || endIdx == -1 || startIdx >= endIdx)
            {
                Debug.LogError("[TextTripleExtractor] JSON 형식 응답이 아님");
                return null;
            }

            string jsonStr = response.Substring(startIdx, endIdx - startIdx + 1);

            try
            {
                TripleJsonResponse parsed = JsonUtility.FromJson<TripleJsonResponse>(jsonStr);
                return parsed.triples;
            }
            catch (Exception e)
            {
                Debug.LogError($"[TextTripleExtractor] JSON 파싱 실패: {e.Message}");
                Debug.LogError($"[TextTripleExtractor] 파싱 시도 JSON: {jsonStr}");
                return null;
            }
        }

        /// <summary>
        /// 트리플 1개를 SQLite triples 테이블에 저장
        /// </summary>
        private void SaveToSQLite(TripleJson t, string sourceInput)
        {
            var triple = new Triple
            {
                Subject = t.s,
                Predicate = t.p,
                Object = t.o,
                Datatype = t.datatype,
                Source = $"text_input:{sourceInput}",
                Timestamp = DateTime.UtcNow.ToString("o"),
                Synced = 0
            };

            SQLiteManager.Instance.Connection.Insert(triple);
            Debug.Log($"[TextTripleExtractor] DB 저장: Id={triple.Id}, {triple.Subject} {triple.Predicate} {triple.Object}");
        }
    }
}