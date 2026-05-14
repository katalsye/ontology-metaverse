using System;
using UnityEngine;
using OntologyMetaverse.DataCollection.SQLite;

namespace OntologyMetaverse.DataCollection.Gallery
{
    /// <summary>
    /// 갤러리 사진의 EXIF 메타데이터(위치, 시간)를 추출하여 SQLite에 저장하는 모듈
    /// </summary>
    public class GalleryEXIFCollector : MonoBehaviour
    {
        public void CollectRecentPhotoEXIF()
        {
            Debug.Log("[GalleryEXIFCollector] 갤러리 EXIF 수집 로직 시작");

            // Unity Editor 환경 방어 로직
#if UNITY_EDITOR
            Debug.LogWarning("[GalleryEXIFCollector] Unity Editor에서는 안드로이드 갤러리 접근이 불가합니다. 더미 데이터를 저장합니다.");
            SaveToSQLite("/storage/emulated/0/DCIM/Camera/dummy_photo.jpg", 35.8868f, 128.6084f, DateTime.UtcNow.AddHours(-2).ToString("o"));
            return;
#endif

            // 실제 안드로이드 실기기 로직 (Java 브릿지 활용)
            try
            {
                Debug.Log("[GalleryEXIFCollector] 안드로이드 MediaStore 및 ExifInterface 접근 (추후 Native Plugin 연동)");
                // TODO: JNI를 활용한 실제 EXIF 추출 로직 구현부
            }
            catch (Exception e)
            {
                Debug.LogError($"[GalleryEXIFCollector] EXIF 추출 실패: {e.Message}");
            }
        }

        /// <summary>
        /// 추출된 EXIF 데이터를 SQLite raw_data에 저장
        /// </summary>
        private void SaveToSQLite(string imagePath, float lat, float lng, string captureTime)
        {
            string content = $"{{\"image_path\":\"{imagePath}\",\"lat\":{lat},\"lng\":{lng},\"capture_time\":\"{captureTime}\"}}";
            
            var rawData = new RawData
            {
                Type = "exif",
                Content = content,
                Timestamp = DateTime.UtcNow.ToString("o")
            };

            SQLiteManager.Instance.Connection.Insert(rawData);
            Debug.Log($"[GalleryEXIFCollector] EXIF DB 저장 완료: Id={rawData.Id}");
        }
    }
}