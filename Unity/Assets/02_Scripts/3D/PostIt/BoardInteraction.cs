using UnityEngine;

// board 오브젝트에 이 스크립트 붙이기
public class BoardInteraction : MonoBehaviour
{
    void Awake()
    {
        var col = GetComponent<BoxCollider>();
        if (col == null) col = gameObject.AddComponent<BoxCollider>();

        // 벽 앞 공중에 얇은 감지 평면 배치
        // board world z=1, wall world z≈3 → center.z=5이면 world z=6 (벽보다 카메라 쪽으로 3m 앞)
        // FBX 메시 실제 로컬 범위(cm→m 변환 기준): X ±3.72, Y 0~2.45
        // board 피벗이 하단 중앙이므로 center.y를 메시 중심(1.22)으로 보정
        // scale (6,10,1) 적용 전 로컬 기준으로 설정
        col.center = new Vector3(0f, 1.22f, 5f);
        col.size   = new Vector3(8f, 2.8f, 0.05f);
    }
}
