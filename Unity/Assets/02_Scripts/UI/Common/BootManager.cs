using UnityEngine;
using UnityEngine.SceneManagement;
using Firebase;
using System.Collections;

/// <summary>
/// BootManager — 앱 최초 실행 시 초기화 처리
/// BootScene에 배치. 스플래시 애니메이션을 보여주며 Firebase 초기화 완료 후 MainScene으로 전환.
/// </summary>
public class BootManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private string mainSceneName = "MainScene";
    [SerializeField] private SplashScreenController splashScreen;

    private void Start()
    {
        StartCoroutine(InitializeApp());
    }

    private IEnumerator InitializeApp()
    {
        Debug.Log("[Boot] 앱 초기화 시작");

        // 1) Firebase 초기화 — 완료 전까지 씬 전환 금지
        var dependencyTask = FirebaseApp.CheckAndFixDependenciesAsync();
        yield return new WaitUntil(() => dependencyTask.IsCompleted);

        if (dependencyTask.Result != DependencyStatus.Available)
        {
            Debug.LogError($"[Boot] Firebase 초기화 실패: {dependencyTask.Result}");
            yield break;
        }
        Debug.Log("[Boot] Firebase 초기화 완료");

        // 2) 로컬 데이터 로드 + 진입 화면 결정
        LoadLocalData();
        AppRouter.ResolveEntryScreen();

        // 3) 스플래시 애니메이션이 끝날 때까지 대기 (최소 노출 시간 보장)
        if (splashScreen != null && !splashScreen.IsAnimationFinished)
            yield return new WaitUntil(() => splashScreen.IsAnimationFinished);

        // 4) MainScene 로드
        Debug.Log($"[Boot] → {mainSceneName} (entry: {AppRouter.EntryScreen})");
        SceneManager.LoadScene(mainSceneName);
    }

    private void LoadLocalData()
    {
        bool onboardingDone = PlayerPrefs.HasKey("onboarding_complete");
        Debug.Log($"[Boot] 온보딩 완료: {onboardingDone}");
    }
}
