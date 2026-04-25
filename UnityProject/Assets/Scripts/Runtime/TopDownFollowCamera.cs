using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class TopDownFollowCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("View")]
    [SerializeField] private float height = 16f;
    [SerializeField] private float distance = 14f;
    [SerializeField] private float pitch = 60f;
    [SerializeField] private float yaw = 0f;
    [SerializeField] private float followSmooth = 14f;

    [Header("Zoom")]
    [SerializeField] private float zoomStep = 1.75f;
    [SerializeField] private float minHeight = 4f;
    [SerializeField] private float maxHeight = 42f;
    [SerializeField] private float minDistance = 2.5f;
    [SerializeField] private float maxDistance = 26f;

    [Header("Rotate")]
    [SerializeField] private bool allowRotate = true;
    [SerializeField] private float rotateSpeed = 90f;

    public void SetTarget(Transform followTarget)
    {
        target = followTarget;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        UpdateZoom();
        UpdateYaw();

        Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredOffset = orbit * new Vector3(0f, 0f, -distance) + Vector3.up * height;
        Vector3 desiredPos = target.position + desiredOffset;

        float t = 1f - Mathf.Exp(-followSmooth * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPos, t);
        transform.rotation = Quaternion.LookRotation((target.position - transform.position).normalized, Vector3.up);
    }

    private void UpdateZoom()
    {
        float scroll = ReadScroll();
        if (Mathf.Approximately(scroll, 0f))
        {
            return;
        }

        height = Mathf.Clamp(height - scroll * zoomStep, minHeight, maxHeight);
        distance = Mathf.Clamp(distance - scroll * (zoomStep * 0.75f), minDistance, maxDistance);
    }

    private void UpdateYaw()
    {
        if (!allowRotate)
        {
            return;
        }

        float delta = ReadRotateDeltaX();
        if (Mathf.Approximately(delta, 0f))
        {
            return;
        }

        yaw += delta * rotateSpeed * Time.deltaTime;
    }

    private static float ReadScroll()
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

    private static float ReadRotateDeltaX()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.isPressed)
        {
            return mouse.delta.ReadValue().x;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButton(1))
        {
            return Input.GetAxisRaw("Mouse X");
        }
        return 0f;
#else
        return 0f;
#endif
    }
}
