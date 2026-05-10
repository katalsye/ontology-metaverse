using UnityEngine;

// board 오브젝트에 이 스크립트 붙이기
// 유니티 Inspector 설정:
//   1. 이 스크립트를 board 오브젝트에 추가
//   2. board 오브젝트 태그를 "Board" 로 설정 (Inspector 상단 Tag 드롭다운)
//   3. Collider 없으면 자동으로 MeshCollider 추가됨
public class BoardInteraction : MonoBehaviour
{
    void Awake()
    {
        // Collider 없으면 자동 추가
        if (GetComponent<Collider>() == null)
            gameObject.AddComponent<MeshCollider>();
    }

    void OnMouseDown()
    {
        BoardFocusController.Instance?.FocusBoard();
    }
}
