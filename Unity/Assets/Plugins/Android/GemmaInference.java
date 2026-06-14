package com.ontology.metaverse.gemma;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.util.Log;

import com.google.mediapipe.framework.image.BitmapImageBuilder;
import com.google.mediapipe.framework.image.MPImage;
import com.google.mediapipe.tasks.genai.llminference.GraphOptions;
import com.google.mediapipe.tasks.genai.llminference.LlmInference;
import com.google.mediapipe.tasks.genai.llminference.LlmInference.LlmInferenceOptions;
import com.google.mediapipe.tasks.genai.llminference.LlmInferenceSession;
import com.google.mediapipe.tasks.genai.llminference.LlmInferenceSession.LlmInferenceSessionOptions;

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
                    .setMaxNumImages(1)        // Gemma 3n 비전 모달리티 활성화 (갤러리 사진 1장씩 처리)
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
     * 이미지 + 텍스트 프롬프트를 받아 동기적으로 응답 문자열 반환 (멀티모달).
     * 메인 스레드에서 호출 금지 (몇 초 걸림). Unity 쪽에서 Coroutine + Thread로 감싸야 함.
     * @param prompt 함께 전달할 텍스트 프롬프트
     * @param imageBytes 이미지 파일 바이트(PNG/JPEG 등 BitmapFactory가 디코딩 가능한 포맷)
     * @return 응답 문자열, 실패 시 빈 문자열.
     */
    public String generateResponseWithImage(String prompt, byte[] imageBytes) {
        if (llmInference == null) {
            Log.w(TAG, "generateResponseWithImage: 초기화 안 됨");
            return "";
        }
        if (imageBytes == null || imageBytes.length == 0) {
            return generateResponse(prompt);
        }

        Bitmap bitmap = BitmapFactory.decodeByteArray(imageBytes, 0, imageBytes.length);
        if (bitmap == null) {
            Log.w(TAG, "generateResponseWithImage: 이미지 디코딩 실패 - 텍스트만 전달");
            return generateResponse(prompt);
        }

        LlmInferenceSession session = null;
        try {
            LlmInferenceSessionOptions sessionOptions = LlmInferenceSessionOptions.builder()
                    .setTopK(40)
                    .setTemperature(0.8f)
                    .setGraphOptions(GraphOptions.builder().setEnableVisionModality(true).build())
                    .build();

            session = LlmInferenceSession.createFromOptions(llmInference, sessionOptions);
            session.addQueryChunk(prompt == null ? "" : prompt);

            MPImage image = new BitmapImageBuilder(bitmap).build();
            session.addImage(image);

            String response = session.generateResponse();
            Log.d(TAG, "generateResponseWithImage 길이=" + (response == null ? 0 : response.length()));
            return response == null ? "" : response;
        } catch (Throwable t) {
            Log.e(TAG, "generateResponseWithImage 실패: " + t.getMessage(), t);
            return "";
        } finally {
            if (session != null) {
                try {
                    session.close();
                } catch (Throwable t) {
                    Log.e(TAG, "session close 실패: " + t.getMessage(), t);
                }
            }
            bitmap.recycle();
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
