using System;
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
        [Header("모델 파일 이름")]
        public string modelFileName = "gemma-3n-E2B-it-int4.task";

        public bool isModelLoaded { get; private set; }

        // ModelDownloader가 채워주는 실제 모델 파일 절대 경로
        private string modelPath = "";

#if UNITY_ANDROID && !UNITY_EDITOR
        // com.ontology.metaverse.gemma.GemmaInference 인스턴스
        private AndroidJavaObject nativeBridge;
#endif

        /// <summary>
        /// 모델 파일 경로를 받아 네이티브 초기화. ModelDownloader 완료 콜백에서 호출.
        /// 실기기에서 수 초 ~ 십수 초 걸릴 수 있음 (메인 스레드 OK, MediaPipe 내부 처리).
        /// </summary>
        public void InitWithModelPath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
            {
                Debug.LogError($"[GemmaManager] 모델 파일을 찾을 수 없습니다: {absolutePath}");
                isModelLoaded = false;
                return;
            }

            modelPath = absolutePath;
            Debug.Log($"[GemmaManager] 모델 초기화 시작: {modelPath}");

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                nativeBridge = new AndroidJavaObject("com.ontology.metaverse.gemma.GemmaInference");
                bool ok = nativeBridge.Call<bool>("init", modelPath);
                if (!ok)
                {
                    Debug.LogError("[GemmaManager] 네이티브 init 실패 - logcat의 GemmaInference TAG 확인");
                    nativeBridge.Dispose();
                    nativeBridge = null;
                    isModelLoaded = false;
                    return;
                }
                isModelLoaded = true;
                Debug.Log("[GemmaManager] 네이티브 모델 로딩 성공");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GemmaManager] AndroidJavaObject 호출 실패: {e}");
                isModelLoaded = false;
            }
#else
            // PC 에디터: 네이티브 호출 불가, mock 모드
            isModelLoaded = true;
            Debug.Log("[GemmaManager] Editor 모드: mock 응답 활성");
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
        /// 멀티모달 입력. 현재 네이티브 브릿지에 vision API 미구현 — 텍스트만 전달.
        /// MediaPipe LlmInferenceSession.addImage() 추가 후 GemmaInference.java 보강 필요.
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
            // TODO: GemmaInference.java에 generateResponseWithImage(byte[], String) 추가 후 연결.
            // 현재는 텍스트만 전달.
            Debug.LogWarning("[GemmaManager] 실기기 비전 API 미구현 - 텍스트만 전달");
            return GenerateResponse(prompt);
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
