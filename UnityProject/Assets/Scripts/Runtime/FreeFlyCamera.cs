using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class FreeFlyCamera : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float sprintMultiplier = 2.5f;
    [SerializeField] private float slowMultiplier = 0.35f;
    [SerializeField] private float verticalSpeed = 6f;
    [SerializeField] private float scrollSpeedStep = 1.2f;
    [SerializeField] private float minMoveSpeed = 1f;
    [SerializeField] private float maxMoveSpeed = 60f;

    [Header("Look")]
    [SerializeField] private bool requireRightMouseToLook = true;
    [SerializeField] private bool lockCursorWhileLooking = true;
    [SerializeField] private float lookSensitivity = 150f;
    [SerializeField] private float pitchMin = -85f;
    [SerializeField] private float pitchMax = 85f;

    private float _yaw;
    private float _pitch;
    private bool _cursorLocked;

    private void Awake()
    {
        Vector3 startEuler = transform.rotation.eulerAngles;
        _yaw = startEuler.y;
        _pitch = NormalizePitch(startEuler.x);
    }

    private void Update()
    {
        bool isLooking = !requireRightMouseToLook || IsLookHeld();
        UpdateCursorLock(isLooking);

        if (isLooking)
        {
            RotateCamera();
        }

        MoveCamera();
        AdjustSpeedFromScroll();
    }

    private void RotateCamera()
    {
        Vector2 look = ReadLookDelta();
        _yaw += look.x * lookSensitivity * Time.deltaTime;
        _pitch -= look.y * lookSensitivity * Time.deltaTime;
        _pitch = Mathf.Clamp(_pitch, pitchMin, pitchMax);
        transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    private void MoveCamera()
    {
        Vector3 planarInput = new Vector3(ReadHorizontal(), 0f, ReadVertical());
        float upDown = 0f;
        if (IsRiseHeld()) upDown += 1f;
        if (IsDescendHeld()) upDown -= 1f;

        float speed = moveSpeed;
        if (IsSprintHeld()) speed *= sprintMultiplier;
        if (IsSlowHeld()) speed *= slowMultiplier;

        Vector3 move = transform.TransformDirection(planarInput.normalized) * speed;
        move += Vector3.up * (upDown * verticalSpeed);
        transform.position += move * Time.deltaTime;
    }

    private void AdjustSpeedFromScroll()
    {
        float scroll = ReadScrollY();
        if (Mathf.Approximately(scroll, 0f))
        {
            return;
        }

        moveSpeed = Mathf.Clamp(moveSpeed + scroll * scrollSpeedStep, minMoveSpeed, maxMoveSpeed);
    }

    private void UpdateCursorLock(bool shouldLock)
    {
        if (!lockCursorWhileLooking)
        {
            if (_cursorLocked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                _cursorLocked = false;
            }
            return;
        }

        if (shouldLock && !_cursorLocked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _cursorLocked = true;
        }
        else if (!shouldLock && _cursorLocked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _cursorLocked = false;
        }
    }

    private static float NormalizePitch(float eulerX)
    {
        return eulerX > 180f ? eulerX - 360f : eulerX;
    }

    private static float ReadHorizontal()
    {
#if ENABLE_INPUT_SYSTEM
        float value = 0f;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed) value -= 1f;
            if (keyboard.dKey.isPressed) value += 1f;
            if (Mathf.Abs(value) > 0.01f) return value;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetAxisRaw("Horizontal");
#else
        return 0f;
#endif
    }

    private static float ReadVertical()
    {
#if ENABLE_INPUT_SYSTEM
        float value = 0f;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.sKey.isPressed) value -= 1f;
            if (keyboard.wKey.isPressed) value += 1f;
            if (Mathf.Abs(value) > 0.01f) return value;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetAxisRaw("Vertical");
#else
        return 0f;
#endif
    }

    private static Vector2 ReadLookDelta()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 delta = mouse.delta.ReadValue();
            if (delta.sqrMagnitude > 0f)
            {
                return delta;
            }
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
#else
        return Vector2.zero;
#endif
    }

    private static float ReadScrollY()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y / 120f;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                return scroll;
            }
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.mouseScrollDelta.y;
#else
        return 0f;
#endif
    }

    private static bool IsLookHeld()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.isPressed) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButton(1);
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

    private static bool IsSlowHeld()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed)) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
#else
        return false;
#endif
    }

    private static bool IsRiseHeld()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.eKey.isPressed) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.E);
#else
        return false;
#endif
    }

    private static bool IsDescendHeld()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.qKey.isPressed) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.Q);
#else
        return false;
#endif
    }
}
