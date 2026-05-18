using System;
using System.IO;
using System.Collections;
using UnityEngine;

// 모바일에서만 AI 라이브러리가 작동하게 묶어두기 (PC 유니티 에디터 팅김 방지용)
#if UNITY_ANDROID && !UNITY_EDITOR
using Mediapipe.Tasks.Text.LlmInference;
#endif

namespace OntologyMetaverse.OnDeviceAI.Gemma
{
    public class GemmaOnDeviceManager : MonoBehaviour
    {
        [Header("모델 파일 이름")]
        public string modelFileName = "gemma-1.1-2b-it-gpu-int4.bin";
        
        // 모델이 다 불러와졌는지 체크하는 변수
        public bool isModelLoaded = false;

#if UNITY_ANDROID && !UNITY_EDITOR
        // 실제 AI 뇌를 담을 변수
        private LlmInference llmInference;
#endif

        void Start()
        {
            // 코루틴으로 모델 로딩 시작 (유니티 화면 멈춤 방지)
            StartCoroutine(InitGemmaModel());
        }

        private IEnumerator InitGemmaModel()
        {
            Debug.Log("[GemmaManager] AI 모델 로딩 시작!");
            
            // 안드로이드 폰 안에서 파일 위치 찾기
            string modelPath = Path.Combine(Application.streamingAssetsPath, modelFileName);
            
            // 파일이 진짜로 있는지 먼저 확인
            if (!modelPath.Contains("://") && !File.Exists(modelPath))
            {
                Debug.LogError("[GemmaManager] 앗! 모델 파일이 없습니다: " + modelPath);
                yield break; // 함수 강제 종료
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                // 실제 안드로이드 폰에서 AI 세팅하기
                LlmInferenceOptions options = new LlmInferenceOptions(modelPath);
                llmInference = LlmInference.CreateFromOptions(options);
                isModelLoaded = true;
                Debug.Log("[GemmaManager] 안드로이드 기기에서 AI 로딩 진짜 성공!");
            }
            catch (Exception e)
            {
                Debug.LogError("[GemmaManager] AI 로딩 실패: " + e.Message);
            }
#else
            // PC 유니티 에디터에서는 1초 기다리는 척만 하기 (테스트용)
            yield return new WaitForSeconds(1.0f);
            isModelLoaded = true;
            Debug.Log("[GemmaManager] PC 에디터 테스트 모드: AI 로딩 완료 (가짜)");
#endif
        }

        // AI한테 질문 던지고 대답 받아오는 함수
        public string GenerateResponse(string prompt)
        {
            if (isModelLoaded == false)
            {
                Debug.LogWarning("[GemmaManager] 아직 AI가 준비 안 됐어요!");
                return "AI 준비 중...";
            }

            Debug.Log("[GemmaManager] AI한테 질문: " + prompt);
            
#if UNITY_ANDROID && !UNITY_EDITOR
            // 실제 안드로이드 기기에서 AI가 생각해서 대답하기
            return llmInference.GenerateResponse(prompt);
#else
            // PC 에디터에서는 온톨로지 테스트용 더미(가짜) 데이터 내보내기
            return "{\"subject\":\"user\", \"predicate\":\"tested\", \"object\":\"gemma\"}";
#endif
        }
    }
}