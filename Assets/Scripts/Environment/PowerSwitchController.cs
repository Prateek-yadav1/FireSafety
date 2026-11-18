using System.Collections.Generic;
using UnityEngine;
using FireSafety.Fire;

/// <summary>
/// Controls the main electrical power switch in the kitchen simulation.
///
/// Requirement:
///   - BEFORE cutting off main power supply: proper, warm, well-lit ambient & ceiling fixture illumination.
///   - AFTER cutting off main power supply: room turns into a realistic emergency blackout with just
///     sufficient ambient/emergency visibility (emergency backlights, moonlight, EXIT sign glow, fire alarm strobe).
/// </summary>
public class PowerSwitchController : MonoBehaviour
{
    [Header("Cross-System References")]
    public FireAlarmController fireAlarmController;
    public ExitSignController exitSignController;
    public FireVFXController fireVFXController;

    [Header("Ceiling Light Fixtures & Room Illumination")]
    [Tooltip("Primary room lights that shut off completely when power is cut.")]
    public List<Light> primaryRoomLights = new List<Light>();

    [Tooltip("Secondary emergency backlights that remain faintly lit (dim emergency visibility).")]
    public List<Light> emergencyBackupLights = new List<Light>();

    [Header("Emergency Blackout Light Level")]
    [Range(0f, 0.25f)]
    [SerializeField] private float emergencyNightAmbient = 0.08f; // Just sufficient light to perceive objects and escape route

    [Header("Switch Visual Handle")]
    public Transform switchHandle;
    public Vector3 powerOnRotation = new Vector3(0f, 0f, -30f);
    public Vector3 powerOffRotation = new Vector3(0f, 0f, 30f);

    [Header("Desktop Testing")]
    public KeyCode togglePowerKey = KeyCode.L;

    private bool _powerOn = true;
    private readonly Dictionary<Light, float> _storedIntensities = new Dictionary<Light, float>();

    public System.Action<bool> OnPowerStateChanged;
    public bool IsPowerOn => _powerOn;

    private void Start()
    {
        if (fireAlarmController == null) fireAlarmController = FindObjectOfType<FireAlarmController>();
        if (exitSignController == null) exitSignController = FindObjectOfType<ExitSignController>();
        if (fireVFXController == null) fireVFXController = FindObjectOfType<FireVFXController>();

        EnsureKitchenLightingFixtures();

        // Store standard power-on intensities
        foreach (Light l in primaryRoomLights)
        {
            if (l != null && !_storedIntensities.ContainsKey(l))
                _storedIntensities[l] = l.intensity;
        }

        // Apply initial state (Full proper light in the room at start)
        _powerOn = true;
        ApplyPowerState(true);
    }

    private void Update()
    {
        if (Input.GetKeyDown(togglePowerKey))
        {
            SetPower(!_powerOn);
            Debug.Log($"[PowerSwitchController] L key → Power is now {(_powerOn ? "ON" : "OFF")}");
        }
    }

    public void TogglePower() => SetPower(!_powerOn);

    public void SetPower(bool on)
    {
        if (_powerOn == on) return;
        _powerOn = on;
        ApplyPowerState(on);
        OnPowerStateChanged?.Invoke(_powerOn);
    }

    private void ApplyPowerState(bool on)
    {
        if (on)
        {
            // Restore full room illumination
            foreach (var kvp in _storedIntensities)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.enabled = true;
                    kvp.Key.intensity = kvp.Value;
                }
            }

            // Standalone emergency lights off
            foreach (var el in emergencyBackupLights)
            {
                if (el != null) el.intensity = 0f;
            }

            RenderSettings.ambientIntensity = 1.2f;
            RenderSettings.ambientLight = new Color(0.38f, 0.40f, 0.44f);

            if (exitSignController != null) exitSignController.SetNormalState();
            if (switchHandle != null) switchHandle.localEulerAngles = powerOnRotation;
            Debug.Log("[PowerSwitchController] Proper room illumination ACTIVE.");
        }
        else
        {
            // Power CUT: Shut down primary overhead fixtures ONLY
            // (DO NOT touch fire alarm strobe or exit sign — both are battery-backed)
            foreach (Light l in primaryRoomLights)
            {
                if (l != null)
                {
                    // Skip any light belonging to the alarm or exit sign
                    string lName = l.gameObject.name.ToLower();
                    if (lName.Contains("strobe") || lName.Contains("alarm") || lName.Contains("exit") || lName.Contains("emergency"))
                        continue;

                    l.intensity = 0f;
                    l.enabled = false;
                }
            }

            // Emergency visibility: soft ambient + backup lights (clearly visible escape & equipment)
            foreach (var el in emergencyBackupLights)
            {
                if (el != null)
                {
                    el.enabled = true;
                    el.intensity = 0.85f;
                }
            }

            RenderSettings.ambientIntensity = 0.65f;
            RenderSettings.ambientLight = new Color(0.22f, 0.24f, 0.28f);

            // Activate fire alarm (battery-backed — it stays on)
            if (fireAlarmController != null)
            {
                if (!fireAlarmController.IsAlarmActive)
                    fireAlarmController.ActivateAlarm();
                else
                    fireAlarmController.OnMainPowerCut(); // Ensure strobe keeps running
            }

            // EXIT sign: emergency battery backup (stays GREEN)
            if (exitSignController != null)
                exitSignController.SetEmergencyBackupState();

            if (switchHandle != null) switchHandle.localEulerAngles = powerOffRotation;
            Debug.Log("[PowerSwitchController] Power CUT — Room darkened. Alarm & EXIT sign on battery backup.");
        }
    }

    /// <summary>
    /// Builds dedicated warm kitchen ceiling lighting fixtures if none are set.
    /// </summary>
    private void EnsureKitchenLightingFixtures()
    {
        GameObject lightingRoot = GameObject.Find("Kitchen_Ceiling_Lighting");
        if (lightingRoot == null)
        {
            lightingRoot = new GameObject("Kitchen_Ceiling_Lighting");
            lightingRoot.transform.position = new Vector3(202.45f, 2.70f, 0f);

            // Fixture 1: Central Kitchen Area (Above dining/prep)
            GameObject light1Obj = new GameObject("CeilingLight_Main");
            light1Obj.transform.SetParent(lightingRoot.transform, false);
            light1Obj.transform.position = new Vector3(202.45f, 2.60f, 0.0f);
            Light l1 = light1Obj.AddComponent<Light>();
            l1.type = LightType.Point;
            l1.color = new Color(1.0f, 0.96f, 0.88f); // Warm halogen / LED ceiling light
            l1.intensity = 1.4f;
            l1.range = 7.5f;
            primaryRoomLights.Add(l1);

            // Fixture 2: Cooking & Stove Counter Task Light
            GameObject light2Obj = new GameObject("CeilingLight_StoveCounter");
            light2Obj.transform.SetParent(lightingRoot.transform, false);
            light2Obj.transform.position = new Vector3(200.80f, 2.50f, 0.20f);
            Light l2 = light2Obj.AddComponent<Light>();
            l2.type = LightType.Point;
            l2.color = new Color(1.0f, 0.94f, 0.84f);
            l2.intensity = 1.2f;
            l2.range = 5.5f;
            primaryRoomLights.Add(l2);

            // Fixture 3: Dining Table Chandelier / Overhead Light
            GameObject light3Obj = new GameObject("CeilingLight_DiningTable");
            light3Obj.transform.SetParent(lightingRoot.transform, false);
            light3Obj.transform.position = new Vector3(202.45f, 2.50f, -1.80f);
            Light l3 = light3Obj.AddComponent<Light>();
            l3.type = LightType.Point;
            l3.color = new Color(1.0f, 0.92f, 0.80f);
            l3.intensity = 1.1f;
            l3.range = 5.0f;
            primaryRoomLights.Add(l3);

            // Emergency Dim Backup Light (Simulates low ambient moonlight / emergency battery LED)
            GameObject emergLightObj = new GameObject("Emergency_Ambient_NightLight");
            emergLightObj.transform.SetParent(lightingRoot.transform, false);
            emergLightObj.transform.position = new Vector3(202.45f, 2.30f, 0.0f);
            Light el = emergLightObj.AddComponent<Light>();
            el.type = LightType.Point;
            el.color = new Color(0.40f, 0.55f, 0.75f); // Cool moonlight
            el.intensity = 0f; // off until power cut
            el.range = 9.0f;
            emergencyBackupLights.Add(el);

            // Emergency Exit Area Light — faint green glow near exit door (from EXIT sign battery backup)
            GameObject emergExitLightObj = new GameObject("Emergency_Exit_AreaLight");
            emergExitLightObj.transform.SetParent(lightingRoot.transform, false);
            emergExitLightObj.transform.position = new Vector3(199.45f, 2.00f, 0.08f);
            Light el2 = emergExitLightObj.AddComponent<Light>();
            el2.type = LightType.Point;
            el2.color = new Color(0.1f, 0.9f, 0.4f); // Green from EXIT sign halo
            el2.intensity = 0f; // off until power cut
            el2.range = 3.5f;
            emergencyBackupLights.Add(el2);
        }
    }
}
