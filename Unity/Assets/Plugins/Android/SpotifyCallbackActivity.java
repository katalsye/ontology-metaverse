package com.ontology.metaverse.spotify;

import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.util.Log;

/**
 * Spotify OAuth callback Activity.
 *
 * 사용자가 Spotify 인증 페이지에서 로그인 + 권한 허용하면 시스템이
 * `ontologyapp://spotify-callback?code=XXX&state=YYY` URI로 redirect.
 *
 * AndroidManifest의 intent-filter가 이 URI를 받아 본 Activity를 invoke함.
 *
 * 동작:
 *   1. Intent data에서 code/state/error 추출
 *   2. SpotifyAuthPlugin에 결과 저장 (Unity Coroutine이 polling)
 *   3. Translucent theme + finish() 즉시 호출 → 사용자에겐 매끄러운 복귀
 *   4. Unity 앱이 백그라운드면 reorderToFront로 가져옴
 *
 * 보안: Spotify가 응답에 state 파라미터를 그대로 반환하므로 CSRF 검증 가능.
 *      → C# 쪽에서 보낸 state와 일치 확인.
 */
public class SpotifyCallbackActivity extends Activity {
    private static final String TAG = "SpotifyCallback";

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        handleIntent(getIntent());
        finish();
    }

    @Override
    protected void onNewIntent(Intent intent) {
        super.onNewIntent(intent);
        handleIntent(intent);
        finish();
    }

    private void handleIntent(Intent intent) {
        if (intent == null) {
            Log.w(TAG, "intent null");
            return;
        }
        Uri data = intent.getData();
        if (data == null) {
            Log.w(TAG, "intent.getData() null");
            return;
        }

        String code = data.getQueryParameter("code");
        String state = data.getQueryParameter("state");
        String error = data.getQueryParameter("error");

        Log.i(TAG, "callback 수신: code=" + (code != null ? "OK(" + code.length() + "자)" : "null")
            + ", state=" + state
            + ", error=" + error);

        // Plugin에 결과 저장 (Unity polling용)
        SpotifyAuthPlugin.setCallbackResult(code, state, error);

        // Unity 메인 액티비티를 포그라운드로
        try {
            Intent launch = getPackageManager().getLaunchIntentForPackage(getPackageName());
            if (launch != null) {
                launch.addFlags(Intent.FLAG_ACTIVITY_REORDER_TO_FRONT
                    | Intent.FLAG_ACTIVITY_SINGLE_TOP);
                startActivity(launch);
            }
        } catch (Exception e) {
            Log.e(TAG, "Unity 액티비티 복귀 실패: " + e.getMessage(), e);
        }
    }
}
