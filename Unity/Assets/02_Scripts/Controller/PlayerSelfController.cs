using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerSelfController : MonoBehaviour
{
    public static PlayerSelfController Instance { get; private set; }

    [Header("연결")]
    public Transform playerObject;
    public GameObject joystickObject;

    [Header("카메라 설정")]
    public float viewDistance = 2f;
    public float moveSpeed    = 5f;

    [Header("UI")]
    public GameObject selfUI;
    public TMP_InputField inputField;
    [Tooltip("말풍선 첫 번째 텍스트")]
    public TMP_Text balloonText1;
    [Tooltip("말풍선 두 번째 텍스트")]
    public TMP_Text balloonText2;
    [Tooltip("selfUI 뒤에 깔린 전체화면 투명 버튼 — 누르면 닫힘")]
    public Button backdropButton;

    public bool IsOpen { get; private set; } = false;

    private Camera     _cam;
    private Vector3    _camPosTarget;
    private Quaternion _camRotTarget;
    private Vector3    _camPosBefore;
    private Quaternion _camRotBefore;
    private bool       _moving = false;
    private bool       _returning = false;

    void Awake()
    {
        Instance = this;
        _cam = Camera.main;
        if (selfUI != null) selfUI.SetActive(false);
        if (backdropButton != null) backdropButton.onClick.AddListener(Close);
        if (balloonText1 != null) balloonText1.text = "...";
        if (balloonText2 != null) balloonText2.text = "...";

        if (joystickObject == null)
        {
            var jc = FindObjectOfType<JoystickController>();
            if (jc != null && jc.joystickArea != null)
                joystickObject = jc.joystickArea.gameObject;
        }
    }

    void Update()
    {
        if (!_moving && !_returning) return;

        _cam.transform.position = Vector3.Lerp(_cam.transform.position, _camPosTarget, moveSpeed * Time.deltaTime);
        _cam.transform.rotation = Quaternion.Slerp(_cam.transform.rotation, _camRotTarget, moveSpeed * Time.deltaTime);

        if (_returning && Vector3.Distance(_cam.transform.position, _camPosTarget) < 0.05f)
        {
            _cam.transform.position = _camPosTarget;
            _cam.transform.rotation = _camRotTarget;
            _returning = false;
            _moving    = false;
        }
    }

    public void Open()
    {
        if (IsOpen) return;

        _camPosBefore = _cam.transform.position;
        _camRotBefore = _cam.transform.rotation;

        CalcViewPoint(out _camPosTarget, out _camRotTarget);
        _moving    = true;
        _returning = false;
        IsOpen     = true;

        if (joystickObject != null) joystickObject.SetActive(false);
        if (selfUI != null) selfUI.SetActive(true);

        // TODO: DB에서 기존 inputField 텍스트 불러오기
        // 예: FirebaseManager.Instance.GetSelfText(userId, text => inputField.text = text);
        Debug.Log("[PlayerSelf] 줌인 + UI 오픈 — TODO: DB에서 텍스트 불러오기");

        LoadBalloonTexts();
    }

    public void Close()
    {
        if (!IsOpen) return;

        // TODO: 현재 inputField.text를 DB에 저장
        // 예: FirebaseManager.Instance.SaveSelfText(userId, inputField.text);
        Debug.Log($"[PlayerSelf] UI 닫힘 — TODO: DB에 저장 | text={inputField?.text}");

        if (selfUI != null) selfUI.SetActive(false);
        if (joystickObject != null) joystickObject.SetActive(true);
        IsOpen = false;

        _camPosTarget = _camPosBefore;
        _camRotTarget = _camRotBefore;
        _returning    = true;
        _moving       = false;

        CameraController.Instance?.RestoreState();
    }

    void LoadBalloonTexts()
    {
        // TODO: DB에서 오늘의 한마디 불러와서 balloonText1.text에 넣기
        // 예: FirebaseManager.Instance.GetTodayWord(userId, text => balloonText1.text = string.IsNullOrEmpty(text) ? "..." : text);
        if (balloonText1 != null) balloonText1.text = "...";

        // TODO: DB에서 오늘의 한마디 두 번째 줄 불러와서 balloonText2.text에 넣기
        // 예: FirebaseManager.Instance.GetTodayWord2(userId, text => balloonText2.text = string.IsNullOrEmpty(text) ? "..." : text);
        if (balloonText2 != null) balloonText2.text = "...";

        Debug.Log("[PlayerSelf] 말풍선 — TODO: DB에서 오늘의 한마디 불러오기");
    }

    void CalcViewPoint(out Vector3 pos, out Quaternion rot)
    {
        if (playerObject == null) { pos = _cam.transform.position; rot = _cam.transform.rotation; return; }

        Vector3 center = playerObject.position;
        var ren = playerObject.GetComponentInChildren<Renderer>();
        if (ren != null) center = ren.bounds.center;

        Vector3 dir = playerObject.forward;
        dir.y = 0f;
        dir.Normalize();

        pos = center + dir * viewDistance + Vector3.up * 0.5f;
        rot = Quaternion.LookRotation(center - pos, Vector3.up);
    }
}
