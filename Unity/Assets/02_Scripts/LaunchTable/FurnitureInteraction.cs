using UnityEngine;

public class FurnitureInteraction : MonoBehaviour
{
    public string sceneName;
    public bool allowInVisitRoom;  // VisitRoom에서 줌인 허용 여부
    public bool zoomOnlyNoScene;   // 줌인만, 씬 전환 없음

    [Tooltip("줌인 후 설명 패널에 표시할 메시지.\n" +
             "Inspector에서 직접 입력하거나, 백엔드 로드 완료 후 SetMessage()로 주입.")]
    public string message;

    /// <summary>백엔드에서 메시지를 받아온 뒤 외부에서 주입할 때 사용.</summary>
    public void SetMessage(string msg) { message = msg; }
}
