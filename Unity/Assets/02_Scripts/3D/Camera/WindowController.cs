using UnityEngine;

public class WindowController : MonoBehaviour
{
    public GameObject panel; // 띄울 UI (Panel 등)

    // 창 띄우기
    public void Open()
    {
        panel.SetActive(true);
    }

    // 창 닫기
    public void Close()
    {
        panel.SetActive(false);
    }
}