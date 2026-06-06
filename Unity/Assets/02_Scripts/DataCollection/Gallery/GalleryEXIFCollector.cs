using System;
using UnityEngine;
using UnityEngine.Android;
using OntologyMetaverse.DataCollection.SQLite;

namespace OntologyMetaverse.DataCollection.Gallery
{
    /// <summary>
    /// 갤러리 사진의 EXIF (위치, 시각) 자동 수집기.
    ///
    /// 동작:
    ///   1. READ_MEDIA_IMAGES (Android 13+) 또는 READ_EXTERNAL_STORAGE (12-) 권한 요청
    ///   2. Plugins/Android/GalleryHelper.java 호출하여 최근 N장 EXIF JSON 수신
    ///   3. 각 사진을 raw_data (type=exif) 1건씩 저장
    ///
    /// RawDataToTripleConverter가 이 raw_data를 photo 노드 트리플로 변환.
    /// foodType/placeType 같은 의미 정보는 P3b (multimodal Gemma) 후속에서 처리.
    /// </summary>
    public class GalleryEXIFCollector : MonoBehaviour
    {
        [Header("최근 N장 사진 수집")]
        public int maxPhotos = 5;

        public bool PermissionGranted { get; private set; }

        /// <summary>
        /// 저장소 권한 요청. 한 번 호출하면 사용자 응답 대기 후 결과 반영.
        /// Android 13+: READ_MEDIA_IMAGES, 12-: READ_EXTERNAL_STORAGE
        /// </summary>
        public System.Collections.IEnumerator RequestPermission()
        {
#if UNITY_ANDROID
            string perm = (Application.platform == RuntimePlatform.Android &&
                           SystemInfo.operatingSystem.Contains("API-3") /* 13+: 30+ */)
                ? "android.permission.READ_MEDIA_IMAGES"
                : Permission.ExternalStorageRead;

            // 위 추정이 정확치 않을 수 있어 두 권한 다 시도
            string[] candidates = new[] { "android.permission.READ_MEDIA_IMAGES", Permission.ExternalStorageRead };

            foreach (var p in candidates)
            {
                if (Permission.HasUserAuthorizedPermission(p))
                {
                    PermissionGranted = true;
                    yield break;
                }
            }

            Debug.Log("[GalleryEXIFCollector] 저장소 권한 요청");
            Permission.RequestUserPermission(candidates[0]); // 13+ 우선 시도
            float wait = 0;
            while (wait < 10f)
            {
                yield return new WaitForSeconds(0.5f);
                wait += 0.5f;
                foreach (var p in candidates)
                {
                    if (Permission.HasUserAuthorizedPermission(p))
                    {
                        PermissionGranted = true;
                        Debug.Log($"[GalleryEXIFCollector] 권한 허용됨: {p}");
                        yield break;
                    }
                }
            }
            Debug.LogWarning("[GalleryEXIFCollector] 권한 미허용 → 갤러리 수집 비활성");
            PermissionGranted = false;
#else
            PermissionGranted = true;
            yield break;
#endif
        }

        /// <summary>
        /// 최근 N장 사진의 EXIF를 SQLite raw_data 에 저장.
        /// BatchScheduler가 주기적으로 호출.
        /// </summary>
        public void CollectRecentPhotos()
        {
            if (!PermissionGranted)
            {
                Debug.LogWarning("[GalleryEXIFCollector] 권한 미허용 → 스킵");
                return;
            }

            string json = ReadRecentPhotosJson(maxPhotos);
            if (string.IsNullOrEmpty(json) || json == "[]")
            {
                Debug.Log("[GalleryEXIFCollector] 수집된 사진 없음");
                return;
            }

            string wrapped = "{\"items\":" + json + "}";
            PhotoList list;
            try { list = JsonUtility.FromJson<PhotoList>(wrapped); }
            catch (Exception e)
            {
                Debug.LogError($"[GalleryEXIFCollector] JSON 파싱 실패: {e.Message}\n{json}");
                return;
            }

            if (list?.items == null || list.items.Length == 0)
            {
                Debug.Log("[GalleryEXIFCollector] 빈 items");
                return;
            }

            int saved = 0;
            string nowIso = DateTime.UtcNow.ToString("o");
            foreach (var p in list.items)
            {
                // GPS 0,0이면 위치 정보 없는 사진 — 그래도 저장 (시간만이라도 활용)
                string content = $"{{\"image_path\":\"{EscapeJson(p.image_path)}\",\"lat\":{p.lat},\"lng\":{p.lng},\"capture_time\":\"{p.capture_time}\"}}";
                var raw = new RawData
                {
                    Type = "exif",
                    Content = content,
                    Timestamp = nowIso,
                    Processed = 0
                };
                SQLiteManager.Instance.Connection.Insert(raw);
                saved++;
            }
            Debug.Log($"[GalleryEXIFCollector] {saved}건 저장");
        }

        private string ReadRecentPhotosJson(int max)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var cls = new AndroidJavaClass("com.ontology.metaverse.gallery.GalleryHelper"))
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                return cls.CallStatic<string>("getRecentPhotos", activity, max);
            }
#else
            return "[{\"image_path\":\"editor_mock.jpg\",\"lat\":35.88,\"lng\":128.60,\"capture_time\":\"2026-06-06T12:00:00\"}]";
#endif
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        [Serializable] private class PhotoEntry { public string image_path; public float lat; public float lng; public string capture_time; }
        [Serializable] private class PhotoList { public PhotoEntry[] items; }
    }
}
