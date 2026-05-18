using System;
using UnityEngine;
using OntologyMetaverse.OnDeviceAI.Gemma;
using OntologyMetaverse.DataCollection.SQLite;

namespace OntologyMetaverse.OnDeviceAI.TripleExtraction
{
    /// <summary>
    /// 사용자 자연어 입력을 Gemma 3n으로 트리플(S, P, O)로 변환하는 추출기
    /// Docs/triple-json-spec.md 규격을 따름
    /// </summary>
    public class TextTripleExtractor : MonoBehaviour
    {
        [Header("Gemma 매니저 참조")]
        [Tooltip("Inspector에서 GemmaOnDeviceManager가 붙은 GameObject 드래그")]
        public GemmaOnDeviceManager gemmaManager;

        // 테스트용 임시 user_uid (나중에 Firebase Auth로 대체)
        private string testUserUid = "user_001";

        /// <summary>
        /// 자연어 텍스트 → 트리플 추출 → SQLite 저장
        /// </summary>
        /// <param name="userInput">사용자 입력 텍스트 (예: "오늘 강남 카페 갔다")</param>
        /// <returns>추출되어 SQLite에 저장된 트리플 개수</returns>
        public int ExtractAndSaveTriples(string userInput)
        {
            // 1. Gemma 준비 상태 확인
            if (gemmaManager == null || !gemmaManager.isModelLoaded)
            {
                Debug.LogWarning("[TextTripleExtractor] Gemma 매니저 미준비");
                return 0;
            }

            Debug.Log($"[TextTripleExtractor] 입력 텍스트: \"{userInput}\"");

            // 2. 프롬프트 생성 (Few-shot 예시 포함)
            string prompt = BuildPrompt(userInput);

            // 3. Gemma 호출
            string response = gemmaManager.GenerateResponse(prompt);
            Debug.Log($"[TextTripleExtractor] Gemma 응답:\n{response}");

            // 4. JSON 파싱
            TripleJson[] triples = ParseTriples(response);
            if (triples == null || triples.Length == 0)
            {
                Debug.LogWarning("[TextTripleExtractor] 추출된 트리플 없음");
                return 0;
            }

            Debug.Log($"[TextTripleExtractor] {triples.Length}개 트리플 추출 성공");

            // 5. 명세서 규격 검증 + SQLite 저장
            int savedCount = 0;
            foreach (var t in triples)
            {
                if (IsValidTriple(t))
                {
                    SaveToSQLite(t, sourceText: userInput);
                    savedCount++;
                }
                else
                {
                    Debug.LogWarning($"[TextTripleExtractor] 잘못된 형식 트리플 무시: {t}");
                }
            }

            Debug.Log($"[TextTripleExtractor] {savedCount}개 트리플 SQLite 저장 완료");
            return savedCount;
        }

        /// <summary>
        /// Gemma에 보낼 프롬프트 생성 (Few-shot 예시 + 사용자 입력)
        /// </summary>
        private string BuildPrompt(string userInput)
        {
            // 명세서 규칙을 Gemma에게 알려주는 프롬프트
            // Few-shot으로 3개 예시를 보여줘서 출력 형식 학습시킴
            string prompt = @"당신은 자연어 입력을 RDF 트리플(Subject, Predicate, Object)로 변환하는 AI입니다.

규칙:
1. 응답은 반드시 다음 JSON 형식만 포함하세요: {""triples"": [...]}
2. 노드 ID는 ""prod:{타입}_{uid}_{ts}"" 형식을 사용하세요.
3. 문자열은 datatype을 ""xsd:string"", 숫자는 ""xsd:float"" 또는 ""xsd:integer"", 날짜는 ""xsd:date""로 표시하세요.
4. 관계만 표현하는 트리플은 datatype을 null로 두세요.

예시 1) 입력: ""오늘 스타벅스 강남점 갔다""
출력:
{""triples"": [
  {""s"": ""prod:user_" + testUserUid + @""", ""p"": ""prod:visited"", ""o"": ""prod:loc_" + testUserUid + @"_001"", ""datatype"": null},
  {""s"": ""prod:loc_" + testUserUid + @"_001"", ""p"": ""prod:placeName"", ""o"": ""스타벅스 강남점"", ""datatype"": ""xsd:string""},
  {""s"": ""prod:loc_" + testUserUid + @"_001"", ""p"": ""prod:placeType"", ""o"": ""cafe"", ""datatype"": ""xsd:string""}
]}

예시 2) 입력: ""저녁에 파스타 먹었어""
출력:
{""triples"": [
  {""s"": ""prod:user_" + testUserUid + @""", ""p"": ""prod:ate"", ""o"": ""prod:food_" + testUserUid + @"_002"", ""datatype"": null},
  {""s"": ""prod:food_" + testUserUid + @"_002"", ""p"": ""prod:foodName"", ""o"": ""pasta"", ""datatype"": ""xsd:string""}
]}

이제 다음 입력을 변환하세요:
입력: """ + userInput + @"""
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
            // (Gemma가 가끔 앞뒤에 설명을 붙이기 때문)
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
                // JsonUtility로 파싱 (래퍼 클래스 사용)
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
        /// 명세서 규격 검증
        /// </summary>
        private bool IsValidTriple(TripleJson t)
        {
            // 필수 필드 체크
            if (string.IsNullOrEmpty(t.s) || string.IsNullOrEmpty(t.p) || string.IsNullOrEmpty(t.o))
            {
                return false;
            }

            // Subject, Predicate는 prod: prefix 필수 (Object는 리터럴 값일 수 있어서 제외)
            if (!t.s.StartsWith("prod:") || !t.p.StartsWith("prod:"))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 트리플 1개를 SQLite triples 테이블에 저장
        /// </summary>
        private void SaveToSQLite(TripleJson t, string sourceText)
        {
            var triple = new Triple
            {
                Subject = t.s,
                Predicate = t.p,
                Object = t.o,
                Datatype = t.datatype,           // null 가능
                Source = $"text_input:{sourceText}",
                Timestamp = DateTime.UtcNow.ToString("o"),
                Synced = 0                       // 아직 Firestore 미전송
            };

            SQLiteManager.Instance.Connection.Insert(triple);
            Debug.Log($"[TextTripleExtractor] DB 저장: Id={triple.Id}, {triple.Subject} {triple.Predicate} {triple.Object}");
        }
    }
}