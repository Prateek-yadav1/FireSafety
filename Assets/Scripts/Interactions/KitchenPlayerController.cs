using UnityEngine;
using UnityEngine.XR;
using FireSafety.Extinguishers;
using FireSafety.Fire;
using FireSafety.Interactions;

/// <summary>
/// Dual Desktop & VR Walkthrough Controller.
/// Uses a robust Player Rig architecture so Unity's XR stereo eye driver
/// NEVER blocks WASD movement or 360-degree rotation!
/// </summary>
public class KitchenPlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3.0f;
    [SerializeField] private float sprintSpeed = 5.2f;

    [Header("Mouse & Keyboard Look")]
    [SerializeField] private float mouseSensitivity = 3.0f;
    [SerializeField] private float keyboardTurnSpeed = 75f;
    [SerializeField] private float maxPitch = 82f;
    [SerializeField] private float minPitch = -78f;

    [Header("Interaction Raycast")]
    [SerializeField] private float interactionDistance = 4.5f;

    private float pitch = 0f;
    private float yaw = -90f; // Default facing stove
    private bool isCursorLocked = true;
    private FireExtinguisher activeHeldExtinguisher;

    // Rig hierarchy references
    private Transform playerRoot;
    private Transform cameraTransform;

    // Kitchen boundary box
    private const float KitchenMinX = 199.0f;
    private const float KitchenMaxX = 205.8f;
    private const float KitchenMinZ = -3.8f;
    private const float KitchenMaxZ = 3.8f;
    private const float PlayerEyeHeight = 1.60f;

    public Transform PlayerRoot => playerRoot != null ? playerRoot : transform;

    private void Awake()
    {
        EnsurePlayerRig();
    }

    private void EnsurePlayerRig()
    {
        Camera cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;

        if (cam != null)
        {
            cameraTransform = cam.transform;

            // If camera is at top-level without a parent rig, wrap it in a Player Rig
            if (cameraTransform.parent == null)
            {
                GameObject rigGO = new GameObject("VR_Player_Rig");
                rigGO.transform.position = new Vector3(cameraTransform.position.x, 0f, cameraTransform.position.z);
                rigGO.transform.rotation = Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f);

                cameraTransform.SetParent(rigGO.transform, false);
                cameraTransform.localPosition = new Vector3(0f, PlayerEyeHeight, 0f);
                cameraTransform.localRotation = Quaternion.identity;

                playerRoot = rigGO.transform;
            }
            else
            {
                playerRoot = cameraTransform.parent;
            }
        }
        else
        {
            playerRoot = transform;
            cameraTransform = transform;
        }

        // Initialize angles
        if (playerRoot != null)
        {
            yaw = playerRoot.eulerAngles.y;
        }
    }

    private void Start()
    {
        EnsurePlayerRig();
        SetCursorLock(true);
        Debug.Log("[KitchenPlayerController] Active! WASD or Arrow Keys to walk, Mouse or Q/E to rotate 360°.");
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

        // Left click re-locks cursor
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

        // 1. Mouse Input
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

        // Apply Horizontal Yaw to the Player Rig (NEVER overridden by XR eye driver!)
        if (playerRoot != null)
        {
            playerRoot.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // Apply Vertical Pitch to the Camera
        if (cameraTransform != null)
        {
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    private void HandleMovement()
    {
        float speed = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? sprintSpeed : moveSpeed;

        float h = 0f, v = 0f;

        // Read direct keys for 100% fail-proof responsiveness
        if (Input.GetKey(KeyCode.W)) v += 1f;
        if (Input.GetKey(KeyCode.S)) v -= 1f;
        if (Input.GetKey(KeyCode.A)) h -= 1f;
        if (Input.GetKey(KeyCode.D)) h += 1f;

        // Also add Input Manager axes
        h += Input.GetAxisRaw("Horizontal");
        v += Input.GetAxisRaw("Vertical");

        h = Mathf.Clamp(h, -1f, 1f);
        v = Mathf.Clamp(v, -1f, 1f);

        if (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f)
        {
            Transform orient = playerRoot != null ? playerRoot : transform;
            Vector3 forward = orient.forward; forward.y = 0f; forward.Normalize();
            Vector3 right = orient.right; right.y = 0f; right.Normalize();

            Vector3 moveDir = (forward * v + right * h).normalized;
            Transform target = playerRoot != null ? playerRoot : transform;
            Vector3 newPos = target.position + moveDir * (speed * Time.deltaTime);

            if (newPos.x > 100f)
            {
                newPos.y = 0f; // Rig sits on floor
                newPos.x = Mathf.Clamp(newPos.x, KitchenMinX, KitchenMaxX);
                newPos.z = Mathf.Clamp(newPos.z, KitchenMinZ, KitchenMaxZ);
            }

            target.position = newPos;
        }
    }

    public void Teleport(Vector3 targetPosition, Vector3 targetEuler)
    {
        EnsurePlayerRig();
        Transform target = playerRoot != null ? playerRoot : transform;
        target.position = new Vector3(targetPosition.x, 0f, targetPosition.z);
        yaw = targetEuler.y;
        target.rotation = Quaternion.Euler(0f, yaw, 0f);

        pitch = targetEuler.x > 180f ? targetEuler.x - 360f : targetEuler.x;
        if (cameraTransform != null)
        {
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
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

                    extinguisher.Pickup(origin);
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
        Transform origin = cameraTransform != null ? cameraTransform : transform;
        foreach (var ext in FindObjectsOfType<FireExtinguisher>())
        {
            if (ext.Type == type)
            {
                if (activeHeldExtinguisher != null && activeHeldExtinguisher != ext)
                {
                    activeHeldExtinguisher.Drop();
                }

                ext.Pickup(origin);
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
