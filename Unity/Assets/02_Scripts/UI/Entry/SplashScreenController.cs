using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

/// <summary>
/// 1-1. SplashScreen 컨트롤러
/// 앱 실행 시 로고 애니메이션 → 자동 전환
/// - 첫 실행: OnboardingScreen으로 이동
/// - 재실행: MyRoomScreen으로 이동
/// </summary>
public class SplashScreenController : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Settings")]
    [SerializeField] private float splashDuration = 2.5f;
    [SerializeField] private float logoAnimDuration = 0.6f;
    [SerializeField] private float fadeInDelay = 0.3f;

    // UI 요소 참조
    private VisualElement root;
    private VisualElement logoRing;
    private VisualElement spinner;
    private Label appName;
    private Label appSubtitle;
    private Label loadingText;

    private void OnEnable()
    {
        root = uiDocument.rootVisualElement;

        // 요소 바인딩
        logoRing    = root.Q("logo-ring");
        spinner     = root.Q("spinner");
        appName     = root.Q<Label>("app-name");
        appSubtitle = root.Q<Label>("app-subtitle");
        loadingText = root.Q<Label>("loading-text");

        // 초기 상태: 투명
        SetOpacity(logoRing, 0f);
        SetOpacity(appName, 0f);
        SetOpacity(appSubtitle, 0f);
        SetOpacity(spinner, 0f);
        SetOpacity(loadingText, 0f);

        StartCoroutine(PlaySplashSequence());
    }

    private IEnumerator PlaySplashSequence()
    {
        // 1) 로고 팝 애니메이션
        yield return StartCoroutine(AnimateLogoPop());

        // 2) 텍스트 페이드 인 (0.3s 딜레이 후)
        yield return new WaitForSeconds(fadeInDelay);
        yield return StartCoroutine(FadeIn(appName, 0.4f));

        yield return StartCoroutine(FadeIn(appSubtitle, 0.3f));

        // 3) 스피너 표시 + 회전 시작
        yield return StartCoroutine(FadeIn(spinner, 0.2f));
        yield return StartCoroutine(FadeIn(loadingText, 0.2f));
        StartCoroutine(RotateSpinner());

        // 4) 남은 시간 대기
        float elapsed = logoAnimDuration + fadeInDelay + 0.9f; // 대략적 경과 시간
        float remaining = splashDuration - elapsed;
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        // 5) 화면 전환
        NavigateNext();
    }

    /// <summary>
    /// 로고 팝 애니메이션: scale 0.85 → 1.04 → 1.0
    /// </summary>
    private IEnumerator AnimateLogoPop()
    {
        float duration = logoAnimDuration;
        float t = 0f;

        SetOpacity(logoRing, 1f);

        while (t < duration)
        {
            t += Time.deltaTime;
            float p = t / duration;

            float scale;
            float alpha;

            if (p < 0.6f)
            {
                // 0 → 0.6: scale 0.85 → 1.04, opacity 0 → 1
                float sub = p / 0.6f;
                scale = Mathf.Lerp(0.85f, 1.04f, EaseOutCubic(sub));
                alpha = Mathf.Lerp(0f, 1f, sub);
            }
            else
            {
                // 0.6 → 1.0: scale 1.04 → 1.0
                float sub = (p - 0.6f) / 0.4f;
                scale = Mathf.Lerp(1.04f, 1f, EaseInOutCubic(sub));
                alpha = 1f;
            }

            logoRing.transform.scale = Vector3.one * scale;
            SetOpacity(logoRing, alpha);

            yield return null;
        }

        logoRing.transform.scale = Vector3.one;
        SetOpacity(logoRing, 1f);
    }

    /// <summary>
    /// 요소 페이드 인
    /// </summary>
    private IEnumerator FadeIn(VisualElement element, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            SetOpacity(element, Mathf.Clamp01(t / duration));
            yield return null;
        }
        SetOpacity(element, 1f);
    }

    /// <summary>
    /// 스피너 무한 회전
    /// </summary>
    private IEnumerator RotateSpinner()
    {
        float angle = 0f;
        while (true)
        {
            angle += Time.deltaTime * 450f; // 0.8s per rotation = 450 deg/s
            if (angle >= 360f) angle -= 360f;
            spinner.transform.rotation = Quaternion.Euler(0, 0, angle);
            yield return null;
        }
    }

    /// <summary>
    /// 다음 화면으로 전환
    /// </summary>
    private void NavigateNext()
    {
        var user = Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser;
        bool isProperlyLoggedIn = user != null && !user.IsAnonymous;

        bool hasOnboarding = PlayerPrefs.HasKey("onboarding_complete");
        bool hasNickname   = PlayerPrefs.HasKey("nickname");
        // 온보딩 + 로그인 + 프로필 설정까지 전체 플로우를 완료한 경우에만 "셋업 완료"
        bool setupComplete = hasOnboarding && hasNickname;

        Debug.Log($"[Splash] 상태 진단 — user={user?.UserId ?? "null"} isAnonymous={user?.IsAnonymous} " +
                  $"onboarding_complete={hasOnboarding} nickname={hasNickname} " +
                  $"→ properlyLoggedIn={isProperlyLoggedIn} setupComplete={setupComplete}");

        if (isProperlyLoggedIn)
        {
            Debug.Log("[Splash] 로그인 상태 → MyRoomScreen");
            ScreenManager.Instance.ClearHistory();
            ScreenManager.Instance.GoTo("myroom");
        }
        else if (setupComplete)
        {
            // 셋업은 완료됐으나 로그인 세션 없음 → 재인증
            Debug.Log("[Splash] 셋업 완료, 재인증 필요 → LoginFlowScreen");
            ScreenManager.Instance.GoTo("login");
        }
        else
        {
            // 첫 실행이거나 셋업 미완료(온보딩만 했고 프로필 미설정 등)
            Debug.Log("[Splash] 셋업 미완료 → OnboardingScreen");
            ScreenManager.Instance.GoTo("onboarding");
        }
    }

    // ── 유틸리티 ──

    private void SetOpacity(VisualElement el, float alpha)
    {
        el.style.opacity = alpha;
    }

    private float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private float EaseInOutCubic(float t)
    {
        return t < 0.5f
            ? 4f * t * t * t
            : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    }
}
