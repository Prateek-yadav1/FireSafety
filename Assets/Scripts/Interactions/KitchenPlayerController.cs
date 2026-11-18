using UnityEngine;
using FireSafety.Extinguishers;
using FireSafety.Fire;
using FireSafety.Training;
using UnityEngine.XR;

/// <summary>
/// KitchenPlayerController — Desktop (WASD + 360° mouse look) controller.
/// In VR mode, locomotion and look are fully handled by VRControllerManager.
/// This script only handles Desktop mode; it gracefully yields in VR.
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
    private float _yaw   = 0f;
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
        _isVR = XRSettings.isDeviceActive;
        BuildRigHierarchy();
        if (!_isVR) LockCursor(true);
        Debug.Log($"[KitchenPlayerController] Started. VR={_isVR}, InKitchen={IsInKitchen}, Rig={_rig?.position}");
    }

    private void BuildRigHierarchy()
    {
        Camera mainCam = GetComponent<Camera>();
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) mainCam = FindObjectOfType<Camera>();

        if (mainCam != null)
            _cam = mainCam.transform;
        else
            _cam = transform;

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
            // VR: HMD reports head Y position natively — do NOT add fixed EyeHeight offset (causes camera on roof!)
            // Desktop: set fixed eye height for WASD+mouse play
            if (XRSettings.isDeviceActive)
                _cam.localPosition = new Vector3(0f, 0f, IsInKitchen ? 0f : 0.45f);
            else
                _cam.localPosition = new Vector3(0f, EyeHeight, IsInKitchen ? 0f : 0.45f);
            _cam.localRotation = Quaternion.identity;
        }

        _yaw   = _rig.eulerAngles.y;
        _pitch = 0f;

        // Only apply desktop rotation on startup if NOT in VR
        if (!XRSettings.isDeviceActive)
            ApplyRotation();
    }

    // ─── Update Loop ──────────────────────────────────────────────────────────

    private void Update()
    {
        _isVR = XRSettings.isDeviceActive;

        // In VR mode, skip ALL keyboard/mouse input — VRControllerManager handles everything
        if (_isVR) return;

        HandleCursorLock();
        HandleLook();
        HandleMovement();
        HandleDesktopInteraction();
    }

    private void LateUpdate()
    {
        // Safety watchdog: prevent falling below floor
        Transform target = _rig != null ? _rig : transform;
        if (target.position.y < -5f)
        {
            Vector3 p = target.position;
            p.y = 0f;
            target.position = p;
        }
    }

    // ─── Cursor Lock ──────────────────────────────────────────────────────────

    private void HandleCursorLock()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            LockCursor(false);
        else if (Input.GetMouseButtonDown(0) && !_cursorLocked)
            LockCursor(true);
    }

    private void LockCursor(bool locked)
    {
        _cursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible   = !locked;
    }

    // ─── Desktop Look ─────────────────────────────────────────────────────────

    private void HandleLook()
    {
        // Mouse look (Desktop only)
        if (_cursorLocked)
        {
            _yaw   += Input.GetAxis("Mouse X") * mouseSensitivity;
            _pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            _pitch  = Mathf.Clamp(_pitch, minPitch, maxPitch);
        }

        // Keyboard turn fallback (Desktop only)
        float keyTurn = 0f;
        if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftArrow))  keyTurn = -1f;
        if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.RightArrow)) keyTurn =  1f;
        _yaw += keyTurn * keyboardTurnSpeed * Time.deltaTime;

        ApplyRotation();
    }

    private void ApplyRotation()
    {
        if (_rig != null)
            _rig.localRotation = Quaternion.Euler(0f, _yaw, 0f);
        if (_cam != null)
            _cam.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    // ─── Desktop Movement ─────────────────────────────────────────────────────

    private void HandleMovement()
    {
        bool sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        float speed = sprint ? sprintSpeed : moveSpeed;

        float h = Input.GetAxis("Horizontal"); // A/D or Left/Right
        float v = Input.GetAxis("Vertical");   // W/S or Up/Down

        if (Mathf.Abs(h) < 0.01f && Mathf.Abs(v) < 0.01f) return;

        Transform anchor = _rig != null ? _rig : transform;
        Vector3 forward  = anchor.forward;
        Vector3 right    = anchor.right;
        forward.y = 0f; right.y = 0f;
        forward.Normalize(); right.Normalize();

        Vector3 move = (forward * v + right * h).normalized * speed * Time.deltaTime;
        Vector3 newPos = anchor.position + move;

        // Clamp to room bounds
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
        newPos.y = 0f;
        anchor.position = newPos;
    }

    // ─── Desktop-only Interaction (F key / mouse click) ───────────────────────

    private void HandleDesktopInteraction()
    {
        // Extinguisher spray via mouse (desktop fallback only)
        if (_heldExt != null)
        {
            if (_cursorLocked && Input.GetMouseButton(0))
                _heldExt.SqueezeHandle();
            else
                _heldExt.ReleaseHandle();
        }

        // Raycast interact: F key or left click
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
        if (!_isVR) ApplyRotation();

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
