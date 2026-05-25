using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using OntologyMetaverse.DataCollection.SQLite;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace OntologyMetaverse.DataCollection.Gallery
{
    /// <summary>
    /// 갤러리 사진의 EXIF 메타데이터(위치, 시간)를 추출하여 SQLite에 저장하는 모듈
    /// 
    /// 동작:
    /// - Editor: 더미 데이터 1건 저장
    /// - 실기기: DCIM/Camera 폴더의 최근 N개 사진 EXIF 추출 후 저장
    /// 
    /// EXIF 라이브러리: MetadataExtractor (NuGet)
    /// </summary>
    public class GalleryEXIFCollector : MonoBehaviour
    {
        [Header("설정")]
        [Tooltip("스캔할 최근 사진 개수")]
        public int recentPhotoCount = 10;

        [Tooltip("실기기에서 스캔할 폴더 경로")]
        public string cameraFolderPath = "/storage/emulated/0/DCIM/Camera";

        /// <summary>
        /// 갤러리 EXIF 수집 시작
        /// </summary>
        public void CollectRecentPhotoEXIF()
        {
            Debug.Log("[GalleryEXIFCollector] 갤러리 EXIF 수집 시작");

#if UNITY_EDITOR
            CollectInEditor();
#else
            CollectInAndroid();
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor 모드: 더미 데이터 1건 저장
        /// </summary>
        private void CollectInEditor()
        {
            Debug.LogWarning("[GalleryEXIFCollector] Editor에서는 더미 데이터 사용");
            SaveToSQLite(
                imagePath: "/storage/emulated/0/DCIM/Camera/dummy_photo.jpg",
                lat: 35.8868f,
                lng: 128.6084f,
                captureTime: DateTime.UtcNow.AddHours(-2).ToString("o")
            );
        }
#else
        /// <summary>
        /// 실기기 모드: 폴더 스캔 + 최근 사진 EXIF 추출
        /// </summary>
        private void CollectInAndroid()
        {
            // 1. 권한 체크
            if (!HasGalleryPermission())
            {
                RequestGalleryPermission();
                
                if (!HasGalleryPermission())
                {
                    Debug.LogWarning("[GalleryEXIFCollector] 갤러리 권한 없음 - 수집 중단");
                    return;
                }
            }

            // 2. 폴더 존재 확인
            if (!Directory.Exists(cameraFolderPath))
            {
                Debug.LogError($"[GalleryEXIFCollector] 폴더 없음: {cameraFolderPath}");
                return;
            }

            // 3. 폴더에서 최근 사진 N개 가져오기
            List<string> recentPhotos = GetRecentPhotos(cameraFolderPath, recentPhotoCount);

            if (recentPhotos.Count == 0)
            {
                Debug.LogWarning("[GalleryEXIFCollector] 폴더에 사진 없음");
                return;
            }

            Debug.Log($"[GalleryEXIFCollector] 최근 사진 {recentPhotos.Count}개 발견");

            // 4. 각 사진의 EXIF 추출 + SQLite 저장
            int savedCount = 0;
            foreach (string photoPath in recentPhotos)
            {
                try
                {
                    if (ExtractEXIF(photoPath, out float lat, out float lng, out string captureTime))
                    {
                        SaveToSQLite(photoPath, lat, lng, captureTime);
                        savedCount++;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[GalleryEXIFCollector] EXIF 추출 실패 ({photoPath}): {e.Message}");
                }
            }

            Debug.Log($"[GalleryEXIFCollector] 수집 완료: {savedCount}/{recentPhotos.Count}건 저장");
        }

        /// <summary>
        /// 폴더에서 .jpg 파일 중 최근 N개 가져오기 (수정 시간 기준)
        /// </summary>
        private List<string> GetRecentPhotos(string folder, int count)
        {
            try
            {
                return Directory.GetFiles(folder, "*.jpg", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .Take(count)
                    .ToList();
            }
            catch (Exception e)
            {
                Debug.LogError($"[GalleryEXIFCollector] 폴더 스캔 실패: {e.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// 사진 파일에서 EXIF 메타데이터 추출
        /// </summary>
        /// <returns>추출 성공 여부</returns>
        private bool ExtractEXIF(string imagePath, out float lat, out float lng, out string captureTime)
        {
            lat = 0;
            lng = 0;
            captureTime = "";

            IEnumerable<MetadataExtractor.Directory> directories = ImageMetadataReader.ReadMetadata(imagePath);

            // GPS 정보 추출
            var gpsDir = directories.OfType<GpsDirectory>().FirstOrDefault();
            if (gpsDir != null)
            {
                var location = gpsDir.GetGeoLocation();
                if (location != null)
                {
                    lat = (float)location.Latitude;
                    lng = (float)location.Longitude;
                }
            }

            // 촬영 시간 추출
            var subIfdDir = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (subIfdDir != null)
            {
                if (subIfdDir.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out DateTime dateTime))
                {
                    captureTime = dateTime.ToString("o");
                }
            }

            // GPS 또는 시간 둘 중 하나는 있어야 함
            if (lat == 0 && lng == 0 && string.IsNullOrEmpty(captureTime))
            {
                return false;
            }

            // 시간 없으면 파일 수정 시간 사용
            if (string.IsNullOrEmpty(captureTime))
            {
                captureTime = File.GetLastWriteTime(imagePath).ToString("o");
            }

            return true;
        }

        /// <summary>
        /// 갤러리 접근 권한 체크
        /// </summary>
        private bool HasGalleryPermission()
        {
            // 안드로이드 13+ : READ_MEDIA_IMAGES
            // 안드로이드 12 이하: READ_EXTERNAL_STORAGE
            return Permission.HasUserAuthorizedPermission("android.permission.READ_MEDIA_IMAGES")
                || Permission.HasUserAuthorizedPermission(Permission.ExternalStorageRead);
        }

        /// <summary>
        /// 갤러리 권한 요청
        /// </summary>
        private void RequestGalleryPermission()
        {
            Debug.Log("[GalleryEXIFCollector] 갤러리 권한 요청");

            // 안드로이드 13+ 와 12 이하 둘 다 요청
            Permission.RequestUserPermission("android.permission.READ_MEDIA_IMAGES");
            Permission.RequestUserPermission(Permission.ExternalStorageRead);
        }
#endif

        /// <summary>
        /// EXIF 데이터를 SQLite raw_data에 저장
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
            Debug.Log($"[GalleryEXIFCollector] EXIF DB 저장 완료: Id={rawData.Id}, image={Path.GetFileName(imagePath)}");
        }
    }
}