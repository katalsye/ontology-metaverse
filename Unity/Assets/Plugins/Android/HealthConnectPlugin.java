package com.ontology.metaverse.health;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.net.Uri;
import android.util.Log;

import androidx.health.connect.client.HealthConnectClient;
import androidx.health.connect.client.records.HeartRateRecord;
import androidx.health.connect.client.records.SleepSessionRecord;
import androidx.health.connect.client.records.StepsRecord;
import androidx.health.connect.client.request.ReadRecordsRequest;
import androidx.health.connect.client.response.ReadRecordsResponse;
import androidx.health.connect.client.time.TimeRangeFilter;

import com.google.common.util.concurrent.FutureCallback;
import com.google.common.util.concurrent.Futures;
import com.google.common.util.concurrent.ListenableFuture;
import com.google.common.util.concurrent.MoreExecutors;

import java.time.Duration;
import java.time.Instant;
import java.util.Collections;
import java.util.HashSet;
import java.util.Set;
import java.util.concurrent.ConcurrentHashMap;

import kotlin.reflect.KClass;
import kotlin.jvm.JvmClassMappingKt;

/**
 * Health Connect 통합 (Unity ↔ Java 브릿지).
 *
 * Health Connect 1.1.0-alpha14 SDK의 Java 호환 API 사용.
 * 비동기 read는 ListenableFuture 기반 → C# Coroutine이 polling.
 *
 * Unity 호출 패턴:
 *   var cls = new AndroidJavaClass("com.ontology.metaverse.health.HealthConnectPlugin");
 *   int status = cls.CallStatic&lt;int&gt;("getSdkStatus", activity);
 *
 *   cls.CallStatic("requestRead", activity, "sleep", startMillis, endMillis);
 *   yield return new WaitUntil(() => cls.CallStatic&lt;bool&gt;("isReady", "sleep"));
 *   string json = cls.CallStatic&lt;string&gt;("getResult", "sleep");
 *
 * 권한 흐름:
 *   Unity 코드에서 hasAllPermissions() false면 openHealthConnectSettings() 호출
 *   → 사용자가 Health Connect 앱에서 권한 부여 → 다음 batch에서 read 성공
 *
 * SDK status 코드 (HealthConnectClient):
 *   SDK_AVAILABLE = 3
 *   SDK_UNAVAILABLE = 1
 *   SDK_UNAVAILABLE_PROVIDER_UPDATE_REQUIRED = 2
 */
public class HealthConnectPlugin {
    private static final String TAG = "HealthConnectPlugin";

    // 동시 read를 type별로 분리 저장 ("sleep" / "steps" / "heart_rate")
    private static final ConcurrentHashMap<String, String> resultJson = new ConcurrentHashMap<>();
    private static final ConcurrentHashMap<String, String> resultError = new ConcurrentHashMap<>();
    private static final ConcurrentHashMap<String, Boolean> resultReady = new ConcurrentHashMap<>();

    // ─────────────────────────────────────────────────────
    // Availability & Permissions
    // ─────────────────────────────────────────────────────

    /**
     * Health Connect SDK 사용 가능 여부.
     * @return 3 = AVAILABLE, 2 = PROVIDER_UPDATE_REQUIRED, 1 = UNAVAILABLE
     */
    public static int getSdkStatus(Activity activity) {
        try {
            return HealthConnectClient.getSdkStatus(activity, "com.google.android.apps.healthdata");
        } catch (Exception e) {
            Log.e(TAG, "getSdkStatus 실패: " + e.getMessage(), e);
            return 1; // UNAVAILABLE
        }
    }

    /**
     * 우리가 요구하는 모든 권한이 grant 됐는지 확인. 동기 호출 (ListenableFuture.get).
     * 권한: READ_SLEEP, READ_STEPS, READ_HEART_RATE
     */
    public static boolean hasAllPermissions(Activity activity) {
        try {
            HealthConnectClient client = HealthConnectClient.getOrCreate(activity);
            ListenableFuture<Set<String>> future = client.getPermissionController().getGrantedPermissionsAsync();
            Set<String> granted = future.get(); // 즉시 반환 (캐시됨)

            Set<String> required = requiredPermissions();
            return granted.containsAll(required);
        } catch (Exception e) {
            Log.e(TAG, "hasAllPermissions 실패: " + e.getMessage(), e);
            return false;
        }
    }

    /**
     * 현재 grant된 권한 목록 (디버깅용).
     */
    public static String getGrantedPermissionsJson(Activity activity) {
        try {
            HealthConnectClient client = HealthConnectClient.getOrCreate(activity);
            Set<String> granted = client.getPermissionController().getGrantedPermissionsAsync().get();
            StringBuilder sb = new StringBuilder("[");
            boolean first = true;
            for (String p : granted) {
                if (!first) sb.append(",");
                sb.append("\"").append(p).append("\"");
                first = false;
            }
            sb.append("]");
            return sb.toString();
        } catch (Exception e) {
            return "[]";
        }
    }

    /**
     * Health Connect 앱의 권한 설정 화면을 띄움.
     * 사용자가 거기서 우리 앱에 필요한 권한 grant.
     */
    public static void openHealthConnectSettings(Activity activity) {
        try {
            // Health Connect 1.1.0+: ACTION_HEALTH_CONNECT_SETTINGS
            Intent intent = new Intent("androidx.health.ACTION_HEALTH_CONNECT_SETTINGS");
            activity.startActivity(intent);
            Log.i(TAG, "Health Connect 권한 설정 화면 띄움");
        } catch (Exception e) {
            Log.w(TAG, "Health Connect intent 실패, Play Store로 폴백: " + e.getMessage());
            try {
                // Health Connect 앱 미설치 → Play Store
                Intent storeIntent = new Intent(Intent.ACTION_VIEW);
                storeIntent.setData(Uri.parse("market://details?id=com.google.android.apps.healthdata"));
                storeIntent.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                activity.startActivity(storeIntent);
            } catch (Exception e2) {
                Log.e(TAG, "Play Store intent도 실패: " + e2.getMessage(), e2);
            }
        }
    }

    private static Set<String> requiredPermissions() {
        Set<String> perms = new HashSet<>();
        perms.add("android.permission.health.READ_SLEEP");
        perms.add("android.permission.health.READ_STEPS");
        perms.add("android.permission.health.READ_HEART_RATE");
        return perms;
    }

    // ─────────────────────────────────────────────────────
    // Async Read (Unity Coroutine polls isReady/getResult)
    // ─────────────────────────────────────────────────────

    /**
     * 지정 type의 데이터를 비동기로 읽음.
     * @param type "sleep" | "steps" | "heart_rate"
     * @param startMillis 시작 시각 (epoch ms)
     * @param endMillis 종료 시각 (epoch ms)
     */
    public static void requestRead(Activity activity, String type, long startMillis, long endMillis) {
        resultReady.put(type, false);
        resultJson.remove(type);
        resultError.remove(type);

        try {
            HealthConnectClient client = HealthConnectClient.getOrCreate(activity);
            TimeRangeFilter filter = TimeRangeFilter.between(
                Instant.ofEpochMilli(startMillis),
                Instant.ofEpochMilli(endMillis)
            );

            switch (type) {
                case "sleep":      readSleep(client, filter); break;
                case "steps":      readSteps(client, filter); break;
                case "heart_rate": readHeartRate(client, filter); break;
                default:
                    setError(type, "알 수 없는 type: " + type);
            }
        } catch (Exception e) {
            setError(type, "requestRead 예외: " + e.getMessage());
        }
    }

    /** 폴링용 — 결과 준비 여부 확인. */
    public static boolean isReady(String type) {
        Boolean ready = resultReady.get(type);
        return ready != null && ready;
    }

    /** 결과 JSON 문자열 반환. isReady() true 후 호출. null 가능. */
    public static String getResult(String type) {
        return resultJson.get(type);
    }

    /** 에러 메시지 반환. 결과가 null이면 이쪽이 채워짐. */
    public static String getError(String type) {
        return resultError.get(type);
    }

    // ─────────────────────────────────────────────────────
    // Sleep
    // ─────────────────────────────────────────────────────

    @SuppressWarnings("unchecked")
    private static void readSleep(HealthConnectClient client, TimeRangeFilter filter) {
        KClass<SleepSessionRecord> kClass = JvmClassMappingKt.getKotlinClass(SleepSessionRecord.class);
        ReadRecordsRequest<SleepSessionRecord> request = new ReadRecordsRequest<>(
            kClass, filter, Collections.emptySet(), false, 100, null
        );

        ListenableFuture<ReadRecordsResponse<SleepSessionRecord>> future = client.readRecordsAsync(request);
        Futures.addCallback(future, new FutureCallback<ReadRecordsResponse<SleepSessionRecord>>() {
            @Override
            public void onSuccess(ReadRecordsResponse<SleepSessionRecord> response) {
                StringBuilder sb = new StringBuilder("{\"records\":[");
                boolean first = true;
                for (SleepSessionRecord rec : response.getRecords()) {
                    if (!first) sb.append(",");
                    Duration dur = Duration.between(rec.getStartTime(), rec.getEndTime());
                    double durationHours = dur.toMillis() / 3_600_000.0;

                    // stages가 있으면 deep sleep ratio 계산
                    double deepMillis = 0;
                    long totalStageMillis = 0;
                    for (SleepSessionRecord.Stage stage : rec.getStages()) {
                        long stageDur = Duration.between(stage.getStartTime(), stage.getEndTime()).toMillis();
                        totalStageMillis += stageDur;
                        if (stage.getStage() == SleepSessionRecord.STAGE_TYPE_DEEP) {
                            deepMillis += stageDur;
                        }
                    }
                    double deepRatio = totalStageMillis > 0 ? deepMillis / totalStageMillis : 0;

                    // 단순 quality 추론 (무성님 Rule 1 임계값 60 기준)
                    int quality = computeQuality(durationHours, deepRatio);

                    sb.append("{")
                        .append("\"durationHours\":").append(String.format("%.2f", durationHours))
                        .append(",\"quality\":").append(quality)
                        .append(",\"deepSleepRatio\":").append(String.format("%.2f", deepRatio))
                        .append(",\"startTime\":\"").append(rec.getStartTime().toString()).append("\"")
                        .append(",\"endTime\":\"").append(rec.getEndTime().toString()).append("\"")
                        .append("}");
                    first = false;
                }
                sb.append("]}");
                setResult("sleep", sb.toString());
            }

            @Override
            public void onFailure(Throwable t) {
                setError("sleep", "readSleep 실패: " + t.getMessage());
            }
        }, MoreExecutors.directExecutor());
    }

    /**
     * 수면 시간 + 깊은수면 비율 → 0~100 quality 점수.
     * 무성님 Rule 1 (quality < 60), Rule 5 (quality < 60), Rule 10 (quality < 60) 매칭 기준.
     */
    private static int computeQuality(double durationHours, double deepRatio) {
        int q = 50;
        if (durationHours >= 8) q += 30;
        else if (durationHours >= 7) q += 30;
        else if (durationHours >= 6) q += 15;
        else if (durationHours >= 4) q += 0;
        else q -= 20;

        if (deepRatio >= 0.20) q += 20;
        else if (deepRatio >= 0.15) q += 10;
        else if (deepRatio < 0.05) q -= 10;

        if (q < 0) q = 0;
        if (q > 100) q = 100;
        return q;
    }

    // ─────────────────────────────────────────────────────
    // Steps
    // ─────────────────────────────────────────────────────

    @SuppressWarnings("unchecked")
    private static void readSteps(HealthConnectClient client, TimeRangeFilter filter) {
        KClass<StepsRecord> kClass = JvmClassMappingKt.getKotlinClass(StepsRecord.class);
        ReadRecordsRequest<StepsRecord> request = new ReadRecordsRequest<>(
            kClass, filter, Collections.emptySet(), false, 1000, null
        );

        ListenableFuture<ReadRecordsResponse<StepsRecord>> future = client.readRecordsAsync(request);
        Futures.addCallback(future, new FutureCallback<ReadRecordsResponse<StepsRecord>>() {
            @Override
            public void onSuccess(ReadRecordsResponse<StepsRecord> response) {
                long totalSteps = 0;
                Instant earliest = null;
                Instant latest = null;
                for (StepsRecord rec : response.getRecords()) {
                    totalSteps += rec.getCount();
                    if (earliest == null || rec.getStartTime().isBefore(earliest)) earliest = rec.getStartTime();
                    if (latest == null || rec.getEndTime().isAfter(latest)) latest = rec.getEndTime();
                }
                String sb = "{\"totalSteps\":" + totalSteps
                    + ",\"startTime\":\"" + (earliest != null ? earliest.toString() : "")
                    + "\",\"endTime\":\"" + (latest != null ? latest.toString() : "")
                    + "\"}";
                setResult("steps", sb);
            }

            @Override
            public void onFailure(Throwable t) {
                setError("steps", "readSteps 실패: " + t.getMessage());
            }
        }, MoreExecutors.directExecutor());
    }

    // ─────────────────────────────────────────────────────
    // Heart Rate
    // ─────────────────────────────────────────────────────

    @SuppressWarnings("unchecked")
    private static void readHeartRate(HealthConnectClient client, TimeRangeFilter filter) {
        KClass<HeartRateRecord> kClass = JvmClassMappingKt.getKotlinClass(HeartRateRecord.class);
        ReadRecordsRequest<HeartRateRecord> request = new ReadRecordsRequest<>(
            kClass, filter, Collections.emptySet(), false, 1000, null
        );

        ListenableFuture<ReadRecordsResponse<HeartRateRecord>> future = client.readRecordsAsync(request);
        Futures.addCallback(future, new FutureCallback<ReadRecordsResponse<HeartRateRecord>>() {
            @Override
            public void onSuccess(ReadRecordsResponse<HeartRateRecord> response) {
                // 평균 BPM + 샘플 카운트 집계
                long sumBpm = 0;
                int sampleCount = 0;
                for (HeartRateRecord rec : response.getRecords()) {
                    for (HeartRateRecord.Sample sample : rec.getSamples()) {
                        sumBpm += sample.getBeatsPerMinute();
                        sampleCount++;
                    }
                }
                double avgBpm = sampleCount > 0 ? (double) sumBpm / sampleCount : 0;
                String sb = "{\"avgBpm\":" + String.format("%.1f", avgBpm)
                    + ",\"sampleCount\":" + sampleCount
                    + "}";
                setResult("heart_rate", sb);
            }

            @Override
            public void onFailure(Throwable t) {
                setError("heart_rate", "readHeartRate 실패: " + t.getMessage());
            }
        }, MoreExecutors.directExecutor());
    }

    // ─────────────────────────────────────────────────────
    // Result setters
    // ─────────────────────────────────────────────────────

    private static void setResult(String type, String json) {
        resultJson.put(type, json);
        resultReady.put(type, true);
        Log.i(TAG, type + " 결과: " + (json.length() > 200 ? json.substring(0, 200) + "..." : json));
    }

    private static void setError(String type, String msg) {
        resultError.put(type, msg);
        resultReady.put(type, true);
        Log.e(TAG, msg);
    }
}
