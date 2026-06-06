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

    private const string RoomScene = "3DRoomScene";

    private void OnEnable()
    {
        if (mainSceneCamera != null)
            mainSceneCamera.gameObject.SetActive(false);

        if (!SceneManager.GetSceneByName(RoomScene).isLoaded)
            SceneManager.LoadScene(RoomScene, LoadSceneMode.Additive);

        if (QuestManager.Instance != null)
            QuestManager.Instance.OnUnreadQuestCountChanged += OnUnreadQuestChanged;
    }

    private void OnDisable()
    {
        if (QuestManager.Instance != null)
            QuestManager.Instance.OnUnreadQuestCountChanged -= OnUnreadQuestChanged;

        Scene scene = SceneManager.GetSceneByName(RoomScene);
        if (scene.isLoaded)
            SceneManager.UnloadSceneAsync(scene);

        if (mainSceneCamera != null)
            mainSceneCamera.gameObject.SetActive(true);
    }

    private void OnUnreadQuestChanged(int count)
    {
        // 하단 BottomNav 뱃지는 BottomNavController 에서 처리
        // 여기서 필요한 추가 처리 있으면 구현
    }
}
