using UnityEngine;

// Sticky_note_yellow에 런타임으로 붙이는 마커.
// 1) CameraController.CheckBoardClick의 raycast 감지용 (컴포넌트 체크)
// 2) OnMouseUpAsButton으로 직접 감지 (Input System이 Both 모드일 때 작동)
public class CommentButtonMarker : MonoBehaviour
{
    void OnMouseUpAsButton()
    {
        var bfc = BoardFocusController.Instance;
        if (bfc == null || bfc.State != BoardFocusController.FocusState.Board) return;
        Debug.Log("[CommentButtonMarker] OnMouseUpAsButton → TogglePanel");
        CommentInputUI.Instance?.TogglePanel();
    }
}
