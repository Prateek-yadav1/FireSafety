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
/// Movement: WASD moves the VR_Player_Rig in XZ plane within current room bounds.
/// Look:     Mouse X rotates the rig (yaw). Mouse Y tilts the camera (pitch).
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

    public bool IsInKitchen => _rig != null && _rig.position.x > 100f;

    // References
    private Transform _rig;        // VR_Player_Rig — holds yaw, moves in XZ
    private Transform _cam;        // Main Camera child — holds pitch at eye height

    // State
    private float _yaw   = 0f;     // starts at 0° (facing forward toward Room 01 table)
    private float _pitch = 0f;
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
        Debug.Log($"[KitchenPlayerController] Ready. VR={_isVR}, InKitchen={IsInKitchen}. Rig pos={(_rig != null ? _rig.position.ToString() : "null")}");
    }

    private void CheckVRMode()
    {
        _isVR = UnityEngine.XR.XRSettings.isDeviceActive;
    }

    private void BuildRigHierarchy()
    {
        GameObject rigGO = GameObject.Find("VR_Player_Rig");
        if (rigGO == null)
        {
            rigGO = new GameObject("VR_Player_Rig");
            rigGO.transform.position = Vector3.zero; // Starts in Room 01
            rigGO.transform.rotation = Quaternion.identity;
        }
        _rig = rigGO.transform;

        Camera mainCam = Camera.main;
        if (mainCam == null) mainCam = GetComponent<Camera>();
        if (mainCam == null) mainCam = FindObjectOfType<Camera>();

        if (mainCam != null)
        {
            _cam = mainCam.transform;

            if (_cam.parent != _rig)
            {
                _cam.SetParent(_rig, false);
                _cam.localPosition = new Vector3(0f, EyeHeight, IsInKitchen ? 0f : 0.45f);
                _cam.localRotation = Quaternion.identity;
            }
        }

        _yaw   = _rig.eulerAngles.y;
        _pitch = 0f;
        ApplyRotation();
    }

    // ─── Update Loop ──────────────────────────────────────────────────────────

    private void Update()
    {
        CheckVRMode();
        if (_isVR) return; // VRControllerManager handles VR locomotion

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
        if (_rig != null && _rig.position.y < -5f)
        {
            _rig.position = IsInKitchen ? new Vector3(202.8f, 0f, 0f) : Vector3.zero;
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
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            LockCursor(!_cursorLocked);
            return;
        }
        if (!_cursorLocked && Input.GetMouseButtonDown(0))
        {
            LockCursor(true);
        }
    }

    // ─── Look (360° Yaw + Pitch) ──────────────────────────────────────────────

    private void HandleLook()
    {
        if (_cursorLocked || Input.GetMouseButton(1))
        {
            float mx = Input.GetAxis("Mouse X");
            float my = Input.GetAxis("Mouse Y");

            _yaw   += mx * mouseSensitivity;
            _pitch -= my * mouseSensitivity;
        }

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
        if (_cam != null)
        {
            _cam.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }
    }

    // ─── Movement (WASD) ──────────────────────────────────────────────────────

    private void HandleMovement()
    {
        if (_rig == null) return;

        float speed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : moveSpeed;

        float fwd  = 0f;
        float side = 0f;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))   fwd  =  1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) fwd  = -1f;
        if (Input.GetKey(KeyCode.A))                                     side = -1f;
        if (Input.GetKey(KeyCode.D))                                     side =  1f;

        if (Mathf.Approximately(fwd, 0f))  fwd  = Input.GetAxisRaw("Vertical");
        if (Mathf.Approximately(side, 0f)) side = Input.GetAxisRaw("Horizontal");

        if (Mathf.Abs(fwd) < 0.01f && Mathf.Abs(side) < 0.01f) return;

        Vector3 camFwd   = _cam != null ? _cam.forward : _rig.forward;
        Vector3 camRight = _cam != null ? _cam.right   : _rig.right;

        camFwd.y = 0f;  camFwd.Normalize();
        camRight.y = 0f; camRight.Normalize();

        Vector3 move = (camFwd * fwd + camRight * side).normalized * (speed * Time.deltaTime);
        Vector3 newPos = _rig.position + move;

        // Clamp based on current room
        if (IsInKitchen)
        {
            newPos.x = Mathf.Clamp(newPos.x, KitchenMinX, KitchenMaxX);
            newPos.y = 0f;
            newPos.z = Mathf.Clamp(newPos.z, KitchenMinZ, KitchenMaxZ);
        }
        else
        {
            newPos.x = Mathf.Clamp(newPos.x, Room01MinX, Room01MaxX);
            newPos.y = 0f;
            newPos.z = Mathf.Clamp(newPos.z, Room01MinZ, Room01MaxZ);
        }

        _rig.position = newPos;
    }

    // ─── Interaction Hotkeys ──────────────────────────────────────────────────

    private void HandleHotkeys()
    {
        // Extinguishers
        if (Input.GetKeyDown(KeyCode.Alpha1)) QuickEquip(ExtinguisherType.Water);
        if (Input.GetKeyDown(KeyCode.Alpha2)) QuickEquip(ExtinguisherType.DryPowder);
        if (Input.GetKeyDown(KeyCode.Alpha3)) QuickEquip(ExtinguisherType.CO2);

        // [A] Fire Alarm (only when in kitchen)
        if (IsInKitchen && Input.GetKeyDown(KeyCode.A) && !Input.GetKey(KeyCode.LeftShift))
        {
            var alarm = FindObjectOfType<FireAlarmController>();
            if (alarm != null) alarm.ToggleAlarm();
        }

        // [L] Power Switch
        if (IsInKitchen && Input.GetKeyDown(KeyCode.L))
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
    }

    // ─── Extinguisher Spray ───────────────────────────────────────────────────

    private void HandleExtinguisherSpray()
    {
        if (_heldExt == null) return;

        if (Input.GetKeyDown(KeyCode.P))
            _heldExt.PullSafetyPin();

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
        if (_rig == null) return;

        Vector3 safe;
        if (position.x > 100f)
        {
            // Kitchen
            safe = new Vector3(
                Mathf.Clamp(position.x, KitchenMinX, KitchenMaxX),
                0f,
                Mathf.Clamp(position.z, KitchenMinZ, KitchenMaxZ));
        }
        else
        {
            // Room 01
            safe = new Vector3(
                Mathf.Clamp(position.x, Room01MinX, Room01MaxX),
                0f,
                Mathf.Clamp(position.z, Room01MinZ, Room01MaxZ));
        }

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
