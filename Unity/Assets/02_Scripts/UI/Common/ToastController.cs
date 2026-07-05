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

    private void OnEnable()
    {
        if (FcmManager.Instance == null) return;
        FcmManager.Instance.OnForegroundNotification += HandleForegroundNotification;
        FcmManager.Instance.OnNotificationTapped     += HandleNotificationTapped;
    }

    private void OnDisable()
    {
        if (FcmManager.Instance == null) return;
        FcmManager.Instance.OnForegroundNotification -= HandleForegroundNotification;
        FcmManager.Instance.OnNotificationTapped     -= HandleNotificationTapped;
    }

    private void HandleForegroundNotification(NotificationData data)
    {
        Show(data.Title, data.Body, ScreenForType(data.Type));
    }

    private void HandleNotificationTapped(NotificationData data)
    {
        string screen = ScreenForType(data.Type);
        if (!string.IsNullOrEmpty(screen))
            ScreenManager.Instance.GoTo(screen);
    }

    // 추론 엔진(Functions/fcm_sender.py, ontology_engine.py)이 실제 보내는 data.type과 정렬.
    //   room_updated   : 팔로워 방 갱신 (fcm_sender.py)
    //   quest_completed: 자동 완료된 퀘스트 알림 (fcm_sender.py)
    //   new_quest      : 신규 퀘스트 토픽 푸시. 엔진은 type 없이 questCount만 보내므로
    //                    FcmManager.OnMessageReceived에서 questCount 감지 시 이 값으로 세팅함.
    // 과거 오타 값(room_update)도 하위호환으로 함께 매핑.
    private static string ScreenForType(string type) => type switch
    {
        "follow_request"  => "feed",
        "room_updated"    => "myroom",
        "room_update"     => "myroom",  // legacy alias
        "quest_completed" => "quest",
        "new_quest"       => "quest",
        _                 => ""
    };

    public void Show(string title, string body, string screen, float duration = 3f)
    {
        toastTitle.text = title;
        toastBody.text = body;
        targetScreen = screen;
        toastContainer.style.display = DisplayStyle.Flex;
        AudioManager.Instance?.PlaySFX(5);

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
