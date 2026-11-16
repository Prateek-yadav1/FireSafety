using System.Collections;
using UnityEngine;
using UnityEngine.XR;
using FireSafety.Extinguishers;
using FireSafety.Fire;
using FireSafety.Training;
using FireSafety.Interactions;

/// <summary>
/// SceneBootstrap – Automatically initializes the Fire Safety Training Scene
/// on BOTH Desktop and VR (Meta Quest).
/// Uses [RuntimeInitializeOnLoadMethod] so it executes reliably in Play mode and builds.
/// </summary>
public static class SceneBootstrap
{
    private static bool _initialized = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void AutoBootstrap()
    {
        Initialize();
    }

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        Debug.Log("[SceneBootstrap] ═══ Initializing Fire Safety Experience (Dual VR & Desktop) ═══");

        // ── 1. Main Camera Setup ──────────────────────────────────────────────
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            cam = camGO.AddComponent<Camera>();
        }

        // Enforce solid background color (Alpha = 1.0) to prevent Meta Quest AR passthrough bleedthrough
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 1.0f);
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 1000f;

        // Position player inside kitchen facing the stove/fire
        cam.transform.position = new Vector3(202.8f, 1.60f, 0.0f);
        cam.transform.eulerAngles = new Vector3(0f, -90f, 0f);

        // ── 2. Attach Controllers onto Main Camera ────────────────────────────
        KitchenPlayerController kpc = cam.GetComponent<KitchenPlayerController>();
        if (kpc == null)
        {
            kpc = cam.gameObject.AddComponent<KitchenPlayerController>();
        }

        VRControllerManager vrMgr = cam.GetComponent<VRControllerManager>();
        if (vrMgr == null)
        {
            vrMgr = cam.gameObject.AddComponent<VRControllerManager>();
        }

        TrainingHUD hud = cam.GetComponent<TrainingHUD>();
        if (hud == null)
        {
            hud = cam.gameObject.AddComponent<TrainingHUD>();
        }

        // ── 3. Kitchen Safety Fixtures ─────────────────────────────────────────
        if (Object.FindObjectOfType<FireAlarmController>() == null)
        {
            GameObject alarmGO = new GameObject("FireAlarm_Controller");
            alarmGO.transform.position = new Vector3(205.40f, 1.45f, 1.20f);
            alarmGO.AddComponent<FireAlarmController>();
        }

        if (Object.FindObjectOfType<PowerSwitchController>() == null)
        {
            GameObject swGO = new GameObject("PowerSwitch_Controller");
            swGO.transform.position = new Vector3(204.99f, 1.50f, 3.52f);
            swGO.transform.eulerAngles = new Vector3(0f, -90f, 0f);
            swGO.AddComponent<PowerSwitchController>();
        }

        if (Object.FindObjectOfType<ExitSignController>() == null)
        {
            GameObject exitGO = new GameObject("ExitSign_Controller");
            exitGO.transform.position = new Vector3(199.45f, 2.18f, 0.08f);
            exitGO.transform.eulerAngles = new Vector3(0f, 90f, 0f);
            exitGO.AddComponent<ExitSignController>();
        }

        // ── 4. Fire VFX Controller ─────────────────────────────────────────────
        GameObject fireGO = GameObject.Find("Kitchen_Fire_VFX");
        if (fireGO != null)
        {
            // Position at chimney hood above stove
            fireGO.transform.position = new Vector3(200.55f, 2.15f, -0.80f);
            if (fireGO.GetComponent<FireVFXController>() == null)
            {
                fireGO.AddComponent<FireVFXController>();
            }
        }

        // ── 5. Wall Extinguisher Station (CO2, Dry Powder, Water) ──────────────
        GameObject stationGO = GameObject.Find("FireExtinguisher_WallStation");
        if (stationGO == null)
        {
            stationGO = new GameObject("FireExtinguisher_WallStation");
            ExtinguisherStation.BuildWallStation(stationGO);
            stationGO.AddComponent<ExtinguisherStation>();
        }

        // ── 6. Directional Light & Room Lighting ──────────────────────────────
        if (Object.FindObjectOfType<Light>() == null)
        {
            GameObject lightGO = new GameObject("Directional Light");
            Light dl = lightGO.AddComponent<Light>();
            dl.type = LightType.Directional;
            dl.intensity = 1.0f;
            dl.color = new Color(1f, 0.96f, 0.88f);
            lightGO.transform.eulerAngles = new Vector3(50f, -30f, 0f);
        }

        // ── 7. Floor Collider for Physics ─────────────────────────────────────
        if (GameObject.Find("Kitchen_Floor_Collider") == null)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Kitchen_Floor_Collider";
            floor.transform.position = new Vector3(202.45f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(12f, 0.1f, 12f);
            var r = floor.GetComponent<Renderer>();
            if (r != null) r.enabled = false; // Invisible physics barrier
        }

        Debug.Log("[SceneBootstrap] ✓ Setup complete. Desktop (WASD + Mouse) & VR (Quest HMD + Controllers) ready!");
    }
}
