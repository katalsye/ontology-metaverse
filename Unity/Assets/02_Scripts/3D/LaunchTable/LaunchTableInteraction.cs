using UnityEngine;

// LaunchTable 오브젝트에 이 스크립트를 붙이기
// CameraController가 이 컴포넌트 존재 여부로 LaunchTable 클릭을 감지함
public class LaunchTableInteraction : MonoBehaviour
{
    void Awake()
    {
        var col = GetComponent<BoxCollider>();
        if (col == null) col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = false;
    }
}
