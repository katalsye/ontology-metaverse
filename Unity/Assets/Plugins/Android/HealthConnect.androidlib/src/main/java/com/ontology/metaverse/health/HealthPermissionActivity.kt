package com.ontology.metaverse.health

import android.os.Bundle
import android.util.Log
import androidx.activity.ComponentActivity
import androidx.health.connect.client.PermissionController

/**
 * Health Connect 권한 요청 전용 투명 Activity.
 *
 * 왜 별도 Activity가 필요한가:
 *   Health Connect 권한 요청은 ActivityResultContract(registerForActivityResult) 기반인데,
 *   이 API는 androidx.activity.ComponentActivity 에만 존재한다.
 *   Unity의 UnityPlayerGameActivity는 androidx.core.app.ComponentActivity 기반이라
 *   registerForActivityResult 를 쓸 수 없어, 권한 intent 를 직접 startActivity 하면
 *   "No Activity found to handle Intent {act=...REQUEST_PERMISSIONS}" 로 실패한다.
 *   (삼성 S24/Android 14+ 처럼 Health Connect 가 OS 통합형인 기기에서 특히)
 *
 *   → androidx.activity.ComponentActivity 를 상속한 투명 Activity 를 띄워
 *     표준 권한 팝업을 정상 표시하고, 결과를 HealthConnectPlugin 으로 콜백한다.
 *
 * 흐름:
 *   HealthConnectCollector(권한 없음 감지)
 *     → HealthConnectBridge.OpenHealthConnectSettings()
 *     → HealthConnectPlugin.openHealthConnectSettings(activity)
 *     → 이 Activity 를 startActivity
 *     → registerForActivityResult 로 권한 팝업 → 사용자 허용
 *     → onPermissionResult 콜백 → finish()
 *     → 다음 batch 에서 hasAllPermissions()=true → 데이터 수집
 */
class HealthPermissionActivity : ComponentActivity() {

    // registerForActivityResult 는 Activity 가 STARTED 되기 전(= onCreate 이전)에 등록돼야 한다.
    // 프로퍼티 초기화는 ComponentActivity 생성 시점에 실행되므로 이 요구사항을 만족한다.
    private val requestPermissions =
        registerForActivityResult(PermissionController.createRequestPermissionResultContract()) { granted ->
            Log.i(TAG, "권한 결과 수신: ${granted.size}개 grant")
            HealthConnectPlugin.onPermissionResult(granted)
            finish()
        }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        Log.i(TAG, "onCreate 진입")
        val perms = HealthConnectPlugin.getAllRequestablePermissions()
        Log.i(TAG, "요청할 권한 ${perms.size}개: $perms")
        try {
            requestPermissions.launch(perms)
            Log.i(TAG, "권한 요청 launch 호출 완료")
        } catch (e: Exception) {
            Log.e(TAG, "권한 요청 launch 실패: ${e.message}", e)
            finish()
        }
    }

    companion object {
        private const val TAG = "HealthPermissionActivity"
    }
}
