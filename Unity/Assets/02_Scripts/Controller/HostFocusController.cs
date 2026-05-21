using UnityEngine;

public class HostFocusController : MonoBehaviour
{
    public static HostFocusController Instance { get; private set; }

    [Header("연결")]
    public Transform hostObject;

    [Header("UI")]
    public GameObject hostUI;

    public enum FocusState { Free, Host }
    public FocusState State { get; private set; } = FocusState.Free;

    void Awake()
    {
        Instance = this;

        if (hostObject != null && hostObject.GetComponent<HostInteraction>() == null)
            hostObject.gameObject.AddComponent<HostInteraction>();

        if (hostUI != null) hostUI.SetActive(false);
    }

    public void FocusHost()
    {
        if (State != FocusState.Free) return;

        State = FocusState.Host;
        if (hostUI != null) hostUI.SetActive(true);
        Debug.Log("[Host] UI 오픈");
    }

    public void BackToFree()
    {
        if (State == FocusState.Free) return;

        if (hostUI != null) hostUI.SetActive(false);
        State = FocusState.Free;
        Debug.Log("[Host] UI 닫기");
    }

}
