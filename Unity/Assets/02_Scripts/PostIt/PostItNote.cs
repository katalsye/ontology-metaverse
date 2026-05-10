using UnityEngine;

// postit 프리팹 루트에 이 스크립트 붙이기
// 유니티 Inspector 설정:
//   1. postit 프리팹 루트 태그를 "PostIt" 으로 설정
//   2. Collider 없으면 자동으로 BoxCollider 추가됨
//   (머티리얼·데이터는 PostItManager가 코드로 세팅)
public class PostItNote : MonoBehaviour
{
    public string Username { get; private set; }
    public string Comment  { get; private set; }

    void Awake()
    {
        if (GetComponentInChildren<Collider>() == null)
            gameObject.AddComponent<BoxCollider>();
    }

    // PostItManager에서 생성 직후 호출
    public void Setup(string username, string comment, Material mat, PostItManager manager)
    {
        Username = username;
        Comment  = comment;

        var ren = GetComponentInChildren<MeshRenderer>();
        if (ren != null)
            ren.material = mat;
    }

    void OnMouseDown()
    {
        // 보드가 포커스 상태일 때만 포스트잇 클릭 동작
        if (BoardFocusController.Instance?.State == BoardFocusController.FocusState.Board)
            BoardFocusController.Instance.FocusPostIt(this);
    }
}
