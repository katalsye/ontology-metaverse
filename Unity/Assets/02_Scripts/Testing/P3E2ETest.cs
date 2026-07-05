#if UNITY_EDITOR
using System.Collections;
using UnityEngine;
using OntologyMetaverse.OnDeviceAI.Gemma;
using OntologyMetaverse.OnDeviceAI.TripleExtraction;

namespace OntologyMetaverse.Testing
{
    /// <summary>
    /// P3a E2E 통합 테스트.
    ///
    /// 흐름: 텍스트 입력 → Gemma → triples JSON → 검증 → SQLite 저장 → Firestore 업로드.
    ///
    /// 사용법:
    ///   1. 씬에 빈 GameObject 추가 (e.g. "_P3Test")
    ///   2. 이 컴포넌트 부착
    ///   3. Inspector에서 씬에 이미 있는 GemmaOnDeviceManager / TempTripleManager 드래그
    ///      - 둘 다 P1~P4 작업으로 이미 씬에 존재한다고 가정
    ///   4. (옵션) testInput 텍스트 수정
    ///   5. Play 또는 빌드 후 실행 → Gemma 로딩 대기(수십초) → 자동으로 RunOnce() 실행
    ///   6. 검증:
    ///      - logcat: [P3E2ETest] 로그 확인
    ///      - Firebase 콘솔 → Firestore → temp_triples/{uid}/items/{auto_id} 새 문서 확인
    ///
    /// 주의:
    ///   - Gemma 추론은 동기 호출이라 호출 시 메인 스레드가 수 초간 멈춤 (데모용 OK)
    ///   - 한 번 실행하면 Firestore에 트리플이 누적되므로 반복 실행 시 중복 데이터 생김
    ///     (검증 끝나면 콘솔에서 컬렉션 삭제)
    /// </summary>
    public class P3E2ETest : MonoBehaviour
    {
        [Header("씬에 이미 있는 매니저 참조 (Inspector에서 드래그)")]
        public GemmaOnDeviceManager gemmaManager;
        public TempTripleManager tempTripleManager;

        [Header("테스트 입력 (자연어)")]
        [TextArea(2, 4)]
        public string testInput = "오늘 강남 카페에서 친구 만났다";

        [Header("Gemma 로딩 후 자동 실행 여부")]
        public bool autoRunOnStart = true;

        // 내부에서 자동 생성하는 추출기
        private TextTripleExtractor _extractor;

        IEnumerator Start()
        {
            if (!autoRunOnStart)
            {
                Debug.Log("[P3E2ETest] autoRunOnStart=false → 수동 호출 대기");
                yield break;
            }

            // ───────────────────────────────────────────────────────────
            // 진단: 씬에 GemmaOnDeviceManager가 몇 개 있는지 / 각각 init 됐는지
            // (Inspector 참조 실수 또는 중복 인스턴스 잡기 위한 가드)
            // ───────────────────────────────────────────────────────────
            var allGemmas = FindObjectsByType<GemmaOnDeviceManager>(FindObjectsSortMode.None);
            Debug.Log($"[P3E2ETest] 진단: 씬에 GemmaOnDeviceManager {allGemmas.Length}개 존재");
            for (int i = 0; i < allGemmas.Length; i++)
            {
                Debug.Log($"[P3E2ETest]   [{i}] GO=\"{allGemmas[i].gameObject.name}\" isModelLoaded={allGemmas[i].isModelLoaded} (instanceId={allGemmas[i].GetInstanceID()})");
            }

            // Inspector 참조 비어있으면 자동 fallback (씬에서 첫 번째 사용)
            if (gemmaManager == null)
            {
                if (allGemmas.Length == 0)
                {
                    Debug.LogError("[P3E2ETest] 씬에 GemmaOnDeviceManager가 하나도 없음 → 종료");
                    yield break;
                }
                gemmaManager = allGemmas[0];
                Debug.LogWarning($"[P3E2ETest] gemmaManager 비어있어 자동 사용: {gemmaManager.gameObject.name}");
            }
            else
            {
                Debug.Log($"[P3E2ETest] gemmaManager 참조: GO=\"{gemmaManager.gameObject.name}\" instanceId={gemmaManager.GetInstanceID()}");
            }

            // Inspector가 init 안 된 인스턴스를 가리키는 경우 → 실제로 init된 인스턴스로 교체
            if (!gemmaManager.isModelLoaded)
            {
                foreach (var g in allGemmas)
                {
                    if (g != gemmaManager && g.isModelLoaded)
                    {
                        Debug.LogWarning($"[P3E2ETest] Inspector 참조({gemmaManager.gameObject.name})는 init 안 됨. 실제 init된 \"{g.gameObject.name}\"로 교체");
                        gemmaManager = g;
                        break;
                    }
                }
            }

            Debug.Log("[P3E2ETest] Gemma 로딩 대기 시작 (5초마다 상태 출력, 60초 timeout)");

            // 5초마다 상태 찍기 (Stuck 진단용)
            float elapsed = 0f;
            const float timeoutSec = 60f;
            while (!gemmaManager.isModelLoaded)
            {
                yield return new WaitForSeconds(5f);
                elapsed += 5f;
                Debug.Log($"[P3E2ETest] 대기중 {elapsed:F0}s ... isModelLoaded={gemmaManager.isModelLoaded} GO=\"{gemmaManager.gameObject.name}\"");

                if (elapsed >= timeoutSec)
                {
                    Debug.LogError("[P3E2ETest] 60초 timeout → Gemma 로딩 실패. 위 진단 로그 확인");
                    yield break;
                }
            }

            Debug.Log("[P3E2ETest] Gemma 로딩 완료 → 5초 후 추출 시작");

            // Firebase Auth + Firestore init까지 약간의 여유를 둠
            yield return new WaitForSeconds(5f);

            RunOnce();
        }

        /// <summary>
        /// 한 번 실행: 입력 → 트리플 추출 → SQLite → Firestore.
        /// UI 버튼 OnClick에 직접 바인딩해도 됨.
        /// </summary>
        public void RunOnce()
        {
            if (gemmaManager == null || !gemmaManager.isModelLoaded)
            {
                Debug.LogError("[P3E2ETest] Gemma 미준비 — 종료");
                return;
            }

            // 1. Extractor 준비 (없으면 같은 GameObject에 붙여서 생성)
            if (_extractor == null)
            {
                _extractor = gameObject.AddComponent<TextTripleExtractor>();
                _extractor.gemmaManager = gemmaManager;
            }

            Debug.Log($"[P3E2ETest] === 입력 === \"{testInput}\"");

            // 2. Gemma 호출 + 트리플 추출 + SQLite 저장 (수 초 걸림, 메인 스레드 블록)
            int saved = _extractor.ExtractAndSaveTriples(testInput);
            Debug.Log($"[P3E2ETest] === SQLite 저장 === {saved}건");

            if (saved == 0)
            {
                Debug.LogWarning("[P3E2ETest] 저장된 트리플 없음 → Firestore 업로드 스킵");
                return;
            }

            // 3. Firestore 업로드 (TempTripleManager가 synced=0인 것들을 모아 올림)
            if (tempTripleManager == null)
            {
                Debug.LogError("[P3E2ETest] tempTripleManager 참조 없음 → Firestore 업로드 스킵");
                Debug.LogError("[P3E2ETest]   → Inspector에서 씬의 TempTripleManager GameObject를 드래그하세요");
                return;
            }

            Debug.Log("[P3E2ETest] === Firestore 업로드 시작 ===");
            tempTripleManager.SyncPendingTriples(
                onSuccess: cnt =>
                {
                    Debug.Log($"[P3E2ETest] ✅✅✅ Firestore 업로드 성공: {cnt}건");
                    Debug.Log($"[P3E2ETest]   → Firebase 콘솔에서 temp_triples/{{uid}}/items 확인");
                },
                onFailure: msg =>
                {
                    Debug.LogError($"[P3E2ETest] ❌ Firestore 업로드 실패: {msg}");
                }
            );
        }
    }
}
#endif
