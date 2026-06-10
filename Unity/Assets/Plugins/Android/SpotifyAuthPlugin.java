package com.ontology.metaverse.spotify;

import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import android.util.Log;

/**
 * Spotify OAuth 인증 시작 + callback 결과 보관 (Unity ↔ Java 브릿지).
 *
 * 흐름:
 *   1. C#이 PKCE code_verifier/challenge 생성 + state 생성
 *   2. C#이 buildAuthUrl()로 인증 URL 받음
 *   3. C#이 launchAuth()로 시스템 브라우저(또는 Chrome Tabs) 띄움
 *   4. 사용자 로그인 → Spotify가 ontologyapp://spotify-callback?code=XXX 로 redirect
 *   5. SpotifyCallbackActivity가 받아서 setCallbackResult() 호출
 *   6. C# Coroutine이 isCallbackReady() polling → getCallback*() 로 결과 추출
 *   7. C#이 직접 POST https://accounts.spotify.com/api/token → access/refresh token
 *
 * Java는 token 교환에 관여하지 않음 (PKCE라 client_secret 불필요, C#에서 HTTP 직접).
 */
public class SpotifyAuthPlugin {
    private static final String TAG = "SpotifyAuthPlugin";

    private static final String AUTHORIZE_URL = "https://accounts.spotify.com/authorize";

    // Callback 결과 (스레드 안전을 위해 volatile, 단일 callback 가정)
    private static volatile String lastCode = null;
    private static volatile String lastState = null;
    private static volatile String lastError = null;
    private static volatile boolean callbackReady = false;

    /**
     * Authorization URL 빌드. C#이 이 URL을 launchAuth()에 전달.
     *
     * scopes 예: "user-read-recently-played user-read-playback-state user-top-read"
     */
    public static String buildAuthUrl(
            String clientId,
            String redirectUri,
            String scopes,
            String codeChallenge,
            String state) {
        Uri uri = Uri.parse(AUTHORIZE_URL).buildUpon()
            .appendQueryParameter("client_id", clientId)
            .appendQueryParameter("response_type", "code")
            .appendQueryParameter("redirect_uri", redirectUri)
            .appendQueryParameter("scope", scopes)
            .appendQueryParameter("code_challenge_method", "S256")
            .appendQueryParameter("code_challenge", codeChallenge)
            .appendQueryParameter("state", state)
            .build();
        return uri.toString();
    }

    /**
     * 시스템 브라우저로 인증 URL 띄움.
     * 새 callback 시작 전에 이전 결과 초기화.
     */
    public static void launchAuth(Activity activity, String authUrl) {
        resetCallback();
        try {
            Intent intent = new Intent(Intent.ACTION_VIEW, Uri.parse(authUrl));
            intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            activity.startActivity(intent);
            Log.i(TAG, "Spotify 인증 페이지 띄움");
        } catch (Exception e) {
            Log.e(TAG, "launchAuth 실패: " + e.getMessage(), e);
            lastError = "브라우저 띄우기 실패: " + e.getMessage();
            callbackReady = true;
        }
    }

    /**
     * Callback Activity가 결과를 저장할 때 호출.
     */
    public static void setCallbackResult(String code, String state, String error) {
        lastCode = code;
        lastState = state;
        lastError = error;
        callbackReady = true;
        Log.i(TAG, "callback 결과 저장: code=" + (code != null ? "OK" : "null") + ", error=" + error);
    }

    /** Unity Coroutine polling용. */
    public static boolean isCallbackReady() {
        return callbackReady;
    }

    /** 결과 추출 후 reset 권장 (resetCallback). */
    public static String getCallbackCode() {
        return lastCode;
    }

    public static String getCallbackState() {
        return lastState;
    }

    public static String getCallbackError() {
        return lastError;
    }

    /** 다음 인증 사이클을 위해 결과 초기화. */
    public static void resetCallback() {
        lastCode = null;
        lastState = null;
        lastError = null;
        callbackReady = false;
    }
}
