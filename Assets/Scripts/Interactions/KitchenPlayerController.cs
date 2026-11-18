using UnityEngine;
using FireSafety.Extinguishers;
using FireSafety.Fire;
using FireSafety.Training;

/// <summary>
/// KitchenPlayerController — Rock-solid Desktop (WASD + 360° mouse look) controller.
///
/// Architecture:
///   VR_Player_Rig (at kitchen floor level, holds yaw rotation)
///     └── Main Camera  (local Y=1.6m eye height, holds pitch rotation)
///
/// Movement: WASD moves the VR_Player_Rig in XZ plane.
/// Look:     Mouse X rotates the rig (yaw). Mouse Y tilts the camera (pitch).
/// Bounds:   Player cannot walk through kitchen walls.
///
/// When VR (Meta Quest) is active, this script skips all input handling —
/// VRControllerManager + VRCameraAnchor handle VR locomotion and position.
/// </summary>
public class KitchenPlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed   = 3.2f;
    [SerializeField] private float sprintSpeed = 5.8f;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 2.5f;
    [SerializeField] private float keyboardTurnSpeed = 85f;
    [SerializeField] private float minPitch = -75f;
    [SerializeField] private float maxPitch =  80f;

    [Header("Interaction")]
    [SerializeField] private float interactionDistance = 4.5f;

    // Kitchen walkable area (world space)
    public const float KitchenMinX = 199.5f;
    public const float KitchenMaxX = 205.2f;
    public const float KitchenMinZ = -3.4f;
    public const float KitchenMaxZ =  3.4f;
    public const float EyeHeight   = 1.60f;
    public const float KitchenRigX = 202.8f;

    // References
    private Transform _rig;        // VR_Player_Rig — holds yaw, moves in XZ
    private Transform _cam;        // Main Camera child — holds pitch at eye height

    // State
    private float _yaw   = -90f;   // start facing -X direction (toward stove)
    private float _pitch =   0f;
    private bool  _cursorLocked = false;
    private bool  _isVR = false;

    // Held extinguisher
    private FireExtinguisher _heldExt = null;
    public  FireExtinguisher HeldExtinguisher => _heldExt;
    public  Transform        PlayerRoot       => _rig != null ? _rig : transform;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        BuildRigHierarchy();
    }

    private void Start()
    {
        BuildRigHierarchy();
        CheckVRMode();
        if (!_isVR)
        {
            LockCursor(true);
        }
        Debug.Log($"[KitchenPlayerController] Ready. VR={_isVR}. Rig pos={(_rig != null ? _rig.position.ToString() : "null")}");
    }

    private void CheckVRMode()
    {
#if !UNITY_EDITOR
        _isVR = UnityEngine.XR.XRSettings.isDeviceActive;
#else
        _isVR = UnityEngine.XR.XRSettings.isDeviceActive;
#endif
    }

    private void BuildRigHierarchy()
    {
        // Find or create VR_Player_Rig
        GameObject rigGO = GameObject.Find("VR_Player_Rig");
        if (rigGO == null)
        {
            rigGO = new GameObject("VR_Player_Rig");
        }
        _rig = rigGO.transform;

        // Always ensure rig is AT kitchen level (not at origin)
        if (_rig.position.x < 100f)
        {
            _rig.position = new Vector3(KitchenRigX, 0f, 0f);
        }

        // Find Main Camera
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            // Check if this GO has a Camera
            mainCam = GetComponent<Camera>();
        }
        if (mainCam == null)
        {
            mainCam = FindObjectOfType<Camera>();
        }

        if (mainCam != null)
        {
            _cam = mainCam.transform;

            // Parent camera to rig IF not already correctly set
            if (_cam.parent != _rig)
            {
                // Before re-parenting, preserve world position to avoid jump
                Vector3 worldPos = _cam.position;

                _cam.SetParent(_rig, false);

                // Camera sits at eye height relative to rig
                // If the camera was at a reasonable world position, use it
                float localY = worldPos.y > 0.5f ? Mathf.Clamp(worldPos.y, 1.0f, 2.5f) : EyeHeight;
                _cam.localPosition = new Vector3(0f, localY, 0f);
                _cam.localRotation = Quaternion.identity;
            }
        }

        // Set initial yaw to match current rig rotation
        _yaw   = _rig.eulerAngles.y;
        _pitch = 0f;
        ApplyRotation();
    }

    // ─── Update Loop ──────────────────────────────────────────────────────────

    private void Update()
    {
        // Keep VR state updated
        CheckVRMode();

        // VR mode: skip all desktop input (VRControllerManager handles it)
        if (_isVR) return;

        HandleCursorLock();
        HandleLook();
        HandleMovement();
        HandleHotkeys();
        HandleExtinguisherSpray();
        TrackHeldExtinguisher();

        if (_heldExt == null)
            HandleRaycastInteract();
    }

    private void LateUpdate()
    {
        // Safety watchdog: if rig drifted away from kitchen, snap back
        if (_rig != null && _rig.position.x < 100f && !_isVR)
        {
            _rig.position = new Vector3(KitchenRigX, 0f, 0f);
            Debug.LogWarning("[KitchenPlayerController] Watchdog: Rig was at origin — snapped to kitchen.");
        }
    }

    // ─── Cursor ───────────────────────────────────────────────────────────────

    public void LockCursor(bool locked)
    {
        _cursorLocked    = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible   = !locked;
    }

    private void HandleCursorLock()
    {
        // ESC unlocks cursor
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            LockCursor(!_cursorLocked);
            return;
        }
        // Clicking in game window re-locks
        if (!_cursorLocked && Input.GetMouseButtonDown(0))
        {
            LockCursor(true);
        }
    }

    // ─── Look (360° Yaw + Pitch) ──────────────────────────────────────────────

    private void HandleLook()
    {
        // Mouse look: active when cursor locked OR right mouse held
        if (_cursorLocked || Input.GetMouseButton(1))
        {
            float mx = Input.GetAxis("Mouse X");
            float my = Input.GetAxis("Mouse Y");

            _yaw   += mx * mouseSensitivity;
            _pitch -= my * mouseSensitivity;   // subtract = look up when moving mouse up
        }

        // Keyboard turn: Q/E or Left/Right arrows
        float kbTurn = 0f;
        if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftArrow))  kbTurn -= 1f;
        if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.RightArrow)) kbTurn += 1f;
        _yaw += kbTurn * keyboardTurnSpeed * Time.deltaTime;

        _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
        ApplyRotation();
    }

    private void ApplyRotation()
    {
        if (_rig != null)
        {
            // Rig holds yaw (horizontal rotation)
            _rig.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }
        if (_cam != null)
        {
            // Camera holds pitch (vertical tilt) as LOCAL rotation
            _cam.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }
    }

    // ─── Movement (WASD) ──────────────────────────────────────────────────────

    private void HandleMovement()
    {
        if (_rig == null) return;

        float speed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : moveSpeed;

        // Collect movement input — direct KeyCode + Axis for maximum responsiveness
        float fwd  = 0f;
        float side = 0f;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))   fwd  =  1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) fwd  = -1f;
        if (Input.GetKey(KeyCode.A))                                     side = -1f;
        if (Input.GetKey(KeyCode.D))                                     side =  1f;

        // Fallback to axis if direct keys have no input
        if (Mathf.Approximately(fwd, 0f))  fwd  = Input.GetAxisRaw("Vertical");
        if (Mathf.Approximately(side, 0f)) side = Input.GetAxisRaw("Horizontal");

        if (Mathf.Abs(fwd) < 0.01f && Mathf.Abs(side) < 0.01f) return;

        // Movement direction is relative to camera's HORIZONTAL heading
        Vector3 camFwd   = _cam != null ? _cam.forward : _rig.forward;
        Vector3 camRight = _cam != null ? _cam.right   : _rig.right;

        // Flatten to horizontal plane
        camFwd.y = 0f;  camFwd.Normalize();
        camRight.y = 0f; camRight.Normalize();

        Vector3 move = (camFwd * fwd + camRight * side).normalized * (speed * Time.deltaTime);

        Vector3 newPos = _rig.position + move;
        // Clamp within kitchen walls
        newPos.x = Mathf.Clamp(newPos.x, KitchenMinX, KitchenMaxX);
        newPos.y = 0f;
        newPos.z = Mathf.Clamp(newPos.z, KitchenMinZ, KitchenMaxZ);

        _rig.position = newPos;
    }

    // ─── Interaction Hotkeys ──────────────────────────────────────────────────

    private void HandleHotkeys()
    {
        // [1] Water, [2] Dry Powder, [3] CO2
        if (Input.GetKeyDown(KeyCode.Alpha1)) QuickEquip(ExtinguisherType.Water);
        if (Input.GetKeyDown(KeyCode.Alpha2)) QuickEquip(ExtinguisherType.DryPowder);
        if (Input.GetKeyDown(KeyCode.Alpha3)) QuickEquip(ExtinguisherType.CO2);

        // [A] Fire Alarm (only when not pressing [A] for movement — A is left-strafe)
        // We use GetKeyDown carefully to avoid conflict: A is for strafing in HandleMovement,
        // but [A] alarm uses GetKeyDown which only fires once
        if (Input.GetKeyDown(KeyCode.A) && !Input.GetKey(KeyCode.LeftShift))
        {
            var alarm = FindObjectOfType<FireAlarmController>();
            if (alarm != null) alarm.ToggleAlarm();
        }

        // [L] Power Switch
        if (Input.GetKeyDown(KeyCode.L))
        {
            var psw = FindObjectOfType<PowerSwitchController>();
            if (psw != null) psw.TogglePower();
        }

        // [T] Debug-Skip current objective
        if (Input.GetKeyDown(KeyCode.T))
        {
            var hud = FindObjectOfType<TrainingHUD>();
            if (hud != null) hud.DebugSkipObjective();
        }
    }

    private void QuickEquip(ExtinguisherType type)
    {
        Transform hand = _cam != null ? _cam : transform;
        var all = FindObjectsOfType<FireExtinguisher>();

        foreach (var ext in all)
        {
            if (ext.Type == type)
            {
                if (_heldExt != null && _heldExt != ext)
                    _heldExt.Drop();

                ext.Pickup(hand);
                _heldExt = ext;

                var hud = FindObjectOfType<TrainingHUD>();
                if (hud != null) hud.OnExtinguisherSelected(type);

                Debug.Log($"[KitchenPlayerController] Equipped: {type}");
                return;
            }
        }
        Debug.LogWarning($"[KitchenPlayerController] No {type} extinguisher found in scene.");
    }

    // ─── Extinguisher Spray ───────────────────────────────────────────────────

    private void HandleExtinguisherSpray()
    {
        if (_heldExt == null) return;

        // Pull pin [P]
        if (Input.GetKeyDown(KeyCode.P))
            _heldExt.PullSafetyPin();

        // Squeeze handle: Space or Left Mouse Button (when cursor locked)
        bool squeezing = Input.GetKey(KeyCode.Space) ||
                         (_cursorLocked && Input.GetMouseButton(0));

        if (squeezing)
            _heldExt.SqueezeHandle();
        else
            _heldExt.ReleaseHandle();
    }

    private void TrackHeldExtinguisher()
    {
        if (_heldExt != null && !_heldExt.IsHeld)
            _heldExt = null;
    }

    // ─── Raycast Interaction ──────────────────────────────────────────────────

    private void HandleRaycastInteract()
    {
        bool doInteract = Input.GetKeyDown(KeyCode.F) ||
                          (_cursorLocked && Input.GetMouseButtonDown(0));
        if (!doInteract) return;

        Transform origin = _cam != null ? _cam : transform;
        Ray ray = new Ray(origin.position, origin.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance)) return;

        // Pick up extinguisher
        var ext = hit.collider.GetComponentInParent<FireExtinguisher>();
        if (ext != null && !ext.IsHeld)
        {
            if (_heldExt != null) _heldExt.Drop();
            ext.Pickup(origin);
            _heldExt = ext;

            var hud = FindObjectOfType<TrainingHUD>();
            if (hud != null) hud.OnExtinguisherSelected(ext.Type);
            return;
        }

        // Fire alarm
        var alarm = hit.collider.GetComponentInParent<FireAlarmController>();
        if (alarm != null) { alarm.ToggleAlarm(); return; }

        // Power switch
        var psw = hit.collider.GetComponentInParent<PowerSwitchController>();
        if (psw != null) { psw.TogglePower(); return; }
    }

    // ─── Teleport API ─────────────────────────────────────────────────────────

    /// <summary>Teleport the player rig to a world position with optional yaw angle.</summary>
    public void Teleport(Vector3 position, float yaw = -90f)
    {
        if (_rig == null) BuildRigHierarchy();
        if (_rig == null) return;

        Vector3 safe = new Vector3(
            Mathf.Clamp(position.x, KitchenMinX, KitchenMaxX),
            0f,
            Mathf.Clamp(position.z, KitchenMinZ, KitchenMaxZ));

        _rig.position = safe;
        _yaw   = yaw;
        _pitch = 0f;
        ApplyRotation();

        Debug.Log($"[KitchenPlayerController] Teleported to {safe}, yaw={yaw}°");
    }

    public void Teleport(Vector3 position, Vector3 eulerAngles)
    {
        Teleport(position, eulerAngles.y);
    }

    public void TeleportTo(Vector3 position, float yawAngle)
    {
        Teleport(position, yawAngle);
    }
}
