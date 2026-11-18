using System.Collections.Generic;
using UnityEngine;
using FireSafety.Fire;

/// <summary>
/// Controls the main electrical power switch in the kitchen simulation.
///
/// Features:
///   - 3D wall-mounted industrial circuit breaker enclosure with rotatable switch handle
///   - BoxCollider for direct VR hand proximity touch and laser interaction
///   - Tactile mechanical breaker click / clunk audio feedback
///   - Proper warm room lighting when ON, emergency blackout with battery-backed safety fixtures when OFF
/// </summary>
[RequireComponent(typeof(AudioSource))]
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
    [Range(0f, 1f)]
    [SerializeField] private float emergencyNightAmbient = 0.65f;

    [Header("Switch Visual Handle")]
    public Transform switchHandle;
    public Vector3 powerOnRotation  = new Vector3(0f, 0f, -35f);
    public Vector3 powerOffRotation = new Vector3(0f, 0f,  35f);

    [Header("Audio")]
    [SerializeField] private AudioClip switchSound;
    private AudioSource _audioSource;

    [Header("Desktop Testing")]
    public KeyCode togglePowerKey = KeyCode.L;

    private bool _powerOn = true;
    private readonly Dictionary<Light, float> _storedIntensities = new Dictionary<Light, float>();

    private TextMesh _statusTextMesh;

    public System.Action<bool> OnPowerStateChanged;
    public bool IsPowerOn => _powerOn;

    private void Awake()
    {
        SetupAudio();
        BuildVisualStationIfNeeded();
    }

    private void Start()
    {
        if (fireAlarmController == null) fireAlarmController = FindObjectOfType<FireAlarmController>();
        if (exitSignController == null) exitSignController   = FindObjectOfType<ExitSignController>();
        if (fireVFXController == null) fireVFXController     = FindObjectOfType<FireVFXController>();

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
            TogglePower();
            Debug.Log($"[PowerSwitchController] L key → Power is now {(_powerOn ? "ON" : "OFF")}");
        }
    }

    private void OnMouseDown()
    {
        TogglePower();
    }

    public void TogglePower() => SetPower(!_powerOn);

    public void SetPower(bool on)
    {
        if (_powerOn == on) return;
        _powerOn = on;

        // Play breaker clunk audio
        if (_audioSource != null && switchSound != null)
        {
            _audioSource.pitch = on ? 1.05f : 0.95f;
            _audioSource.PlayOneShot(switchSound, 1.0f);
        }

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
            if (_statusTextMesh != null) { _statusTextMesh.text = "POWER: ON"; _statusTextMesh.color = Color.green; }

            Debug.Log("[PowerSwitchController] Proper room illumination ACTIVE.");
        }
        else
        {
            // Power CUT: Shut down primary overhead fixtures ONLY
            foreach (Light l in primaryRoomLights)
            {
                if (l != null)
                {
                    string lName = l.gameObject.name.ToLower();
                    if (lName.Contains("strobe") || lName.Contains("alarm") || lName.Contains("exit") || lName.Contains("emergency"))
                        continue;

                    l.intensity = 0f;
                    l.enabled = false;
                }
            }

            // Emergency visibility: soft ambient + backup lights
            foreach (var el in emergencyBackupLights)
            {
                if (el != null)
                {
                    el.enabled = true;
                    el.intensity = 0.85f;
                }
            }

            RenderSettings.ambientIntensity = emergencyNightAmbient;
            RenderSettings.ambientLight = new Color(0.22f, 0.24f, 0.28f);

            // Activate fire alarm (battery-backed)
            if (fireAlarmController != null)
            {
                if (!fireAlarmController.IsAlarmActive)
                    fireAlarmController.ActivateAlarm();
                else
                    fireAlarmController.OnMainPowerCut();
            }

            // EXIT sign: emergency battery backup (stays GREEN)
            if (exitSignController != null)
                exitSignController.SetEmergencyBackupState();

            if (switchHandle != null) switchHandle.localEulerAngles = powerOffRotation;
            if (_statusTextMesh != null) { _statusTextMesh.text = "POWER: CUT"; _statusTextMesh.color = Color.red; }

            Debug.Log("[PowerSwitchController] Power CUT — Room darkened. Alarm & EXIT sign on battery backup.");
        }
    }

    // ─── Visual Enclosure & Collider Setup ───────────────────────────────────

    private void BuildVisualStationIfNeeded()
    {
        if (transform.Find("BreakerBox") != null) return;

        // Position on kitchen wall if default
        if (transform.position == Vector3.zero)
        {
            transform.position = new Vector3(204.99f, 1.50f, 3.52f);
            transform.rotation = Quaternion.Euler(0f, -90f, 0f);
        }

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // 1. Outer Box Enclosure (Dark industrial steel)
        var boxMat = new Material(litShader);
        boxMat.color = new Color(0.20f, 0.22f, 0.24f);
        boxMat.SetFloat("_Metallic", 0.65f);
        boxMat.SetFloat("_Smoothness", 0.45f);

        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = "BreakerBox";
        box.transform.SetParent(transform, false);
        box.transform.localPosition = Vector3.zero;
        box.transform.localScale = new Vector3(0.22f, 0.32f, 0.08f);
        Destroy(box.GetComponent<Collider>());
        box.GetComponent<Renderer>().sharedMaterial = boxMat;

        // 2. Yellow hazard panel insert
        var panelMat = new Material(litShader);
        panelMat.color = new Color(0.85f, 0.72f, 0.12f);
        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "HazardPanel";
        panel.transform.SetParent(box.transform, false);
        panel.transform.localPosition = new Vector3(0f, 0f, 0.52f);
        panel.transform.localScale = new Vector3(0.85f, 0.85f, 0.05f);
        Destroy(panel.GetComponent<Collider>());
        panel.GetComponent<Renderer>().sharedMaterial = panelMat;

        // 3. Red Switch Lever
        var handleMat = new Material(litShader);
        handleMat.color = new Color(0.85f, 0.08f, 0.08f);
        GameObject lever = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lever.name = "BreakerLever";
        lever.transform.SetParent(box.transform, false);
        lever.transform.localPosition = new Vector3(0f, 0f, 0.60f);
        lever.transform.localScale = new Vector3(0.22f, 0.50f, 0.25f);
        lever.transform.localEulerAngles = powerOnRotation;
        Destroy(lever.GetComponent<Collider>());
        lever.GetComponent<Renderer>().sharedMaterial = handleMat;
        switchHandle = lever.transform;

        // 4. Status Text
        GameObject textGO = new GameObject("StatusText");
        textGO.transform.SetParent(box.transform, false);
        textGO.transform.localPosition = new Vector3(0f, 0.55f, 0.55f);
        textGO.transform.localRotation = Quaternion.identity;
        _statusTextMesh = textGO.AddComponent<TextMesh>();
        _statusTextMesh.text = "POWER: ON";
        _statusTextMesh.characterSize = 0.022f;
        _statusTextMesh.fontSize = 48;
        _statusTextMesh.alignment = TextAlignment.Center;
        _statusTextMesh.anchor = TextAnchor.MiddleCenter;
        _statusTextMesh.color = Color.green;

        // 5. Interaction Collider
        BoxCollider boxCol = GetComponent<BoxCollider>();
        if (boxCol == null) boxCol = gameObject.AddComponent<BoxCollider>();
        boxCol.size = new Vector3(0.26f, 0.36f, 0.18f);
        boxCol.center = Vector3.zero;
    }

    // ─── Audio Setup ──────────────────────────────────────────────────────────

    private void SetupAudio()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();

        _audioSource.loop = false;
        _audioSource.playOnAwake = false;
        _audioSource.volume = 1.0f;
        _audioSource.spatialBlend = 0.5f;
        _audioSource.minDistance = 1.0f;
        _audioSource.maxDistance = 25f;

        if (switchSound == null)
        {
            switchSound = CreateBreakerClip();
        }
    }

    private static AudioClip CreateBreakerClip()
    {
        int sampleRate = 44100;
        int samples = (int)(sampleRate * 0.22f);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float click = Mathf.Sin(2f * Mathf.PI * 1800f * t) * Mathf.Exp(-t * 80f);
            float thud  = Mathf.Sin(2f * Mathf.PI * 130f  * t) * Mathf.Exp(-t * 22f);
            float snap  = (UnityEngine.Random.value * 2f - 1f) * Mathf.Exp(-t * 120f) * 0.4f;
            data[i] = Mathf.Clamp(click * 0.6f + thud * 0.7f + snap, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create("Breaker_Switch_Clunk", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
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
            l1.color = new Color(1.0f, 0.96f, 0.88f);
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

            // Emergency Dim Backup Light
            GameObject emergLightObj = new GameObject("Emergency_Ambient_NightLight");
            emergLightObj.transform.SetParent(lightingRoot.transform, false);
            emergLightObj.transform.position = new Vector3(202.45f, 2.30f, 0.0f);
            Light el = emergLightObj.AddComponent<Light>();
            el.type = LightType.Point;
            el.color = new Color(0.40f, 0.55f, 0.75f);
            el.intensity = 0f;
            el.range = 9.0f;
            emergencyBackupLights.Add(el);

            // Emergency Exit Area Light
            GameObject emergExitLightObj = new GameObject("Emergency_Exit_AreaLight");
            emergExitLightObj.transform.SetParent(lightingRoot.transform, false);
            emergExitLightObj.transform.position = new Vector3(199.45f, 2.00f, 0.08f);
            Light el2 = emergExitLightObj.AddComponent<Light>();
            el2.type = LightType.Point;
            el2.color = new Color(0.1f, 0.9f, 0.4f);
            el2.intensity = 0f;
            el2.range = 3.5f;
            emergencyBackupLights.Add(el2);
        }
    }
}
