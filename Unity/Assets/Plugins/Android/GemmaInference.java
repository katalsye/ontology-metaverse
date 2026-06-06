package com.ontology.metaverse.gemma;

import android.content.Context;
import android.util.Log;

import com.google.mediapipe.tasks.genai.llminference.LlmInference;
import com.google.mediapipe.tasks.genai.llminference.LlmInference.LlmInferenceOptions;

/**
 * Unity ↔ MediaPipe LlmInference 네이티브 브릿지.
 *
 * Unity 측 C#은 AndroidJavaObject로 이 클래스를 호출한다:
 *   AndroidJavaObject helper = new AndroidJavaObject("com.ontology.metaverse.gemma.GemmaInference");
 *   helper.Call("init", modelPath);
 *   string response = helper.Call<string>("generateResponse", prompt);
 *
 * 멀티 인스턴스 금지 (LlmInference가 무겁고 모델당 1개만 가능).
 * Unity 쪽에서 OnDestroy()로 close() 호출 필수.
 */
public class GemmaInference {
    private static final String TAG = "GemmaInference";

    // MediaPipe LlmInference 인스턴스. init() 이후 valid, close() 이후 null.
    private LlmInference llmInference;

    /**
     * 모델 파일 경로를 받아 LlmInference 초기화.
     * @param modelPath 디바이스 절대 경로 (예: /data/data/.../files/gemma-3n-E2B-it-int4.task)
     * @return true 성공, false 실패 (Unity는 false 시 재시도 X, 로그캣 확인)
     */
    public boolean init(String modelPath) {
        if (llmInference != null) {
            Log.w(TAG, "init: 이미 초기화됨, 기존 인스턴스 close 후 재생성");
            close();
        }

        try {
            // GPU 백엔드 사용 이유:
            //   CPU 백엔드는 XNNPack weight cache를 /data/.../cache/에 만드는데
            //   Gemma 3n int4의 캐시 크기가 cache 파티션 용량을 초과해서 SIGABRT 발생.
            //   GPU 백엔드는 XNNPack 안 쓰므로 캐시 문제 자체가 사라짐.
            //   S24 Adreno GPU면 CPU 대비 추론 속도도 더 빠름.
            LlmInferenceOptions options = LlmInferenceOptions.builder()
                    .setModelPath(modelPath)
                    .setMaxTokens(1024)        // 입력+출력 합산 토큰 상한. 트리플 추출은 짧음.
                    .setMaxTopK(40)            // 0.10.27 신규 API. 디코딩 topK 상한.
                    .setPreferredBackend(LlmInference.Backend.GPU)  // CPU 대신 GPU
                    .build();

            // MediaPipe 0.10.27 API: createFromOptions(Context, LlmInferenceOptions)
            // Context는 Unity Player Activity에서 가져옴.
            android.app.Activity activity = com.unity3d.player.UnityPlayer.currentActivity;
            llmInference = LlmInference.createFromOptions(activity.getApplicationContext(), options);

            Log.i(TAG, "init 성공: " + modelPath);
            return true;
        } catch (Throwable t) {
            Log.e(TAG, "init 실패: " + t.getMessage(), t);
            llmInference = null;
            return false;
        }
    }

    /**
     * 프롬프트를 받아 동기적으로 응답 문자열 반환.
     * 메인 스레드에서 호출 금지 (몇 초 걸림). Unity 쪽에서 Coroutine + Thread로 감싸야 함.
     * @return 응답 문자열, 실패 시 빈 문자열.
     */
    public String generateResponse(String prompt) {
        if (llmInference == null) {
            Log.w(TAG, "generateResponse: 초기화 안 됨");
            return "";
        }
        if (prompt == null || prompt.isEmpty()) {
            return "";
        }

        try {
            String response = llmInference.generateResponse(prompt);
            Log.d(TAG, "generateResponse 길이=" + (response == null ? 0 : response.length()));
            return response == null ? "" : response;
        } catch (Throwable t) {
            Log.e(TAG, "generateResponse 실패: " + t.getMessage(), t);
            return "";
        }
    }

    /**
     * LlmInference 자원 해제. Unity OnDestroy()에서 호출 필수 (메모리 누수 방지).
     */
    public void close() {
        if (llmInference != null) {
            try {
                llmInference.close();
                Log.i(TAG, "close 완료");
            } catch (Throwable t) {
                Log.e(TAG, "close 실패: " + t.getMessage(), t);
            } finally {
                llmInference = null;
            }
        }
    }

    public boolean isReady() {
        return llmInference != null;
    }
}
