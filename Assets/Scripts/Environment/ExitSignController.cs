
using System.Collections;
using UnityEngine;

/// <summary>
/// Professional wall-mounted emergency EXIT sign.
/// Features:
/// - Compact metallic enclosure
/// - Silver front bezel
/// - Illuminated green sign panel
/// - White EXIT lettering and directional arrow
/// - Controlled green illumination
/// - Emergency battery backup
/// - Integration with FireAlarmController
/// </summary>
public class ExitSignController : MonoBehaviour
{
    public enum SignState
    {
        Normal,
        EmergencyBackup,
        Off
    }

    [Header("State")]
    [SerializeField] private SignState currentState = SignState.Normal;

    [Header("Sign Position and Dimensions")]
    [SerializeField] private Vector3 signPosition =
        new Vector3(205.43f, 2.32f, -0.08f);

    // Original orientation: faces into the kitchen (-X).
    [SerializeField] private Vector3 signRotation =
        new Vector3(0f, -90f, 0f);

    [SerializeField] private float signWidth = 0.52f;
    [SerializeField] private float signHeight = 0.25f;
    [SerializeField] private float signDepth = 0.045f;

    [Header("Sign Emission")]
    [ColorUsage(true, true)]
    [SerializeField] private Color normalSignEmission =
        new Color(0.08f, 0.35f, 0.12f, 1f);

    [ColorUsage(true, true)]
    [SerializeField] private Color emergencySignEmission =
        new Color(0.08f, 0.35f, 0.12f, 1f);

    [SerializeField] private Color offEmission = Color.black;

    [Header("Lighting")]
    [SerializeField] private Light signFaceLight;
    [SerializeField] private Light downwardThresholdLight;

    [SerializeField] private float normalFaceLightIntensity = 0.08f;
    [SerializeField] private float emergencyFaceLightIntensity = 0.07f;

    [SerializeField] private float faceLightRange = 0.55f;
    [SerializeField] private float downwardLightIntensity = 0.08f;

    private Material faceMaterial;
    private Material frameMaterial;
    private Material rimMaterial;
    private Material whiteMaterial;

    private Coroutine pulseCoroutine;

    private static readonly int EmissionColorID =
        Shader.PropertyToID("_EmissionColor");

    public SignState CurrentState => currentState;

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        BuildRealisticExitSignModel();
    }

    private void Start()
    {
        ApplyState(currentState);
    }

    private void Update()
    {
        // Controlled automatically by FireAlarm and PowerSwitch
    }

    // =========================================================
    // BUILD EXIT SIGN
    // =========================================================

    public void BuildRealisticExitSignModel()
    {
        // Remove previous generated sign.
        Transform existing = transform.Find("ExitSign_Board_Mesh");

        if (existing != null)
        {
            if (Application.isPlaying)
                Destroy(existing.gameObject);
            else
                DestroyImmediate(existing.gameObject);
        }

        // Hide legacy imported EXIT signs.
        string[] legacyNames =
        {
            "Sketchfab_model.004",
            "Exit sign.fbx_2",
            "Cube_4",
            "Cube_Material_0_5"
        };

        foreach (string objectName in legacyNames)
        {
            GameObject oldObject = GameObject.Find(objectName);

            if (oldObject != null)
            {
                Renderer oldRenderer =
                    oldObject.GetComponent<Renderer>();

                if (oldRenderer != null)
                    oldRenderer.enabled = false;
            }
        }

        // Root position and orientation.
        transform.position = signPosition;
        transform.eulerAngles = signRotation;

        GameObject root = new GameObject("ExitSign_Board_Mesh");

        root.transform.SetParent(transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;

        Shader litShader =
            Shader.Find("Universal Render Pipeline/Lit");

        if (litShader == null)
            litShader = Shader.Find("Standard");

        // =====================================================
        // 1. OUTER METALLIC CASING
        // =====================================================

        GameObject casing =
            GameObject.CreatePrimitive(PrimitiveType.Cube);

        casing.name = "OuterCasing";

        casing.transform.SetParent(root.transform, false);
        casing.transform.localPosition = Vector3.zero;

        casing.transform.localScale = new Vector3(
            signWidth + 0.025f,
            signHeight + 0.025f,
            signDepth
        );

        Destroy(casing.GetComponent<Collider>());

        frameMaterial = CreateLitMat(
            new Color(0.10f, 0.11f, 0.12f),
            0.75f,
            0.65f
        );

        casing.GetComponent<Renderer>().sharedMaterial =
            frameMaterial;

        // =====================================================
        // 2. SILVER METALLIC FRONT BEZEL
        // =====================================================

        GameObject rim =
            GameObject.CreatePrimitive(PrimitiveType.Cube);

        rim.name = "MetallicBezelRim";

        rim.transform.SetParent(root.transform, false);

        rim.transform.localPosition =
            new Vector3(0f, 0f, signDepth * 0.5f - 0.002f);

        rim.transform.localScale = new Vector3(
            signWidth + 0.008f,
            signHeight + 0.008f,
            0.006f
        );

        Destroy(rim.GetComponent<Collider>());

        rimMaterial = CreateLitMat(
            new Color(0.65f, 0.68f, 0.70f),
            0.85f,
            0.75f
        );

        rim.GetComponent<Renderer>().sharedMaterial =
            rimMaterial;

        // =====================================================
        // 3. GREEN SIGN FACE
        // =====================================================

        float faceZ = signDepth * 0.5f + 0.002f;

        GameObject face =
            GameObject.CreatePrimitive(PrimitiveType.Quad);

        face.name = "IlluminatedSignFace";

        face.transform.SetParent(root.transform, false);

        face.transform.localPosition =
            new Vector3(0f, 0f, faceZ);
        
        // Flip the sign face towards the viewing direction
face.transform.localRotation =
    Quaternion.Euler(0f, 180f, 0f);

        // IMPORTANT:
        // Keep local rotation at identity.
        // The root rotation of -90 degrees makes the face
        // point into the kitchen.
        // Do not flip this Quad independently.

        face.transform.localRotation = Quaternion.identity;

        face.transform.localScale =
            new Vector3(signWidth, signHeight, 1f);

        Destroy(face.GetComponent<Collider>());

        faceMaterial = new Material(litShader);

        faceMaterial.name = "EXIT_Green_Face";

        faceMaterial.color =
            new Color(0.015f, 0.20f, 0.045f);

        faceMaterial.SetFloat("_Metallic", 0f);
        faceMaterial.SetFloat("_Smoothness", 0.15f);

        faceMaterial.EnableKeyword("_EMISSION");

        faceMaterial.SetColor(
            EmissionColorID,
            normalSignEmission
        );

        face.GetComponent<Renderer>().sharedMaterial =
            faceMaterial;

        // =====================================================
        // 4. WHITE INNER BORDER
        // =====================================================

        whiteMaterial = CreateLitMat(
            new Color(0.9f, 0.9f, 0.9f),
            0f,
            0.15f
        );

        float border = 0.009f;
        float borderZ = faceZ + 0.004f;

        // Top border
        CreateSignBar(
            root.transform,
            "Top_WhiteBorder",
            new Vector3(
                0f,
                signHeight / 2f - border / 2f,
                borderZ
            ),
            new Vector3(
                signWidth - 0.018f,
                border,
                0.003f
            ),
            whiteMaterial
        );

        // Bottom border
        CreateSignBar(
            root.transform,
            "Bottom_WhiteBorder",
            new Vector3(
                0f,
                -signHeight / 2f + border / 2f,
                borderZ
            ),
            new Vector3(
                signWidth - 0.018f,
                border,
                0.003f
            ),
            whiteMaterial
        );

        // Left border
        CreateSignBar(
            root.transform,
            "Left_WhiteBorder",
            new Vector3(
                -signWidth / 2f + border / 2f,
                0f,
                borderZ
            ),
            new Vector3(
                border,
                signHeight - 0.018f,
                0.003f
            ),
            whiteMaterial
        );

        // Right border
        CreateSignBar(
            root.transform,
            "Right_WhiteBorder",
            new Vector3(
                signWidth / 2f - border / 2f,
                0f,
                borderZ
            ),
            new Vector3(
                border,
                signHeight - 0.018f,
                0.003f
            ),
            whiteMaterial
        );

        // =====================================================
        // 5. WHITE LEFT DIRECTIONAL ARROW
        // =====================================================

        float contentZ = borderZ + 0.005f;

        float arrowX = -signWidth * 0.29f;
        float arrowHeadX = -signWidth * 0.38f;

        // Arrow shaft
        CreateSignBar(
            root.transform,
            "Arrow_Shaft",
            new Vector3(arrowX, 0f, contentZ),
            new Vector3(
                signWidth * 0.19f,
                0.010f,
                0.003f
            ),
            whiteMaterial
        );

        // Upper arrow head
        CreateSignBar(
            root.transform,
            "Arrow_Head_Upper",
            new Vector3(arrowHeadX, 0.014f, contentZ),
            new Vector3(0.035f, 0.009f, 0.003f),
            whiteMaterial,
            -35f
        );

        // Lower arrow head
        CreateSignBar(
            root.transform,
            "Arrow_Head_Lower",
            new Vector3(arrowHeadX, -0.014f, contentZ),
            new Vector3(0.035f, 0.009f, 0.003f),
            whiteMaterial,
            35f
        );

        // =====================================================
        // 6. EXIT LETTERING
        // =====================================================

        GameObject exitText =
            new GameObject("EXIT_Lettering");

        exitText.transform.SetParent(root.transform, false);

        exitText.transform.localPosition =
            new Vector3(0.207f, -0.099f, 0.054f);

        exitText.transform.localEulerAngles =
            new Vector3(0f, 180f, 0f);

        TextMesh tm = exitText.AddComponent<TextMesh>();

        tm.text = "EXIT";
        tm.fontSize = 64;
        tm.characterSize = 0.05f;
        tm.fontStyle = FontStyle.Bold;
        tm.color = Color.white;

        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;

        // =====================================================
        // 7. SUBTLE GREEN FACE LIGHT
        // =====================================================

        GameObject faceLightObj =
            new GameObject("SignGlow_PointLight");

        faceLightObj.transform.SetParent(root.transform, false);

        // Light is positioned in front of the sign.
        faceLightObj.transform.localPosition =
            new Vector3(0.217f, -0.129f, 0.12f);

        signFaceLight = faceLightObj.AddComponent<Light>();

        signFaceLight.type = LightType.Point;

        signFaceLight.color =
            new Color(0.1f, 0.8f, 0.25f);

        signFaceLight.intensity =
            normalFaceLightIntensity;

        signFaceLight.range = faceLightRange;

        signFaceLight.shadows = LightShadows.None;

        // =====================================================
        // 8. SUBTLE DOWNWARD THRESHOLD LIGHT
        // =====================================================

        GameObject downLightObj =
            new GameObject("DownwardThreshold_Light");

        downLightObj.transform.SetParent(root.transform, false);

        downLightObj.transform.localPosition =
            new Vector3(
                0f,
                -signHeight * 0.5f - 0.025f,
                0.04f
            );

        downLightObj.transform.localEulerAngles =
            new Vector3(85f, 0f, 0f);

        downwardThresholdLight =
            downLightObj.AddComponent<Light>();

        downwardThresholdLight.type = LightType.Spot;

        downwardThresholdLight.spotAngle = 75f;
        downwardThresholdLight.innerSpotAngle = 45f;

        downwardThresholdLight.color =
            new Color(0.2f, 0.8f, 0.35f);

        downwardThresholdLight.intensity =
            downwardLightIntensity;

        downwardThresholdLight.range = 0.7f;

        downwardThresholdLight.shadows =
            LightShadows.None;

        Debug.Log(
            "[ExitSignController] Compact EXIT sign generated."
        );
    }

    // =========================================================
    // HELPER: CREATE SIGN COMPONENT
    // =========================================================

    private GameObject CreateSignBar(
        Transform parent,
        string objectName,
        Vector3 localPosition,
        Vector3 localScale,
        Material material,
        float rotationZ = 0f)
    {
        GameObject bar =
            GameObject.CreatePrimitive(PrimitiveType.Cube);

        bar.name = objectName;

        bar.transform.SetParent(parent, false);

        bar.transform.localPosition = localPosition;

        bar.transform.localRotation =
            Quaternion.Euler(0f, 0f, rotationZ);

        bar.transform.localScale = localScale;

        Destroy(bar.GetComponent<Collider>());

        bar.GetComponent<Renderer>().sharedMaterial =
            material;

        return bar;
    }

    // =========================================================
    // APPLY SIGN STATES
    // =========================================================

    public void ApplyState(SignState state)
    {
        currentState = state;

        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }

        switch (state)
        {
            case SignState.Normal:

                SetEmissiveColor(normalSignEmission);

                if (signFaceLight != null)
                {
                    signFaceLight.enabled = true;

                    signFaceLight.intensity =
                        normalFaceLightIntensity;
                }

                if (downwardThresholdLight != null)
                {
                    downwardThresholdLight.enabled = true;

                    downwardThresholdLight.intensity =
                        downwardLightIntensity;
                }

                break;

            case SignState.EmergencyBackup:

                pulseCoroutine =
                    StartCoroutine(EmergencyPulseRoutine());

                break;

            case SignState.Off:

                SetEmissiveColor(offEmission);

                if (signFaceLight != null)
                    signFaceLight.enabled = false;

                if (downwardThresholdLight != null)
                    downwardThresholdLight.enabled = false;

                break;
        }
    }

    public void SetNormalState()
    {
        ApplyState(SignState.Normal);
    }

    public void SetEmergencyBackupState()
    {
        ApplyState(SignState.EmergencyBackup);
    }

    public void SetOffState()
    {
        ApplyState(SignState.Off);
    }

    // =========================================================
    // EMISSION CONTROL
    // =========================================================

    private void SetEmissiveColor(Color color)
    {
        if (faceMaterial != null)
        {
            faceMaterial.SetColor(
                EmissionColorID,
                color
            );
        }
    }

    // =========================================================
    // EMERGENCY BATTERY BACKUP
    // =========================================================

    private IEnumerator EmergencyPulseRoutine()
    {
        // Emergency mode keeps the sign green and visible.

        if (signFaceLight != null)
        {
            signFaceLight.enabled = true;

            signFaceLight.color =
                new Color(0.1f, 0.8f, 0.25f);
        }

        if (downwardThresholdLight != null)
        {
            downwardThresholdLight.enabled = true;

            downwardThresholdLight.color =
                new Color(0.2f, 0.8f, 0.35f);
        }

        while (true)
        {
            float t =
                (Mathf.Sin(Time.time * 1.5f) + 1f) * 0.5f;

            Color currentEmission = Color.Lerp(
                emergencySignEmission * 0.85f,
                emergencySignEmission * 1.10f,
                t
            );

            SetEmissiveColor(currentEmission);

            if (signFaceLight != null)
            {
                signFaceLight.intensity = Mathf.Lerp(
                    emergencyFaceLightIntensity * 0.85f,
                    emergencyFaceLightIntensity * 1.10f,
                    t
                );
            }

            if (downwardThresholdLight != null)
            {
                downwardThresholdLight.intensity =
                    Mathf.Lerp(
                        downwardLightIntensity * 0.8f,
                        downwardLightIntensity * 1.1f,
                        t
                    );
            }

            yield return null;
        }
    }

    // =========================================================
    // MATERIAL CREATION
    // =========================================================

    private static Material CreateLitMat(
        Color color,
        float metallic,
        float smoothness)
    {
        Shader shader =
            Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);

        material.color = color;

        material.SetFloat("_Metallic", metallic);

        material.SetFloat("_Smoothness", smoothness);

        return material;
    }

    // =========================================================
    // CLEANUP
    // =========================================================

    private void OnDestroy()
    {
        if (pulseCoroutine != null)
            StopCoroutine(pulseCoroutine);

        if (faceMaterial != null)
            Destroy(faceMaterial);

        if (frameMaterial != null)
            Destroy(frameMaterial);

        if (rimMaterial != null)
            Destroy(rimMaterial);

        if (whiteMaterial != null)
            Destroy(whiteMaterial);
    }
}
