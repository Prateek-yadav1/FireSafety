using UnityEngine;
using UnityEngine.Rendering;
using FireSafety.Extinguishers;
using FireSafety.Fire;
using FireSafety.Training;
using FireSafety.Interactions;
using FireSafety.Environment;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// FireSafetyMasterBootstrap — single entry point that guarantees the entire
/// scene is wired and ready for both Desktop (WASD) and Meta Quest VR.
///
/// Runs automatically at scene load via [RuntimeInitializeOnLoadMethod].
/// </summary>
public static class FireSafetyMasterBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void RuntimeSetup()
    {
        Debug.Log("[FireSafetyMasterBootstrap] === Booting Fire Safety VR Training ===");
        EnsureFullExperience();
    }

#if UNITY_EDITOR
    [MenuItem("FireSafety/⚡ 1-Click Setup Scene %#b", priority = -10)]
    public static void EditorSetup()
    {
        Debug.Log("[FireSafetyMasterBootstrap] Editor 1-Click Setup...");
        EnsureFullExperience();
    }
#endif

    // ═══════════════════════════════════════════════════════════════════════════

    public static void EnsureFullExperience()
    {
        // ── 1. Lighting ──────────────────────────────────────────────────────
        RenderSettings.ambientMode      = AmbientMode.Flat;
        RenderSettings.ambientLight     = new Color(0.38f, 0.40f, 0.44f);
        RenderSettings.ambientIntensity = 1.0f;
        RenderSettings.fog              = false;

        // ── 2. Directional light ─────────────────────────────────────────────
        EnsureDirectionalLight();

        // ── 2b. Build Room 01 (Orientation Studio matching Figma) ───────────
        OrientationRoomBuilder.BuildOrientationRoom();

        // ── 3. Camera + Controllers ──────────────────────────────────────────
        EnsureCameraAndControllers();

        // ── 4. Kitchen floor collider ────────────────────────────────────────
        EnsureFloorCollider();

        // ── 5. Fire VFX ──────────────────────────────────────────────────────
        EnsureFireVFX();

        // ── 6. Extinguisher wall station ─────────────────────────────────────
        EnsureExtinguisherStation();

        // ── 7. Fire alarm ────────────────────────────────────────────────────
        EnsureFireAlarm();

        // ── 8. Power switch ──────────────────────────────────────────────────
        EnsurePowerSwitch();

        // ── 9. Exit sign ─────────────────────────────────────────────────────
        EnsureExitSign();

        // ── 10. VR hologram board ────────────────────────────────────────────
        EnsureVRHologramBoard();

        // ── 11. Kitchen decor ────────────────────────────────────────────────
        KitchenDecorRedesigner.RedesignAllPots();

        Debug.Log("[FireSafetyMasterBootstrap] ✓ All systems ready. Desktop WASD + Quest VR.");
    }

    // ─── Camera + Controllers ─────────────────────────────────────────────────

    private static void EnsureCameraAndControllers()
    {
        // ── Find or create camera ──
        Camera cam = Camera.main;
        if (cam == null) cam = Object.FindObjectOfType<Camera>();
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            cam    = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
        }

        // ── Camera render settings ──
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 1f);
        cam.nearClipPlane   = 0.05f;
        cam.farClipPlane    = 500f;
        cam.fieldOfView     = 70f;
#if UNITY_EDITOR
        cam.stereoTargetEye = StereoTargetEyeMask.None;
#else
        cam.stereoTargetEye = StereoTargetEyeMask.Both;
#endif

        // ── Build VR_Player_Rig hierarchy ──
        // Starts in Room 01 (Orientation Studio) at (0,0,0) facing table (+Z)
        GameObject rigGO = GameObject.Find("VR_Player_Rig");
        if (rigGO == null)
        {
            rigGO = new GameObject("VR_Player_Rig");
            rigGO.transform.position = Vector3.zero;
            rigGO.transform.rotation = Quaternion.identity;
        }

        // Parent camera to rig
        if (cam.transform.parent != rigGO.transform)
        {
            cam.transform.SetParent(rigGO.transform, false);
            cam.transform.localPosition = new Vector3(0f, 1.55f, 0.45f);
            cam.transform.localRotation = Quaternion.identity;
        }

        VRCameraAnchor.SetRoom01Anchor();

        // ── Add KitchenPlayerController to camera ──
        if (cam.GetComponent<KitchenPlayerController>() == null)
            cam.gameObject.AddComponent<KitchenPlayerController>();

        // ── Add VRCameraAnchor to camera (for Quest VR) ──
        if (cam.GetComponent<VRCameraAnchor>() == null)
            cam.gameObject.AddComponent<VRCameraAnchor>();

        // ── Add TrainingHUD to camera ──
        if (cam.GetComponent<TrainingHUD>() == null)
            cam.gameObject.AddComponent<TrainingHUD>();

        // ── Add VRControllerManager to camera ──
        if (cam.GetComponent<VRControllerManager>() == null)
            cam.gameObject.AddComponent<VRControllerManager>();

        Debug.Log($"[FireSafetyMasterBootstrap] Camera at world={cam.transform.position}, rig at {rigGO.transform.position}");
    }

    // ─── Floor Collider ───────────────────────────────────────────────────────

    private static void EnsureFloorCollider()
    {
        if (GameObject.Find("Kitchen_Floor_Collider") != null) return;

        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Kitchen_Floor_Collider";
        floor.transform.position   = new Vector3(202.45f, -0.05f, 0f);
        floor.transform.localScale = new Vector3(16f, 0.1f, 10f);
        var r = floor.GetComponent<Renderer>();
        if (r != null) r.enabled = false;
    }

    // ─── Fire VFX ─────────────────────────────────────────────────────────────

    private static void EnsureFireVFX()
    {
        GameObject go = GameObject.Find("Kitchen_Fire_VFX");
        if (go == null) go = new GameObject("Kitchen_Fire_VFX");
        go.transform.position = new Vector3(200.31f, 1.374f, 0.16f);
        if (go.GetComponent<FireVFXController>() == null)
            go.AddComponent<FireVFXController>();
    }

    // ─── Extinguisher Station ─────────────────────────────────────────────────

    private static void EnsureExtinguisherStation()
    {
        GameObject go = GameObject.Find("FireExtinguisher_WallStation");
        if (go == null)
        {
            go = new GameObject("FireExtinguisher_WallStation");
            ExtinguisherStation.BuildWallStation(go);
            go.AddComponent<ExtinguisherStation>();
        }
        else
        {
            var comp = go.GetComponent<ExtinguisherStation>();
            if (comp == null) comp = go.AddComponent<ExtinguisherStation>();
            comp.FindSceneExtinguishers();
            if (comp.Extinguishers.Count < 1)
                ExtinguisherStation.BuildWallStation(go);
        }
    }

    // ─── Fire Alarm ───────────────────────────────────────────────────────────

    private static void EnsureFireAlarm()
    {
        if (Object.FindObjectOfType<FireAlarmController>() != null) return;
        var go = new GameObject("FireAlarm_Controller");
        go.transform.position    = new Vector3(205.40f, 1.45f, 1.20f);
        go.transform.eulerAngles = new Vector3(0f, -90f, 0f);
        go.AddComponent<FireAlarmController>();
    }

    // ─── Power Switch ─────────────────────────────────────────────────────────

    private static void EnsurePowerSwitch()
    {
        if (Object.FindObjectOfType<PowerSwitchController>() != null) return;
        var go = new GameObject("PowerSwitch_Controller");
        go.transform.position    = new Vector3(204.99f, 1.50f, 3.52f);
        go.transform.eulerAngles = new Vector3(0f, -90f, 0f);
        go.AddComponent<PowerSwitchController>();
    }

    // ─── Exit Sign ────────────────────────────────────────────────────────────

    private static void EnsureExitSign()
    {
        if (Object.FindObjectOfType<ExitSignController>() != null) return;
        var go = new GameObject("ExitSign_Controller");
        go.transform.position    = new Vector3(199.45f, 2.18f, 0.08f);
        go.transform.eulerAngles = new Vector3(0f, 90f, 0f);
        go.AddComponent<ExitSignController>();
    }

    // ─── VR Hologram Board ────────────────────────────────────────────────────

    private static void EnsureVRHologramBoard()
    {
        if (Object.FindObjectOfType<VRTrainingBoard>() != null) return;
        var go = new GameObject("VR_Training_Hologram_Board");
        // VRTrainingBoard.Start() will set the final position and rotation
        go.transform.position    = new Vector3(201.75f, 1.55f, 1.35f);
        go.transform.eulerAngles = new Vector3(0f, 155f, 0f);
        go.AddComponent<VRTrainingBoard>();
    }

    // ─── Directional Light ────────────────────────────────────────────────────

    private static void EnsureDirectionalLight()
    {
        var lights = Object.FindObjectsOfType<Light>();
        foreach (var l in lights)
            if (l.type == LightType.Directional) return;

        var go = new GameObject("Directional Light");
        var dl = go.AddComponent<Light>();
        dl.type      = LightType.Directional;
        dl.intensity = 1.0f;
        dl.color     = new Color(1f, 0.96f, 0.88f);
        go.transform.eulerAngles = new Vector3(50f, -30f, 0f);
    }
}
