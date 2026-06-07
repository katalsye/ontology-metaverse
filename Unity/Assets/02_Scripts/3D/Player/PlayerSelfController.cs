using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Firebase.Extensions;

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

        LoadStatusText();
        LoadBalloonTexts();
    }

    public void Close()
    {
        if (!IsOpen) return;

        SaveStatusText();

        if (selfUI != null) selfUI.SetActive(false);
        if (joystickObject != null) joystickObject.SetActive(true);
        IsOpen = false;

        _camPosTarget = _camPosBefore;
        _camRotTarget = _camRotBefore;
        _returning    = true;
        _moving       = false;

        CameraController.Instance?.RestoreState();
    }

    void LoadStatusText()
    {
        var auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        if (auth?.CurrentUser == null) return;
        string uid = auth.CurrentUser.UserId;

        Firebase.Firestore.FirebaseFirestore.DefaultInstance
            .Collection("users").Document(uid)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || !task.Result.Exists) return;
                if (task.Result.ContainsField("StatusMessage") && inputField != null)
                    inputField.text = task.Result.GetValue<string>("StatusMessage") ?? "";
            });
    }

    void SaveStatusText()
    {
        var auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        if (auth?.CurrentUser == null || inputField == null) return;
        string uid  = auth.CurrentUser.UserId;
        string text = inputField.text?.Trim() ?? "";

        Firebase.Firestore.FirebaseFirestore.DefaultInstance
            .Collection("users").Document(uid)
            .UpdateAsync("StatusMessage", text)
            .ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted) Debug.LogError("[PlayerSelfController] 상태 저장 실패: " + t.Exception);
            });
    }

    void LoadBalloonTexts()
    {
        var auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        if (auth?.CurrentUser == null)
        {
            if (balloonText1 != null) balloonText1.text = "...";
            if (balloonText2 != null) balloonText2.text = "...";
            return;
        }
        string uid = auth.CurrentUser.UserId;

        Firebase.Firestore.FirebaseFirestore.DefaultInstance
            .Collection("users").Document(uid)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                string w1 = "...", w2 = "...";
                if (!task.IsFaulted && task.Result.Exists)
                {
                    if (task.Result.ContainsField("todayWord1"))
                        w1 = task.Result.GetValue<string>("todayWord1") ?? "...";
                    if (task.Result.ContainsField("todayWord2"))
                        w2 = task.Result.GetValue<string>("todayWord2") ?? "...";
                }
                if (balloonText1 != null) balloonText1.text = w1;
                if (balloonText2 != null) balloonText2.text = w2;
            });
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
