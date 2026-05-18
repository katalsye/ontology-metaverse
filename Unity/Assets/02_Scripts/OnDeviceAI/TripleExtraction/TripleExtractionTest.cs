using System.Collections;
using UnityEngine;
using OntologyMetaverse.OnDeviceAI.Gemma;
using OntologyMetaverse.DataCollection.SQLite;

namespace OntologyMetaverse.OnDeviceAI.TripleExtraction
{
    /// <summary>
    /// TextTripleExtractor 동작 테스트
    /// 빈 GameObject에 부착해서 Play 시 자동 실행
    /// </summary>
    public class TripleExtractionTest : MonoBehaviour
    {
        private GemmaOnDeviceManager _gemma;
        private TextTripleExtractor _extractor;

        IEnumerator Start()
        {
            Debug.Log("[TripleExtractionTest] 시작");

            // 1. GemmaOnDeviceManager 컴포넌트 추가
            _gemma = gameObject.AddComponent<GemmaOnDeviceManager>();

            // 2. Gemma 로딩 대기 (Editor mock은 약 1초)
            yield return new WaitUntil(() => _gemma.isModelLoaded);
            Debug.Log("[TripleExtractionTest] Gemma 로딩 완료");

            // 3. TextTripleExtractor 컴포넌트 추가
            _extractor = gameObject.AddComponent<TextTripleExtractor>();
            _extractor.gemmaManager = _gemma;

            // 4. 테스트 입력으로 트리플 추출 시도
            string testInput = "오늘 강남 카페 갔다";
            Debug.Log($"[TripleExtractionTest] 테스트 입력: \"{testInput}\"");

            int savedCount = _extractor.ExtractAndSaveTriples(testInput);
            Debug.Log($"[TripleExtractionTest] {savedCount}개 트리플 저장됨");

            // 5. SQLite triples 테이블 조회
            Debug.Log("[TripleExtractionTest] === SQLite triples 조회 ===");
            var manager = SQLiteManager.Instance;
            var allTriples = manager.Connection.Table<Triple>();
            int count = 0;
            foreach (var t in allTriples)
            {
                Debug.Log($"[TripleExtractionTest] triple[{t.Id}]: " +
                          $"({t.Subject}, {t.Predicate}, {t.Object}) " +
                          $"datatype={t.Datatype}, source={t.Source}");
                count++;
            }
            Debug.Log($"[TripleExtractionTest] DB 총 {count}건");

            Debug.Log("[TripleExtractionTest] 테스트 완료");
        }
    }
}