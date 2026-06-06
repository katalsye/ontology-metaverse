using System.Collections;
using UnityEngine;
using OntologyMetaverse.DataCollection.SQLite;

namespace OntologyMetaverse.DataCollection.Gallery
{
    /// <summary>
    /// GalleryEXIFCollector 동작 테스트용 스크립트.
    /// (P3c 자동화 흐름과는 별개 — 단독 테스트용)
    /// </summary>
    public class GalleryEXIFTest : MonoBehaviour
    {
        private GalleryEXIFCollector _collector;

        IEnumerator Start()
        {
            Debug.Log("[GalleryEXIFTest] 시작");

            // 1. Collector 컴포넌트 추가
            _collector = gameObject.AddComponent<GalleryEXIFCollector>();

            // 2. 권한 요청 (Android 13+ READ_MEDIA_IMAGES 또는 12- READ_EXTERNAL_STORAGE)
            yield return StartCoroutine(_collector.RequestPermission());

            // 3. EXIF 수집 실행
            _collector.CollectRecentPhotos();

            // 4. SQLite 저장 결과 확인
            VerifySQLiteData();
        }

        private void VerifySQLiteData()
        {
            Debug.Log("[GalleryEXIFTest] === SQLite raw_data 조회 (exif 타입) ===");
            var manager = SQLiteManager.Instance;
            
            // Type이 "exif"인 데이터만 필터링해서 조회
            var allExif = manager.Connection.Table<RawData>().Where(r => r.Type == "exif");
            
            int count = 0;
            foreach (var row in allExif)
            {
                Debug.Log($"[GalleryEXIFTest] Id={row.Id}, Content={row.Content}");
                count++;
            }
            Debug.Log($"[GalleryEXIFTest] EXIF 총 {count}건 조회 완료");
            Debug.Log("[GalleryEXIFTest] 테스트 완료");
        }
    }
}