using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

// 유니티 Inspector 설정:
// 카메라 오브젝트에 이 스크립트 붙이기
// Player: 씬에 있는 Player 오브젝트 연결
// JoystickArea: 조이스틱 터치 감지 영역 연결 (드래그 영역 제외용)

public class CameraController : MonoBehaviour
{
    [Header("플레이어 연결")]
    public Transform player;
    public Transform cameraPivot; // 카메라가 orbit할 기준점 (Player 자식 오브젝트)

    [Header("카메라 설정")]
    public float distance = 5f;         // 플레이어로부터 카메라까지 거리 (방 크기에 맞게 조절)
    public float followSpeed = 8f;      // 플레이어 따라가는 속도

    [Header("카메라 회전")]
    public float rotateSpeed = 0.1f;    // 드래그 회전 민감도
    public float startPitch = 30f;      // 시작 상하 각도 (Inspector에서 조절)
    public float minPitch = 10f;        // 상하 최소 각도
    public float maxPitch = 80f;        // 상하 최대 각도

    [Header("조이스틱 영역 (드래그 제외)")]
    public RectTransform joystickArea;

    private float _yaw = 0f;
    private float _pitch;

    private int _dragFingerId = -1;
    private Vector2 _lastDragPos;
    private bool _mouseDragActive = false;

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    void Start()
    {
        _pitch = startPitch; // Inspector에서 설정한 초기 pitch 적용

        if (player == null) return;

        // 시작 시 즉시 올바른 위치/방향으로 스냅 (Lerp 시작 튐 방지)
        ApplyPositionAndRotation(snap: true);
    }

    void LateUpdate()
    {
        HandleRotationInput();
        ApplyPositionAndRotation(snap: false);
    }

    // 카메라 위치와 회전을 yaw/pitch 기준으로 한번에 결정
    // LookAt을 쓰지 않음 → 위치와 회전이 항상 일치해서 텀블링 없음
    void ApplyPositionAndRotation(bool snap)
    {
        if (player == null) return;

        // pivot이 있으면 pivot 기준, 없으면 player 위치 기준
        Vector3 pivotPos = cameraPivot != null ? cameraPivot.position : player.position;

        // yaw: 플레이어 기준 좌우 회전, pitch: 상하 회전
        Quaternion yawRot   = Quaternion.Euler(0f, _yaw, 0f);
        Quaternion pitchRot = Quaternion.Euler(_pitch, 0f, 0f);
        Quaternion camRot   = yawRot * pitchRot;

        // 카메라 위치 = pivot 중심에서 orbit 방향으로 distance만큼 떨어진 곳
        Vector3 desiredPos = pivotPos + camRot * new Vector3(0f, 0f, -distance);

        // 카메라 회전 = pivot을 정확히 바라보는 방향 (roll 없음)
        // camRot을 그대로 쓰면 카메라가 엉뚱한 방향을 보기 때문에 별도 계산
        Quaternion desiredRot = Quaternion.LookRotation(pivotPos - desiredPos, Vector3.up);

        if (snap)
        {
            transform.position = desiredPos;
            transform.rotation = desiredRot;
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, desiredPos, followSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, followSpeed * Time.deltaTime);
        }
    }

    // 드래그 입력으로 yaw/pitch 값을 변경
    void HandleRotationInput()
    {
        // 터치 입력 (모바일)
        if (Touch.activeTouches.Count > 0)
        {
            foreach (Touch touch in Touch.activeTouches)
            {
                if (touch.phase == TouchPhase.Began)
                {
                    if (IsTouchOnJoystick(touch.screenPosition)) continue;
                    if (EventSystem.current.IsPointerOverGameObject(touch.finger.index)) continue;

                    _dragFingerId = touch.finger.index;
                    _lastDragPos = touch.screenPosition;
                }
                else if (touch.phase == TouchPhase.Moved && touch.finger.index == _dragFingerId)
                {
                    Vector2 delta = touch.screenPosition - _lastDragPos;
                    ApplyRotation(delta);
                    _lastDragPos = touch.screenPosition;
                }
                else if ((touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                         && touch.finger.index == _dragFingerId)
                {
                    _dragFingerId = -1;
                }
            }
        }
        // 마우스 입력 (에디터 테스트용)
        else if (Mouse.current != null)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (!IsTouchOnJoystick(mousePos))
                {
                    _lastDragPos = mousePos;
                    _mouseDragActive = true;
                }
            }
            else if (Mouse.current.leftButton.isPressed && _mouseDragActive)
            {
                Vector2 delta = mousePos - _lastDragPos;
                ApplyRotation(delta);
                _lastDragPos = mousePos;
            }
            else if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                _mouseDragActive = false;
            }
        }
    }

    void ApplyRotation(Vector2 delta)
    {
        // 1픽셀 미만 이동은 노이즈로 간주하고 무시
        if (delta.magnitude < 1f) return;

        _yaw   -= delta.x * rotateSpeed;
        _pitch -= delta.y * rotateSpeed;
        _pitch  = Mathf.Clamp(_pitch, minPitch, maxPitch);
    }

    bool IsTouchOnJoystick(Vector2 screenPos)
    {
        if (joystickArea == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(joystickArea, screenPos);
    }
}
