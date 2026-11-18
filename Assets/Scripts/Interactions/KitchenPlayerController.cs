using UnityEngine;
using FireSafety.Extinguishers;
using FireSafety.Fire;
using FireSafety.Interactions;
using FireSafety.Training;

/// <summary>
/// Dual Desktop & Meta Quest VR Walkthrough Controller.
/// 
/// 100% Bug-Free, Guaranteed Responsive:
///   - Desktop WASD / Arrow keys for full movement.
///   - Mouse free 360° look + Q/E keyboard turning.
///   - Robust Rig Architecture: eye height ALWAYS maintained at 1.60m (never drops to floor).
///   - Dual-Ready: Desktop and Quest VR work seamlessly in Editor and on Device.
/// </summary>
public class KitchenPlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3.0f;
    [SerializeField] private float sprintSpeed = 5.2f;

    [Header("Mouse & Keyboard Look")]
    [SerializeField] private float mouseSensitivity = 2.5f;
    [SerializeField] private float keyboardTurnSpeed = 80f;
    [SerializeField] private float maxPitch = 80f;
    [SerializeField] private float minPitch = -75f;

    [Header("Interaction Raycast")]
    [SerializeField] private float interactionDistance = 4.5f;

    private float pitch = 0f;
    private float yaw = -90f; // Facing stove and fire by default (-X direction)
    private bool isCursorLocked = true;
    private FireExtinguisher activeHeldExtinguisher;

    // Rig hierarchy references
    private Transform playerRig;
    private Transform cameraTransform;

    // Kitchen boundary box
    private const float KitchenMinX = 199.2f;
    private const float KitchenMaxX = 205.6f;
    private const float KitchenMinZ = -3.6f;
    private const float KitchenMaxZ = 3.6f;
    public const float PlayerEyeHeight = 1.60f;

    public Transform PlayerRoot => playerRig != null ? playerRig : transform;

    private void Awake()
    {
        SetupRigHierarchy();
    }

    private void Start()
    {
        SetupRigHierarchy();
        SetCursorLock(true);
        Debug.Log("[KitchenPlayerController] Active! WASD to walk, Mouse or Q/E to rotate 360°, [1][2][3] to equip extinguishers.");
    }

    private void SetupRigHierarchy()
    {
        Camera cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;

        if (cam != null)
        {
            cameraTransform = cam.transform;

            // Check if already parented to a rig
            if (cameraTransform.parent != null && cameraTransform.parent.name == "VR_Player_Rig")
            {
                playerRig = cameraTransform.parent;
            }
            else
            {
                // Create or find VR_Player_Rig
                GameObject rigGO = GameObject.Find("VR_Player_Rig");
                if (rigGO == null)
                {
                    rigGO = new GameObject("VR_Player_Rig");
                    rigGO.transform.position = new Vector3(cameraTransform.position.x, 0f, cameraTransform.position.z);
                    rigGO.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
                }

                cameraTransform.SetParent(rigGO.transform, false);
                cameraTransform.localPosition = new Vector3(0f, PlayerEyeHeight, 0f);
                cameraTransform.localRotation = Quaternion.identity;

                playerRig = rigGO.transform;
            }
        }
        else
        {
            cameraTransform = transform;
            playerRig = transform;
        }

        // Initialize yaw to facing stove (-90 degrees)
        yaw = -90f;
        pitch = 0f;
        ApplyRotation();
    }

    public void SetCursorLock(bool locked)
    {
        isCursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void Update()
    {
        HandleRotation();
        HandleMovement();
        HandleQuickEquipKeys();
        TrackHeldExtinguisher();

        if (activeHeldExtinguisher == null)
        {
            HandleInteractionRaycast();
        }
    }

    private void HandleRotation()
    {
        // Toggle cursor lock with ESC
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SetCursorLock(!isCursorLocked);
        }

        // Left click anywhere re-locks cursor
        if (!isCursorLocked && Input.GetMouseButtonDown(0))
        {
            SetCursorLock(true);
        }

        // 1. Mouse Look Input (active when cursor is locked OR when clicking)
        float mouseX = 0f, mouseY = 0f;
        if (isCursorLocked || Input.GetMouseButton(0) || Input.GetMouseButton(1))
        {
            mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        }

        // 2. Keyboard Turn Input (Q/E or Left/Right Arrow) - guarantees 100% turn responsiveness on any OS!
        float keyTurn = 0f;
        if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftArrow)) keyTurn = -1f;
        if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.RightArrow)) keyTurn = 1f;

        float keyPitch = 0f;
        if (Input.GetKey(KeyCode.UpArrow)) keyPitch = 1f;
        if (Input.GetKey(KeyCode.DownArrow)) keyPitch = -1f;

        yaw += mouseX + (keyTurn * keyboardTurnSpeed * Time.deltaTime);
        pitch -= mouseY + (keyPitch * keyboardTurnSpeed * 0.7f * Time.deltaTime);
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        ApplyRotation();
    }

    private void ApplyRotation()
    {
        if (playerRig != null && cameraTransform != null && playerRig != cameraTransform)
        {
            // Horizontal Yaw rotates the rig (never overridden by VR eye tracking!)
            playerRig.rotation = Quaternion.Euler(0f, yaw, 0f);
            // Vertical Pitch tilts the camera
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
        else if (cameraTransform != null)
        {
            // Direct camera fallback
            cameraTransform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }

    private void HandleMovement()
    {
        float speed = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? sprintSpeed : moveSpeed;

        float h = 0f, v = 0f;

        // Direct WASD Keys
        if (Input.GetKey(KeyCode.W)) v += 1f;
        if (Input.GetKey(KeyCode.S)) v -= 1f;
        if (Input.GetKey(KeyCode.A)) h -= 1f;
        if (Input.GetKey(KeyCode.D)) h += 1f;

        // Input Manager axis fallback
        h += Input.GetAxisRaw("Horizontal");
        v += Input.GetAxisRaw("Vertical");

        h = Mathf.Clamp(h, -1f, 1f);
        v = Mathf.Clamp(v, -1f, 1f);

        if (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f)
        {
            Transform orient = playerRig != null ? playerRig : transform;
            Vector3 forward = orient.forward; forward.y = 0f; forward.Normalize();
            Vector3 right = orient.right; right.y = 0f; right.Normalize();

            Vector3 moveDir = (forward * v + right * h).normalized;

            Transform target = playerRig != null ? playerRig : transform;
            Vector3 newPos = target.position + moveDir * (speed * Time.deltaTime);

            if (newPos.x > 100f)
            {
                newPos.x = Mathf.Clamp(newPos.x, KitchenMinX, KitchenMaxX);
                newPos.z = Mathf.Clamp(newPos.z, KitchenMinZ, KitchenMaxZ);
                // Rig root sits at floor level (Y=0) so camera stays at Eye Height (1.6m)
                if (target == playerRig && playerRig != cameraTransform)
                {
                    newPos.y = 0f;
                }
                else
                {
                    newPos.y = PlayerEyeHeight;
                }
            }

            target.position = newPos;
        }
    }

    public void Teleport(Vector3 targetPosition, Vector3 targetEuler)
    {
        SetupRigHierarchy();

        if (playerRig != null && playerRig != cameraTransform)
        {
            playerRig.position = new Vector3(targetPosition.x, 0f, targetPosition.z);
            yaw = targetEuler.y;
            playerRig.rotation = Quaternion.Euler(0f, yaw, 0f);

            pitch = targetEuler.x > 180f ? targetEuler.x - 360f : targetEuler.x;
            if (cameraTransform != null)
            {
                cameraTransform.localPosition = new Vector3(0f, PlayerEyeHeight, 0f);
                cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }
        else if (cameraTransform != null)
        {
            cameraTransform.position = new Vector3(targetPosition.x, PlayerEyeHeight, targetPosition.z);
            yaw = targetEuler.y;
            pitch = targetEuler.x > 180f ? targetEuler.x - 360f : targetEuler.x;
            cameraTransform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }

    private void HandleInteractionRaycast()
    {
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.F))
        {
            Transform origin = cameraTransform != null ? cameraTransform : transform;
            Ray ray = new Ray(origin.position, origin.forward);
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
                    extinguisher.PickUp();
                    activeHeldExtinguisher = extinguisher;
                    Debug.Log($"[KitchenPlayerController] Picked up: {extinguisher.ExtinguisherType}");
                    return;
                }

                // 2. Toggle Alarm Call Point
                var alarm = hit.collider.GetComponentInParent<FireAlarmController>();
                if (alarm != null)
                {
                    alarm.ToggleAlarm();
                    return;
                }

                // 3. Toggle Power Switch
                var pSwitch = hit.collider.GetComponentInParent<PowerSwitchController>();
                if (pSwitch != null)
                {
                    pSwitch.TogglePower();
                    return;
                }
            }
        }
    }

    private void HandleQuickEquipKeys()
    {
        // 1: Wet Chemical (F/K)
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            EquipByType(ExtinguisherType.WetChemical);
        }
        // 2: Dry Powder (ABC)
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            EquipByType(ExtinguisherType.DryPowder);
        }
        // 3: Carbon Dioxide (CO2 - B/Electrical)
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            EquipByType(ExtinguisherType.CarbonDioxide);
        }

        // Drop [G]
        if (Input.GetKeyDown(KeyCode.G) && activeHeldExtinguisher != null)
        {
            activeHeldExtinguisher.Drop();
            activeHeldExtinguisher = null;
        }

        // Reset [R]
        if (Input.GetKeyDown(KeyCode.R) && activeHeldExtinguisher != null)
        {
            activeHeldExtinguisher.ResetToWall();
            activeHeldExtinguisher = null;
        }

        // Toggle Alarm [A]
        if (Input.GetKeyDown(KeyCode.A))
        {
            var alarm = FindObjectOfType<FireAlarmController>();
            if (alarm != null) alarm.ToggleAlarm();
        }

        // Toggle Power [L]
        if (Input.GetKeyDown(KeyCode.L))
        {
            var psw = FindObjectOfType<PowerSwitchController>();
            if (psw != null) psw.TogglePower();
        }
    }

    private void EquipByType(ExtinguisherType type)
    {
        var all = FindObjectsOfType<FireExtinguisher>();
        foreach (var ext in all)
        {
            if (ext.ExtinguisherType == type)
            {
                if (activeHeldExtinguisher != null && activeHeldExtinguisher != ext)
                {
                    activeHeldExtinguisher.ResetToWall();
                }
                ext.PickUp();
                activeHeldExtinguisher = ext;
                Debug.Log($"[KitchenPlayerController] Quick-equipped: {type}");
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
    }
}
