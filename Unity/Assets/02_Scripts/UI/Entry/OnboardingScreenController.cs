using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using Firebase.Auth;
using Firebase.Storage;
using OntologyMetaverse.OnDeviceAI.Gemma;

/// <summary>
/// 1-2. OnboardingScreen 컨트롤러
/// 4개 슬라이드 전환 + Gemma 모델 다운로드
/// 완료 시 → LoginFlowScreen으로 이동
/// </summary>
public class OnboardingScreenController : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Settings")]
    [SerializeField] private float slideTransitionDuration = 0.3f;

    [Header("Gemma 모델")]
    [SerializeField] private string gemmaStoragePath = "models/gemma-3n-E2B-it-int4.task";

    private VisualElement root;
    private VisualElement slideContainer;
    private Button btnNext;
    private Button btnSkip;
    private Label downloadStatus;
    private Label downloadSize;
    private VisualElement progressFill;

    private VisualElement[] slides;
    private VisualElement[] dots;
    private int currentSlide = 0;
    private const int TOTAL_SLIDES = 4;
    private const int DOWNLOAD_SLIDE = 3; // 마지막 슬라이드가 다운로드

    private bool isDownloading = false;
    private bool downloadComplete = false;
    private bool _isTransitioning = false;

    private void OnEnable()
    {
        root = uiDocument.rootVisualElement;

        // 슬라이드 바인딩
        slides = new VisualElement[TOTAL_SLIDES];
        dots = new VisualElement[TOTAL_SLIDES];
        for (int i = 0; i < TOTAL_SLIDES; i++)
        {
            slides[i] = root.Q($"slide-{i}");
            dots[i] = root.Q($"dot-{i}");
        }

        // 버튼 바인딩
        btnNext = root.Q<Button>("btn-next");
        btnSkip = root.Q<Button>("btn-skip");

        // 다운로드 UI 바인딩
        downloadStatus = root.Q<Label>("download-status");
        downloadSize = root.Q<Label>("download-size");
        progressFill = root.Q("progress-bar-fill");

        // 이벤트 등록
        btnNext.clicked += OnNextClicked;
        btnSkip.clicked += OnSkipClicked;

        // 초기 상태
        ShowSlide(0);
    }

    private void OnDisable()
    {
        if (btnNext != null) btnNext.clicked -= OnNextClicked;
        if (btnSkip != null) btnSkip.clicked -= OnSkipClicked;
    }

    private void OnNextClicked()
    {
        if (currentSlide == DOWNLOAD_SLIDE)
        {
            // 다운로드 슬라이드에서 버튼 클릭
            if (downloadComplete)
            {
                NavigateToLogin();
            }
            else if (!isDownloading)
            {
                StartCoroutine(DownloadGemmaModel());
            }
            return;
        }

        // 다음 슬라이드로
        if (currentSlide < TOTAL_SLIDES - 1)
        {
            ShowSlide(currentSlide + 1);
        }
    }

    private void OnSkipClicked()
    {
        // 건너뛰기 → 바로 다운로드 슬라이드로
        ShowSlide(DOWNLOAD_SLIDE);
    }

    /// <summary>
    /// 특정 슬라이드 표시 (초기 호출 또는 전환 중에는 즉시 적용)
    /// </summary>
    private void ShowSlide(int index)
    {
        if (_isTransitioning) return;

        if (currentSlide == index)
        {
            // 초기 세팅: 애니메이션 없이 바로 표시
            slides[index].RemoveFromClassList("hidden");
            slides[index].AddToClassList("active");
            dots[index].AddToClassList("dot--active");
            UpdateButtons();
            return;
        }

        StartCoroutine(TransitionSlides(currentSlide, index));
    }

    private IEnumerator TransitionSlides(int from, int to)
    {
        _isTransitioning = true;
        float half = slideTransitionDuration * 0.5f;

        // 현재 슬라이드 페이드 아웃
        float elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            slides[from].style.opacity = Mathf.Lerp(1f, 0f, elapsed / half);
            yield return null;
        }
        slides[from].style.opacity = 0f;
        slides[from].RemoveFromClassList("active");
        slides[from].AddToClassList("hidden");
        dots[from].RemoveFromClassList("dot--active");

        // 새 슬라이드 준비 (투명 상태로 표시)
        slides[to].style.opacity = 0f;
        slides[to].RemoveFromClassList("hidden");
        slides[to].AddToClassList("active");
        dots[to].AddToClassList("dot--active");
        currentSlide = to;
        UpdateButtons();

        // 새 슬라이드 페이드 인
        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            slides[to].style.opacity = Mathf.Lerp(0f, 1f, elapsed / half);
            yield return null;
        }
        slides[to].style.opacity = 1f;

        _isTransitioning = false;
    }

    /// <summary>
    /// 슬라이드에 따라 버튼 상태 변경
    /// </summary>
    private void UpdateButtons()
    {
        if (currentSlide == DOWNLOAD_SLIDE)
        {
            // 다운로드 슬라이드
            btnNext.text = isDownloading ? "다운로드 중..." : 
                           downloadComplete ? "시작하기" : "다운로드 시작";
            btnNext.SetEnabled(!isDownloading);
            btnSkip.style.display = DisplayStyle.None;
        }
        else if (currentSlide == TOTAL_SLIDES - 2)
        {
            // 마지막 소개 슬라이드
            btnNext.text = "다음";
            btnSkip.style.display = DisplayStyle.None;
        }
        else
        {
            btnNext.text = "다음";
            btnSkip.style.display = DisplayStyle.Flex;
        }
    }

    private IEnumerator DownloadGemmaModel()
    {
        isDownloading = true;
        UpdateButtons();

        string destPath = GemmaOnDeviceManager.ModelDestPath;

        if (File.Exists(destPath))
        {
            OnModelDownloadComplete();
            yield break;
        }

        downloadStatus.text = "다운로드 준비 중...";
        progressFill.style.width = Length.Percent(0f);

        // Storage 규칙이 auth != null — 온보딩은 로그인 전이므로 익명 인증 먼저
        if (FirebaseAuth.DefaultInstance.CurrentUser == null)
        {
            var anonTask = FirebaseAuth.DefaultInstance.SignInAnonymouslyAsync();
            yield return new WaitUntil(() => anonTask.IsCompleted);

            if (anonTask.IsFaulted)
            {
                OnModelDownloadFailed("인증 실패: " + anonTask.Exception?.GetBaseException().Message);
                yield break;
            }
        }

        // Firebase Storage 다운로드 URL 취득
        var urlTask = FirebaseStorage.DefaultInstance
            .GetReference(gemmaStoragePath)
            .GetDownloadUrlAsync();

        yield return new WaitUntil(() => urlTask.IsCompleted);

        if (urlTask.IsFaulted || urlTask.IsCanceled)
        {
            OnModelDownloadFailed(urlTask.Exception?.GetBaseException().Message ?? "URL 취득 실패");
            yield break;
        }

        // DownloadHandlerFile: 파일에 직접 기록 — 2.91GB를 메모리에 올리지 않음
        using var www = new UnityWebRequest(urlTask.Result.ToString(), UnityWebRequest.kHttpVerbGET);
        www.downloadHandler = new DownloadHandlerFile(destPath);
        www.SendWebRequest();

        const float totalMB = 2910f;
        while (!www.isDone)
        {
            float p = Mathf.Max(0f, www.downloadProgress);
            progressFill.style.width = Length.Percent(p * 100f);
            downloadStatus.text = $"다운로드 중... {p * totalMB:F0}MB / 2,910MB";
            downloadSize.text = $"{p * 100f:F0}%";
            yield return null;
        }

        if (www.result != UnityWebRequest.Result.Success)
        {
            if (File.Exists(destPath)) File.Delete(destPath); // 불완전한 파일 제거
            OnModelDownloadFailed(www.error);
            yield break;
        }

        OnModelDownloadComplete();
    }

    private void OnModelDownloadComplete()
    {
        isDownloading = false;
        downloadComplete = true;
        progressFill.style.width = Length.Percent(100f);
        progressFill.style.backgroundColor = new StyleColor(AppColors.Mint);
        downloadStatus.text = "다운로드 완료!";
        downloadSize.text = "AI 엔진 준비 완료";
        UpdateButtons();
    }

    private void OnModelDownloadFailed(string error)
    {
        isDownloading = false;
        progressFill.style.width = Length.Percent(0f);
        downloadStatus.text = "다운로드 실패. 다시 시도해주세요.";
        downloadSize.text = "";
        Debug.LogError($"[Onboarding] Gemma 모델 다운로드 실패: {error}");
        UpdateButtons();
    }

    private void NavigateToLogin()
    {
        Debug.Log("[Onboarding] → LoginFlowScreen");
        ScreenManager.Instance.GoTo("login");
    }
}
