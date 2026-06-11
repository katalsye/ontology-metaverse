using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class ScreenManager : MonoBehaviour
{
    public static ScreenManager Instance { get; private set; }

    [Header("Screen UI Documents")]
    [Tooltip("화면 이름(key)과 UIDocument를 매핑")]
    [SerializeField] private List<ScreenEntry> screens = new List<ScreenEntry>();

    [System.Serializable]
    public class ScreenEntry
    {
        public string key;
        public UIDocument document;
        public bool hideBottomNav;
    }

    private Stack<string> history = new Stack<string>();
    private string currentScreen;

    public event System.Action<string> OnScreenChanged;

    private void Awake()
    {
        Debug.Log($"[ScreenManager] Awake — InstanceID: {GetInstanceID()}");

        if (Instance != null && Instance != this)
        {
            Debug.Log($"[ScreenManager] 중복 인스턴스 감지 → Destroy");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        foreach (var entry in screens)
        {
            if (entry.document != null)
                entry.document.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        Debug.Log($"[ScreenManager] Start — InstanceID: {GetInstanceID()}");
        GoTo("splash", false);
    }

    public void GoTo(string screenKey, bool addToHistory = true)
    {
        Debug.Log($"[ScreenManager] GoTo({screenKey}) — currentScreen={currentScreen}");

        if (currentScreen == screenKey) return;

        if (!string.IsNullOrEmpty(currentScreen))
        {
            // splash는 한 번 표시 후 재활성화 방지
            if (currentScreen != "splash")
            {
                SetScreenActive(currentScreen, false);
            }
            else
            {
                // splash는 비활성화만 하고 히스토리에 쌓지 않음
                SetScreenActive(currentScreen, false);
                addToHistory = false;
            }

            if (addToHistory)
                history.Push(currentScreen);
        }

        currentScreen = screenKey;
        SetScreenActive(screenKey, true);

        OnScreenChanged?.Invoke(screenKey);
        Debug.Log($"[ScreenManager] → {screenKey}");
    }

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

    public bool ShouldHideBottomNav()
    {
        var entry = screens.Find(s => s.key == currentScreen);
        return entry != null && entry.hideBottomNav;
    }

    public string GetCurrentScreen() => currentScreen;

    public void ClearHistory() => history.Clear();

    private void SetScreenActive(string key, bool active)
    {
        var entry = screens.Find(s => s.key == key);
        if (entry == null || entry.document == null)
        {
            Debug.LogWarning($"[ScreenManager] SetScreenActive: '{key}' 키를 찾을 수 없음");
            return;
        }
        Debug.Log($"[ScreenManager] SetScreenActive({key}, {active})");
        entry.document.gameObject.SetActive(active);
    }
}