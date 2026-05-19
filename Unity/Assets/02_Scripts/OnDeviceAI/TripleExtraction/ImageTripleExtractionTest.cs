using System.Collections;
using UnityEngine;
using OntologyMetaverse.OnDeviceAI.Gemma;
using OntologyMetaverse.DataCollection.SQLite;

namespace OntologyMetaverse.OnDeviceAI.TripleExtraction
{
    /// <summary>
    /// ImageTripleExtractor 동작 테스트
    /// 빈 GameObject에 부착해서 Play 시 자동 실행
    /// Editor에서는 가짜 이미지 바이트로 mock 응답 흐름 검증
    /// </summary>
    public class ImageTripleExtractionTest : MonoBehaviour
    {
        private GemmaOnDeviceManager _gemma;
        private ImageTripleExtractor _extractor;

        IEnumerator Start()
        {
            Debug.Log("[ImageTripleExtractionTest] 시작");

            // 1. GemmaOnDeviceManager 컴포넌트 추가
            _gemma = gameObject.AddComponent<GemmaOnDeviceManager>();

            // 2. Gemma 로딩 대기
            yield return new WaitUntil(() => _gemma.isModelLoaded);
            Debug.Log("[ImageTripleExtractionTest] Gemma 로딩 완료");

            // 3. ImageTripleExtractor 컴포넌트 추가
            _extractor = gameObject.AddComponent<ImageTripleExtractor>();
            _extractor.gemmaManager = _gemma;

            // 4. 테스트용 가짜 이미지 바이트 생성
            //    실제 동작은 안드로이드 실기기에서 갤러리 사진을 읽어 byte[]로 전달
            //    Editor에선 GenerateResponseWithImage가 mock 응답을 반환하므로 내용은 무관
            byte[] fakeImageBytes = CreateFakeImageBytes();
            string fakeImagePath = "/storage/emulated/0/DCIM/Camera/test_food.jpg";

            Debug.Log($"[ImageTripleExtractionTest] 가짜 이미지 입력: " +
                      $"{fakeImagePath} (크기: {fakeImageBytes.Length} bytes)");

            // 5. 이미지 → 트리플 추출 시도
            int savedCount = _extractor.ExtractAndSaveTriples(fakeImageBytes, fakeImagePath);
            Debug.Log($"[ImageTripleExtractionTest] {savedCount}개 트리플 저장됨");

            // 6. SQLite triples 테이블에서 이미지 출처 트리플만 조회
            Debug.Log("[ImageTripleExtractionTest] === SQLite triples 조회 (image_input만) ===");
            var manager = SQLiteManager.Instance;
            var imageTriples = manager.Connection.Table<Triple>()
                .Where(t => t.Source.StartsWith("image_input:"));
            int count = 0;
            foreach (var t in imageTriples)
            {
                Debug.Log($"[ImageTripleExtractionTest] triple[{t.Id}]: " +
                          $"({t.Subject}, {t.Predicate}, {t.Object}) " +
                          $"datatype={t.Datatype}, source={t.Source}");
                count++;
            }
            Debug.Log($"[ImageTripleExtractionTest] 이미지 출처 트리플 총 {count}건");

            Debug.Log("[ImageTripleExtractionTest] 테스트 완료");
        }

        /// <summary>
        /// 테스트용 가짜 이미지 바이트 배열 생성
        /// Editor mock 테스트라 실제 이미지 데이터는 필요 없음
        /// 단순히 byte[] 인터페이스 검증용
        /// </summary>
        private byte[] CreateFakeImageBytes()
        {
            // 작은 더미 바이트 (1KB 정도)
            byte[] fakeBytes = new byte[1024];
            for (int i = 0; i < fakeBytes.Length; i++)
            {
                fakeBytes[i] = (byte)(i % 256);
            }
            return fakeBytes;
        }
    }
}