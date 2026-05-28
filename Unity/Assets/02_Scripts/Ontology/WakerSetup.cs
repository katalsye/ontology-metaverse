using UnityEngine;

/// <summary>
/// waker(알람시계) 내부 라이트를 전용 렌더링 레이어로 격리.
/// 라이트는 레이어 4(mask=8)만 비추고,
/// 내부 MeshRenderer는 레이어 1+4(mask=9)를 가져서
/// 외부 오브젝트에는 영향을 주지 않습니다.
/// </summary>
public class WakerSetup : MonoBehaviour
{
    [Tooltip("waker 전용 렌더링 레이어 (bit index, 0부터 시작). 기본값 4 = mask 16")]
    public int wakerLightLayerIndex = 4;

    void Start()
    {
        uint lightMask = 1u << wakerLightLayerIndex;       // 라이트 전용 레이어만
        uint meshMask  = 1u | lightMask;                   // 기본(1) + 전용 레이어

        // 자식 라이트 → 전용 레이어만 비추도록
        foreach (var light in GetComponentsInChildren<Light>(true))
        {
            light.renderingLayerMask = (int)lightMask;
        }

        // 자식 MeshRenderer → 기본 레이어 + 전용 레이어 둘 다 수신
        foreach (var mr in GetComponentsInChildren<MeshRenderer>(true))
        {
            mr.renderingLayerMask = meshMask;
        }
    }
}
