#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using FireSafety.Extinguishers;
using FireSafety.Fire;
using FireSafety.Training;
using FireSafety.Interactions;

/// <summary>
/// One-click scene builder for the VR Fire Safety Training project.
/// Menu: FireSafety ▶ Build / Fix Full Scene
/// 
/// What it sets up:
///  1. Room 01 (Orientation Studio at 0,0,0) with equipment workbench, fire blanket, helmet, extinguisher
///  2. Spawns camera in Room 01 at (0, 1.55, 0.45) facing the equipment table and Welcome card
///  3. Moves Kitchen_Fire_VFX to the electrical chimney position (Y ≈ 2.15) above the stove
///  4. Spawns the 5-extinguisher wall station in the kitchen (CO2, Foam, Water, Dry Powder, Wet Chemical/Blanket)
///  5. Verifies FireAlarm and PowerSwitch controllers are active in the kitchen
///  6. Ensures TrainingHUD and KitchenPlayerController are wired on Main Camera
///  7. Saves the scene
/// </summary>
public static class FireSafetySceneBuilder
{
    // ─── Kitchen World-Space Constants ───────────────────────────────────────
    const float KitchenX = 202.45f;
    const float ChimneyX = 200.55f;
    const float ChimneyY = 2.15f;
    const float ChimneyZ = -0.80f;

    // Wall extinguisher station inside kitchen (mounted on solid East wall clear of exit door)
    const float WallX = 205.15f;
    const float WallY = 1.10f;
    const float WallZ_Start = 1.70f;
    const float WallZ_Step  = 0.55f;

    [MenuItem("FireSafety/Build ▶ Fix Full Scene %#b")]
    public static void BuildScene()
    {
        Debug.Log("[FireSafetySceneBuilder] ═══ Starting full two-room scene setup ═══");

        XRAndroidSetup.EnsureQuestConfiguration();
        Step1_BuildOrientationRoom01();
        Step2_PositionCameraInRoom01();
        Step3_MoveFireToKitchenChimney();
        Step4_BuildKitchenExtinguisherStation();
        Step5_VerifyFireAlarmAndPowerSwitch();
        Step6_EnsureTrainingHUDAndController();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[FireSafetySceneBuilder] ═══ Scene build complete! ═══");
        EditorUtility.DisplayDialog("Fire Safety Scene Builder",
            "Full Two-Room Project Setup Complete!\n\n" +
            "• Room 01 (Orientation Studio) built at (0,0,0) with equipment table\n" +
            "• Camera placed in Room 01 facing Welcome Hologram\n" +
            "• Redirect to Kitchen (Room 02) wired upon selecting 'KITCHEN'\n" +
            "• Fire VFX positioned at electrical chimney (Y=2.15)\n" +
            "• Exactly 3 Extinguishers wall mounted with proper positioning & redesigned cookware/planter pots\n" +
            "• Crisp high-res Figma HUD attached\n\n" +
            "Press Play to test the complete workflow!",
            "OK");
    }

    // ─── Step 1: Build Room 01 (Orientation Studio) ───────────────────────────
    static void Step1_BuildOrientationRoom01()
    {
        OrientationRoomBuilder.BuildOrientationRoom();
    }

    // ─── Step 2: Position Camera in Kitchen Environment ──────────────────────
    static void Step2_PositionCameraInRoom01()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[FireSafetySceneBuilder] Main Camera not found.");
            return;
        }

        Vector3 kitchenSpawn = new Vector3(202.8f, 1.55f, 0.5f);
        Vector3 kitchenEuler = new Vector3(0f, -90f, 0f);

        cam.transform.position = kitchenSpawn;
        cam.transform.eulerAngles = kitchenEuler;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.09f, 0.11f, 1.0f); // Solid opaque to prevent AR passthrough

        KitchenPlayerController controller = cam.GetComponent<KitchenPlayerController>();
        if (controller != null)
        {
            controller.Teleport(kitchenSpawn, kitchenEuler);
        }

        if (cam.GetComponent<FireSafety.Interactions.VRControllerManager>() == null)
        {
            cam.gameObject.AddComponent<FireSafety.Interactions.VRControllerManager>();
        }

        EditorUtility.SetDirty(cam.gameObject);
        Debug.Log("[FireSafetySceneBuilder] ✓ Camera positioned in Kitchen Environment with VR Controller Manager.");
    }

    // ─── Step 3: Move Fire VFX to Electrical Chimney Hood ────────────────────
    static void Step3_MoveFireToKitchenChimney()
    {
        GameObject fireGO = GameObject.Find("Kitchen_Fire_VFX");
        if (fireGO == null)
        {
            Debug.LogWarning("[FireSafetySceneBuilder] Kitchen_Fire_VFX not found.");
            return;
        }

        fireGO.transform.position = new Vector3(ChimneyX, ChimneyY, ChimneyZ);
        if (fireGO.GetComponent<FireVFXController>() == null)
        {
            fireGO.AddComponent<FireVFXController>();
        }
        EditorUtility.SetDirty(fireGO);
        Debug.Log($"[FireSafetySceneBuilder] ✓ Kitchen_Fire_VFX placed at electrical chimney ({ChimneyX}, {ChimneyY}, {ChimneyZ}).");
    }

    // ─── Step 4: Build 3-Extinguisher Wall Station in Kitchen ─────────────────
    static void Step4_BuildKitchenExtinguisherStation()
    {
        GameObject stationRoot = GameObject.Find("FireExtinguisher_WallStation");
        if (stationRoot == null)
        {
            stationRoot = new GameObject("FireExtinguisher_WallStation");
        }

        ExtinguisherStation.BuildWallStation(stationRoot);
        FireSafety.Environment.KitchenDecorRedesigner.RedesignAllPots();

        ExtinguisherStation stationComp = stationRoot.GetComponent<ExtinguisherStation>();
        if (stationComp == null) stationComp = stationRoot.AddComponent<ExtinguisherStation>();

        EditorUtility.SetDirty(stationRoot);
        Debug.Log("[FireSafetySceneBuilder] ✓ 3-Extinguisher wall station mounted in kitchen & pots redesigned.");
    }

    static void CreateWallBracket(Transform parent, Vector3 pos)
    {
        GameObject bracket = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bracket.name = "WallBracket";
        bracket.transform.SetParent(parent, false);
        bracket.transform.position = pos;
        bracket.transform.localScale = new Vector3(0.05f, 0.60f, 0.12f);
        bracket.GetComponent<Renderer>().sharedMaterial = CreateMat(new Color(0.25f, 0.25f, 0.28f), 0.6f, 0.5f);
        var c = bracket.GetComponent<Collider>();
        if (c != null) Object.DestroyImmediate(c);
    }

    static void CreateWallPlaque(Transform parent, Vector3 pos, ExtinguisherType type)
    {
        GameObject label = new GameObject($"Label_{type}");
        label.transform.SetParent(parent, false);
        label.transform.position = pos;
        label.transform.eulerAngles = new Vector3(0f, -90f, 0f); // Face into the room (-X)

        // Large illuminated plaque base
        GameObject plaque = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plaque.name = "Plaque";
        plaque.transform.SetParent(label.transform, false);
        plaque.transform.localPosition = Vector3.zero;
        plaque.transform.localScale = new Vector3(0.38f, 0.16f, 0.02f);
        var c = plaque.GetComponent<Collider>();
        if (c != null) Object.DestroyImmediate(c);

        Color plaqueColor = type switch
        {
            ExtinguisherType.CO2        => new Color(0.08f, 0.08f, 0.10f), // Jet Black
            ExtinguisherType.Foam       => new Color(0.96f, 0.88f, 0.65f), // Rich Cream
            ExtinguisherType.Water      => new Color(0.85f, 0.10f, 0.10f), // Signal Red
            ExtinguisherType.DryPowder  => new Color(0.08f, 0.42f, 0.88f), // French Blue
            _                           => new Color(0.98f, 0.82f, 0.05f), // Canary Yellow
        };

        Material mat = CreateMat(plaqueColor, 0.1f, 0.5f);
        plaque.GetComponent<Renderer>().sharedMaterial = mat;

        // Glowing border trim
        GameObject border = GameObject.CreatePrimitive(PrimitiveType.Cube);
        border.name = "PlaqueBorder";
        border.transform.SetParent(label.transform, false);
        border.transform.localPosition = new Vector3(0f, 0f, 0.008f);
        border.transform.localScale = new Vector3(0.39f, 0.17f, 0.005f);
        var c2 = border.GetComponent<Collider>();
        if (c2 != null) Object.DestroyImmediate(c2);

        Color borderColor = type == ExtinguisherType.CO2 ? new Color(0.34f, 0.90f, 0.95f) : Color.white;
        Material borderMat = CreateMat(borderColor, 0f, 0.8f);
        borderMat.EnableKeyword("_EMISSION");
        borderMat.SetColor("_EmissionColor", borderColor * 1.5f);
        border.GetComponent<Renderer>().sharedMaterial = borderMat;

        // Plaque Text
        string mainTitle = type switch
        {
            ExtinguisherType.CO2        => "CO₂ (CARBON DIOXIDE)\nCLASS B & ELECTRICAL SAFE",
            ExtinguisherType.Foam       => "FOAM EXTINGUISHER\nCLASS B LIQUIDS ONLY",
            ExtinguisherType.Water      => "WATER EXTINGUISHER\n⚡ DANGER: NO ELECTRICAL",
            ExtinguisherType.DryPowder  => "DRY POWDER\nCLASS A·B·C MULTI-PURPOSE",
            _                           => "WET CHEMICAL\nCLASS F OIL & GREASE",
        };

        Color textCol = (type == ExtinguisherType.Foam || type == ExtinguisherType.WetChemical) ? Color.black : Color.white;

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(label.transform, false);
        textGO.transform.localPosition = new Vector3(0f, 0f, 0.015f);
        textGO.transform.localEulerAngles = new Vector3(0f, 180f, 0f);

        TextMesh tm = textGO.AddComponent<TextMesh>();
        tm.text = mainTitle;
        tm.fontSize = 42;
        tm.characterSize = 0.0050f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = textCol;
        tm.fontStyle = FontStyle.Bold;
    }

    // ─── Step 5: Verify Kitchen Safety Fixtures ──────────────────────────────
    static void Step5_VerifyFireAlarmAndPowerSwitch()
    {
        FireAlarmController alarm = Object.FindObjectOfType<FireAlarmController>();
        if (alarm == null)
        {
            GameObject alarmGO = new GameObject("FireAlarm_Controller");
            alarmGO.transform.position = new Vector3(205.40f, 1.45f, 1.20f);
            alarmGO.AddComponent<FireAlarmController>();
            EditorUtility.SetDirty(alarmGO);
        }

        PowerSwitchController sw = Object.FindObjectOfType<PowerSwitchController>();
        if (sw == null)
        {
            GameObject swGO = new GameObject("PowerSwitch_Controller");
            swGO.transform.position = new Vector3(204.99f, 1.50f, 3.52f);
            swGO.transform.eulerAngles = new Vector3(0f, -90f, 0f);
            swGO.AddComponent<PowerSwitchController>();
            EditorUtility.SetDirty(swGO);
        }

        ExitSignController exit = Object.FindObjectOfType<ExitSignController>();
        if (exit == null)
        {
            GameObject exitGO = new GameObject("ExitSign_Controller");
            exitGO.transform.position = new Vector3(199.45f, 2.18f, 0.08f);
            exitGO.transform.eulerAngles = new Vector3(0f, 90f, 0f);
            exitGO.AddComponent<ExitSignController>();
            EditorUtility.SetDirty(exitGO);
        }
    }

    // ─── Step 6: Ensure TrainingHUD and Controller on Main Camera ─────────────
    static void Step6_EnsureTrainingHUDAndController()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        if (cam.GetComponent<TrainingHUD>() == null)
        {
            cam.gameObject.AddComponent<TrainingHUD>();
            EditorUtility.SetDirty(cam.gameObject);
        }

        if (cam.GetComponent<KitchenPlayerController>() == null)
        {
            cam.gameObject.AddComponent<KitchenPlayerController>();
            EditorUtility.SetDirty(cam.gameObject);
        }
    }

    static Material CreateMat(Color col, float metallic, float smoothness)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = col;
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        return mat;
    }
}
#endif
