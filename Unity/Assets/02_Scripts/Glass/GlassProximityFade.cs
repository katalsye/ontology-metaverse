using UnityEngine;

// 유니티 Inspector 설정:
// 유리 오브젝트에 이 스크립트 붙이기
// GlassSG 셰이더를 쓰는 Material이 있어야 함 (_Alpha 프로퍼티 필요)

public class GlassProximityFade : MonoBehaviour
{
    [Header("페이드 거리 설정")]
    [Tooltip("이 거리 이상이면 완전히 불투명 (기본 Alpha 사용)")]
    public float fadeStartDistance = 8f;

    [Tooltip("이 거리 이하이면 완전히 투명")]
    public float fadeEndDistance = 2f;

    [Header("Alpha 범위")]
    [Tooltip("멀 때 Alpha 값 (GlassMat 기본값 0.2)")]
    public float maxAlpha = 0.2f;

    [Tooltip("가까울 때 Alpha 값 (0 = 완전 투명)")]
    public float minAlpha = 0f;

    private Material _mat;
    private Transform _cam;

    void Start()
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
            _mat = rend.material; // 인스턴스 복사본 사용 (원본 건드리지 않음)

        if (Camera.main != null)
            _cam = Camera.main.transform;
    }

    void Update()
    {
        if (_mat == null || _cam == null) return;

        float dist = Vector3.Distance(_cam.position, transform.position);
        float t = Mathf.InverseLerp(fadeEndDistance, fadeStartDistance, dist);
        float alpha = Mathf.Lerp(minAlpha, maxAlpha, t);

        _mat.SetFloat("_Alpha", alpha);
    }
}
