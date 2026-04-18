using UnityEngine;

// 유니티 Inspector 설정:
// Player 오브젝트에 이 스크립트 붙이기
// Rigidbody - Is Kinematic: 반드시 해제 (체크 해제)
// Rigidbody - Freeze Rotation X, Y, Z: 체크
// Rigidbody - Freeze Position Y: 체크 (중력으로 떨어지지 않게)
// Rigidbody - Collision Detection: Continuous

public class PlayerMovementController : MonoBehaviour
{
    [Header("그리드 설정")]
    public float tileSize = 10f;
    public float moveSpeed = 100f;   // tileSize/moveSpeed = 이동 시간 (10/40 = 0.25초)

    private Rigidbody _rb;
    private Vector3 _startPos;
    private Vector3 _moveDir;
    private bool _isMoving = false;
    private Vector2 _lastInput;
    private Vector3 _blockedDir = Vector3.zero; // 벽에 막힌 방향 (같은 방향으로 재시도 방지)

    public bool IsMoving => _isMoving;
    public Vector3 TargetPosition => _startPos + _moveDir * tileSize;

    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        _startPos = transform.position;
    }

    void FixedUpdate()
    {
        if (!_isMoving) return;

        float moved = Vector3.Dot(_rb.position - _startPos, _moveDir);

        if (moved >= tileSize)
        {
            // 목표 위치에 정확히 스냅
            _rb.position = _startPos + _moveDir * tileSize;
            _rb.linearVelocity = Vector3.zero;
            _isMoving = false;

            // 조이스틱 계속 잡고 있으면 다음 칸 이동
            if (_lastInput.magnitude >= 0.01f)
                SetJoystickInput(_lastInput);
        }
    }

    // 벽/가구에 부딪히면 이동 취소
    void OnCollisionEnter(Collision collision)
    {
        if (!_isMoving) return;

        foreach (ContactPoint contact in collision.contacts)
        {
            // 이동 방향 반대쪽에서 충돌 = 벽 (바닥/천장은 무시)
            if (Vector3.Dot(contact.normal, _moveDir) < -0.5f)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.position = _startPos; // 출발 위치로 복귀
                _blockedDir = _moveDir;  // 막힌 방향 기록
                _isMoving = false;
                return;
            }
        }
    }

    public void SetJoystickInput(Vector2 input)
    {
        _lastInput = input;

        if (_isMoving) return;

        // 조이스틱을 놓으면 막힘 방향 초기화 → 이후 이동 가능
        if (input.magnitude < 0.01f)
        {
            _blockedDir = Vector3.zero;
            return;
        }

        Vector3 dir;
        if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
            dir = input.x > 0 ? Vector3.right : Vector3.left;
        else
            dir = input.y > 0 ? Vector3.forward : Vector3.back;

        // 막힌 방향으로는 재시도 안 함 (벽에 박히는 현상 방지)
        if (dir == _blockedDir) return;

        // 다른 방향으로 바꿨으면 막힘 해제
        _blockedDir = Vector3.zero;

        _startPos = _rb.position;
        _moveDir = dir;
        _isMoving = true;
        _rb.linearVelocity = dir * moveSpeed;

        transform.rotation = Quaternion.LookRotation(dir);
    }
}
