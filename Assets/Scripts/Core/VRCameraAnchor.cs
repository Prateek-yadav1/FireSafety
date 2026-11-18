using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// VRCameraAnchor — Lightweight anchor that teleports the XR rig to the correct
/// room (Room 01 origin, or Room 02 Kitchen at X≈202.8) ONLY when requested
/// via SetKitchenAnchor() / SetRoom01Anchor().
///
/// Critical: We NEVER reset the rig rotation every frame.
/// The Oculus XR Plugin owns _xrRigRoot.rotation — it updates it from the HMD
/// sensor data. Any per-frame overwrite breaks 6-DoF head-tracking completely.
///
/// We use a _pendingTeleport flag: position + yaw-only rotation are applied
/// once when teleporting, then never again until the next teleport request.
/// </summary>
[DefaultExecutionOrder(-32000)]
public class VRCameraAnchor : MonoBehaviour
{
    // ─── Room Anchors ─────────────────────────────────────────────────────────
    public static readonly Vector3    KitchenAnchor   = new Vector3(202.8f, 0f, 0f);
    public static readonly Quaternion KitchenRotation = Quaternion.Euler(0f, -90f, 0f);

    public static Vector3    CurrentAnchor   = Vector3.zero;
    public static Quaternion CurrentRotation = Quaternion.identity;
    public static bool IsKitchenActive => CurrentAnchor.x > 100f;

    // ─── Teleport API ─────────────────────────────────────────────────────────
    public static void SetKitchenAnchor()
    {
        CurrentAnchor   = KitchenAnchor;
        CurrentRotation = KitchenRotation;
        _pendingTeleport = true;
        Debug.Log("[VRCameraAnchor] Teleport requested → ROOM 02 (Kitchen).");
    }

    public static void SetRoom01Anchor()
    {
        CurrentAnchor   = Vector3.zero;
        CurrentRotation = Quaternion.identity;
        _pendingTeleport = true;
        Debug.Log("[VRCameraAnchor] Teleport requested → ROOM 01 (Orientation Studio).");
    }

    // ─── Internal State ───────────────────────────────────────────────────────
    private static bool _pendingTeleport = false;
    private bool        _rigPlacedOnce   = false;
    private Transform   _xrRigRoot;

    // ─── Unity Messages ───────────────────────────────────────────────────────
    private void OnEnable()  { Application.onBeforeRender += OnBeforeRender; }
    private void OnDisable() { Application.onBeforeRender -= OnBeforeRender; }

    private void Start()
    {
        // Place rig exactly once at scene load
        if (!_rigPlacedOnce)
        {
            FindXRRigRoot();
            if (_xrRigRoot != null)
            {
                // Position only — do NOT set rotation (let HMD driver own it)
                _xrRigRoot.position = CurrentAnchor;
                // Set a sensible initial yaw so the player faces the kitchen
                _xrRigRoot.rotation = CurrentRotation;
                _rigPlacedOnce = true;
                Debug.Log($"[VRCameraAnchor] Initial rig placement → {CurrentAnchor}");
            }
        }
    }

    private void LateUpdate()
    {
        if (!_pendingTeleport) return;
        ExecuteTeleport();
    }

    // Called AFTER XR driver applies HMD data, BEFORE the frame renders
    private void OnBeforeRender()
    {
        if (!_pendingTeleport) return;
        ExecuteTeleport();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────
    private void ExecuteTeleport()
    {
        FindXRRigRoot();
        if (_xrRigRoot == null) return;

        // Apply target position (teleport to new room)
        _xrRigRoot.position = CurrentAnchor;
        // Apply a yaw-only rotation to face the right direction after the teleport.
        // The HMD will then add its own relative tracking on top of this base yaw.
        _xrRigRoot.rotation = CurrentRotation;

        _pendingTeleport = false;
        Debug.Log($"[VRCameraAnchor] Teleport executed → {CurrentAnchor}, yaw={CurrentRotation.eulerAngles.y:F0}°");
    }

    private void FindXRRigRoot()
    {
        if (_xrRigRoot != null) return; // already cached

        Camera cam = Camera.main ?? FindObjectOfType<Camera>();
        if (cam == null) return;

        // Walk up hierarchy to find topmost transform
        Transform t = cam.transform;
        while (t.parent != null) t = t.parent;
        _xrRigRoot = t;
    }
}
