using UnityEngine;

/// <summary>
/// 3DRoomScene 문 오브젝트에 부착
/// 문 탭 시 → ScreenManager "feed"로 이동 (MyRoomScreenController.OnDisable → 3DRoomScene 언로드)
/// </summary>
public class RoomExitController : MonoBehaviour
{
    public void OnDoorTapped()
    {
        if (ScreenManager.Instance != null)
            ScreenManager.Instance.GoTo("feed");
    }
}
