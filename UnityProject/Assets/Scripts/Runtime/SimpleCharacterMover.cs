using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class SimpleCharacterMover : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Animator targetAnimator;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintMultiplier = 1.8f;
    [SerializeField] private bool instantPlanarMovement = true;
    [SerializeField] private float acceleration = 12f;
    [SerializeField] private bool snapMoveFacingToDirections = true;
    [SerializeField] private float moveFacingStepDegrees = 45f;
    [SerializeField] private bool instantMoveFacingRotation = true;
    [SerializeField] private float rotationLerpSpeed = 12f;
    [SerializeField] private bool orientToMoveDirection = true;

    [Header("Gravity")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private bool enableJump = true;
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float groundedSnapVelocity = -2f;

    [Header("Animation")]
    [SerializeField] private string moveSpeedParameter = "MoveSpeed";
    [SerializeField] private float moveSpeedDampTime = 0f;

    private Vector3 _planarVelocity;
    private float _verticalVelocity;
    private int _moveSpeedParameterHash;

    private void Reset()
    {
        characterController = GetComponent<CharacterController>();
        targetAnimator = GetComponentInChildren<Animator>();
    }

    private void Awake()
    {
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (targetAnimator == null)
        {
            targetAnimator = GetComponentInChildren<Animator>();
        }

        _moveSpeedParameterHash = Animator.StringToHash(moveSpeedParameter);
    }

    private void Update()
    {
        Vector2 moveInput = ReadMoveInput();
        Vector3 worldMoveDir = BuildWorldMove(moveInput);
        bool hasMoveInput = worldMoveDir.sqrMagnitude > 0.0001f;
        float targetSpeed = moveSpeed * (IsSprintHeld() ? sprintMultiplier : 1f);
        Vector3 targetPlanarVelocity = worldMoveDir * targetSpeed;

        _planarVelocity = instantPlanarMovement
            ? targetPlanarVelocity
            : Vector3.MoveTowards(
                _planarVelocity,
                targetPlanarVelocity,
                acceleration * Time.deltaTime);

        bool grounded = IsGrounded();
        if (grounded && _verticalVelocity < 0f)
        {
            _verticalVelocity = groundedSnapVelocity;
        }

        if (enableJump && grounded && IsJumpPressed())
        {
            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        _verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = _planarVelocity + Vector3.up * _verticalVelocity;
        ApplyVelocity(velocity);

        if (orientToMoveDirection)
        {
            RotateTowardMove(_planarVelocity);
        }

        UpdateAnimator(hasMoveInput);
    }

    private Vector3 BuildWorldMove(Vector2 input)
    {
        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;

        if (cameraTransform != null)
        {
            forward = cameraTransform.forward;
            right = cameraTransform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
        }

        Vector3 move = right * input.x + forward * input.y;
        return move.sqrMagnitude > 1f ? move.normalized : move;
    }

    private bool IsGrounded()
    {
        if (characterController != null)
        {
            return characterController.isGrounded;
        }

        return Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.2f);
    }

    private void ApplyVelocity(Vector3 velocity)
    {
        if (characterController != null)
        {
            characterController.Move(velocity * Time.deltaTime);
            return;
        }

        transform.position += velocity * Time.deltaTime;
    }

    private void RotateTowardMove(Vector3 planarVelocity)
    {
        Vector3 facing = planarVelocity;
        facing.y = 0f;
        if (facing.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector3 targetFacing = snapMoveFacingToDirections
            ? SnapPlanarDirection(facing.normalized, moveFacingStepDegrees)
            : facing.normalized;

        Quaternion targetRot = Quaternion.LookRotation(targetFacing, Vector3.up);
        if (instantMoveFacingRotation || rotationLerpSpeed <= 0f)
        {
            transform.rotation = targetRot;
            return;
        }

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationLerpSpeed * Time.deltaTime);
    }

    private static Vector3 SnapPlanarDirection(Vector3 direction, float angleStepDegrees)
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            return Vector3.forward;
        }

        float safeAngleStep = Mathf.Max(1f, angleStepDegrees);
        float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        float snappedYaw = Mathf.Round(yaw / safeAngleStep) * safeAngleStep;
        return Quaternion.Euler(0f, snappedYaw, 0f) * Vector3.forward;
    }

    private void UpdateAnimator(bool hasMoveInput)
    {
        if (targetAnimator == null || string.IsNullOrWhiteSpace(moveSpeedParameter))
        {
            return;
        }

        float normalizedMoveSpeed = hasMoveInput ? 1f : 0f;
        if (moveSpeedDampTime > 0f)
        {
            targetAnimator.SetFloat(_moveSpeedParameterHash, normalizedMoveSpeed, moveSpeedDampTime, Time.deltaTime);
            return;
        }

        targetAnimator.SetFloat(_moveSpeedParameterHash, normalizedMoveSpeed);
    }

    private static Vector2 ReadMoveInput()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            float x = 0f;
            float y = 0f;
            if (keyboard.aKey.isPressed) x -= 1f;
            if (keyboard.dKey.isPressed) x += 1f;
            if (keyboard.sKey.isPressed) y -= 1f;
            if (keyboard.wKey.isPressed) y += 1f;

            Vector2 input = new Vector2(x, y);
            if (input.sqrMagnitude > 0f)
            {
                return input.normalized;
            }
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
#else
        return Vector2.zero;
#endif
    }

    private static bool IsJumpPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Space);
#else
        return false;
#endif
    }

    private static bool IsSprintHeld()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#else
        return false;
#endif
    }
}
