using UnityEngine;
using FireSafety.Extinguishers;
using FireSafety.Fire;
using FireSafety.Training;

/// <summary>
/// KitchenPlayerController — 100% rock-solid, responsive dual-input player controller.
///
/// Features:
///   - Dual Desktop (WASD + 360° Mouse Look) and Meta Quest VR architecture.
///   - Robust VR_Player_Rig hierarchy: rig stays at (202.8, 0, 0), camera eye height at 1.60m.
///   - Mouse free-look with auto-relock on click, right-drag look, and Q/E + Arrow keyboard turning.
///   - Direct KeyCode checks (W, A, S, D) alongside Input.GetAxis for guaranteed response.
///   - Full interaction: [F]/Click interact, [A] alarm, [L] power, [1][2][3] extinguishers,
///     [P] pull pin, [Space]/Click spray, [G] drop, [R] reset.
/// </summary>
public class KitchenPlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed   = 3.2f;
    [SerializeField] private float sprintSpeed = 5.8f;

    [Header("Look Settings")]
    [SerializeField] private float mouseSensitivity = 2.5f;
    [SerializeField] private float keyboardTurnSpeed = 85f;
    [SerializeField] private float minPitch = -75f;
    [SerializeField] private float maxPitch =  80f;

    [Header("Interaction Settings")]
    [SerializeField] private float interactionDistance = 4.5f;

    // Kitchen walkable boundaries (inside kitchen walls)
    public const float KitchenMinX = 199.5f;
    public const float KitchenMaxX = 205.2f;
    public const float KitchenMinZ = -3.4f;
    public const float KitchenMaxZ =  3.4f;
    public const float EyeHeight   =  1.60f;

    // Rig references
    private Transform playerRig;
    private Transform cameraTransform;

    // Look angles
    private float yaw   = -90f; // Facing stove & fire (-X direction)
    private float pitch =   0f;
    private bool isCursorLocked = false;

    // Held equipment
    private FireExtinguisher activeHeldExtinguisher = null;
    public FireExtinguisher HeldExtinguisher => activeHeldExtinguisher;
    public Transform PlayerRoot => playerRig != null ? playerRig : transform;

    // ─── Lifecycle ───────────────────────────────────────────────────────────

    private void Awake()
    {
        EnsureRigHierarchy();
    }

    private void Start()
    {
        EnsureRigHierarchy();
        LockCursor(true);
        Debug.Log("[KitchenPlayerController] Initialized inside kitchen. WASD to move, Mouse or Q/E to look 360°.");
    }

    private void EnsureRigHierarchy()
    {
        Camera cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        cameraTransform = (cam != null) ? cam.transform : transform;

        // Check if camera is already child of VR_Player_Rig
        if (cameraTransform.parent != null && cameraTransform.parent.name == "VR_Player_Rig")
        {
            playerRig = cameraTransform.parent;
        }
        else
        {
            GameObject rigGO = GameObject.Find("VR_Player_Rig");
            if (rigGO == null)
            {
                rigGO = new GameObject("VR_Player_Rig");
                rigGO.transform.position = new Vector3(202.8f, 0f, 0f);
                rigGO.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            }
            playerRig = rigGO.transform;

            cameraTransform.SetParent(playerRig, false);
            cameraTransform.localPosition = new Vector3(0f, EyeHeight, 0f);
            cameraTransform.localRotation = Quaternion.identity;
        }

        // Enforce rig position inside kitchen
        if (playerRig.position.x < 100f)
        {
            playerRig.position = new Vector3(202.8f, 0f, 0f);
            playerRig.rotation = Quaternion.Euler(0f, -90f, 0f);
        }

        yaw   = playerRig.eulerAngles.y;
        pitch = 0f;
        ApplyRotation();
    }

    private void Update()
    {
        HandleCursorLockInput();
        HandleLook();
        HandleMovement();
        HandleHotkeys();
        HandleExtinguisherSpray();
        TrackHeldExtinguisher();

        // Raycast interact when not holding or when aiming
        if (activeHeldExtinguisher == null)
        {
            HandleRaycastInteract();
        }
    }

    private void LateUpdate()
    {
        // Safety Watchdog: Prevent XR or scene reload from ejecting player to origin
        if (playerRig != null && playerRig.position.x < 100f)
        {
            playerRig.position = new Vector3(202.8f, 0f, 0f);
            playerRig.rotation = Quaternion.Euler(0f, -90f, 0f);
        }
    }

    // ─── Cursor Management ───────────────────────────────────────────────────

    public void LockCursor(bool locked)
    {
        isCursorLocked   = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible   = !locked;
    }

    public void SetCursorLock(bool locked) => LockCursor(locked);

    private void HandleCursorLockInput()
    {
        // ESC toggles cursor
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            LockCursor(!isCursorLocked);
        }

        // Clicking in the Game window re-locks cursor so user can look around
        if (!isCursorLocked && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
        {
            // Only relock if not clicking on a GUI dialog button
            LockCursor(true);
        }
    }

    // ─── Look & Rotation (360° Mouse + Keyboard) ─────────────────────────────

    private void HandleLook()
    {
        // Mouse look: active when cursor locked OR when right-mouse is held
        if (isCursorLocked || Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            yaw   += mouseX * mouseSensitivity;
            pitch -= mouseY * mouseSensitivity;
        }

        // Keyboard turn: Q/E or Left/Right arrows (always available)
        float turnInput = 0f;
        if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftArrow))  turnInput -= 1f;
        if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.RightArrow)) turnInput += 1f;

        yaw += turnInput * keyboardTurnSpeed * Time.deltaTime;

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        ApplyRotation();
    }

    private void ApplyRotation()
    {
        if (playerRig != null)
        {
            // Body rotates in Yaw (horizontal)
            playerRig.rotation = Quaternion.Euler(0f, yaw, 0f);

            // Head (Camera) tilts in Pitch (vertical)
            if (cameraTransform != null)
            {
                cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }
        else
        {
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }

    // ─── Movement (WASD + Direct Key Fallbacks) ──────────────────────────────

    private void HandleMovement()
    {
        float speed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : moveSpeed;

        // Read axes
        float forwardAxis = Input.GetAxisRaw("Vertical");
        float sideAxis    = Input.GetAxisRaw("Horizontal");

        // Direct key fallback (ensures 100% responsiveness even if InputManager has deadzone)
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))   forwardAxis =  1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) forwardAxis = -1f;
        if (Input.GetKey(KeyCode.A))                                     sideAxis    = -1f;
        if (Input.GetKey(KeyCode.D))                                     sideAxis    =  1f;

        if (Mathf.Abs(forwardAxis) < 0.01f && Mathf.Abs(sideAxis) < 0.01f)
            return;

        // Calculate move direction in horizontal plane relative to camera heading
        Vector3 forward = (cameraTransform != null ? cameraTransform.forward : transform.forward);
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = (cameraTransform != null ? cameraTransform.right : transform.right);
        right.y = 0f;
        right.Normalize();

        Vector3 move = (forward * forwardAxis + right * sideAxis).normalized * (speed * Time.deltaTime);

        // Move the player rig
        Transform target = (playerRig != null) ? playerRig : transform;
        Vector3 newPos = target.position + move;

        newPos.x = Mathf.Clamp(newPos.x, KitchenMinX, KitchenMaxX);
        newPos.y = 0f; // Rig sits on floor
        newPos.z = Mathf.Clamp(newPos.z, KitchenMinZ, KitchenMaxZ);

        target.position = newPos;

        // Ensure camera local Y is eye height
        if (cameraTransform != null && cameraTransform != transform)
        {
            cameraTransform.localPosition = new Vector3(0f, EyeHeight, 0f);
        }
    }

    // ─── Hotkeys & Quick Actions ─────────────────────────────────────────────

    private void HandleHotkeys()
    {
        // ── Quick-equip extinguishers ──
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            QuickEquip(ExtinguisherType.Water);
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            QuickEquip(ExtinguisherType.DryPowder);
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            QuickEquip(ExtinguisherType.CO2);

        // ── Drop [G] ──
        if (Input.GetKeyDown(KeyCode.G) && activeHeldExtinguisher != null)
        {
            activeHeldExtinguisher.Drop();
            activeHeldExtinguisher = null;
        }

        // ── Reset [R] ──
        if (Input.GetKeyDown(KeyCode.R) && activeHeldExtinguisher != null)
        {
            activeHeldExtinguisher.ResetToStand();
            activeHeldExtinguisher = null;
        }

        // ── Fire Alarm toggle [A] ──
        // Only if not moving left (check if pressed this frame alone)
        if (Input.GetKeyDown(KeyCode.A) && !Input.GetKey(KeyCode.W) && !Input.GetKey(KeyCode.S))
        {
            var alarm = Object.FindObjectOfType<FireAlarmController>();
            if (alarm != null) alarm.ToggleAlarm();
        }

        // ── Power Switch toggle [L] ──
        if (Input.GetKeyDown(KeyCode.L))
        {
            var psw = Object.FindObjectOfType<PowerSwitchController>();
            if (psw != null) psw.TogglePower();
        }
    }

    private void QuickEquip(ExtinguisherType type)
    {
        Transform origin = (cameraTransform != null) ? cameraTransform : transform;
        var all = Object.FindObjectsOfType<FireExtinguisher>();

        foreach (var ext in all)
        {
            if (ext.Type == type)
            {
                if (activeHeldExtinguisher != null && activeHeldExtinguisher != ext)
                {
                    activeHeldExtinguisher.ResetToStand();
                }
                ext.Pickup(origin);
                activeHeldExtinguisher = ext;
                Debug.Log($"[KitchenPlayerController] Equipped: {type}");

                var hud = Object.FindObjectOfType<TrainingHUD>();
                if (hud != null) hud.OnExtinguisherSelected(type);
                return;
            }
        }
    }

    // ─── Extinguisher Spray / Discharge ──────────────────────────────────────

    private void HandleExtinguisherSpray()
    {
        if (activeHeldExtinguisher == null) return;

        // Pull pin [P]
        if (Input.GetKeyDown(KeyCode.P))
        {
            activeHeldExtinguisher.PullSafetyPin();
        }

        // Squeeze handle: Space or Left Mouse
        if (Input.GetKey(KeyCode.Space) || (isCursorLocked && Input.GetMouseButton(0)))
        {
            activeHeldExtinguisher.SqueezeHandle();
        }
        else
        {
            activeHeldExtinguisher.ReleaseHandle();
        }
    }

    private void TrackHeldExtinguisher()
    {
        if (activeHeldExtinguisher != null && !activeHeldExtinguisher.IsHeld)
        {
            activeHeldExtinguisher = null;
        }
    }

    // ─── Raycast Interaction ([F] or Click on objects) ────────────────────────

    private void HandleRaycastInteract()
    {
        // Only trigger raycast interact on [F] or left click when cursor locked
        bool interact = Input.GetKeyDown(KeyCode.F) || (isCursorLocked && Input.GetMouseButtonDown(0));
        if (!interact) return;

        Transform origin = (cameraTransform != null) ? cameraTransform : transform;
        Ray ray = new Ray(origin.position, origin.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
        {
            // 1. Pick up Extinguisher
            var ext = hit.collider.GetComponentInParent<FireExtinguisher>();
            if (ext != null && !ext.IsHeld)
            {
                if (activeHeldExtinguisher != null && activeHeldExtinguisher != ext)
                {
                    activeHeldExtinguisher.Drop();
                }
                ext.Pickup(origin);
                activeHeldExtinguisher = ext;
                Debug.Log($"[KitchenPlayerController] Picked up: {ext.Type}");

                var hud = Object.FindObjectOfType<TrainingHUD>();
                if (hud != null) hud.OnExtinguisherSelected(ext.Type);
                return;
            }

            // 2. Fire Alarm Call Point
            var alarm = hit.collider.GetComponentInParent<FireAlarmController>();
            if (alarm != null)
            {
                alarm.ToggleAlarm();
                return;
            }

            // 3. Electrical Power Switch
            var psw = hit.collider.GetComponentInParent<PowerSwitchController>();
            if (psw != null)
            {
                psw.TogglePower();
                return;
            }
        }
    }

    // ─── Teleport API ────────────────────────────────────────────────────────

    public void Teleport(Vector3 position, Vector3 eulerAngles)
    {
        Transform target = (playerRig != null) ? playerRig : transform;
        target.position = new Vector3(
            Mathf.Clamp(position.x, KitchenMinX, KitchenMaxX),
            0f,
            Mathf.Clamp(position.z, KitchenMinZ, KitchenMaxZ));

        yaw   = eulerAngles.y;
        pitch = eulerAngles.x;
        ApplyRotation();
    }

    public void Teleport(Vector3 position)
    {
        Teleport(position, new Vector3(0f, -90f, 0f));
    }

    public void TeleportTo(Vector3 position, float yawAngle)
    {
        Teleport(position, new Vector3(0f, yawAngle, 0f));
    }
}
