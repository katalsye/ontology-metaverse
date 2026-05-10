using UnityEngine;

// 유니티 Inspector 설정:
// Player 오브젝트에 이 스크립트 붙이기
// Rigidbody - Is Kinematic: 반드시 해제 (체크 해제)
// Rigidbody - Freeze Rotation X, Y, Z: 체크
// Rigidbody - Freeze Position Y: 체크 (중력으로 떨어지지 않게)
// Rigidbody - Collision Detection: Continuous

public class PlayerMovementController : MonoBehaviour
{
    [Header("이동 설정")]
    public float moveSpeed = 8f;        // 초당 이동 속도
    public float rotateSpeed = 15f;     // 방향 전환 부드러움 (높을수록 즉각 반응)

    [Header("애니메이션")]
    public Animator animator;

    [Header("카메라 기준 이동")]
    public Transform cameraTransform;

    private Rigidbody _rb;
    private Vector3 _moveDir = Vector3.zero;

    public bool IsMoving => _moveDir.magnitude > 0.01f;

    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    void FixedUpdate()
    {
        // 속도 직접 적용 (Y축 유지)
        _rb.linearVelocity = new Vector3(
            _moveDir.x * moveSpeed,
            _rb.linearVelocity.y,
            _moveDir.z * moveSpeed
        );

        // 이동 방향으로 부드럽게 회전
        if (_moveDir.magnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(_moveDir);
            _rb.rotation = Quaternion.Slerp(_rb.rotation, targetRot, rotateSpeed * Time.fixedDeltaTime);
        }
    }

    public void SetJoystickInput(Vector2 input)
    {
        if (input.magnitude < 0.01f)
        {
            _moveDir = Vector3.zero;
            animator?.SetBool("isWalking", false);
            return;
        }

        // 상하좌우 4방향 스냅
        if (Mathf.Abs(input.x) >= Mathf.Abs(input.y))
            input = new Vector2(Mathf.Sign(input.x), 0f);
        else
            input = new Vector2(0f, Mathf.Sign(input.y));

        // 카메라 기준 방향 계산
        Vector3 camForward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
        Vector3 camRight   = cameraTransform != null ? cameraTransform.right   : Vector3.right;
        camForward.y = 0f; camForward.Normalize();
        camRight.y   = 0f; camRight.Normalize();

        // 조이스틱 입력을 카메라 기준 월드 방향으로 변환
        _moveDir = (camRight * input.x + camForward * input.y).normalized;

        animator?.SetBool("isWalking", true);
    }
}
