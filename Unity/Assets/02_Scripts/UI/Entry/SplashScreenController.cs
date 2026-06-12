using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

public class SplashScreenController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private float splashDuration = 2.5f;
    [SerializeField] private float logoAnimDuration = 0.6f;
    [SerializeField] private float fadeInDelay = 0.3f;

    private VisualElement root;
    private VisualElement logoRing;
    private VisualElement spinner;
    private Label appName;
    private Label appSubtitle;
    private Label loadingText;

    /// <summary>스플래시 애니메이션 시퀀스가 끝나면 true (BootManager가 대기에 사용)</summary>
    public bool IsAnimationFinished { get; private set; }

    private Coroutine _splashCoroutine;

    private void OnEnable()
    {
        // OnEnable마다 기존 코루틴 중단 후 1개만 실행
        if (_splashCoroutine != null)
        {
            StopCoroutine(_splashCoroutine);
            _splashCoroutine = null;
        }

        root = uiDocument.rootVisualElement;
        logoRing    = root.Q("logo-ring");
        spinner     = root.Q("spinner");
        appName     = root.Q<Label>("app-name");
        appSubtitle = root.Q<Label>("app-subtitle");
        loadingText = root.Q<Label>("loading-text");

        SetOpacity(logoRing, 0f);
        SetOpacity(appName, 0f);
        SetOpacity(appSubtitle, 0f);
        SetOpacity(spinner, 0f);
        SetOpacity(loadingText, 0f);

        _splashCoroutine = StartCoroutine(PlaySplashSequence());
    }

    private void OnDisable()
    {
        StopAllCoroutines(); // ← StopCoroutine(_splashCoroutine) 대신
        _splashCoroutine = null;
    }

    private IEnumerator PlaySplashSequence()
    {
        yield return StartCoroutine(AnimateLogoPop());
        yield return new WaitForSeconds(fadeInDelay);
        yield return StartCoroutine(FadeIn(appName, 0.4f));
        yield return StartCoroutine(FadeIn(appSubtitle, 0.3f));
        yield return StartCoroutine(FadeIn(spinner, 0.2f));
        yield return StartCoroutine(FadeIn(loadingText, 0.2f));
        StartCoroutine(RotateSpinner());

        float elapsed = logoAnimDuration + fadeInDelay + 0.9f;
        float remaining = splashDuration - elapsed;
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        IsAnimationFinished = true;
    }

    private IEnumerator AnimateLogoPop()
    {
        float duration = logoAnimDuration;
        float t = 0f;
        SetOpacity(logoRing, 1f);
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = t / duration;
            float scale, alpha;
            if (p < 0.6f)
            {
                float sub = p / 0.6f;
                scale = Mathf.Lerp(0.85f, 1.04f, EaseOutCubic(sub));
                alpha = Mathf.Lerp(0f, 1f, sub);
            }
            else
            {
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

    private IEnumerator RotateSpinner()
    {
        float angle = 0f;
        while (true)
        {
            angle += Time.deltaTime * 450f;
            if (angle >= 360f) angle -= 360f;
            spinner.transform.rotation = Quaternion.Euler(0, 0, angle);
            yield return null;
        }
    }

    private void SetOpacity(VisualElement el, float alpha) => el.style.opacity = alpha;
    private float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
    private float EaseInOutCubic(float t) => t < 0.5f
        ? 4f * t * t * t
        : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
}