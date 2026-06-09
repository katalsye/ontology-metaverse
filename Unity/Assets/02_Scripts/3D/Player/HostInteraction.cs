using UnityEngine;

// host 오브젝트에 붙이기 — CameraController가 클릭 감지용으로 사용
public class HostInteraction : MonoBehaviour
{
    void Awake()
    {
        var col = GetComponent<Collider>();
        if (col == null) gameObject.AddComponent<CapsuleCollider>();
    }
}
