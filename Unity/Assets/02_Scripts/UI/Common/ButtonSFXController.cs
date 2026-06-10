using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 앱 전체 버튼 탭 SFX 전담 컨트롤러
/// ScreenManager.OnScreenChanged 를 구독해 화면 전환마다 새 UIDocument에 재등록.
/// TrickleDown ClickEvent 로 하위 버튼 클릭을 전부 감지한다.
/// </summary>
public class ButtonSFXController : MonoBehaviour
{
    [Tooltip("AudioManager.sfxClips 배열에서 버튼 탭 SFX의 인덱스")]
    [SerializeField] private int buttonTapSfxIndex = 7;

    private void OnEnable()
    {
        RegisterAllActiveDocuments();

        if (ScreenManager.Instance != null)
            ScreenManager.Instance.OnScreenChanged += OnScreenChanged;
    }

    private void OnDisable()
    {
        if (ScreenManager.Instance != null)
            ScreenManager.Instance.OnScreenChanged -= OnScreenChanged;
    }

    private void OnScreenChanged(string _) => RegisterAllActiveDocuments();

    private void RegisterAllActiveDocuments()
    {
        foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
        {
            var root = doc.rootVisualElement;
            if (root == null) continue;
            // 중복 방지: 기존 등록 제거 후 재등록
            root.UnregisterCallback<ClickEvent>(OnButtonClicked, TrickleDown.TrickleDown);
            root.RegisterCallback<ClickEvent>(OnButtonClicked, TrickleDown.TrickleDown);
        }
    }

    private void OnButtonClicked(ClickEvent evt)
    {
        if (evt.target is Button)
            AudioManager.Instance?.PlaySFX(buttonTapSfxIndex);
    }
}
