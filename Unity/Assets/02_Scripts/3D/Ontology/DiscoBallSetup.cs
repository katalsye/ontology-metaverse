using UnityEngine;

/// <summary>
/// 디스코볼 초기화 + 자동 회전
/// 1) Y축 연속 회전 (Animator 불필요)
/// 2) 자식 MeshRenderer를 Rendering Layer 2에 등록 → Point Light 격리
/// </summary>
public class DiscoBallSetup : MonoBehaviour
{
    [Tooltip("초당 회전 각도 (도/초)")]
    public float spinSpeed = 45f;

    [Tooltip("렌더링 레이어 마스크 (1=Default, 2=DiscoBall, 3=둘다 받음)")]
    public uint discoBallRenderingLayerMask = 3u;

    void Start()
    {
        ApplyRenderingLayers();
    }

    void Update()
    {
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
    }

    void ApplyRenderingLayers()
    {
        var renderers = GetComponentsInChildren<MeshRenderer>(true);
        foreach (var mr in renderers)
            mr.renderingLayerMask = discoBallRenderingLayerMask;
    }
}
