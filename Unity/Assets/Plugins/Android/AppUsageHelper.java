package com.ontology.metaverse.appusage;

import android.app.Activity;
import android.app.AppOpsManager;
import android.app.usage.UsageStats;
import android.app.usage.UsageStatsManager;
import android.content.Context;
import android.content.Intent;
import android.os.Process;
import android.provider.Settings;
import android.util.Log;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.List;

/**
 * Android UsageStatsManager 헬퍼 (Unity ↔ Java 브릿지).
 *
 * 시스템 권한(PACKAGE_USAGE_STATS)이 필요하며, 사용자가 설정에서 직접 허용해야 함.
 * - Unity는 hasPermission()로 확인 후, 없으면 openSettings()로 사용자를 시스템 설정으로 유도.
 *
 * Unity 호출 예:
 *   AndroidJavaClass cls = new AndroidJavaClass("com.ontology.metaverse.appusage.AppUsageHelper");
 *   bool ok = cls.CallStatic&lt;bool&gt;("hasPermission", activity);
 *   if (!ok) cls.CallStatic("openSettings", activity);
 *   string json = cls.CallStatic&lt;string&gt;("getTopApps", activity, 5);
 *   // json: [{"appName":"YouTube","usageDuration":90,"date":"2026-06-06"}, ...]
 */
public class AppUsageHelper {
    private static final String TAG = "AppUsageHelper";

    /**
     * PACKAGE_USAGE_STATS 권한이 사용자에 의해 허용되어 있는지 확인.
     */
    public static boolean hasPermission(Activity activity) {
        try {
            AppOpsManager appOps = (AppOpsManager) activity.getSystemService(Context.APP_OPS_SERVICE);
            int mode = appOps.unsafeCheckOpNoThrow(
                    AppOpsManager.OPSTR_GET_USAGE_STATS,
                    Process.myUid(),
                    activity.getPackageName()
            );
            return mode == AppOpsManager.MODE_ALLOWED;
        } catch (Throwable t) {
            Log.e(TAG, "hasPermission 확인 실패: " + t.getMessage(), t);
            return false;
        }
    }

    /**
     * 시스템 사용 기록 액세스 설정 화면 열기.
     * 사용자가 직접 우리 앱에 권한을 부여하도록 유도.
     */
    public static void openSettings(Activity activity) {
        try {
            Intent intent = new Intent(Settings.ACTION_USAGE_ACCESS_SETTINGS);
            intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            activity.startActivity(intent);
        } catch (Throwable t) {
            Log.e(TAG, "openSettings 실패: " + t.getMessage(), t);
        }
    }

    /**
     * 최근 24시간 동안 가장 많이 사용된 앱 상위 topN개를 JSON 배열로 반환.
     * 각 원소: {"appName":"YouTube","usageDuration":90,"date":"2026-06-06"}
     *   - appName: 패키지명 또는 라벨 (여기선 패키지명, 라벨 변환은 비용 높아서 Unity 측에서 처리)
     *   - usageDuration: 분(minute) 단위 누적 사용 시간
     *   - date: YYYY-MM-DD (오늘)
     */
    public static String getTopApps(Activity activity, int topN) {
        try {
            if (!hasPermission(activity)) {
                Log.w(TAG, "PACKAGE_USAGE_STATS 권한 없음");
                return "[]";
            }

            UsageStatsManager usm = (UsageStatsManager) activity.getSystemService(Context.USAGE_STATS_SERVICE);
            long endTime = System.currentTimeMillis();
            long startTime = endTime - 24L * 60L * 60L * 1000L; // 24시간

            List<UsageStats> stats = usm.queryUsageStats(UsageStatsManager.INTERVAL_DAILY, startTime, endTime);
            if (stats == null || stats.isEmpty()) {
                Log.w(TAG, "UsageStats 결과 없음");
                return "[]";
            }

            // 사용 시간 내림차순 정렬 + 우리 앱 / 시스템 앱 일부 제외
            stats.sort((a, b) -> Long.compare(b.getTotalTimeInForeground(), a.getTotalTimeInForeground()));

            String today = String.format("%tF", System.currentTimeMillis()); // YYYY-MM-DD

            JSONArray arr = new JSONArray();
            int count = 0;
            for (UsageStats s : stats) {
                long ms = s.getTotalTimeInForeground();
                if (ms < 60 * 1000L) continue; // 1분 미만 무시 (노이즈)
                String pkg = s.getPackageName();
                if (pkg.startsWith("com.ontology.metaverse")) continue; // 자기 자신 제외

                JSONObject row = new JSONObject();
                row.put("appName", pkg);
                row.put("usageDuration", (int) (ms / 1000L / 60L)); // 분
                row.put("date", today);
                arr.put(row);

                count++;
                if (count >= topN) break;
            }

            String result = arr.toString();
            Log.i(TAG, "getTopApps: " + count + "건 반환");
            return result;
        } catch (Throwable t) {
            Log.e(TAG, "getTopApps 실패: " + t.getMessage(), t);
            return "[]";
        }
    }
}
