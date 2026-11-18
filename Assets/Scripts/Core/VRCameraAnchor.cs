using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// VRCameraAnchor — HIGHEST PRIORITY script that forces the XR rig to stay
/// inside the kitchen at all times, even when the Oculus XR Plugin overrides
/// camera position after LateUpdate.
///
/// Strategy: We use Application.onBeforeRender which fires AFTER the XR driver
/// has applied HMD tracking but BEFORE the frame is rendered to the display.
/// This is the only reliable hook to override XR position on Quest hardware.
///
/// The XR system positions the "XR Origin" (camera root) at tracking-space origin.
/// We find that root and move it to kitchen coordinates so HMD-relative offsets
/// land the player inside the kitchen rather than at world origin.
/// </summary>
[DefaultExecutionOrder(-32000)]
public class VRCameraAnchor : MonoBehaviour
{
    // Kitchen anchor — player rig root goes here
    public static readonly Vector3 KitchenAnchor   = new Vector3(202.8f, 0f, 0f);
    public static readonly Vector3 KitchenPosition = new Vector3(202.8f, 1.6f, 0f);

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
            ApplyKitchenAnchor();
            Debug.Log("[VRCameraAnchor] VR mode — kitchen anchor applied to XR rig root.");
        }
        else
        {
            Debug.Log("[VRCameraAnchor] Desktop mode — no XR position override needed.");
        }
    }

    private void LateUpdate()
    {
        // Re-check VR state (may become active after a few frames on first run)
        _vrActive = XRSettings.isDeviceActive;
        if (!_vrActive) return;

        FindXRRigRoot();
        ApplyKitchenAnchor();
    }

    // ─── Core: Called AFTER XR driver updates, BEFORE rendering ──────────────

    private void OnBeforeRender()
    {
        if (!_vrActive) return;
        FindXRRigRoot();
        ApplyKitchenAnchor();
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
                rigGO.transform.position = KitchenAnchor;
                rigGO.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            }
            cam.transform.SetParent(rigGO.transform, false);
            cam.transform.localPosition = new Vector3(0f, 1.6f, 0f);
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

    private void ApplyKitchenAnchor()
    {
        if (_xrRigRoot == null) FindXRRigRoot();
        if (_xrRigRoot == null) return;

        // Only correct if the rig root is nowhere near the kitchen
        // (allow some movement freedom within kitchen bounds)
        if (_xrRigRoot.position.x < 180f || _xrRigRoot.position.x > 220f)
        {
            _xrRigRoot.position = KitchenAnchor;
            _xrRigRoot.rotation = Quaternion.Euler(0f, -90f, 0f);
            Debug.Log($"[VRCameraAnchor] Anchored XR rig root '{_xrRigRoot.name}' to kitchen {KitchenAnchor}");
        }
    }
}
