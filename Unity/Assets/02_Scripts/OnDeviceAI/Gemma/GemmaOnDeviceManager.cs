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

        private string modelPath = "";

#if UNITY_ANDROID && !UNITY_EDITOR
        // Kotlin GemmaBridge 인스턴스 (AAR에서 가져옴)
        private AndroidJavaObject gemmaBridge;
#endif

        void Start()
        {
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
                yield return www.SendWebRequest();

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
            try
            {
                // 현재 Activity context 가져오기
                AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

                // GemmaBridge 인스턴스 생성 (context 전달)
                gemmaBridge = new AndroidJavaObject("com.ontology.metaverse.GemmaBridge", activity);

                // 모델 초기화
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
            // 무성님 명세 반영: full URI + prod:hasLocation
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
            // 무성님 명세 반영: full URI + prod:hasGalleryPhoto
            return @"{""triples"": [
              {""s"": ""http://7team.dev/ontology#user_001"", ""p"": ""prod:hasGalleryPhoto"", ""o"": ""http://7team.dev/ontology#photo_001_test"", ""datatype"": null},
              {""s"": ""http://7team.dev/ontology#photo_001_test"", ""p"": ""prod:foodType"", ""o"": ""pasta"", ""datatype"": ""xsd:string""},
              {""s"": ""http://7team.dev/ontology#photo_001_test"", ""p"": ""prod:placeType"", ""o"": ""restaurant"", ""datatype"": ""xsd:string""},
              {""s"": ""http://7team.dev/ontology#photo_001_test"", ""p"": ""prod:analyzedBy"", ""o"": ""Gemma-3n"", ""datatype"": ""xsd:string""}
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