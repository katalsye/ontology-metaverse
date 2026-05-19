using System;
using UnityEngine;
using OntologyMetaverse.OnDeviceAI.Gemma;
using OntologyMetaverse.DataCollection.SQLite;

namespace OntologyMetaverse.OnDeviceAI.TripleExtraction
{
    /// <summary>
    /// 이미지 입력을 Gemma 3n 멀티모달로 분석하여 트리플(S, P, O)로 변환하는 추출기
    /// Docs/triple-json-spec.md 규격을 따름
    /// TextTripleExtractor와 동일한 패턴, 입력만 텍스트→이미지로 다름
    /// </summary>
    public class ImageTripleExtractor : MonoBehaviour
    {
        [Header("Gemma 매니저 참조")]
        [Tooltip("Inspector에서 GemmaOnDeviceManager가 붙은 GameObject 드래그")]
        public GemmaOnDeviceManager gemmaManager;

        // 테스트용 임시 user_uid (나중에 Firebase Auth로 대체)
        private string testUserUid = "user_001";

        /// <summary>
        /// 이미지 → 트리플 추출 → SQLite 저장
        /// </summary>
        /// <param name="imageBytes">이미지 바이트 배열 (JPEG/PNG 등)</param>
        /// <param name="imagePath">이미지 출처 경로 (디버깅/source 필드용)</param>
        /// <returns>저장된 트리플 개수</returns>
        public int ExtractAndSaveTriples(byte[] imageBytes, string imagePath)
        {
            // 1. Gemma 준비 상태 확인
            if (gemmaManager == null || !gemmaManager.isModelLoaded)
            {
                Debug.LogWarning("[ImageTripleExtractor] Gemma 매니저 미준비");
                return 0;
            }

            // 2. 이미지 데이터 확인
            if (imageBytes == null || imageBytes.Length == 0)
            {
                Debug.LogWarning("[ImageTripleExtractor] 이미지 데이터 비어있음");
                return 0;
            }

            Debug.Log($"[ImageTripleExtractor] 입력 이미지: {imagePath} (크기: {imageBytes.Length} bytes)");

            // 3. 프롬프트 생성 (이미지 분석용)
            string prompt = BuildPrompt();

            // 4. Gemma 멀티모달 호출 (이미지 + 프롬프트)
            string response = gemmaManager.GenerateResponseWithImage(prompt, imageBytes);
            Debug.Log($"[ImageTripleExtractor] Gemma 응답:\n{response}");

            // 5. JSON 파싱
            TripleJson[] triples = ParseTriples(response);
            if (triples == null || triples.Length == 0)
            {
                Debug.LogWarning("[ImageTripleExtractor] 추출된 트리플 없음");
                return 0;
            }

            Debug.Log($"[ImageTripleExtractor] {triples.Length}개 트리플 추출 성공");

            // 6. 검증 + SQLite 저장 (TripleValidator 재사용)
            int savedCount = 0;
            foreach (var t in triples)
            {
                ValidationResult result = TripleValidator.Validate(t);

                if (result.IsValid)
                {
                    SaveToSQLite(t, sourceImagePath: imagePath);
                    savedCount++;
                }
                else
                {
                    Debug.LogWarning($"[ImageTripleExtractor] 검증 실패 (사유: {result.ErrorReason}): {t}");
                }
            }

            Debug.Log($"[ImageTripleExtractor] {savedCount}개 트리플 SQLite 저장 완료");
            return savedCount;
        }

        /// <summary>
        /// 이미지 분석용 프롬프트 생성 (Few-shot 예시 포함)
        /// </summary>
        private string BuildPrompt()
        {
            // 이미지 분석 결과를 트리플로 변환하도록 Gemma에게 지시
            // 음식, 장소, 활동 등 다양한 컨텍스트를 추출하도록 가이드
            string prompt = @"이미지를 분석하여 음식, 장소, 활동 정보를 RDF 트리플(Subject, Predicate, Object)로 변환하세요.

규칙:
1. 응답은 반드시 다음 JSON 형식만 포함하세요: {""triples"": [...]}
2. 노드 ID는 ""prod:photo_" + testUserUid + @"_{ts}"" 형식을 사용하세요.
3. 문자열은 datatype을 ""xsd:string"", 숫자는 ""xsd:float"" 또는 ""xsd:integer""로 표시하세요.
4. 관계만 표현하는 트리플은 datatype을 null로 두세요.

추출할 정보:
- foodType: 음식 종류 (예: pasta, ramen, salad)
- placeType: 장소 유형 (예: restaurant, cafe, park, home)
- activity: 활동 유형 (예: dining, exercise, travel)
- analyzedBy: 분석 도구명 (항상 ""Gemma-3n"")

예시 1) 파스타 사진 (레스토랑에서 촬영):
{""triples"": [
  {""s"": ""prod:user_" + testUserUid + @""", ""p"": ""prod:photographed"", ""o"": ""prod:photo_" + testUserUid + @"_001"", ""datatype"": null},
  {""s"": ""prod:photo_" + testUserUid + @"_001"", ""p"": ""prod:foodType"", ""o"": ""pasta"", ""datatype"": ""xsd:string""},
  {""s"": ""prod:photo_" + testUserUid + @"_001"", ""p"": ""prod:placeType"", ""o"": ""restaurant"", ""datatype"": ""xsd:string""},
  {""s"": ""prod:photo_" + testUserUid + @"_001"", ""p"": ""prod:analyzedBy"", ""o"": ""Gemma-3n"", ""datatype"": ""xsd:string""}
]}

예시 2) 공원 풍경 사진:
{""triples"": [
  {""s"": ""prod:user_" + testUserUid + @""", ""p"": ""prod:photographed"", ""o"": ""prod:photo_" + testUserUid + @"_002"", ""datatype"": null},
  {""s"": ""prod:photo_" + testUserUid + @"_002"", ""p"": ""prod:placeType"", ""o"": ""park"", ""datatype"": ""xsd:string""},
  {""s"": ""prod:photo_" + testUserUid + @"_002"", ""p"": ""prod:activity"", ""o"": ""leisure"", ""datatype"": ""xsd:string""},
  {""s"": ""prod:photo_" + testUserUid + @"_002"", ""p"": ""prod:analyzedBy"", ""o"": ""Gemma-3n"", ""datatype"": ""xsd:string""}
]}

이제 입력된 이미지를 분석하여 트리플을 출력하세요.
출력:";

            return prompt;
        }

        /// <summary>
        /// Gemma 응답 문자열에서 JSON 부분을 찾아 TripleJson 배열로 파싱
        /// (TextTripleExtractor와 동일한 패턴 - 코드 재사용 가능하나 학부생 수준 유지를 위해 인라인)
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
                Debug.LogError("[ImageTripleExtractor] JSON 형식 응답이 아님");
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
                Debug.LogError($"[ImageTripleExtractor] JSON 파싱 실패: {e.Message}");
                Debug.LogError($"[ImageTripleExtractor] 파싱 시도 JSON: {jsonStr}");
                return null;
            }
        }

        /// <summary>
        /// 트리플 1개를 SQLite triples 테이블에 저장
        /// </summary>
        private void SaveToSQLite(TripleJson t, string sourceImagePath)
        {
            var triple = new Triple
            {
                Subject = t.s,
                Predicate = t.p,
                Object = t.o,
                Datatype = t.datatype,
                Source = $"image_input:{sourceImagePath}",  // 이미지 출처 명시
                Timestamp = DateTime.UtcNow.ToString("o"),
                Synced = 0
            };

            SQLiteManager.Instance.Connection.Insert(triple);
            Debug.Log($"[ImageTripleExtractor] DB 저장: Id={triple.Id}, {triple.Subject} {triple.Predicate} {triple.Object}");
        }
    }
}