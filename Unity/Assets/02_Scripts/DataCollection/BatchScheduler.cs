using System.Collections;
using UnityEngine;
using OntologyMetaverse.DataCollection.Location;
using OntologyMetaverse.DataCollection.Step;
using OntologyMetaverse.DataCollection.AppUsage;
using OntologyMetaverse.DataCollection.Gallery;
using OntologyMetaverse.DataCollection.Weather;
using OntologyMetaverse.DataCollection.Geocoding;
using OntologyMetaverse.DataCollection.Health;
using OntologyMetaverse.DataCollection.Calendar;
using OntologyMetaverse.DataCollection.Spotify;

namespace OntologyMetaverse.DataCollection
{
    /// <summary>
    /// 데이터 수집 + 트리플 변환 + Firestore 업로드의 자동화 스케줄러.
    ///
    /// 전체 흐름:
    ///   1. 시작 시 모든 collector 초기화 (권한 요청 + 센서 등록)
    ///   2. collectionIntervalSeconds 마다 수집 사이클:
    ///      - LocationCollector.CollectCurrentLocation()
    ///      - StepCollector.CollectCurrentSteps()
    ///      - AppUsageCollector.CollectTopApps(), GalleryEXIFCollector.CollectRecentPhotos()
    ///      → SQLite raw_data 에 누적
    ///   3. batchIntervalSeconds 마다 batch 사이클:
    ///      - RawDataToTripleConverter.ConvertBatch() → SQLite triples
    ///      - TempTripleManager.SyncPendingTriples() → Firestore
    ///   4. Firestore 업로드 후 무성님 onWrite 트리거가 자동 추론 실행
    ///
    /// 사용법:
    ///   - 씬에 빈 GameObject 추가 → 이 컴포넌트 부착
    ///   - Inspector에서 4개 매니저 슬롯 드래그 (Location/Step/Converter/TempTriple)
    ///   - Play 또는 빌드 → autoRunOnStart=true 면 자동으로 무한 루프 시작
    ///
    /// 데모/테스트:
    ///   - collectionIntervalSeconds=60, batchIntervalSeconds=120 정도로 짧게 두면
    ///     빌드 후 1~2분만 기다려도 Firestore에 데이터 들어가는 거 확인 가능.
    /// </summary>
    public class BatchScheduler : MonoBehaviour
    {
        [Header("자동 시작")]
        public bool autoRunOnStart = true;

        [Header("수집 사이클 (초)")]
        [Tooltip("각 collector에서 1회 데이터 수집하는 주기. 테스트 60, 운영 300~600.")]
        public int collectionIntervalSeconds = 60;

        [Header("Batch 변환+업로드 사이클 (초)")]
        [Tooltip("raw_data → triples 변환 + Firestore 업로드 주기. 테스트 60, 운영 600. (수집 12건/분 대비 backlog 방지 위해 60s + batchSize 20 조합 권장)")]
        public int batchIntervalSeconds = 60;

        [Header("초기화 대기 (초)")]
        [Tooltip("권한 요청 + 센서 첫 데이터 안정화 시간")]
        public int initWaitSeconds = 20;

        [Header("Collectors (Inspector에서 드래그)")]
        public LocationCollector locationCollector;
        public StepCollector stepCollector;
        public AppUsageCollector appUsageCollector;
        public GalleryEXIFCollector galleryCollector;
        public WeatherCollector weatherCollector;

        [Header("Weather 수집 주기 (분)")]
        [Tooltip("기상청 발표 주기와 맞춰 60분이 자연스러움. 0이면 매 collection 사이클마다.")]
        public int weatherIntervalMinutes = 60;

        // weather 마지막 수집 시각 (수집 주기 조절용)
        private System.DateTime _lastWeatherCollectedAt = System.DateTime.MinValue;

        [Header("Geocoding (Reverse Geocoder)")]
        [Tooltip("카카오 로컬 API로 최신 GPS의 placeName/placeType 추론. 같은 위치 반복 호출은 ReverseGeocoder가 자동 스킵.")]
        public ReverseGeocoder geocoder;

        [Header("Geocoding 수집 주기 (분)")]
        [Tooltip("위치는 자주 안 바뀌므로 10분이면 충분. 일 호출 144건 (카카오 무료 10만건 한도 대비 무시 가능).")]
        public int geocodeIntervalMinutes = 10;

        private System.DateTime _lastGeocodedAt = System.DateTime.MinValue;

        [Header("Health Connect (Sleep/Steps/HeartRate)")]
        [Tooltip("androidx.health.connect SDK로 수면·걸음·심박수 수집. 권한 없으면 첫 호출 시 자동으로 Health Connect 앱 띄움.")]
        public HealthConnectCollector healthCollector;

        [Header("Health 수집 주기 (분)")]
        [Tooltip("Health Connect는 24시간 윈도우로 read하므로 30분이면 충분. 너무 잦으면 배터리·중복.")]
        public int healthIntervalMinutes = 30;

        private System.DateTime _lastHealthCollectedAt = System.DateTime.MinValue;

        [Header("Calendar Collector")]
        [Tooltip("로컬 캘린더(CalendarContract) 이벤트 수집. 첫 호출 시 READ_CALENDAR 권한 자동 요청.")]
        public CalendarCollector calendarCollector;

        [Header("Calendar 수집 주기 (분)")]
        [Tooltip("캘린더는 자주 안 바뀜. 60분이면 충분. 14일~7일 윈도우 한 번에 조회.")]
        public int calendarIntervalMinutes = 60;

        private System.DateTime _lastCalendarCollectedAt = System.DateTime.MinValue;

        [Header("Spotify Collector (OAuth + Web API)")]
        [Tooltip("Spotify 최근 재생 50건 조회. 첫 호출 시 시스템 브라우저로 OAuth 인증.")]
        public SpotifyCollector spotifyCollector;

        [Header("Spotify 수집 주기 (분)")]
        [Tooltip("recently-played는 최대 50건 보관. 30분 주기면 거의 모든 재생 캡처. 너무 잦으면 rate limit.")]
        public int spotifyIntervalMinutes = 30;

        private System.DateTime _lastSpotifyCollectedAt = System.DateTime.MinValue;

        [Header("변환 + 업로드 (Inspector에서 드래그)")]
        public RawDataToTripleConverter converter;
        public TempTripleManager tempTripleManager;

        [Header("raw_data Retention (일)")]
        [Tooltip("이 일수보다 오래된 Processed=1 raw_data 자동 삭제. 7일 권장. 0이면 비활성 (발표 데모용).")]
        public int rawDataRetentionDays = 7;

        // ─────────────────────────────────────────────────────

        private IEnumerator Start()
        {
            if (!autoRunOnStart)
            {
                Debug.Log("[BatchScheduler] autoRunOnStart=false → 수동 호출 대기");
                yield break;
            }

            Debug.Log("[BatchScheduler] === 자동화 시작 ===");

            // 온보딩 미완료 시 LoginFlow의 권한 토글을 누르기 전에
            // 여기서 시스템 권한 팝업/OAuth 동의 화면이 먼저 떠버리는 것 방지.
            // LoginFlowScreenController.OnStartClicked()가 onboarding_complete를 설정할 때까지 대기.
            while (!PlayerPrefs.HasKey("onboarding_complete"))
                yield return new WaitForSeconds(0.5f);

            // 1. Collectors 초기화 (권한 요청 + 센서 등록)
            if (locationCollector != null)
            {
                yield return StartCoroutine(locationCollector.StartLocationService());
            }
            else Debug.LogWarning("[BatchScheduler] locationCollector 미연결");

            if (stepCollector != null)
            {
                yield return StartCoroutine(stepCollector.StartCollection());
            }
            else Debug.LogWarning("[BatchScheduler] stepCollector 미연결");

            if (galleryCollector != null)
            {
                yield return StartCoroutine(galleryCollector.RequestPermission());
            }

            if (appUsageCollector != null)
            {
                // PACKAGE_USAGE_STATS는 코루틴 대기 불필요 (한 번 체크 + 설정 화면 오픈)
                appUsageCollector.EnsurePermission();
            }

            // Health Connect · Calendar · Spotify는 첫 cycle에 동시 발사하면 화면이 서로 가려서
            // 사용자가 권한을 못 본 채 묻혀버린다 (실제로 그 버그 재현됨). 여기서 직렬로 받아둔다.
            if (healthCollector != null)
            {
                yield return StartCoroutine(healthCollector.EnsurePermissionInteractive());
            }

            if (calendarCollector != null)
            {
                yield return StartCoroutine(calendarCollector.EnsurePermissionInteractive());
            }

            if (spotifyCollector != null)
            {
                yield return StartCoroutine(spotifyCollector.EnsureAuthInteractive());
            }

            // 2. 센서 안정화 대기
            Debug.Log($"[BatchScheduler] 초기화 대기 {initWaitSeconds}초");
            yield return new WaitForSeconds(initWaitSeconds);

            // 3. 첫 수집 즉시 1회 + 무한 루프 시작
            CollectAll();

            StartCoroutine(CollectionLoop());
            StartCoroutine(BatchLoop());

            Debug.Log($"[BatchScheduler] 루프 진입. 수집 주기={collectionIntervalSeconds}s, batch 주기={batchIntervalSeconds}s");
        }

        private IEnumerator CollectionLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(collectionIntervalSeconds);
                CollectAll();
            }
        }

        private IEnumerator BatchLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(batchIntervalSeconds);
                RunBatch();
            }
        }

        /// <summary>
        /// 모든 collector에서 데이터 1회씩 수집 → SQLite raw_data.
        /// 수동 호출 가능 (UI 버튼 등).
        /// </summary>
        public void CollectAll()
        {
            Debug.Log("[BatchScheduler] === 수집 사이클 ===");
            try
            {
                if (locationCollector != null) locationCollector.CollectCurrentLocation();
            }
            catch (System.Exception e) { Debug.LogError($"[BatchScheduler] Location 수집 실패: {e.Message}"); }

            try
            {
                // Health Connect가 정확한 일별 걸음수를 제공하면 센서 기반 StepCollector는 스킵.
                // (센서는 "부팅 후 누적값"이라 Rule 3/P1의 일별 임계값과 안 맞음 — 이중 수집·오발동 방지)
                bool healthHandlesSteps = healthCollector != null && healthCollector.HasProvidedSteps;
                if (stepCollector != null && !healthHandlesSteps) stepCollector.CollectCurrentSteps();
            }
            catch (System.Exception e) { Debug.LogError($"[BatchScheduler] Step 수집 실패: {e.Message}"); }

            try
            {
                if (appUsageCollector != null) appUsageCollector.CollectTopApps();
            }
            catch (System.Exception e) { Debug.LogError($"[BatchScheduler] AppUsage 수집 실패: {e.Message}"); }

            try
            {
                if (galleryCollector != null) galleryCollector.CollectRecentPhotos();
            }
            catch (System.Exception e) { Debug.LogError($"[BatchScheduler] Gallery 수집 실패: {e.Message}"); }

            // Weather는 1시간 간격 (기상청 발표 주기 매칭). 비동기라 코루틴으로 발사.
            try
            {
                if (weatherCollector != null && (System.DateTime.UtcNow - _lastWeatherCollectedAt).TotalMinutes >= weatherIntervalMinutes)
                {
                    StartCoroutine(weatherCollector.CollectCurrentWeather());
                    _lastWeatherCollectedAt = System.DateTime.UtcNow;
                }
            }
            catch (System.Exception e) { Debug.LogError($"[BatchScheduler] Weather 수집 실패: {e.Message}"); }

            // Geocoding은 10분 간격. ReverseGeocoder가 source_gps_id 중복 자동 스킵.
            try
            {
                if (geocoder != null && (System.DateTime.UtcNow - _lastGeocodedAt).TotalMinutes >= geocodeIntervalMinutes)
                {
                    StartCoroutine(geocoder.GeocodeLatestLocation());
                    _lastGeocodedAt = System.DateTime.UtcNow;
                }
            }
            catch (System.Exception e) { Debug.LogError($"[BatchScheduler] Geocoding 실패: {e.Message}"); }

            // Health Connect는 30분 간격. 24h 윈도우라 자주 호출할 필요 없음.
            // 권한 없으면 호출 자체 스킵 + _last 업데이트 안 함 → 다음 cycle에서 재시도 (예전엔 실패해도 30분 잠겨버렸음).
            try
            {
                if (healthCollector != null && (System.DateTime.UtcNow - _lastHealthCollectedAt).TotalMinutes >= healthIntervalMinutes)
                {
                    if (healthCollector.HasPermission())
                    {
                        StartCoroutine(healthCollector.CollectHealthData());
                        _lastHealthCollectedAt = System.DateTime.UtcNow;
                    }
                    else
                    {
                        Debug.Log("[BatchScheduler] Health 권한 미허용 → 수집 스킵 (다음 cycle 재시도)");
                    }
                }
            }
            catch (System.Exception e) { Debug.LogError($"[BatchScheduler] Health 수집 실패: {e.Message}"); }

            // Calendar는 60분 간격. event_id 중복 체크 자동.
            try
            {
                if (calendarCollector != null && (System.DateTime.UtcNow - _lastCalendarCollectedAt).TotalMinutes >= calendarIntervalMinutes)
                {
                    if (calendarCollector.HasPermission())
                    {
                        StartCoroutine(calendarCollector.CollectCalendarEvents());
                        _lastCalendarCollectedAt = System.DateTime.UtcNow;
                    }
                    else
                    {
                        Debug.Log("[BatchScheduler] Calendar 권한 미허용 → 수집 스킵 (다음 cycle 재시도)");
                    }
                }
            }
            catch (System.Exception e) { Debug.LogError($"[BatchScheduler] Calendar 수집 실패: {e.Message}"); }

            // Spotify는 30분 간격. (track_id + played_at) 기반 중복 자동 스킵.
            try
            {
                if (spotifyCollector != null && (System.DateTime.UtcNow - _lastSpotifyCollectedAt).TotalMinutes >= spotifyIntervalMinutes)
                {
                    if (spotifyCollector.HasAuth())
                    {
                        StartCoroutine(spotifyCollector.CollectRecentTracks());
                        _lastSpotifyCollectedAt = System.DateTime.UtcNow;
                    }
                    else
                    {
                        Debug.Log("[BatchScheduler] Spotify 미인증 → 수집 스킵 (다음 cycle 재시도)");
                    }
                }
            }
            catch (System.Exception e) { Debug.LogError($"[BatchScheduler] Spotify 수집 실패: {e.Message}"); }
        }

        /// <summary>
        /// SQLite raw_data → triples 변환 → Firestore 업로드.
        /// 수동 호출 가능 (UI 버튼, 즉시 sync 등).
        /// </summary>
        public void RunBatch()
        {
            Debug.Log("[BatchScheduler] === Batch 사이클 ===");

            int newTriples = 0;
            if (converter != null)
            {
                newTriples = converter.ConvertBatch();
            }
            else
            {
                Debug.LogWarning("[BatchScheduler] converter 미연결 → 변환 스킵");
                return;
            }

            if (newTriples == 0)
            {
                Debug.Log("[BatchScheduler] 신규 트리플 없음 → Firestore 업로드 스킵");
                return;
            }

            if (tempTripleManager == null)
            {
                Debug.LogWarning("[BatchScheduler] tempTripleManager 미연결 → 업로드 스킵");
                return;
            }

            tempTripleManager.SyncPendingTriples(
                onSuccess: cnt => Debug.Log($"[BatchScheduler] ✅✅ Firestore 업로드 성공: {cnt}건"),
                onFailure: msg => Debug.LogError($"[BatchScheduler] ❌ Firestore 업로드 실패: {msg}")
            );

            // batch 끝나면 처리 완료된 오래된 raw_data 정리.
            // Processed=0 (변환 안 됨) · synced=0 트리플 원본은 안 건드림.
            if (rawDataRetentionDays > 0)
            {
                int deleted = OntologyMetaverse.DataCollection.SQLite.SQLiteManager.Instance
                    .CleanupProcessedRawData(rawDataRetentionDays);
                if (deleted > 0)
                    Debug.Log($"[BatchScheduler] raw_data retention: {deleted}건 정리 ({rawDataRetentionDays}일 이전)");
            }
        }
    }
}
