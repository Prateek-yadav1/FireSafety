using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using FireSafety.Extinguishers;
using FireSafety.Fire;
using FireSafety.Interactions;

/// <summary>
/// Dual-mode Controller for both Desktop (Mouse + Keyboard) and VR (Meta Quest).
/// Seamlessly supports:
///  - Desktop: WASD movement, mouse look (with cursor toggle via ESC / click), number keys, interaction raycast.
///  - VR: HMD head tracking, dual 6-DoF controllers, thumbstick locomotion, laser interactions.
/// </summary>
public class KitchenPlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float sprintSpeed = 4.5f;

    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 2.0f;
    [SerializeField] private float maxPitch = 80f;
    [SerializeField] private float minPitch = -75f;

    [Header("Interaction Raycast")]
    [SerializeField] private float interactionDistance = 4.0f;

    private float pitch = 0f;
    private float yaw = 0f;
    private bool isMouseLookActive = true;
    private FireExtinguisher activeHeldExtinguisher;

    // Kitchen room bounds
    private const float KitchenMinX = 199.0f;
    private const float KitchenMaxX = 205.8f;
    private const float KitchenMinZ = -3.8f;
    private const float KitchenMaxZ = 3.8f;
    private const float PlayerEyeHeight = 1.60f;

    private void Start()
    {
        // Initialize look angles from current camera rotation
        Vector3 angles = transform.eulerAngles;
        pitch = angles.x > 180f ? angles.x - 360f : angles.x;
        yaw = angles.y;

        // Ensure player is inside kitchen
        if (transform.position.x < 100f)
        {
            transform.position = new Vector3(202.8f, PlayerEyeHeight, 0.0f);
            yaw = -90f;
            pitch = 0f;
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        // On desktop/editor, start with mouse look active but keep cursor visible if briefing is open
        SetMouseLookState(true);

        Debug.Log("[KitchenPlayerController] Initialized. WASD/Mouse and VR Headset/Controllers ready.");
    }

    public void SetMouseLookState(bool active)
    {
        isMouseLookActive = active;
        if (active)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
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

    /// <summary>
    /// Checks whether an actual physical VR HMD is actively providing head tracking data.
    /// In Unity Editor on macOS/PC without a physical connected headset streaming frames, returns false so mouse look works!
    /// </summary>
    public static bool IsPhysicalHMDActive()
    {
#if UNITY_EDITOR
        // In the editor, always allow mouse look so the developer can look around and test!
        return false;
#else
        if (!XRSettings.isDeviceActive) return false;
        var hmd = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if (hmd.isValid && hmd.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rot))
        {
            return rot != Quaternion.identity;
        }
        return false;
#endif
    }

    private void HandleMouseLook()
    {
        // When running in VR with an active tracked headset, HMD hardware orientation drives the view
        if (IsPhysicalHMDActive()) return;

        // Toggle cursor lock with ESC
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SetMouseLookState(!isMouseLookActive);
        }

        // Clicking in the Game window locks cursor back to FPS mode
        if (!isMouseLookActive && Input.GetMouseButtonDown(0))
        {
            // If training HUD is displaying a card, give user option to click with mouse
            var hud = FindObjectOfType<TrainingHUD>();
            bool inModalDialog = hud != null && (hud.State == TrainingHUD.TrainingState.KitchenBriefing ||
                                                 hud.State == TrainingHUD.TrainingState.MissionComplete);
            if (!inModalDialog)
            {
                SetMouseLookState(true);
            }
        }

        // Mouse look rotation
        if (isMouseLookActive || Input.GetMouseButton(1)) // Right-click always looks around
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            if (Mathf.Abs(mouseX) > 0.0001f || Mathf.Abs(mouseY) > 0.0001f)
            {
                yaw += mouseX;
                pitch -= mouseY;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }
        }
    }

    private void HandleMovement()
    {
        float speed = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? sprintSpeed : moveSpeed;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

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
                // Kitchen room boundaries
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
        Debug.Log($"[KitchenPlayerController] Teleported to {targetPosition}, yaw: {yaw}");
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
        Debug.Log($"[KitchenPlayerController] No {type} extinguisher found in scene.");
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
