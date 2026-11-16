using UnityEngine;
using FireSafety.Extinguishers;
using FireSafety.Fire;
using FireSafety.Interactions;

/// <summary>
/// First-person walkthrough controller for desktop evaluation of the VR Fire Safety Training kitchen.
///
/// Movement & Navigation:
///   - W, A, S, D / Arrow keys: Walk around the kitchen.
///   - Mouse: Look around (360-degree free look).
///   - Shift: Walk faster / sprint.
///
/// Extinguisher Controls:
///   - Press [1]: Quick-equip WET CHEMICAL extinguisher (Yellow - Class F/K Kitchen Oil).
///   - Press [2]: Quick-equip DRY POWDER extinguisher (Blue - Class ABC).
///   - Press [3]: Quick-equip CO₂ extinguisher (Black - Class B & Electrical).
///   - Click Left Mouse Button on any extinguisher to pick it up.
///   - While holding an extinguisher:
///       [P]           : Pull safety pin (PASS step 1)
///       [Hold Space]  : Squeeze handle & spray discharge (PASS step 3)
///       [G]           : Drop extinguisher on the floor in front of you
///       [R]           : Reset extinguisher back to wall station
///
/// Environment Controls:
///   - [F] : Toggle stove fire ON/OFF
///   - [A] : Toggle fire alarm (siren & red flashing strobe)
///   - [X] : Cycle EXIT sign states (Normal -> Emergency Backup -> Off)
///   - [L] : Toggle electrical power switch (cuts room lights)
/// </summary>
public class KitchenPlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float sprintSpeed = 4.5f;

    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 2.0f;
    [SerializeField] private float maxPitch = 75f;
    [SerializeField] private float minPitch = -70f;

    [Header("Interaction Raycast")]
    [SerializeField] private float interactionDistance = 3.5f;

    private float pitch = 0f;
    private float yaw = 0f;
    private bool isMouseLookActive = true;
    private FireExtinguisher activeHeldExtinguisher;

    private void Start()
    {
        // VRControllerManager is added in the scene directly — do not auto-add to avoid duplicates

        // Initialize look angles from the scene rotation
        Vector3 angles = transform.eulerAngles;
        pitch = angles.x > 180f ? angles.x - 360f : angles.x;
        yaw = angles.y;

        // Lock and hide cursor for FPS feel
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("[KitchenPlayerController] Active. VR Controllers & Headset enabled, Desktop WASD/Mouse fallback ready.");
    }

    private void Update()
    {
        HandleMouseLook();
        HandleMovement();
        HandleQuickEquipKeys();
        TrackHeldExtinguisher();

        // Only handle click interaction when NOT holding an extinguisher
        // (otherwise clicks are used for discharge)
        if (activeHeldExtinguisher == null)
        {
            HandleInteractionRaycast();
        }
    }

    private void HandleMouseLook()
    {
        // When wearing a VR headset, HMD head tracking drives orientation natively
        if (UnityEngine.XR.XRSettings.isDeviceActive) return;

        if (isMouseLookActive)
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            yaw += mouseX;
            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            isMouseLookActive = !isMouseLookActive;
            Cursor.lockState = isMouseLookActive ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !isMouseLookActive;
        }
    }

    private void HandleMovement()
    {
        float speed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : moveSpeed;

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

            bool inKitchen = transform.position.x > 100f;
            if (inKitchen)
            {
                // Room 02: Kitchen bounds
                newPos.y = 1.60f;
                newPos.x = Mathf.Clamp(newPos.x, 199.0f, 205.8f);
                newPos.z = Mathf.Clamp(newPos.z, -3.8f, 3.8f);
            }
            else
            {
                // Room 01: Orientation Studio bounds
                newPos.y = 1.55f;
                newPos.x = Mathf.Clamp(newPos.x, -2.2f, 2.2f);
                newPos.z = Mathf.Clamp(newPos.z, -0.8f, 1.8f);
            }

            transform.position = newPos;
        }
    }

    /// <summary>
    /// Teleports the player to a target location and updates look orientation smoothly.
    /// </summary>
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
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = new Ray(transform.position, transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
            {
                // 1. Fire Extinguisher pick-up
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
        // [1] Quick-equip Water (Class A - Electrocution hazard on electrical fire)
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            EquipExtinguisherByType(ExtinguisherType.Water);
        }
        // [2] Quick-equip Dry Powder (Class ABC - Corrosive powder residue / motor damage)
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            EquipExtinguisherByType(ExtinguisherType.DryPowder);
        }
        // [3] Quick-equip CO2 (Class B & Electrical - Correct approved agent)
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

    private void OnGUI()
    {
        // Rendering handled by TrainingHUD for clean unified UI matching Figma
    }
}
