using UnityEngine;
using FireSafety.Extinguishers;
using FireSafety.Fire;
using FireSafety.Interactions;

/// <summary>
/// First-person walkthrough controller for desktop evaluation of the Fire Safety Training project.
///
/// Movement & Navigation:
///   - W, A, S, D / Arrow keys: Walk around the room.
///   - Mouse: Look around (360-degree free look).
///   - Shift: Walk faster / sprint.
///   - ESC: Toggle cursor lock.
///
/// Extinguisher Controls:
///   - Press [1]: Quick-equip Water extinguisher.
///   - Press [2]: Quick-equip Dry Powder extinguisher.
///   - Press [3]: Quick-equip CO₂ extinguisher.
///   - Click Left Mouse Button or [E] on any extinguisher to pick it up.
///   - While holding an extinguisher:
///       [P]           : Pull safety pin (PASS step 1)
///       [Hold Space]  : Squeeze handle & spray discharge (PASS step 3)
///       [G]           : Drop extinguisher on the floor in front of you
///       [R]           : Reset extinguisher back to wall station
///
/// Environment Controls:
///   - [F] : Toggle stove fire ON/OFF
///   - [A] : Toggle fire alarm (siren & red flashing strobe)
///   - [X] : Cycle EXIT sign states
///   - [L] : Toggle electrical power switch (cuts room lights)
/// </summary>
public class KitchenPlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.8f;
    [SerializeField] private float sprintSpeed = 4.8f;

    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 2.2f;
    [SerializeField] private float maxPitch = 80f;
    [SerializeField] private float minPitch = -75f;

    [Header("Interaction Raycast")]
    [SerializeField] private float interactionDistance = 4.0f;

    private float pitch = 0f;
    private float yaw = 0f;
    private bool isMouseLookActive = true;
    private FireExtinguisher activeHeldExtinguisher;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttachToCamera()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            if (cam.GetComponent<KitchenPlayerController>() == null)
            {
                cam.gameObject.AddComponent<KitchenPlayerController>();
                Debug.Log("[KitchenPlayerController] ✓ Auto-attached to Main Camera. Desktop FPS controls ready.");
            }
            if (cam.GetComponent<TrainingHUD>() == null)
            {
                cam.gameObject.AddComponent<TrainingHUD>();
                Debug.Log("[TrainingHUD] ✓ Auto-attached to Main Camera.");
            }
        }
    }

    private void Start()
    {
        // Initialize look angles from current camera rotation
        Vector3 angles = transform.eulerAngles;
        pitch = angles.x > 180f ? angles.x - 360f : angles.x;
        yaw = angles.y;

        // Lock and hide cursor for smooth FPS control
        SetCursorState(true);

        Debug.Log("[KitchenPlayerController] Active. Standard WASD/Mouse control ready.");
    }

    public void SetCursorState(bool locked)
    {
        isMouseLookActive = locked;
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
        // Toggle cursor lock with ESC
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SetCursorState(!isMouseLookActive);
        }

        // Left click in game window locks cursor back to FPS mode
        if (!isMouseLookActive && Input.GetMouseButtonDown(0))
        {
            SetCursorState(true);
        }

        // Rotate camera with mouse
        if (isMouseLookActive || Input.GetMouseButton(1))
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
                newPos.x = Mathf.Clamp(newPos.x, -2.5f, 2.5f);
                newPos.z = Mathf.Clamp(newPos.z, -1.0f, 2.0f);
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
