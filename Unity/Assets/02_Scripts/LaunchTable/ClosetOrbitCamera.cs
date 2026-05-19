using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

public class ClosetOrbitCamera : MonoBehaviour
{
    public static ClosetOrbitCamera Instance { get; private set; }

    public Transform target;
    public float distance    = 2f;
    public float rotateSpeed = 0.25f;
    public float pitch       = 15f;
    public float minPitch    = -20f;
    public float maxPitch    = 75f;

    private float   _yaw;
    private int     _dragFingerId = -1;
    private Vector2 _lastDragPos;
    private bool    _mouseDragActive;

    void Awake() => Instance = this;
    void OnEnable()  => EnhancedTouchSupport.Enable();
    void OnDisable() => EnhancedTouchSupport.Disable();

    void Start()
    {
        _yaw = transform.eulerAngles.y;
        if (target != null) ApplyOrbit();
    }

    void LateUpdate()
    {
        HandleInput();
        if (target != null) ApplyOrbit();
    }

    void HandleInput()
    {
        if (Touch.activeTouches.Count > 0)
        {
            foreach (var touch in Touch.activeTouches)
            {
                if (touch.phase == TouchPhase.Began)
                {
                    if (IsOverUI(touch.screenPosition)) continue;
                    _dragFingerId = touch.finger.index;
                    _lastDragPos  = touch.screenPosition;
                }
                else if (touch.phase == TouchPhase.Moved && touch.finger.index == _dragFingerId)
                {
                    ApplyDelta(touch.screenPosition - _lastDragPos);
                    _lastDragPos = touch.screenPosition;
                }
                else if ((touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                         && touch.finger.index == _dragFingerId)
                {
                    _dragFingerId = -1;
                }
            }
        }
        else if (Mouse.current != null)
        {
            var pos = Mouse.current.position.ReadValue();
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (IsOverUI(pos)) return;
                _lastDragPos     = pos;
                _mouseDragActive = true;
            }
            else if (Mouse.current.leftButton.isPressed && _mouseDragActive)
            {
                ApplyDelta(pos - _lastDragPos);
                _lastDragPos = pos;
            }
            else if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                _mouseDragActive = false;
            }
        }
    }

    void ApplyDelta(Vector2 delta)
    {
        _yaw  += delta.x * rotateSpeed;
        pitch  = Mathf.Clamp(pitch - delta.y * rotateSpeed, minPitch, maxPitch);
    }

    void ApplyOrbit()
    {
        Vector3 center = GetTargetCenter();
        var rot = Quaternion.Euler(pitch, _yaw, 0f);
        transform.position = center + rot * new Vector3(0f, 0f, -distance);
        transform.LookAt(center);
    }

    Vector3 GetTargetCenter()
    {
        var ren = target.GetComponentInChildren<Renderer>();
        return ren != null ? ren.bounds.center : target.position;
    }

    static readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();

    static bool IsOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;
        var ped = new PointerEventData(EventSystem.current) { position = screenPos };
        _raycastResults.Clear();
        EventSystem.current.RaycastAll(ped, _raycastResults);
        return _raycastResults.Count > 0;
    }
}
