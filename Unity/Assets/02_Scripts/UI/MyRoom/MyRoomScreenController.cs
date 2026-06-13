using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

/// <summary>
/// 마이룸 진입 제어 — ScreenManager "myroom" 키에 연결
/// OnEnable: 3DRoomScene 추가 로드 + MainScene 카메라 비활성화
/// OnDisable: 3DRoomScene 언로드 + MainScene 카메라 복원
/// </summary>
public class MyRoomScreenController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    [Tooltip("MainScene의 Main Camera — 3D 씬 활성 중 비활성화해 중복 렌더링 방지")]
    [SerializeField] private Camera mainSceneCamera;

    [Tooltip("MainScene의 Directional Light — 3D/Closet과 겹칠 때 조명 중복으로 색감이 뜨므로 비활성화")]
    [SerializeField] private Light mainSceneLight;

    private const string RoomScene = "3DRoomScene";

    private void OnEnable()
    {
        if (uiDocument != null)
        {
            var root = uiDocument.rootVisualElement;
            root.Q<Button>("btn-settings").clicked += OnSettingsClicked;
        }

        if (mainSceneCamera != null)
            mainSceneCamera.gameObject.SetActive(false);

        // Main Directional Light 끄기 — 3D 씬 라이트와 겹쳐 색감이 뜨는 문제 방지
        if (mainSceneLight != null)
            mainSceneLight.gameObject.SetActive(false);

        if (!SceneManager.GetSceneByName(RoomScene).isLoaded)
            SceneManager.LoadScene(RoomScene, LoadSceneMode.Additive);

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnUnreadQuestCountChanged += OnUnreadQuestChanged;
            QuestManager.Instance.StartQuestListener();
        }
    }

    private void OnDisable()
    {
        if (uiDocument != null)
            uiDocument.rootVisualElement.Q<Button>("btn-settings").clicked -= OnSettingsClicked;

        if (QuestManager.Instance != null)
            QuestManager.Instance.OnUnreadQuestCountChanged -= OnUnreadQuestChanged;

        Scene scene = SceneManager.GetSceneByName(RoomScene);
        if (scene.isLoaded)
            SceneManager.UnloadSceneAsync(scene);

        if (mainSceneCamera != null)
            mainSceneCamera.gameObject.SetActive(true);

        // Main Directional Light 복원
        if (mainSceneLight != null)
            mainSceneLight.gameObject.SetActive(true);
    }

    private void OnUnreadQuestChanged(int count)
    {
        // badge-quest 업데이트는 BottomNavController가 같은 이벤트를 구독해 처리
    }

    private void OnSettingsClicked()
    {
        ScreenManager.Instance.GoTo("settings");
    }
}
