using UnityEngine;

// SM_Room_DeskCalendar_A_b 오브젝트에 이 스크립트를 붙이기
// CameraController가 이 컴포넌트 존재 여부로 캘린더 클릭을 감지함
public class CalendarInteraction : MonoBehaviour
{
    void Awake()
    {
        var col = GetComponent<Collider>();
        if (col == null)
        {
            var box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = false;
        }
    }
}
