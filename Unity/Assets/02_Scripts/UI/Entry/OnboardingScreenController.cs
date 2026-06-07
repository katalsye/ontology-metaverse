using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
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

    [Header("Gemma 모델 매니저")]
    [Tooltip("비워두면 씬에서 자동으로 찾음")]
    [SerializeField] private GemmaOnDeviceManager gemmaManager;

    private GemmaOnDeviceManager GemmaManager
    {
        get
        {
            if (gemmaManager == null)
                gemmaManager = FindObjectOfType<GemmaOnDeviceManager>();
            return gemmaManager;
        }
    }

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
                StartCoroutine(WatchModelDownload());
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

    /// <summary>
    /// Gemma 모델 다운로드 진행 상황을 GemmaOnDeviceManager에서 폴링해 표시
    /// </summary>
    private IEnumerator WatchModelDownload()
    {
        isDownloading = true;
        UpdateButtons();
        downloadStatus.text = "다운로드 중...";

        var gemma = GemmaManager;
        if (gemma == null)
        {
            Debug.LogError("[Onboarding] GemmaOnDeviceManager를 찾을 수 없음");
            downloadStatus.text = "AI 엔진을 찾을 수 없습니다";
            isDownloading = false;
            UpdateButtons();
            yield break;
        }

        while (!gemma.isModelLoaded)
        {
            if (gemma.IsInitializingEngine)
            {
                // 파일 복사는 끝났지만 AI 엔진을 메모리에 올리는 중 — 오래 걸릴 수 있음
                progressFill.style.width = Length.Percent(100f);
                downloadStatus.text = "AI 엔진을 초기화하는 중...\n(기기에 따라 다소 시간이 걸릴 수 있어요)";
                downloadSize.text = "초기화 중";
            }
            else
            {
                float progress = gemma.DownloadProgress;
                progressFill.style.width = Length.Percent(progress * 100f);

                float downloadedMB = gemma.DownloadedBytes / 1024f / 1024f;
                float totalMB = gemma.TotalBytes / 1024f / 1024f;

                downloadStatus.text = totalMB > 0f
                    ? $"다운로드 중... {downloadedMB:F0}MB / {totalMB:F0}MB"
                    : "다운로드 중...";
                downloadSize.text = $"{(progress * 100f):F0}%";
            }

            yield return null;
        }

        // 다운로드 완료
        progressFill.style.width = Length.Percent(100f);
        isDownloading = false;
        downloadComplete = true;
        downloadStatus.text = "다운로드 완료!";
        downloadSize.text = "AI 엔진 준비 완료";

        // 버튼을 "시작하기"로 변경
        UpdateButtons();

        // mint 색상으로 프로그래스 바 변경 (완료 표시)
        progressFill.style.backgroundColor = new StyleColor(AppColors.Mint);
    }

    private void NavigateToLogin()
    {
        Debug.Log("[Onboarding] → LoginFlowScreen");
        ScreenManager.Instance.GoTo("login");
    }
}
