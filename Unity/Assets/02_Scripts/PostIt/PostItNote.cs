using UnityEngine;
using TMPro;

// postit 프리팹(또는 디폴트 Cube) 루트에 PostItManager가 자동으로 붙임.
//   - Awake: Collider 없으면 본체 메시 bounds에 맞춘 BoxCollider를 루트에 추가
//   - Setup: 본체 머티리얼 + 텍스트(작성자/댓글) 세팅
//   - 텍스트는 프리팹에 이미 정확한 위치로 배치된 3D TextMeshPro를 그대로 사용 (위치 안 건드림)
public class PostItNote : MonoBehaviour
{
    public string Username { get; private set; }
    public string Comment  { get; private set; }

    private MeshRenderer _body;

    void Awake()
    {
        // 콜라이더를 '루트'에 붙여야 raycast 시 루트 태그(PostIt)가 잡힘
        PostItCanvasHelper.EnsureFittedCollider(gameObject);
    }

    // 포스트잇의 실제 시각적 중심 — model.dae pivot이 어긋나 있어 transform.position과 다름.
    // 줌인 카메라 타겟 계산에 사용.
    public Vector3 GetVisualCenter()
    {
        if (_body == null) _body = PostItCanvasHelper.FindBodyRenderer(gameObject);
        return _body != null ? _body.bounds.center : transform.position;
    }

    // PostItManager에서 생성 직후 호출
    public void Setup(string username, string comment, Material mat, PostItManager manager)
    {
        Username = username;
        Comment  = comment;

        // 본체 메시 머티리얼 적용
        _body = PostItCanvasHelper.FindBodyRenderer(gameObject);
        if (_body != null) _body.material = mat;

        // 텍스트 — 프리팹의 TMP에 내용만 채움 (위치·폰트·정렬 전부 프리팹 그대로)
        var tmp = GetComponentInChildren<TextMeshPro>(true);
        if (tmp != null) tmp.text = $"{username}\n{comment}";
    }

    // 클릭 감지는 CameraController.CheckBoardClick에서 통합 처리
}
