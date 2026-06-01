using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// BootManager — 앱 최초 실행 시 초기화 처리
/// BootScene에 배치. 초기화 완료 후 MainScene으로 전환.
/// </summary>
public class BootManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private string mainSceneName = "MainScene";

    private void Start()
    {
        InitializeApp();
    }

    private async void InitializeApp()
    {
        Debug.Log("[Boot] 앱 초기화 시작");

        // 1) Firebase 초기화
        // TODO: Firebase SDK 연동 시 아래 주석 해제
        // var dependencyStatus = await Firebase.FirebaseApp.CheckAndFixDependenciesAsync();
        // if (dependencyStatus != Firebase.DependencyStatus.Available)
        // {
        //     Debug.LogError($"[Boot] Firebase 초기화 실패: {dependencyStatus}");
        //     return;
        // }
        // Debug.Log("[Boot] Firebase 초기화 완료");

        // 2) 로컬 데이터 로드 (PlayerPrefs 등)
        LoadLocalData();

        // 3) MainScene 로드
        Debug.Log("[Boot] → MainScene");
        SceneManager.LoadScene(mainSceneName);
    }

    private void LoadLocalData()
    {
        // 온보딩 완료 여부, 닉네임 등 기본 데이터 확인
        bool onboardingDone = PlayerPrefs.HasKey("onboarding_complete");
        Debug.Log($"[Boot] 온보딩 완료: {onboardingDone}");
    }
}
