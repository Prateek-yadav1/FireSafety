
using System.Collections;
using UnityEngine;

/// <summary>
/// Realistic wall-mounted manual fire alarm call point
/// with integrated emergency strobe beacon.
///
/// Features:
/// - Layered red commercial alarm enclosure
/// - White instruction panel
/// - Yellow manual activation flap
/// - FIRE ALARM and PRESS HERE lettering
/// - Metallic mounting screws
/// - Red flashing beacon
/// - Siren audio
/// - Battery backup
/// - Exit sign integration
/// </summary>

[RequireComponent(typeof(AudioSource))]
public class FireAlarmController : MonoBehaviour
{
    [Header("Alarm Unit Visuals")]
    [SerializeField] private Light strobeLight;
    [SerializeField] private Renderer strobeBeaconRenderer;
    [SerializeField] private Renderer buttonRenderer;

    [Header("Settings")]
    [SerializeField] private Color alarmLightColor =
        new Color(1f, 0.04f, 0.02f);

    [SerializeField] private float strobeIntensity = 3.5f;
    [SerializeField] private float strobeRange = 3f;

    [Range(1f, 5f)]
    [SerializeField] private float flashFrequency = 2.5f;

    [Header("Audio Siren")]
    [SerializeField] private AudioClip alarmClip;

    [Range(0f, 1f)]
    [SerializeField] private float alarmVolume = 0.85f;

    [Header("Cross-System Links")]
    [SerializeField] private ExitSignController exitSignController;
    [SerializeField] private PowerSwitchController powerSwitchController;

    private bool isAlarmActive = false;

    private AudioSource audioSource;
    private Coroutine flashCoroutine;

    private Material strobeMaterialInstance;
    private Material buttonMaterialInstance;

    private Material housingMaterial;
    private Material frameMaterial;
    private Material whiteMaterial;
    private Material yellowMaterial;
    private Material darkMaterial;
    private Material screwMaterial;

    private static readonly int EmissionColorID =
        Shader.PropertyToID("_EmissionColor");

    public bool IsAlarmActive => isAlarmActive;

    public event System.Action OnAlarmActivated;
    public event System.Action OnAlarmDeactivated;

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        SetupAudio();

        BuildVisualStationIfNeeded();

        SetupStrobeLight();
    }

    private void Start()
    {
        if (exitSignController == null)
            exitSignController = FindObjectOfType<ExitSignController>();

        if (powerSwitchController == null)
            powerSwitchController = FindObjectOfType<PowerSwitchController>();
    }

    private void Update()
    {
        // Keyboard toggle removed — all interaction via VR controllers or mouse click
    }

    private void OnMouseDown()
    {
        ToggleAlarm();
    }

    // =========================================================
    // BUILD REALISTIC FIRE ALARM
    // =========================================================

    private void BuildVisualStationIfNeeded()
    {
        // Wall location next to the exit door where the old alarm box was (X=205.316, Y=1.565, Z=-1.372)
        Vector3 wallPos = new Vector3(205.316f, 1.565f, -1.372f);
        transform.position = wallPos;

        // Facing into kitchen (-X).
        transform.rotation =
            Quaternion.Euler(0f, -90f, 0f);

        if (transform.Find("AlarmHousing") != null)
            return;

        Shader shader =
            Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
            shader = Shader.Find("Standard");

        // =====================================================
        // MATERIALS
        // =====================================================

        housingMaterial = CreateMaterial(
            shader,
            new Color(0.72f, 0.025f, 0.025f),
            0.08f,
            0.42f
        );

        frameMaterial = CreateMaterial(
            shader,
            new Color(0.32f, 0.015f, 0.015f),
            0.35f,
            0.55f
        );

        whiteMaterial = CreateMaterial(
            shader,
            new Color(0.92f, 0.92f, 0.88f),
            0f,
            0.25f
        );

        yellowMaterial = CreateMaterial(
            shader,
            new Color(1f, 0.65f, 0.04f),
            0.05f,
            0.3f
        );

        darkMaterial = CreateMaterial(
            shader,
            new Color(0.045f, 0.045f, 0.045f),
            0.1f,
            0.35f
        );

        screwMaterial = CreateMaterial(
            shader,
            new Color(0.65f, 0.68f, 0.70f),
            0.85f,
            0.8f
        );

        // =====================================================
        // 1. MAIN RED ENCLOSURE
        // =====================================================

        GameObject housing = CreateCube(
            "AlarmHousing",
            transform,
            Vector3.zero,
            new Vector3(0.24f, 0.32f, 0.055f),
            housingMaterial
        );

        // =====================================================
        // 2. RAISED OUTER FRAME
        // =====================================================

        CreateCube(
            "RaisedFrontFrame",
            housing.transform,
            new Vector3(0f, 0f, 0.030f),
            new Vector3(0.218f, 0.292f, 0.012f),
            frameMaterial
        );

        // =====================================================
        // 3. WHITE INSTRUCTION PANEL
        // =====================================================

        CreateCube(
            "WhiteInstructionPanel",
            housing.transform,
            new Vector3(0f, 0f, 0.038f),
            new Vector3(0.198f, 0.272f, 0.009f),
            whiteMaterial
        );

        // =====================================================
        // 4. TOP RED HEADER
        // =====================================================

        CreateCube(
            "RedHeader",
            housing.transform,
            new Vector3(0f, 0.098f, 0.044f),
            new Vector3(0.185f, 0.065f, 0.008f),
            housingMaterial
        );

        CreateText(
            "FireAlarm_Title",
            housing.transform,
            "FIRE ALARM",
            new Vector3(0f, 0.098f, 0.050f),
            0.012f,
            26,
            Color.white,
            FontStyle.Bold
        );

        // =====================================================
        // 5. SMALL INSTRUCTION TEXT
        // =====================================================

        CreateText(
            "BreakGlass_Instruction",
            housing.transform,
            "BREAK GLASS / PRESS",
            new Vector3(0f, 0.045f, 0.050f),
            0.009f,
            18,
            new Color(0.12f, 0.12f, 0.12f),
            FontStyle.Bold
        );

        // =====================================================
        // 6. CENTRAL YELLOW ACTIVATION AREA
        // =====================================================

        CreateCube(
            "ButtonOuterFrame",
            housing.transform,
            new Vector3(0f, -0.012f, 0.051f),
            new Vector3(0.145f, 0.105f, 0.012f),
            darkMaterial
        );

        GameObject button = CreateCube(
            "AlarmButton",
            housing.transform,
            new Vector3(0f, -0.012f, 0.060f),
            new Vector3(0.128f, 0.088f, 0.009f),
            yellowMaterial
        );

        buttonMaterialInstance =
            new Material(yellowMaterial);

        button.GetComponent<Renderer>().material =
            buttonMaterialInstance;

        buttonRenderer = button.GetComponent<Renderer>();

        // =====================================================
        // 7. CENTRAL PRESS HERE TEXT
        // =====================================================

        CreateText(
            "PressHere_Label",
            housing.transform,
            "PRESS HERE",
            new Vector3(0f, -0.012f, 0.067f),
            0.012f,
            22,
            new Color(0.12f, 0.04f, 0.01f),
            FontStyle.Bold
        );

        // =====================================================
        // 8. BOTTOM INSTRUCTION
        // =====================================================

        CreateText(
            "BottomInstruction",
            housing.transform,
            "IN CASE OF FIRE",
            new Vector3(0f, -0.105f, 0.050f),
            0.009f,
            18,
            new Color(0.15f, 0.15f, 0.15f),
            FontStyle.Bold
        );

        // =====================================================
        // 9. FOUR METALLIC MOUNTING SCREWS
        // =====================================================

        float screwX = 0.091f;
        float screwY = 0.128f;

        CreateScrew(
            housing.transform,
            "Screw_TopLeft",
            new Vector3(-screwX, screwY, 0.051f)
        );

        CreateScrew(
            housing.transform,
            "Screw_TopRight",
            new Vector3(screwX, screwY, 0.051f)
        );

        CreateScrew(
            housing.transform,
            "Screw_BottomLeft",
            new Vector3(-screwX, -screwY, 0.051f)
        );

        CreateScrew(
            housing.transform,
            "Screw_BottomRight",
            new Vector3(screwX, -screwY, 0.051f)
        );

        // =====================================================
        // 10. INTEGRATED RED STROBE BEACON
        // =====================================================

        GameObject beacon = GameObject.CreatePrimitive(
            PrimitiveType.Sphere
        );

        beacon.name = "AlarmBeaconStrobe";

        beacon.transform.SetParent(housing.transform, false);

        beacon.transform.localPosition =
            new Vector3(0f, 0.205f, 0.012f);

        beacon.transform.localScale =
            new Vector3(0.105f, 0.075f, 0.075f);

        Destroy(beacon.GetComponent<Collider>());

        strobeMaterialInstance = CreateMaterial(
            shader,
            new Color(0.65f, 0.015f, 0.015f),
            0f,
            0.25f
        );

        strobeMaterialInstance.name =
            "Mat_Red_Strobe_Beacon";

        strobeMaterialInstance.EnableKeyword("_EMISSION");

        strobeMaterialInstance.SetColor(
            EmissionColorID,
            Color.black
        );

        beacon.GetComponent<Renderer>().sharedMaterial =
            strobeMaterialInstance;

        strobeBeaconRenderer =
            beacon.GetComponent<Renderer>();

        // =====================================================
        // 11. SMALL BEACON BASE
        // =====================================================

        CreateCube(
            "BeaconMount",
            housing.transform,
            new Vector3(0f, 0.172f, 0.018f),
            new Vector3(0.115f, 0.018f, 0.065f),
            darkMaterial
        );

        // =====================================================
        // 12. INTERACTION COLLIDER
        // =====================================================

        BoxCollider boxCol = GetComponent<BoxCollider>();

        if (boxCol == null)
            boxCol = gameObject.AddComponent<BoxCollider>();

        boxCol.size =
            new Vector3(0.35f, 0.45f, 0.25f);

        boxCol.center =
            new Vector3(0f, 0.015f, 0f);

        Debug.Log(
            "[FireAlarmController] Realistic fire alarm created."
        );
    }

    // =========================================================
    // CREATE CUBE
    // =========================================================

    private GameObject CreateCube(
        string objectName,
        Transform parent,
        Vector3 localPosition,
        Vector3 localScale,
        Material material)
    {
        GameObject obj =
            GameObject.CreatePrimitive(PrimitiveType.Cube);

        obj.name = objectName;

        obj.transform.SetParent(parent, false);

        obj.transform.localPosition = localPosition;

        obj.transform.localRotation = Quaternion.identity;

        obj.transform.localScale = localScale;

        Destroy(obj.GetComponent<Collider>());

        obj.GetComponent<Renderer>().sharedMaterial = material;

        return obj;
    }

    // =========================================================
    // CREATE TEXT
    // =========================================================

    private void CreateText(
        string objectName,
        Transform parent,
        string text,
        Vector3 localPosition,
        float characterSize,
        int fontSize,
        Color color,
        FontStyle style)
    {
        GameObject textObject =
            new GameObject(objectName);

        textObject.transform.SetParent(parent, false);

        textObject.transform.localPosition = localPosition;

        textObject.transform.localRotation =
            Quaternion.identity;

        textObject.transform.localScale = Vector3.one;

        TextMesh textMesh =
            textObject.AddComponent<TextMesh>();

        textMesh.text = text;

        textMesh.fontSize = fontSize;

        textMesh.characterSize = characterSize;

        textMesh.fontStyle = style;

        textMesh.color = color;

        textMesh.anchor = TextAnchor.MiddleCenter;

        textMesh.alignment = TextAlignment.Center;
    }

    // =========================================================
    // CREATE METALLIC SCREWS
    // =========================================================

    private void CreateScrew(
        Transform parent,
        string objectName,
        Vector3 localPosition)
    {
        GameObject screw =
            GameObject.CreatePrimitive(PrimitiveType.Cylinder);

        screw.name = objectName;

        screw.transform.SetParent(parent, false);

        screw.transform.localPosition = localPosition;

        screw.transform.localRotation =
            Quaternion.Euler(90f, 0f, 0f);

        screw.transform.localScale =
            new Vector3(0.008f, 0.0025f, 0.008f);

        Destroy(screw.GetComponent<Collider>());

        screw.GetComponent<Renderer>().sharedMaterial =
            screwMaterial;
    }

    // =========================================================
    // MATERIAL CREATION
    // =========================================================

    private Material CreateMaterial(
        Shader shader,
        Color color,
        float metallic,
        float smoothness)
    {
        Material material = new Material(shader);

        material.color = color;

        material.SetFloat("_Metallic", metallic);

        material.SetFloat("_Smoothness", smoothness);

        return material;
    }

    // =========================================================
    // SETUP STROBE LIGHT
    // =========================================================

    private void SetupStrobeLight()
    {
        Transform beaconTransform =
            transform.Find("AlarmHousing/AlarmBeaconStrobe");

        if (beaconTransform != null)
        {
            Transform existingLight =
                beaconTransform.Find("StrobePointLight");

            if (existingLight != null)
            {
                strobeLight =
                    existingLight.GetComponent<Light>();
            }
            else
            {
                GameObject lightObj =
                    new GameObject("StrobePointLight");

                lightObj.transform.SetParent(
                    beaconTransform,
                    false
                );

                lightObj.transform.localPosition =
                    new Vector3(0f, 0f, 0.035f);

                strobeLight =
                    lightObj.AddComponent<Light>();
            }
        }

        if (strobeLight == null)
        {
            strobeLight = GetComponent<Light>();

            if (strobeLight == null)
                strobeLight = gameObject.AddComponent<Light>();
        }

        strobeLight.type = LightType.Point;

        strobeLight.color = alarmLightColor;

        strobeLight.intensity = 0f;

        strobeLight.range = strobeRange;

        strobeLight.shadows = LightShadows.None;
    }

    // =========================================================
    // AUDIO SETUP
    // =========================================================

    private void SetupAudio()
    {
        audioSource = GetComponent<AudioSource>();

        audioSource.loop = true;

        audioSource.playOnAwake = false;

        audioSource.volume = alarmVolume;

        audioSource.spatialBlend = 0.25f;

        audioSource.minDistance = 2f;

        audioSource.maxDistance = 100f;

        audioSource.rolloffMode =
            AudioRolloffMode.Linear;

        if (alarmClip == null)
        {
            alarmClip = LoadOrGenerateAlarmClip();
        }

        if (alarmClip != null)
            audioSource.clip = alarmClip;
    }

    private static AudioClip LoadOrGenerateAlarmClip()
    {
        AudioClip clip = Resources.Load<AudioClip>("Audio/Alarm_Siren");
        if (clip == null) clip = Resources.Load<AudioClip>("Alarm_Siren");
        if (clip == null)
        {
            AudioClip[] allClips = Resources.FindObjectsOfTypeAll<AudioClip>();
            foreach (AudioClip c in allClips)
            {
                if (c.name.Contains("Alarm_Siren") || c.name.Contains("alarm") || c.name.Contains("siren"))
                {
                    clip = c;
                    break;
                }
            }
        }
        if (clip == null)
        {
            // Procedural alternating high/low emergency fire siren (900 Hz / 700 Hz)
            int sampleRate = 44100;
            int samples = sampleRate * 2; // 2 second loop
            float[] data = new float[samples];
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float freq = (t % 0.8f < 0.4f) ? 880f : 660f;
                phase += freq / sampleRate;
                data[i] = Mathf.Sin(2f * Mathf.PI * phase) * 0.85f;
            }
            clip = AudioClip.Create("Procedural_Alarm_Siren", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
        }
        return clip;
    }

    // =========================================================
    // TOGGLE ALARM
    // =========================================================

    public void ToggleAlarm()
    {
        if (isAlarmActive)
            DeactivateAlarm();
        else
            ActivateAlarm();
    }

    // =========================================================
    // ACTIVATE ALARM
    // =========================================================

    public void ActivateAlarm()
    {
        if (isAlarmActive)
            return;

        isAlarmActive = true;

        // Start siren.
        if (
            audioSource != null &&
            audioSource.clip != null &&
            !audioSource.isPlaying
        )
        {
            audioSource.Play();
        }

        // Start flashing beacon.
        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);

        flashCoroutine =
            StartCoroutine(StrobeFlashRoutine());

        // Change activation button appearance.
        if (buttonMaterialInstance != null)
        {
            buttonMaterialInstance.color =
                new Color(1f, 0.85f, 0.05f);

            buttonMaterialInstance.EnableKeyword("_EMISSION");

            buttonMaterialInstance.SetColor(
                EmissionColorID,
                new Color(1f, 0.55f, 0.02f)
            );
        }

        // Activate emergency EXIT sign.
        if (exitSignController != null)
            exitSignController.SetEmergencyBackupState();

        OnAlarmActivated?.Invoke();

        Debug.Log(
            "[FireAlarmController] ALARM ACTIVATED."
        );
    }

    // =========================================================
    // DEACTIVATE ALARM
    // =========================================================

    public void DeactivateAlarm()
    {
        if (!isAlarmActive)
            return;

        isAlarmActive = false;

        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
        }

        if (strobeLight != null)
            strobeLight.intensity = 0f;

        if (strobeMaterialInstance != null)
        {
            strobeMaterialInstance.color =
                new Color(0.65f, 0.015f, 0.015f);

            strobeMaterialInstance.SetColor(
                EmissionColorID,
                Color.black
            );

            strobeMaterialInstance.DisableKeyword("_EMISSION");
        }

        if (buttonMaterialInstance != null)
        {
            buttonMaterialInstance.color =
                new Color(1f, 0.65f, 0.04f);

            buttonMaterialInstance.SetColor(
                EmissionColorID,
                Color.black
            );

            buttonMaterialInstance.DisableKeyword("_EMISSION");
        }

        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();

        OnAlarmDeactivated?.Invoke();

        Debug.Log(
            "[FireAlarmController] Alarm deactivated."
        );
    }

    // =========================================================
    // STROBE FLASH ROUTINE
    // =========================================================

    private IEnumerator StrobeFlashRoutine()
    {
        float halfPeriod = 0.5f / flashFrequency;

        bool strobeOn = true;

        while (isAlarmActive)
        {
            if (strobeLight != null)
            {
                strobeLight.enabled = true;

                strobeLight.intensity =
                    strobeOn ? strobeIntensity : 0.05f;

                strobeLight.color = alarmLightColor;
            }

            if (strobeMaterialInstance != null)
            {
                Color emission = strobeOn
                    ? alarmLightColor * 2.5f
                    : Color.black;

                strobeMaterialInstance.SetColor(
                    EmissionColorID,
                    emission
                );

                if (strobeOn)
                    strobeMaterialInstance.EnableKeyword("_EMISSION");
                else
                    strobeMaterialInstance.DisableKeyword("_EMISSION");
            }

            strobeOn = !strobeOn;

            yield return new WaitForSeconds(halfPeriod);
        }

        if (strobeLight != null)
            strobeLight.intensity = 0f;
    }

    // =========================================================
    // POWER CUT - BATTERY BACKUP
    // =========================================================

    public void OnMainPowerCut()
    {
        if (!isAlarmActive)
            return;

        if (strobeLight != null)
            strobeLight.enabled = true;

        if (
            audioSource != null &&
            !audioSource.isPlaying &&
            audioSource.clip != null
        )
        {
            audioSource.Play();
        }

        if (flashCoroutine == null)
        {
            flashCoroutine =
                StartCoroutine(StrobeFlashRoutine());
        }

        Debug.Log(
            "[FireAlarmController] Power cut - battery backup active."
        );
    }

    // =========================================================
    // CLEANUP
    // =========================================================

    private void OnDestroy()
    {
        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);

        if (strobeMaterialInstance != null)
            Destroy(strobeMaterialInstance);

        if (buttonMaterialInstance != null)
            Destroy(buttonMaterialInstance);

        if (housingMaterial != null)
            Destroy(housingMaterial);

        if (frameMaterial != null)
            Destroy(frameMaterial);

        if (whiteMaterial != null)
            Destroy(whiteMaterial);

        if (yellowMaterial != null)
            Destroy(yellowMaterial);

        if (darkMaterial != null)
            Destroy(darkMaterial);

        if (screwMaterial != null)
            Destroy(screwMaterial);
    }
}
