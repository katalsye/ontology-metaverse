package com.ontology.metaverse.health

import android.app.Activity
import android.content.Intent
import android.util.Log
import androidx.health.connect.client.HealthConnectClient
import androidx.health.connect.client.permission.HealthPermission
import androidx.health.connect.client.records.HeartRateRecord
import androidx.health.connect.client.records.HeartRateVariabilityRmssdRecord
import androidx.health.connect.client.records.SleepSessionRecord
import androidx.health.connect.client.records.StepsRecord
import androidx.health.connect.client.request.ReadRecordsRequest
import androidx.health.connect.client.time.TimeRangeFilter
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withTimeoutOrNull
import java.time.Duration
import java.time.Instant
import java.util.Locale
import java.util.concurrent.ConcurrentHashMap

/**
 * Health Connect 통합 (Unity ↔ Kotlin 브릿지).
 *
 * Health Connect 1.1.0 SDK의 suspend API를 코루틴으로 네이티브 호출하고,
 * 결과를 type별 맵에 저장 → C#(HealthConnectBridge.cs)이 폴링으로 가져감.
 *
 * @JvmStatic 덕분에 object의 메서드가 진짜 static 메서드로 노출되어
 * AndroidJavaClass.CallStatic 으로 그대로 호출 가능.
 *
 * C# 호출 시그너처 (HealthConnectBridge.cs와 1:1):
 *   getSdkStatus(activity): Int            — 3=Available, 2=ProviderUpdateRequired, 1=Unavailable
 *   hasAllPermissions(activity): Boolean
 *   getGrantedPermissionsJson(activity): String
 *   openHealthConnectSettings(activity)    — 권한 요청 화면 띄움
 *   requestRead(activity, type, startMs, endMs)  — 비동기 read 시작
 *   isReady(type): Boolean / getResult(type): String? / getError(type): String?
 *
 * type: "sleep" | "steps" | "heart_rate"
 */
object HealthConnectPlugin {
    private const val TAG = "HealthConnectPlugin"
    private const val PROVIDER = "com.google.android.apps.healthdata"

    // 백그라운드 read 전용 스코프 (앱 라이프타임)
    private val scope = CoroutineScope(Dispatchers.IO + SupervisorJob())

    // type별 결과 (스레드 안전)
    private val resultJson = ConcurrentHashMap<String, String>()
    private val resultError = ConcurrentHashMap<String, String>()
    private val resultReady = ConcurrentHashMap<String, Boolean>()

    // 필수 권한 (hasAllPermissions 체크 기준) — HRV는 옵션이라 제외
    private val requiredPermissions: Set<String> = setOf(
        HealthPermission.getReadPermission(SleepSessionRecord::class),
        HealthPermission.getReadPermission(StepsRecord::class),
        HealthPermission.getReadPermission(HeartRateRecord::class)
    )

    // 옵션 권한 (있으면 좋음 — HRV RMSSD는 갤럭시워치 등 일부 기기만 기록)
    private val optionalPermissions: Set<String> = setOf(
        HealthPermission.getReadPermission(HeartRateVariabilityRmssdRecord::class)
    )

    // 권한 요청 화면엔 필수+옵션 모두 포함 (사용자가 한 번에 허용)
    private val allRequestablePermissions: Set<String> = requiredPermissions + optionalPermissions

    // ─────────────────────────────────────────────────────
    // Availability & Permissions
    // ─────────────────────────────────────────────────────

    @JvmStatic
    fun getSdkStatus(activity: Activity): Int {
        return try {
            HealthConnectClient.getSdkStatus(activity, PROVIDER)
        } catch (e: Exception) {
            Log.e(TAG, "getSdkStatus 실패: ${e.message}", e)
            HealthConnectClient.SDK_UNAVAILABLE
        }
    }

    @JvmStatic
    fun hasAllPermissions(activity: Activity): Boolean {
        return try {
            runBlocking {
                withTimeoutOrNull(5000L) {
                    val client = HealthConnectClient.getOrCreate(activity)
                    val granted = client.permissionController.getGrantedPermissions()
                    granted.containsAll(requiredPermissions)
                } ?: false
            }
        } catch (e: Exception) {
            Log.e(TAG, "hasAllPermissions 실패: ${e.message}", e)
            false
        }
    }

    @JvmStatic
    fun getGrantedPermissionsJson(activity: Activity): String {
        return try {
            runBlocking {
                withTimeoutOrNull(5000L) {
                    val client = HealthConnectClient.getOrCreate(activity)
                    val granted = client.permissionController.getGrantedPermissions()
                    granted.joinToString(separator = ",", prefix = "[", postfix = "]") { "\"$it\"" }
                } ?: "[]"
            }
        } catch (e: Exception) {
            Log.e(TAG, "getGrantedPermissionsJson 실패: ${e.message}", e)
            "[]"
        }
    }

    /**
     * Health Connect 권한 요청 — 전용 HealthPermissionActivity 를 띄운다.
     * (Unity Activity 가 androidx.activity.ComponentActivity 가 아니라 권한 contract 를
     *  직접 launch 할 수 없으므로, ComponentActivity 인 HealthPermissionActivity 에 위임.
     *  결과는 그 Activity 의 onPermissionResult 콜백 → 다음 batch 의 hasAllPermissions 로 재확인)
     */
    @JvmStatic
    fun openHealthConnectSettings(activity: Activity) {
        // 권한 요청은 ActivityResultContract 기반이라 androidx.activity.ComponentActivity 가 필요하다.
        // Unity Activity 는 그게 아니므로, 전용 HealthPermissionActivity 를 띄워 표준 팝업을 표시한다.
        // (intent 직접 startActivity 는 통합형 Health Connect 기기에서 'No Activity found' 로 실패)
        try {
            val intent = Intent(activity, HealthPermissionActivity::class.java)
            intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
            activity.startActivity(intent)
            Log.i(TAG, "권한 요청 Activity 띄움 (HealthPermissionActivity)")
        } catch (e: Exception) {
            Log.w(TAG, "권한 Activity 실패, Health Connect 설정 화면으로 폴백: ${e.message}")
            try {
                val settings = Intent(HealthConnectClient.ACTION_HEALTH_CONNECT_SETTINGS)
                settings.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
                activity.startActivity(settings)
            } catch (e2: Exception) {
                Log.e(TAG, "설정 화면도 실패: ${e2.message}", e2)
            }
        }
    }

    /** HealthPermissionActivity 가 요청할 권한 집합 (필수 + 옵션). */
    internal fun getAllRequestablePermissions(): Set<String> = allRequestablePermissions

    /** HealthPermissionActivity 의 권한 요청 결과 콜백. 다음 batch 에서 hasAllPermissions 로 재확인된다. */
    internal fun onPermissionResult(granted: Set<String>) {
        val ok = granted.containsAll(requiredPermissions)
        Log.i(TAG, "권한 결과: ${granted.size}개 grant, 필수 권한 충족=$ok")
    }

    // ─────────────────────────────────────────────────────
    // Async Read (Unity Coroutine이 isReady/getResult 폴링)
    // ─────────────────────────────────────────────────────

    @JvmStatic
    fun requestRead(activity: Activity, type: String, startMillis: Long, endMillis: Long) {
        resultReady[type] = false
        resultJson.remove(type)
        resultError.remove(type)

        scope.launch {
            try {
                val client = HealthConnectClient.getOrCreate(activity)
                val filter = TimeRangeFilter.between(
                    Instant.ofEpochMilli(startMillis),
                    Instant.ofEpochMilli(endMillis)
                )
                when (type) {
                    "sleep" -> readSleep(client, filter)
                    "steps" -> readSteps(client, filter)
                    "heart_rate" -> readHeartRate(client, filter)
                    "hrv" -> readHrv(client, filter)
                    else -> setError(type, "알 수 없는 type: $type")
                }
            } catch (e: Exception) {
                setError(type, "requestRead 예외: ${e.message}")
            }
        }
    }

    @JvmStatic
    fun isReady(type: String): Boolean = resultReady[type] == true

    @JvmStatic
    fun getResult(type: String): String? = resultJson[type]

    @JvmStatic
    fun getError(type: String): String? = resultError[type]

    // ─────────────────────────────────────────────────────
    // Sleep
    // ─────────────────────────────────────────────────────

    private suspend fun readSleep(client: HealthConnectClient, filter: TimeRangeFilter) {
        val request = ReadRecordsRequest(
            recordType = SleepSessionRecord::class,
            timeRangeFilter = filter
        )
        val response = client.readRecords(request)

        val sb = StringBuilder("{\"records\":[")
        var first = true
        for (rec in response.records) {
            if (!first) sb.append(",")
            val durationHours = Duration.between(rec.startTime, rec.endTime).toMillis() / 3_600_000.0

            // 수면 단계(stages)로 깊은 수면 비율 계산
            var deepMillis = 0L
            var totalStageMillis = 0L
            for (stage in rec.stages) {
                val d = Duration.between(stage.startTime, stage.endTime).toMillis()
                totalStageMillis += d
                if (stage.stage == SleepSessionRecord.STAGE_TYPE_DEEP) deepMillis += d
            }
            val deepRatio = if (totalStageMillis > 0) deepMillis.toDouble() / totalStageMillis else 0.0
            val quality = computeQuality(durationHours, deepRatio)

            sb.append("{")
                .append("\"durationHours\":").append(fmt2(durationHours))
                .append(",\"quality\":").append(quality)
                .append(",\"deepSleepRatio\":").append(fmt2(deepRatio))
                .append(",\"startTime\":\"").append(rec.startTime.toString()).append("\"")
                .append(",\"endTime\":\"").append(rec.endTime.toString()).append("\"")
                .append("}")
            first = false
        }
        sb.append("]}")
        setResult("sleep", sb.toString())
    }

    /**
     * 수면 시간 + 깊은수면 비율 → 0~100 quality.
     * 무성님 Rule 1/5/10 (quality < 60) 매칭 기준.
     */
    private fun computeQuality(durationHours: Double, deepRatio: Double): Int {
        var q = 50
        when {
            durationHours >= 7 -> q += 30
            durationHours >= 6 -> q += 15
            durationHours < 4 -> q -= 20
        }
        when {
            deepRatio >= 0.20 -> q += 20
            deepRatio >= 0.15 -> q += 10
            deepRatio < 0.05 -> q -= 10
        }
        return q.coerceIn(0, 100)
    }

    // ─────────────────────────────────────────────────────
    // Steps
    // ─────────────────────────────────────────────────────

    private suspend fun readSteps(client: HealthConnectClient, filter: TimeRangeFilter) {
        val request = ReadRecordsRequest(
            recordType = StepsRecord::class,
            timeRangeFilter = filter
        )
        val response = client.readRecords(request)

        var total = 0L
        var earliest: Instant? = null
        var latest: Instant? = null
        for (rec in response.records) {
            total += rec.count
            if (earliest == null || rec.startTime.isBefore(earliest)) earliest = rec.startTime
            if (latest == null || rec.endTime.isAfter(latest)) latest = rec.endTime
        }
        val json = "{\"totalSteps\":$total," +
            "\"startTime\":\"${earliest ?: ""}\"," +
            "\"endTime\":\"${latest ?: ""}\"}"
        setResult("steps", json)
    }

    // ─────────────────────────────────────────────────────
    // Heart Rate
    // ─────────────────────────────────────────────────────

    private suspend fun readHeartRate(client: HealthConnectClient, filter: TimeRangeFilter) {
        val request = ReadRecordsRequest(
            recordType = HeartRateRecord::class,
            timeRangeFilter = filter
        )
        val response = client.readRecords(request)

        var sum = 0L
        var count = 0
        for (rec in response.records) {
            for (sample in rec.samples) {
                sum += sample.beatsPerMinute
                count++
            }
        }
        val avg = if (count > 0) sum.toDouble() / count else 0.0
        val json = "{\"avgBpm\":${fmt1(avg)},\"sampleCount\":$count}"
        setResult("heart_rate", json)
    }

    // ─────────────────────────────────────────────────────
    // HRV (RMSSD) — 옵션 (갤럭시워치 등 일부 기기만 기록)
    // ─────────────────────────────────────────────────────

    private suspend fun readHrv(client: HealthConnectClient, filter: TimeRangeFilter) {
        val request = ReadRecordsRequest(
            recordType = HeartRateVariabilityRmssdRecord::class,
            timeRangeFilter = filter
        )
        val response = client.readRecords(request)

        var sum = 0.0
        var count = 0
        for (rec in response.records) {
            sum += rec.heartRateVariabilityMillis
            count++
        }
        val avg = if (count > 0) sum / count else 0.0
        setResult("hrv", "{\"avgRmssd\":${fmt1(avg)},\"sampleCount\":$count}")
    }

    // ─────────────────────────────────────────────────────
    // Result setters / utils
    // ─────────────────────────────────────────────────────

    private fun setResult(type: String, json: String) {
        resultJson[type] = json
        resultReady[type] = true
        Log.i(TAG, "$type 결과: ${json.take(200)}")
    }

    private fun setError(type: String, msg: String) {
        resultError[type] = msg
        resultReady[type] = true
        Log.e(TAG, msg)
    }

    // JSON 유효성 위해 Locale.US 고정 (소수점 콤마 방지)
    private fun fmt1(v: Double): String = String.format(Locale.US, "%.1f", v)
    private fun fmt2(v: Double): String = String.format(Locale.US, "%.2f", v)
}
