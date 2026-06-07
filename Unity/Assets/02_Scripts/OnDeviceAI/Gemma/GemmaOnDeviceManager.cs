using System;
using System.IO;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace OntologyMetaverse.OnDeviceAI.Gemma
{
    public class GemmaOnDeviceManager : MonoBehaviour
    {
        [Header("모델 파일 이름")]
        public string modelFileName = "gemma-3n-E2B-it-int4.task";

        public bool isModelLoaded = false;

        // ── 다운로드 진행 상황 (UI 폴링용) ──────────────────────
        public bool IsDownloadingModel { get; private set; }
        public float DownloadProgress { get; private set; }
        public ulong DownloadedBytes { get; private set; }
        public ulong TotalBytes { get; private set; }

        // 파일 복사가 끝난 뒤 AI 엔진을 메모리에 올리는 단계 (오래 걸릴 수 있음)
        public bool IsInitializingEngine { get; private set; }

        private string modelPath = "";
        private bool _loadingStarted = false;

#if UNITY_ANDROID && !UNITY_EDITOR
        // Kotlin GemmaBridge 인스턴스 (AAR에서 가져옴)
        private AndroidJavaObject gemmaBridge;
#endif

        /// <summary>
        /// 온보딩 화면에서 "다운로드 시작" 버튼을 눌렀을 때 호출 — 모델 로딩을 시작함.
        /// 자동 시작하지 않고 사용자가 버튼을 눌러야 시작되도록 분리함.
        /// </summary>
        public void StartLoading()
        {
            if (_loadingStarted) return;
            _loadingStarted = true;
            StartCoroutine(InitGemmaModel());
        }

        private IEnumerator InitGemmaModel()
        {
            Debug.Log("[GemmaManager] AI 모델 로딩 시작! (Gemma 3n 멀티모달)");

#if UNITY_ANDROID && !UNITY_EDITOR
            // 1. 안드로이드: StreamingAssets는 APK 안에 있어서 바로 못 읽음
            //    persistentDataPath로 복사한 다음에 사용해야 함
            string sourcePath = Path.Combine(Application.streamingAssetsPath, modelFileName);
            string destPath = Path.Combine(Application.persistentDataPath, modelFileName);

            if (!File.Exists(destPath))
            {
                Debug.Log("[GemmaManager] 모델 파일 복사 시작 (시간 걸림)");

                UnityWebRequest www = UnityWebRequest.Get(sourcePath);
                UnityWebRequestAsyncOperation operation = www.SendWebRequest();

                IsDownloadingModel = true;
                while (!operation.isDone)
                {
                    DownloadProgress = www.downloadProgress;
                    DownloadedBytes  = www.downloadedBytes;
                    // Content-Length를 못 받는 로컬 자산이므로 진행률로 총량을 역산
                    if (DownloadProgress > 0.001f)
                        TotalBytes = (ulong)(DownloadedBytes / DownloadProgress);
                    yield return null;
                }

                DownloadProgress   = 1f;
                DownloadedBytes    = www.downloadedBytes;
                TotalBytes         = DownloadedBytes;
                IsDownloadingModel = false;

                if (www.result == UnityWebRequest.Result.Success)
                {
                    File.WriteAllBytes(destPath, www.downloadHandler.data);
                    Debug.Log("[GemmaManager] 모델 파일 복사 완료");
                }
                else
                {
                    Debug.LogError("[GemmaManager] 모델 파일 복사 실패: " + www.error);
                    yield break;
                }
            }
            else
            {
                Debug.Log("[GemmaManager] 모델 파일 이미 있음. 복사 생략.");
            }

            modelPath = destPath;

            // 2. Kotlin GemmaBridge 인스턴스 생성 + 모델 초기화
            // 파일 복사(DownloadProgress)는 끝났지만, 모델을 메모리에 올리는
            // initialize 호출 자체가 오래 걸릴 수 있으므로 별도 상태로 노출
            IsInitializingEngine = true;
            yield return null; // UI가 "초기화 중" 상태를 한 프레임이라도 그릴 수 있게 양보

            try
            {
                // 현재 Activity context 가져오기
                AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

                // GemmaBridge 인스턴스 생성 (context 전달)
                gemmaBridge = new AndroidJavaObject("com.ontology.metaverse.GemmaBridge", activity);

                // 모델 초기화 (블로킹 호출 — 시간이 걸림)
                bool success = gemmaBridge.Call<bool>("initialize", modelPath);

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
                Debug.LogError("[GemmaManager] AI 로딩 실패: " + e.Message);
            }
            finally
            {
                IsInitializingEngine = false;
            }
#else
            // PC 에디터: 가짜로 1초 대기하고 로딩됐다고 치기
            modelPath = Path.Combine(Application.streamingAssetsPath, modelFileName);
            yield return new WaitForSeconds(1.0f);
            isModelLoaded = true;
            Debug.Log("[GemmaManager] PC 에디터 모드: AI 로딩 완료 (가짜)");
#endif
        }

        // ─────────────────────────────────────────────────────
        // 텍스트 입력 → 응답
        // ─────────────────────────────────────────────────────

        public string GenerateResponse(string prompt)
        {
            if (isModelLoaded == false)
            {
                Debug.LogWarning("[GemmaManager] 아직 AI가 준비 안 됐어요!");
                return "AI 준비 중...";
            }

            Debug.Log("[GemmaManager] AI한테 질문 (텍스트): " + prompt);

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                return gemmaBridge.Call<string>("generateResponse", prompt);
            }
            catch (Exception e)
            {
                Debug.LogError("[GemmaManager] 응답 생성 실패: " + e.Message);
                return "";
            }
#else
            // 에디터에서는 명세서 규격 가짜 트리플 응답 (텍스트용 - 위치)
            return @"{""triples"": [
              {""s"": ""prod:user_001"", ""p"": ""prod:visited"", ""o"": ""prod:loc_001_test"", ""datatype"": null},
              {""s"": ""prod:loc_001_test"", ""p"": ""prod:placeName"", ""o"": ""Mock Place"", ""datatype"": ""xsd:string""},
              {""s"": ""prod:loc_001_test"", ""p"": ""prod:placeType"", ""o"": ""cafe"", ""datatype"": ""xsd:string""}
            ]}";
#endif
        }

        // ─────────────────────────────────────────────────────
        // 이미지 + 텍스트 입력 → 응답 (멀티모달)
        // ─────────────────────────────────────────────────────

        public string GenerateResponseWithImage(string prompt, byte[] imageBytes)
        {
            if (isModelLoaded == false)
            {
                Debug.LogWarning("[GemmaManager] 아직 AI가 준비 안 됐어요!");
                return "AI 준비 중...";
            }

            if (imageBytes == null || imageBytes.Length == 0)
            {
                Debug.LogWarning("[GemmaManager] 이미지 데이터가 비어있어요!");
                return null;
            }

            Debug.Log($"[GemmaManager] AI한테 질문 (이미지+텍스트): 이미지 크기={imageBytes.Length} bytes, prompt={prompt}");

#if UNITY_ANDROID && !UNITY_EDITOR
            // 실기기: MediaPipe 멀티모달은 Kotlin Bridge에 별도 메서드 필요
            // 현재는 텍스트만 전달 (이미지 입력은 추후 Kotlin Bridge 확장)
            Debug.LogWarning("[GemmaManager] 실기기 비전 API 호출 미구현 - 텍스트만 전달");
            try
            {
                return gemmaBridge.Call<string>("generateResponse", prompt);
            }
            catch (Exception e)
            {
                Debug.LogError("[GemmaManager] 응답 생성 실패: " + e.Message);
                return "";
            }
#else
            // 에디터에서는 명세서 규격 가짜 트리플 응답 (이미지용 - 음식 사진 가정)
            return @"{""triples"": [
              {""s"": ""prod:user_001"", ""p"": ""prod:photographed"", ""o"": ""prod:photo_001_test"", ""datatype"": null},
              {""s"": ""prod:photo_001_test"", ""p"": ""prod:foodType"", ""o"": ""pasta"", ""datatype"": ""xsd:string""},
              {""s"": ""prod:photo_001_test"", ""p"": ""prod:placeType"", ""o"": ""restaurant"", ""datatype"": ""xsd:string""},
              {""s"": ""prod:photo_001_test"", ""p"": ""prod:analyzedBy"", ""o"": ""Gemma-3n"", ""datatype"": ""xsd:string""}
            ]}";
#endif
        }

        // ─────────────────────────────────────────────────────
        // 메모리 정리
        // ─────────────────────────────────────────────────────

        void OnDestroy()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (gemmaBridge != null)
            {
                try
                {
                    gemmaBridge.Call("close");
                    gemmaBridge.Dispose();
                    gemmaBridge = null;
                    Debug.Log("[GemmaManager] AI 메모리 해제");
                }
                catch (Exception e)
                {
                    Debug.LogError("[GemmaManager] 메모리 해제 실패: " + e.Message);
                }
            }
#endif
            isModelLoaded = false;
        }
    }
}