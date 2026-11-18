using UnityEngine;
using FireSafety.Extinguishers;
using FireSafety.Fire;
using FireSafety.Training;

/// <summary>
/// KitchenPlayerController — Rock-solid Desktop (WASD + 360° mouse look) controller.
/// Supports both Room 01 (Orientation Studio at 0,0,0) and Room 02 (Kitchen at 202.8,0,0).
///
/// Architecture:
///   VR_Player_Rig (at floor level, holds yaw rotation)
///     └── Main Camera  (local Y=1.55m-1.6m eye height, holds pitch rotation)
///
/// Movement: WASD moves in the horizontal XZ plane within current room bounds.
/// Look:     Mouse X rotates Yaw (360°), Mouse Y tilts Pitch (-75° to +80°).
///           Q/E and Arrow keys provide guaranteed keyboard turning backup.
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

    // Room 01: Orientation Studio bounds
    public const float Room01MinX = -3.2f;
    public const float Room01MaxX =  3.2f;
    public const float Room01MinZ = -1.2f;
    public const float Room01MaxZ =  4.8f;

    // Room 02: Kitchen walkable area (world space)
    public const float KitchenMinX = 199.5f;
    public const float KitchenMaxX = 205.2f;
    public const float KitchenMinZ = -3.4f;
    public const float KitchenMaxZ =  3.4f;
    public const float EyeHeight   = 1.55f;

    public bool IsInKitchen => (_rig != null ? _rig.position.x : transform.position.x) > 100f;

    // References
    private Transform _rig;        // VR_Player_Rig — holds yaw, moves in XZ
    private Transform _cam;        // Main Camera child — holds pitch at eye height

    // State
    private float _yaw   = 0f;     // starts facing forward (+Z in Room 01, -X in Kitchen)
    private float _pitch = 0f;
    private bool  _cursorLocked = false;

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
        LockCursor(true);
        Debug.Log($"[KitchenPlayerController] Started. InKitchen={IsInKitchen}, Rig={_rig?.position}");
    }

    private void BuildRigHierarchy()
    {
        Camera mainCam = GetComponent<Camera>();
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) mainCam = FindObjectOfType<Camera>();

        if (mainCam != null)
        {
            _cam = mainCam.transform;
        }
        else
        {
            _cam = transform;
        }

        GameObject rigGO = GameObject.Find("VR_Player_Rig");
        if (rigGO == null)
        {
            rigGO = new GameObject("VR_Player_Rig");
            rigGO.transform.position = IsInKitchen ? new Vector3(202.8f, 0f, 0f) : Vector3.zero;
            rigGO.transform.rotation = IsInKitchen ? Quaternion.Euler(0f, -90f, 0f) : Quaternion.identity;
        }
        _rig = rigGO.transform;

        if (_cam != null && _cam.parent != _rig)
        {
            _cam.SetParent(_rig, false);
            _cam.localPosition = new Vector3(0f, EyeHeight, IsInKitchen ? 0f : 0.45f);
            _cam.localRotation = Quaternion.identity;
        }

        _yaw   = _rig.eulerAngles.y;
        _pitch = 0f;
        ApplyRotation();
    }

    // ─── Update Loop ──────────────────────────────────────────────────────────

    private void Update()
    {
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
        // Safety watchdog: prevent falling below floor
        Transform target = _rig != null ? _rig : transform;
        if (target.position.y < -5f)
        {
            target.position = IsInKitchen ? new Vector3(202.8f, 0f, 0f) : Vector3.zero;
        }
    }

    // ─── Cursor Management ───────────────────────────────────────────────────

    public void LockCursor(bool locked)
    {
        _cursorLocked    = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible   = !locked;
    }

    private void HandleCursorLock()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            LockCursor(!_cursorLocked);
            return;
        }

        // Clicking into game view locks cursor for smooth mouse look
        if (!_cursorLocked && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
        {
            LockCursor(true);
        }
    }

    // ─── 360° Look (Mouse + Keyboard Q/E) ────────────────────────────────────

    private void HandleLook()
    {
        bool isVR = UnityEngine.XR.XRSettings.isDeviceActive;

        // Mouse look is active for desktop play when cursor is locked or mouse buttons held
        if (!isVR && (_cursorLocked || Input.GetMouseButton(0) || Input.GetMouseButton(1)))
        {
            float mx = Input.GetAxis("Mouse X");
            float my = Input.GetAxis("Mouse Y");

            _yaw   += mx * mouseSensitivity;
            _pitch -= my * mouseSensitivity;
        }

        // Keyboard turn: Q/E or Left/Right arrows always active as backup
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
            _rig.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        // In VR, the Meta Quest HMD natively drives 6-DoF camera rotation (Pitch, Yaw, Roll).
        // Overwriting localRotation breaks physical head tracking! Only set it on desktop.
        bool isVR = UnityEngine.XR.XRSettings.isDeviceActive;
        if (!isVR)
        {
            if (_cam != null)
            {
                _cam.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }
            else
            {
                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }
        }
    }

    // ─── Movement (WASD + Arrow Keys) ─────────────────────────────────────────

    private void HandleMovement()
    {
        float speed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : moveSpeed;

        float fwd  = 0f;
        float side = 0f;

        // Direct KeyCode checks (guarantees 100% responsiveness)
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))   fwd  += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) fwd  -= 1f;
        if (Input.GetKey(KeyCode.A))                                     side -= 1f;
        if (Input.GetKey(KeyCode.D))                                     side += 1f;

        // Add standard Axis input
        fwd  += Input.GetAxisRaw("Vertical");
        side += Input.GetAxisRaw("Horizontal");

        fwd  = Mathf.Clamp(fwd, -1f, 1f);
        side = Mathf.Clamp(side, -1f, 1f);

        if (Mathf.Abs(fwd) < 0.01f && Mathf.Abs(side) < 0.01f) return;

        // Move relative to camera horizontal heading
        Transform basis = _cam != null ? _cam : (_rig != null ? _rig : transform);
        Vector3 camFwd   = basis.forward;
        Vector3 camRight = basis.right;

        camFwd.y = 0f;   camFwd.Normalize();
        camRight.y = 0f; camRight.Normalize();

        Vector3 move = (camFwd * fwd + camRight * side).normalized * (speed * Time.deltaTime);

        Transform targetToMove = _rig != null ? _rig : transform;
        Vector3 newPos = targetToMove.position + move;

        // Clamp within current room boundaries
        if (IsInKitchen)
        {
            newPos.x = Mathf.Clamp(newPos.x, KitchenMinX, KitchenMaxX);
            newPos.z = Mathf.Clamp(newPos.z, KitchenMinZ, KitchenMaxZ);
        }
        else
        {
            newPos.x = Mathf.Clamp(newPos.x, Room01MinX, Room01MaxX);
            newPos.z = Mathf.Clamp(newPos.z, Room01MinZ, Room01MaxZ);
        }

        targetToMove.position = new Vector3(newPos.x, targetToMove.position.y, newPos.z);
    }

    // ─── Hotkeys & Interaction ────────────────────────────────────────────────

    private void HandleHotkeys()
    {
        // Extinguishers [1], [2], [3]
        if (Input.GetKeyDown(KeyCode.Alpha1)) QuickEquip(ExtinguisherType.Water);
        if (Input.GetKeyDown(KeyCode.Alpha2)) QuickEquip(ExtinguisherType.DryPowder);
        if (Input.GetKeyDown(KeyCode.Alpha3)) QuickEquip(ExtinguisherType.CO2);

        // Fire Alarm toggle [A] (only fires on new key down when in kitchen and not strafing)
        if (IsInKitchen && Input.GetKeyDown(KeyCode.A) && !Input.GetKey(KeyCode.LeftShift))
        {
            var alarm = FindObjectOfType<FireAlarmController>();
            if (alarm != null) alarm.ToggleAlarm();
        }

        // Power Switch toggle [L]
        if (IsInKitchen && Input.GetKeyDown(KeyCode.L))
        {
            var psw = FindObjectOfType<PowerSwitchController>();
            if (psw != null) psw.TogglePower();
        }

        // Debug-Skip current objective [T]
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
    }

    // ─── Extinguisher Spray ───────────────────────────────────────────────────

    private void HandleExtinguisherSpray()
    {
        if (_heldExt == null) return;

        // Pull pin [P]
        if (Input.GetKeyDown(KeyCode.P))
            _heldExt.PullSafetyPin();

        // Squeeze handle: Space or Left Mouse
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

    // ─── Raycast Interaction ([F] or Click on objects) ────────────────────────

    private void HandleRaycastInteract()
    {
        bool doInteract = Input.GetKeyDown(KeyCode.F) ||
                          (_cursorLocked && Input.GetMouseButtonDown(0));
        if (!doInteract) return;

        Transform origin = _cam != null ? _cam : transform;
        Ray ray = new Ray(origin.position, origin.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance)) return;

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

        var alarm = hit.collider.GetComponentInParent<FireAlarmController>();
        if (alarm != null) { alarm.ToggleAlarm(); return; }

        var psw = hit.collider.GetComponentInParent<PowerSwitchController>();
        if (psw != null) { psw.TogglePower(); return; }
    }

    // ─── Teleport API ─────────────────────────────────────────────────────────

    public void Teleport(Vector3 position, float yaw = 0f)
    {
        if (_rig == null) BuildRigHierarchy();
        Transform target = _rig != null ? _rig : transform;

        Vector3 safe;
        if (position.x > 100f)
        {
            safe = new Vector3(
                Mathf.Clamp(position.x, KitchenMinX, KitchenMaxX),
                0f,
                Mathf.Clamp(position.z, KitchenMinZ, KitchenMaxZ));
        }
        else
        {
            safe = new Vector3(
                Mathf.Clamp(position.x, Room01MinX, Room01MaxX),
                0f,
                Mathf.Clamp(position.z, Room01MinZ, Room01MaxZ));
        }

        target.position = safe;
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
