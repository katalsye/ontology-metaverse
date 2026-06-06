using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

/// <summary>
/// ScreenManager — 앱 전체 화면 전환 관리
/// 싱글톤으로 모든 화면의 GoTo/GoBack 처리
/// 
/// 사용법:
///   ScreenManager.Instance.GoTo("feed");
///   ScreenManager.Instance.GoBack();
/// </summary>
public class ScreenManager : MonoBehaviour
{
    public static ScreenManager Instance { get; private set; }

    [Header("Screen UI Documents")]
    [Tooltip("화면 이름(key)과 UIDocument를 매핑")]
    [SerializeField] private List<ScreenEntry> screens = new List<ScreenEntry>();

    [System.Serializable]
    public class ScreenEntry
    {
        public string key;          // "splash", "onboarding", "myroom", "feed", etc.
        public UIDocument document;
        public bool hideBottomNav;  // 3D 뷰 진입 시 하단 바 숨김
    }

    // 내비게이션 히스토리
    private Stack<string> history = new Stack<string>();
    private string currentScreen;

    // 화면 전환 콜백 (BottomNav 등에서 구독)
    public event System.Action<string> OnScreenChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 특정 화면으로 이동
    /// </summary>
    public void GoTo(string screenKey, bool addToHistory = true)
    {
        if (currentScreen == screenKey) return;

        // 현재 화면 비활성화
        if (!string.IsNullOrEmpty(currentScreen))
        {
            SetScreenActive(currentScreen, false);
            if (addToHistory)
                history.Push(currentScreen);
        }

        // 새 화면 활성화
        SetScreenActive(screenKey, true);
        currentScreen = screenKey;

        OnScreenChanged?.Invoke(screenKey);
        Debug.Log($"[ScreenManager] → {screenKey}");
    }

    /// <summary>
    /// 뒤로가기
    /// </summary>
    public void GoBack()
    {
        if (history.Count == 0)
        {
            Debug.Log("[ScreenManager] 히스토리 없음, 마이룸으로 이동");
            GoTo("myroom", false);
            return;
        }

        string prev = history.Pop();
        SetScreenActive(currentScreen, false);
        SetScreenActive(prev, true);
        currentScreen = prev;

        OnScreenChanged?.Invoke(prev);
        Debug.Log($"[ScreenManager] ← {prev}");
    }

    /// <summary>
    /// 현재 화면이 하단 네비 숨김 대상인지
    /// </summary>
    public bool ShouldHideBottomNav()
    {
        var entry = screens.Find(s => s.key == currentScreen);
        return entry != null && entry.hideBottomNav;
    }

    /// <summary>
    /// 현재 화면 키 반환
    /// </summary>
    public string GetCurrentScreen() => currentScreen;

    private void SetScreenActive(string key, bool active)
    {
        var entry = screens.Find(s => s.key == key);
        if (active)
        {
            // 씬 초기 상태와 무관하게, 활성화 전 모든 화면 비활성화
            foreach (var s in screens)
                if (s.document != null)
                    s.document.gameObject.SetActive(false);

            if (entry != null && entry.document != null)
                entry.document.gameObject.SetActive(true);
        }
        else
        {
            if (entry != null && entry.document != null)
                entry.document.gameObject.SetActive(false);
        }
    }
}
