using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace OntologyMetaverse.OnDeviceAI.Gemma
{
    /// <summary>
    /// Gemma 3n 온디바이스 추론 매니저.
    ///
    /// 실기기:
    ///   Plugins/Android/GemmaInference.java 를 AndroidJavaObject로 호출.
    ///   모델 파일은 별도 ModelDownloader가 Firebase Storage에서 받아오고,
    ///   InitWithModelPath(absolutePath)로 주입한다.
    ///
    /// Editor:
    ///   네이티브 호출 불가 → 명세서 규격 mock 트리플 응답 반환.
    ///
    /// 호출 인터페이스 (TextTripleExtractor 등 기존 호출자가 사용):
    ///   - bool isModelLoaded
    ///   - string GenerateResponse(string prompt)
    ///   - string GenerateResponseWithImage(string prompt, byte[] imageBytes)
    /// </summary>
    public class GemmaOnDeviceManager : MonoBehaviour
    {
        public const string ModelFileName = "gemma-3n-E2B-it-int4.task";
        public static string ModelDestPath => Path.Combine(Application.persistentDataPath, ModelFileName);

        public bool isModelLoaded { get; private set; }

#if UNITY_ANDROID && !UNITY_EDITOR
        // com.ontology.metaverse.gemma.GemmaInference 인스턴스
        private AndroidJavaObject nativeBridge;
#endif

        void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Debug.Log("[GemmaManager] 안드로이드: GemmaModelDownloader의 InitWithModelPath 호출 대기 중");
#else
            StartCoroutine(InitGemmaModelEditorMock());
#endif
        }

#if !UNITY_ANDROID || UNITY_EDITOR
        private IEnumerator InitGemmaModelEditorMock()
        {
            Debug.Log("[GemmaManager] AI 모델 로딩 시작! (Gemma 3n 멀티모달)");
            yield return new WaitForSeconds(1.0f);
            isModelLoaded = true;
            Debug.Log("[GemmaManager] Editor 모드: mock 응답 활성");
        }
#endif

        // ─────────────────────────────────────────────────────
        // 모델 파일 경로 주입 → 네이티브 브릿지 초기화
        // GemmaModelDownloader가 다운로드(또는 로컬 캐시 확인) 완료 후 호출.
        // ─────────────────────────────────────────────────────
        public void InitWithModelPath(string absolutePath)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
            {
                Debug.LogError("[GemmaManager] 모델 파일 없음: " + absolutePath);
                return;
            }

            try
            {
                // GemmaInference.init() 내부에서 UnityPlayer.currentActivity로 Context를 직접 가져오므로
                // 여기서는 인자 없이 생성한다.
                nativeBridge = new AndroidJavaObject("com.ontology.metaverse.gemma.GemmaInference");
                bool success = nativeBridge.Call<bool>("init", absolutePath);

                if (success)
                {
                    isModelLoaded = true;
                    Debug.Log("[GemmaManager] 안드로이드 AI 로딩 성공!");
                }
                else
                {
                    Debug.LogError("[GemmaManager] AI 로딩 실패");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[GemmaManager] AndroidJavaObject 호출 실패: {e}");
                isModelLoaded = false;
            }
#else
            isModelLoaded = true;
            Debug.Log("[GemmaManager] Editor 모드: mock 응답 활성 (path=" + absolutePath + ")");
#endif
        }

        // ─────────────────────────────────────────────────────
        // 텍스트 입력 → 응답
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// 텍스트 프롬프트 전달 → 응답 문자열 반환 (동기).
        /// 실기기에서 수 초 걸림. 백그라운드 batch 시점에서 호출 권장.
        /// </summary>
        public string GenerateResponse(string prompt)
        {
            if (!isModelLoaded)
            {
                Debug.LogWarning("[GemmaManager] 모델 미로딩 상태에서 GenerateResponse 호출");
                return "AI 준비 중...";
            }

            if (string.IsNullOrEmpty(prompt))
            {
                return "";
            }

            Debug.Log($"[GemmaManager] generateResponse 호출 (prompt 길이={prompt.Length})");

#if UNITY_ANDROID && !UNITY_EDITOR
            if (nativeBridge == null) return "";
            try
            {
                return nativeBridge.Call<string>("generateResponse", prompt) ?? "";
            }
            catch (Exception e)
            {
                Debug.LogError($"[GemmaManager] 네이티브 generateResponse 실패: {e}");
                return "";
            }
#else
            // Editor mock: 위치 트리플 (TextTripleExtractor 테스트용)
            return @"{""triples"": [
              {""s"": ""http://7team.dev/ontology#user_001"", ""p"": ""prod:hasLocation"", ""o"": ""http://7team.dev/ontology#loc_001_test"", ""datatype"": null},
              {""s"": ""http://7team.dev/ontology#loc_001_test"", ""p"": ""prod:placeName"", ""o"": ""Mock Place"", ""datatype"": ""xsd:string""},
              {""s"": ""http://7team.dev/ontology#loc_001_test"", ""p"": ""prod:placeType"", ""o"": ""cafe"", ""datatype"": ""xsd:string""}
            ]}";
#endif
        }

        // ─────────────────────────────────────────────────────
        // 이미지 + 텍스트 입력 → 응답 (멀티모달)
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// 멀티모달 입력. 실기기에서는 GemmaInference.generateResponseWithImage(prompt, imageBytes)
        /// (LlmInferenceSession + vision modality)를 호출.
        /// </summary>
        public string GenerateResponseWithImage(string prompt, byte[] imageBytes)
        {
            if (!isModelLoaded)
            {
                Debug.LogWarning("[GemmaManager] 모델 미로딩 상태에서 GenerateResponseWithImage 호출");
                return "AI 준비 중...";
            }

            if (imageBytes == null || imageBytes.Length == 0)
            {
                Debug.LogWarning("[GemmaManager] 이미지 데이터 비어있음");
                return null;
            }

            Debug.Log($"[GemmaManager] 멀티모달 호출 (이미지 {imageBytes.Length}B, prompt 길이={prompt?.Length ?? 0})");

#if UNITY_ANDROID && !UNITY_EDITOR
            if (nativeBridge == null) return "";
            try
            {
                return nativeBridge.Call<string>("generateResponseWithImage", prompt, imageBytes) ?? "";
            }
            catch (Exception e)
            {
                Debug.LogError($"[GemmaManager] 네이티브 generateResponseWithImage 실패: {e}");
                return "";
            }
#else
            // Editor mock: 갤러리 사진 트리플
            return @"{""triples"": [
              {""s"": ""http://7team.dev/ontology#user_001"", ""p"": ""prod:hasGalleryPhoto"", ""o"": ""http://7team.dev/ontology#photo_001_test"", ""datatype"": null},
              {""s"": ""http://7team.dev/ontology#photo_001_test"", ""p"": ""prod:foodType"", ""o"": ""pasta"", ""datatype"": ""xsd:string""},
              {""s"": ""http://7team.dev/ontology#photo_001_test"", ""p"": ""prod:placeType"", ""o"": ""restaurant"", ""datatype"": ""xsd:string""},
              {""s"": ""http://7team.dev/ontology#photo_001_test"", ""p"": ""prod:analyzedBy"", ""o"": ""Gemma-3n"", ""datatype"": ""xsd:string""}
            ]}";
#endif
        }

        // ─────────────────────────────────────────────────────
        // 메모리 해제
        // ─────────────────────────────────────────────────────

        void OnDestroy()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (nativeBridge != null)
            {
                try
                {
                    nativeBridge.Call("close");
                    nativeBridge.Dispose();
                    Debug.Log("[GemmaManager] 네이티브 모델 해제");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GemmaManager] close 실패: {e}");
                }
                finally
                {
                    nativeBridge = null;
                }
            }
#endif
            isModelLoaded = false;
        }
    }
}
