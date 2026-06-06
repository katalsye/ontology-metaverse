package com.ontology.metaverse.step;

import android.app.Activity;
import android.content.Context;
import android.hardware.Sensor;
import android.hardware.SensorEvent;
import android.hardware.SensorEventListener;
import android.hardware.SensorManager;
import android.util.Log;

/**
 * Android Step Counter 센서 헬퍼 (Unity ↔ Java 브릿지).
 *
 * Android TYPE_STEP_COUNTER 센서는 디바이스 부팅 후 누적 걸음수를 반환함.
 * (API 19, KitKat+). 권한: ACTIVITY_RECOGNITION (API 29+) — Manifest에 선언.
 *
 * Unity 호출:
 *   AndroidJavaClass cls = new AndroidJavaClass("com.ontology.metaverse.step.StepHelper");
 *   bool started = cls.CallStatic&lt;bool&gt;("start", activity);
 *   int count = cls.CallStatic&lt;int&gt;("getCurrentCount");  // -1 = 아직 데이터 없음 또는 센서 없음
 *
 * 싱글톤 패턴 — 앱 라이프타임 동안 한 번만 register, polling 식으로 사용.
 * 센서 이벤트는 사용자가 걸을 때마다 발생 → 마지막 값을 lastCount에 저장.
 */
public class StepHelper implements SensorEventListener {
    private static final String TAG = "StepHelper";

    private static StepHelper instance;

    private SensorManager sensorManager;
    private Sensor stepCounter;
    private int lastCount = -1;
    private boolean registered = false;

    /**
     * 센서 리스너 등록. 이미 등록되었으면 noop.
     * @return true = 센서 사용 가능 / false = 디바이스에 step counter 없음
     */
    public static boolean start(Activity activity) {
        if (instance == null) instance = new StepHelper();
        return instance.register(activity);
    }

    /**
     * 마지막으로 센서가 보고한 누적 걸음수 반환.
     * @return -1 = 아직 데이터 없음 또는 start() 호출 전
     */
    public static int getCurrentCount() {
        if (instance == null) return -1;
        return instance.lastCount;
    }

    /**
     * 센서 등록 해제. Unity OnDestroy()에서 호출 권장.
     */
    public static void stop() {
        if (instance != null) instance.unregister();
    }

    // ─────────────────────────────────────────────────────

    private boolean register(Activity activity) {
        if (registered) return true;

        sensorManager = (SensorManager) activity.getSystemService(Context.SENSOR_SERVICE);
        if (sensorManager == null) {
            Log.e(TAG, "SensorManager 못 가져옴");
            return false;
        }

        stepCounter = sensorManager.getDefaultSensor(Sensor.TYPE_STEP_COUNTER);
        if (stepCounter == null) {
            Log.w(TAG, "이 디바이스에 TYPE_STEP_COUNTER 센서 없음");
            return false;
        }

        boolean ok = sensorManager.registerListener(this, stepCounter, SensorManager.SENSOR_DELAY_NORMAL);
        if (ok) {
            registered = true;
            Log.i(TAG, "Step counter 리스너 등록 성공");
        } else {
            Log.e(TAG, "registerListener 실패");
        }
        return ok;
    }

    private void unregister() {
        if (sensorManager != null && registered) {
            sensorManager.unregisterListener(this);
            registered = false;
            Log.i(TAG, "Step counter 리스너 해제");
        }
    }

    @Override
    public void onSensorChanged(SensorEvent event) {
        if (event.values != null && event.values.length > 0) {
            // 부팅 후 누적값 (float이지만 정수만 변동)
            lastCount = (int) event.values[0];
        }
    }

    @Override
    public void onAccuracyChanged(Sensor sensor, int accuracy) {
        // 우리는 정확도 변화 무시 (counter 값만 사용)
    }
}
