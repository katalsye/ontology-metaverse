using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

/// <summary>
/// 7-3. Toast 컨트롤러
/// 푸시 알림 토스트, 탭 시 해당 화면 이동
/// </summary>
public class ToastController : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;

    private VisualElement toastContainer;
    private Label toastTitle;
    private Label toastBody;
    private string targetScreen;

    private void Awake()
    {
        var root = uiDocument.rootVisualElement;
        toastContainer = root.Q("toast-container");
        toastTitle = root.Q<Label>("toast-title");
        toastBody = root.Q<Label>("toast-body");

        toastContainer.RegisterCallback<ClickEvent>(evt =>
        {
            Hide();
            if (!string.IsNullOrEmpty(targetScreen))
                ScreenManager.Instance.GoTo(targetScreen);
        });
    }

    public void Show(string title, string body, string screen, float duration = 3f)
    {
        toastTitle.text = title;
        toastBody.text = body;
        targetScreen = screen;
        toastContainer.style.display = DisplayStyle.Flex;

        StartCoroutine(AutoHide(duration));
    }

    private IEnumerator AutoHide(float duration)
    {
        yield return new WaitForSeconds(duration);
        Hide();
    }

    private void Hide()
    {
        toastContainer.style.display = DisplayStyle.None;
    }
}
