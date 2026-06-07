using System.Collections;
using UnityEngine;
using OntologyMetaverse.DataCollection.Location;
using OntologyMetaverse.DataCollection.Step;
using OntologyMetaverse.DataCollection.AppUsage;
using OntologyMetaverse.DataCollection.Gallery;
using OntologyMetaverse.DataCollection.Weather;

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
    ///      - (추후) AppUsageCollector, GalleryEXIFCollector
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

        [Header("변환 + 업로드 (Inspector에서 드래그)")]
        public RawDataToTripleConverter converter;
        public TempTripleManager tempTripleManager;

        // ─────────────────────────────────────────────────────

        private IEnumerator Start()
        {
            if (!autoRunOnStart)
            {
                Debug.Log("[BatchScheduler] autoRunOnStart=false → 수동 호출 대기");
                yield break;
            }

            Debug.Log("[BatchScheduler] === 자동화 시작 ===");

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
                if (stepCollector != null) stepCollector.CollectCurrentSteps();
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
        }
    }
}
