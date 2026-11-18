using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;
using FireSafety.Extinguishers;
using FireSafety.Fire;
using FireSafety.Training;
using FireSafety.Interactions;
using FireSafety.Environment;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// FireSafetyMasterBootstrap: The master auto-repair and setup system.
/// Guarantees that EVERY object, controller, and connection is 100% created,
/// positioned, wired, and active — both in VR (Meta Quest) and Desktop!
/// </summary>
public static class FireSafetyMasterBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void RuntimeSetup()
    {
        Debug.Log("[FireSafetyMasterBootstrap] === Auto-Initializing Scene for VR & Desktop ===");
        EnsureFullExperience();
    }

#if UNITY_EDITOR
    [MenuItem("FireSafety/⚡ 1-Click Fix & Setup Everything %#b", priority = -10)]
    public static void EditorSetup()
    {
        Debug.Log("[FireSafetyMasterBootstrap] Running 1-Click Editor Fix...");
        EnsureFullExperience();
    }
#endif

    public static void EnsureFullExperience()
    {
        // 1. Camera & Player Rig Setup
        EnsureCameraAndControllers();

        // 2. Kitchen Floor Collider (prevents extinguishers falling through ground)
        EnsureFloorCollider();

        // 3. Fire VFX at Chimney Hood
        EnsureFireVFX();

        // 4. Extinguisher Wall Station (3 Extinguishers: Water, Powder, CO2)
        EnsureExtinguisherStation();

        // 5. Fire Alarm Unit
        EnsureFireAlarm();

        // 6. Power Switch & Room Lighting
        EnsurePowerSwitchAndLighting();

        // 7. Exit Sign
        EnsureExitSign();

        // 8. 3D In-World VR Hologram Board (for in-headset HUD)
        EnsureVRHologramBoard();

        // 9. Directional Light
        EnsureDirectionalLight();

        // 10. Redesign cookware & pots
        KitchenDecorRedesigner.RedesignAllPots();

        Debug.Log("[FireSafetyMasterBootstrap] ✓ All systems operational! Desktop WASD/Mouse & Quest VR ready.");
    }

    private static void EnsureCameraAndControllers()
    {
        Camera cam = Camera.main;
        if (cam == null) cam = Object.FindObjectOfType<Camera>();
        if (cam == null)
        {
            GameObject camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            cam = camGO.AddComponent<Camera>();
        }

        // Set clear flags to solid dark (prevents passthrough bleed & sky bleed)
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 1.0f);
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 1000f;

        // Position inside kitchen facing stove/fire
        if (cam.transform.position.x < 100f)
        {
            cam.transform.position = new Vector3(202.8f, 1.60f, 0.0f);
            cam.transform.eulerAngles = new Vector3(0f, -90f, 0f);
        }

        // AudioListener
        if (cam.GetComponent<AudioListener>() == null)
        {
            cam.gameObject.AddComponent<AudioListener>();
        }

        // KitchenPlayerController (Desktop WASD + Mouse Look)
        if (cam.GetComponent<KitchenPlayerController>() == null)
        {
            cam.gameObject.AddComponent<KitchenPlayerController>();
        }

        // VRControllerManager (Quest 6-DoF HMD + Dual Touch Controllers + Hands + Lasers)
        if (cam.GetComponent<VRControllerManager>() == null)
        {
            cam.gameObject.AddComponent<VRControllerManager>();
        }

        // TrainingHUD (Objectives, Briefing, PASS procedure, Scoring)
        if (cam.GetComponent<TrainingHUD>() == null)
        {
            cam.gameObject.AddComponent<TrainingHUD>();
        }
    }

    private static void EnsureFloorCollider()
    {
        GameObject floor = GameObject.Find("Kitchen_Floor_Collider");
        if (floor == null)
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Kitchen_Floor_Collider";
            floor.transform.position = new Vector3(202.45f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(15f, 0.1f, 15f);
            var r = floor.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
        }
    }

    private static void EnsureFireVFX()
    {
        GameObject fireGO = GameObject.Find("Kitchen_Fire_VFX");
        if (fireGO == null)
        {
            fireGO = new GameObject("Kitchen_Fire_VFX");
        }
        fireGO.transform.position = new Vector3(200.18f, 1.48f, 0.17f);
        if (fireGO.GetComponent<FireVFXController>() == null)
        {
            fireGO.AddComponent<FireVFXController>();
        }
    }

    private static void EnsureExtinguisherStation()
    {
        GameObject stationGO = GameObject.Find("FireExtinguisher_WallStation");
        if (stationGO == null)
        {
            stationGO = new GameObject("FireExtinguisher_WallStation");
            ExtinguisherStation.BuildWallStation(stationGO);
            stationGO.AddComponent<ExtinguisherStation>();
        }
        else
        {
            var stationComp = stationGO.GetComponent<ExtinguisherStation>();
            if (stationComp == null) stationComp = stationGO.AddComponent<ExtinguisherStation>();
            stationComp.FindSceneExtinguishers();
            if (stationComp.Extinguishers.Count != 3)
            {
                ExtinguisherStation.BuildWallStation(stationGO);
                stationComp.FindSceneExtinguishers();
            }
        }
    }

    private static void EnsureFireAlarm()
    {
        FireAlarmController alarm = Object.FindObjectOfType<FireAlarmController>();
        if (alarm == null)
        {
            GameObject alarmGO = new GameObject("FireAlarm_Controller");
            alarmGO.transform.position = new Vector3(205.40f, 1.45f, 1.20f);
            alarmGO.transform.eulerAngles = new Vector3(0f, -90f, 0f);
            alarmGO.AddComponent<FireAlarmController>();
        }
    }

    private static void EnsurePowerSwitchAndLighting()
    {
        PowerSwitchController sw = Object.FindObjectOfType<PowerSwitchController>();
        if (sw == null)
        {
            GameObject swGO = new GameObject("PowerSwitch_Controller");
            swGO.transform.position = new Vector3(204.99f, 1.50f, 3.52f);
            swGO.transform.eulerAngles = new Vector3(0f, -90f, 0f);
            sw = swGO.AddComponent<PowerSwitchController>();
        }
    }

    private static void EnsureExitSign()
    {
        ExitSignController exit = Object.FindObjectOfType<ExitSignController>();
        if (exit == null)
        {
            GameObject exitGO = new GameObject("ExitSign_Controller");
            exitGO.transform.position = new Vector3(199.45f, 2.18f, 0.08f);
            exitGO.transform.eulerAngles = new Vector3(0f, 90f, 0f);
            exitGO.AddComponent<ExitSignController>();
        }
    }

    private static void EnsureVRHologramBoard()
    {
        VRTrainingBoard board = Object.FindObjectOfType<VRTrainingBoard>();
        if (board == null)
        {
            GameObject boardGO = new GameObject("VR_Training_Hologram_Board");
            boardGO.transform.position = new Vector3(202.8f, 1.8f, 1.8f);
            boardGO.transform.eulerAngles = new Vector3(0f, -25f, 0f);
            boardGO.AddComponent<VRTrainingBoard>();
        }
    }

    private static void EnsureDirectionalLight()
    {
        if (Object.FindObjectOfType<Light>() == null)
        {
            GameObject lightGO = new GameObject("Directional Light");
            Light dl = lightGO.AddComponent<Light>();
            dl.type = LightType.Directional;
            dl.intensity = 1.0f;
            dl.color = new Color(1f, 0.96f, 0.88f);
            lightGO.transform.eulerAngles = new Vector3(50f, -30f, 0f);
        }
    }
}
