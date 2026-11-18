using UnityEngine;
using UnityEngine.XR;
using FireSafety.Extinguishers;
using FireSafety.Fire;
using FireSafety.Interactions;

/// <summary>
/// First-Person Desktop & Editor Walkthrough Controller for the VR Fire Safety Simulation.
/// Seamlessly provides full mouse-look, WASD walking, and object interaction on Desktop,
/// while yielding camera rotation to VR hardware when a physical headset is tracking.
/// </summary>
public class KitchenPlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.8f;
    [SerializeField] private float sprintSpeed = 5.0f;

    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 2.4f;
    [SerializeField] private float maxPitch = 82f;
    [SerializeField] private float minPitch = -78f;

    [Header("Interaction Raycast")]
    [SerializeField] private float interactionDistance = 4.0f;

    private float pitch = 0f;
    private float yaw = 0f;
    private bool isCursorLocked = true;
    private FireExtinguisher activeHeldExtinguisher;

    // Kitchen boundary box
    private const float KitchenMinX = 199.0f;
    private const float KitchenMaxX = 205.8f;
    private const float KitchenMinZ = -3.8f;
    private const float KitchenMaxZ = 3.8f;
    private const float PlayerEyeHeight = 1.60f;

    private void Start()
    {
        Vector3 angles = transform.eulerAngles;
        pitch = angles.x > 180f ? angles.x - 360f : angles.x;
        yaw = angles.y;

        SetCursorLock(true);
        Debug.Log("[KitchenPlayerController] Active. WASD to walk, Mouse to look around, Left Click to interact.");
    }

    public void SetCursorLock(bool locked)
    {
        isCursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void Update()
    {
        HandleMouseLook();
        HandleMovement();
        HandleQuickEquipKeys();
        TrackHeldExtinguisher();

        if (activeHeldExtinguisher == null)
        {
            HandleInteractionRaycast();
        }
    }

    private void HandleMouseLook()
    {
        // When in VR on actual headset, let HMD sensors drive head rotation
#if !UNITY_EDITOR
        if (XRSettings.isDeviceActive)
        {
            var hmd = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (hmd.isValid && hmd.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion hmdRot) && hmdRot != Quaternion.identity)
            {
                return;
            }
        }
#endif

        // Toggle cursor lock with ESC
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SetCursorLock(!isCursorLocked);
        }

        // Left click inside game view re-locks cursor
        if (!isCursorLocked && Input.GetMouseButtonDown(0))
        {
            var hud = FindObjectOfType<TrainingHUD>();
            bool onModal = hud != null && (hud.State == TrainingHUD.TrainingState.KitchenBriefing ||
                                           hud.State == TrainingHUD.TrainingState.MissionComplete);
            if (!onModal)
            {
                SetCursorLock(true);
            }
        }

        // Rotate camera with mouse whenever cursor is locked OR when right mouse is held
        if (isCursorLocked || Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            yaw += mouseX;
            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }

    private void HandleMovement()
    {
        float speed = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? sprintSpeed : moveSpeed;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        // Direct key fallback (ensures 100% responsiveness on any platform / focus state)
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v = 1f;
        else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v = -1f;

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h = -1f;
        else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h = 1f;

        if (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f)
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 right = transform.right;
            right.y = 0f;
            right.Normalize();

            Vector3 moveDir = (forward * v + right * h).normalized;
            Vector3 newPos = transform.position + moveDir * (speed * Time.deltaTime);

            if (transform.position.x > 100f)
            {
                newPos.y = PlayerEyeHeight;
                newPos.x = Mathf.Clamp(newPos.x, KitchenMinX, KitchenMaxX);
                newPos.z = Mathf.Clamp(newPos.z, KitchenMinZ, KitchenMaxZ);
            }

            transform.position = newPos;
        }
    }

    public void Teleport(Vector3 targetPosition, Vector3 targetEuler)
    {
        transform.position = targetPosition;
        pitch = targetEuler.x > 180f ? targetEuler.x - 360f : targetEuler.x;
        yaw = targetEuler.y;
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void HandleInteractionRaycast()
    {
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E))
        {
            Ray ray = new Ray(transform.position, transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
            {
                // 1. Pick up extinguisher
                FireExtinguisher extinguisher = hit.collider.GetComponentInParent<FireExtinguisher>();
                if (extinguisher != null && !extinguisher.IsHeld)
                {
                    if (activeHeldExtinguisher != null && activeHeldExtinguisher != extinguisher)
                    {
                        activeHeldExtinguisher.Drop();
                    }

                    extinguisher.Pickup(transform);
                    activeHeldExtinguisher = extinguisher;

                    var hud = FindObjectOfType<TrainingHUD>();
                    if (hud != null) hud.OnExtinguisherSelected(extinguisher.Type);

                    return;
                }

                // 2. Fire Alarm Unit
                FireAlarmController alarm = hit.collider.GetComponentInParent<FireAlarmController>();
                if (alarm != null)
                {
                    alarm.ToggleAlarm();
                    return;
                }

                // 3. Power Switch
                PowerSwitchController powerSwitch = hit.collider.GetComponentInParent<PowerSwitchController>();
                if (powerSwitch != null)
                {
                    powerSwitch.TogglePower();
                    return;
                }
            }
        }
    }

    private void HandleQuickEquipKeys()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            EquipExtinguisherByType(ExtinguisherType.Water);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            EquipExtinguisherByType(ExtinguisherType.DryPowder);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            EquipExtinguisherByType(ExtinguisherType.CO2);
        }
    }

    private void EquipExtinguisherByType(ExtinguisherType type)
    {
        foreach (var ext in FindObjectsOfType<FireExtinguisher>())
        {
            if (ext.Type == type)
            {
                if (activeHeldExtinguisher != null && activeHeldExtinguisher != ext)
                {
                    activeHeldExtinguisher.Drop();
                }

                ext.Pickup(transform);
                activeHeldExtinguisher = ext;

                var hud = FindObjectOfType<TrainingHUD>();
                if (hud != null) hud.OnExtinguisherSelected(type);

                Debug.Log($"[KitchenPlayerController] Equipped {ext.DisplayName}");
                return;
            }
        }
    }

    private void TrackHeldExtinguisher()
    {
        if (activeHeldExtinguisher != null && !activeHeldExtinguisher.IsHeld)
        {
            activeHeldExtinguisher = null;
        }

        if (activeHeldExtinguisher == null)
        {
            foreach (var ext in FindObjectsOfType<FireExtinguisher>())
            {
                if (ext.IsHeld)
                {
                    activeHeldExtinguisher = ext;
                    break;
                }
            }
        }
    }
}
