using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// VRCameraAnchor — HIGHEST PRIORITY script that forces the XR rig to stay
/// inside the current active room (Room 01 Orientation Studio or Room 02 Kitchen),
/// even when the Oculus XR Plugin overrides camera position after LateUpdate.
///
/// Strategy: We use Application.onBeforeRender which fires AFTER the XR driver
/// has applied HMD tracking but BEFORE the frame is rendered to the display.
/// This is the only reliable hook to override XR position on Quest hardware.
/// </summary>
[DefaultExecutionOrder(-32000)]
public class VRCameraAnchor : MonoBehaviour
{
    // Anchors
    public static readonly Vector3 KitchenAnchor   = new Vector3(202.8f, 0f, 0f);
    public static readonly Quaternion KitchenRotation = Quaternion.Euler(0f, -90f, 0f);

    public static Vector3 CurrentAnchor = Vector3.zero; // Starts in Room 01 (Orientation Studio)
    public static Quaternion CurrentRotation = Quaternion.identity;
    public static bool IsKitchenActive => CurrentAnchor.x > 100f;

    public static void SetKitchenAnchor()
    {
        CurrentAnchor = KitchenAnchor;
        CurrentRotation = KitchenRotation;
        Debug.Log("[VRCameraAnchor] Switched active anchor to ROOM 02 (Kitchen).");
    }

    public static void SetRoom01Anchor()
    {
        CurrentAnchor = Vector3.zero;
        CurrentRotation = Quaternion.identity;
        Debug.Log("[VRCameraAnchor] Switched active anchor to ROOM 01 (Orientation Studio).");
    }

    private Transform _xrRigRoot;   // topmost XR-controlled transform above Camera.main
    private bool      _vrActive;

    // ─── Unity Messages ───────────────────────────────────────────────────────

    private void OnEnable()
    {
        Application.onBeforeRender += OnBeforeRender;
    }

    private void OnDisable()
    {
        Application.onBeforeRender -= OnBeforeRender;
    }

    private void Awake()
    {
        _vrActive = XRSettings.isDeviceActive;
    }

    private void Start()
    {
        _vrActive = XRSettings.isDeviceActive;
        if (_vrActive)
        {
            FindXRRigRoot();
            ApplyActiveAnchor();
            Debug.Log($"[VRCameraAnchor] VR mode — anchor applied to '{_xrRigRoot?.name}'.");
        }
        else
        {
            Debug.Log("[VRCameraAnchor] Desktop mode — no XR position override needed.");
        }
    }

    private void LateUpdate()
    {
        _vrActive = XRSettings.isDeviceActive;
        if (!_vrActive) return;

        FindXRRigRoot();
        ApplyActiveAnchor();
    }

    // ─── Core: Called AFTER XR driver updates, BEFORE rendering ──────────────

    private void OnBeforeRender()
    {
        if (!_vrActive) return;
        FindXRRigRoot();
        ApplyActiveAnchor();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private void FindXRRigRoot()
    {
        Camera cam = Camera.main;
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null) return;

        // If camera has no parent, ensure it is parented to VR_Player_Rig so HMD local tracking offsets work correctly
        if (cam.transform.parent == null)
        {
            GameObject rigGO = GameObject.Find("VR_Player_Rig");
            if (rigGO == null)
            {
                rigGO = new GameObject("VR_Player_Rig");
                rigGO.transform.position = CurrentAnchor;
                rigGO.transform.rotation = CurrentRotation;
            }
            cam.transform.SetParent(rigGO.transform, false);
            cam.transform.localPosition = new Vector3(0f, 1.55f, IsKitchenActive ? 0f : 0.45f);
            cam.transform.localRotation = Quaternion.identity;
            _xrRigRoot = rigGO.transform;
            return;
        }

        // Walk UP the hierarchy to find the topmost transform above Camera
        Transform t = cam.transform;
        Transform topmost = t;

        while (t.parent != null)
        {
            t = t.parent;
            topmost = t;
        }

        _xrRigRoot = topmost;
    }

    private void ApplyActiveAnchor()
    {
        if (_xrRigRoot == null) FindXRRigRoot();
        if (_xrRigRoot == null) return;

        if (IsKitchenActive)
        {
            if (_xrRigRoot.position.x < 180f || _xrRigRoot.position.x > 220f)
            {
                _xrRigRoot.position = CurrentAnchor;
                _xrRigRoot.rotation = CurrentRotation;
            }
        }
        else
        {
            if (_xrRigRoot.position.x > 50f)
            {
                _xrRigRoot.position = CurrentAnchor;
                _xrRigRoot.rotation = CurrentRotation;
            }
        }
    }
}
