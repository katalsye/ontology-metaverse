using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

// 유니티 Inspector 설정:
// JoystickArea Image 오브젝트에 이 스크립트 붙이기
// JoystickArea: 조이스틱 터치 감지 영역 (이 스크립트가 붙은 오브젝트)
// JoystickHandle: 자식 오브젝트인 움직이는 동그라미 Image
// Player: 씬에 있는 Player 오브젝트 연결

public class JoystickController : MonoBehaviour
{
    [Header("조이스틱 UI 연결")]
    public RectTransform joystickArea;      // 터치 감지 영역
    public RectTransform joystickHandle;    // 움직이는 핸들

    [Header("플레이어 연결")]
    public PlayerMovementController player;

    [Header("설정")]
    public float handleRadius = 50f;    // 핸들이 움직일 수 있는 최대 반경
    public float inputThreshold = 0.2f; // 이 값 이상이어야 이동 명령 전달

    // 현재 조이스틱을 조작 중인 손가락 ID (-1이면 미사용)
    private int _activeFingerId = -1;
    // 에디터 마우스 드래그 중인지
    private bool _mouseActive = false;

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
        joystickHandle.anchoredPosition = Vector2.zero;
    }

    void Update()
    {
        // 터치 입력 (모바일)
        if (Touch.activeTouches.Count > 0)
        {
            foreach (Touch touch in Touch.activeTouches)
            {
                if (touch.phase == TouchPhase.Began)
                {
                    // 이미 다른 손가락이 조이스틱 조작 중이면 무시
                    if (_activeFingerId != -1) continue;
                    // 조이스틱 영역 안에서 시작된 터치만 등록
                    if (!IsOnJoystickArea(touch.screenPosition)) continue;

                    _activeFingerId = touch.finger.index;
                    UpdateHandle(touch.screenPosition);
                }
                else if ((touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary) && touch.finger.index == _activeFingerId)
                {
                    UpdateHandle(touch.screenPosition);
                }
                else if ((touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                         && touch.finger.index == _activeFingerId)
                {
                    ResetJoystick();
                }
            }
        }
        // 마우스 입력 (에디터 테스트용)
        else if (Mouse.current != null)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();

            if (Mouse.current.leftButton.wasPressedThisFrame && IsOnJoystickArea(mousePos))
            {
                _mouseActive = true;
                UpdateHandle(mousePos);
            }
            else if (Mouse.current.leftButton.isPressed && _mouseActive)
            {
                UpdateHandle(mousePos);
            }
            else if (Mouse.current.leftButton.wasReleasedThisFrame && _mouseActive)
            {
                ResetJoystick();
            }
        }
    }

    // 스크린 좌표를 joystickArea 로컬 좌표로 변환해 핸들 위치 및 플레이어 입력 업데이트
    void UpdateHandle(Vector2 screenPos)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            joystickArea, screenPos, null, out Vector2 localPos);

        // pivot이 중앙이 아닐 경우 rect 중심 기준으로 보정
        // 예: pivot(0,0)이면 localPos가 항상 양수 → 우측위로만 이동하는 버그 발생
        localPos -= joystickArea.rect.center;

        Vector2 clamped = Vector2.ClampMagnitude(localPos, handleRadius);
        joystickHandle.anchoredPosition = clamped;

        Vector2 input = clamped / handleRadius;
        player.SetJoystickInput(input.magnitude >= inputThreshold ? input : Vector2.zero);
    }

    // 조이스틱 초기화
    void ResetJoystick()
    {
        _activeFingerId = -1;
        _mouseActive = false;
        joystickHandle.anchoredPosition = Vector2.zero;
        player.SetJoystickInput(Vector2.zero);
    }

    // 터치가 joystickArea 안에 있는지 확인
    bool IsOnJoystickArea(Vector2 screenPos)
    {
        // 조이스틱이 숨겨져 있으면(줌인 상태) 입력 처리 안 함
        if (joystickArea == null || !joystickArea.gameObject.activeInHierarchy) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(joystickArea, screenPos);
    }
}
