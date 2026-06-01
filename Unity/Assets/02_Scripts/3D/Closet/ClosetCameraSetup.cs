using UnityEngine;

[RequireComponent(typeof(Camera))]
public class ClosetCameraSetup : MonoBehaviour
{
    [Range(0f, 1f)]
    [Tooltip("뷰포트 높이 비율")]
    public float height = 1f;

    [Range(0f, 1f)]
    [Tooltip("뷰포트 하단 시작점 (올릴수록 위로)")]
    public float bottomY = 0f;

    void Awake() => Apply();

    void OnValidate() => Apply();

    void Apply()
    {
        var cam = GetComponent<Camera>();
        if (cam != null)
            cam.rect = new Rect(0f, bottomY, 1f, height);
    }
}
