using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FireSafety.Extinguishers;
using FireSafety.Fire;
using FireSafety.Training;

/// <summary>
/// Full Figma-matching Training HUD overlay for the VR Fire Safety Training project.
///
/// TWO ROOM ARCHITECTURE:
///   ROOM 01: Orientation Studio Chamber (at world 0, 0, 0)
///            Features equipment table with Fire Blanket, Extinguisher, Helmet, Gloves.
///            Displays:
///              - Screen 1: "Welcome Trainee" hologram with [START TRAINING ->]
///              - Screen 2: "SELECT TRAINING ENVIRONMENT" with [KITCHEN], [WAREHOUSE], [CHEMICAL LAB], [OIL REFINERY]
///            When trainee selects KITCHEN -> Smooth screen fade transition teleports player to Kitchen.
///
///   ROOM 02: Kitchen Environment (at world 202.5, 1.6, 2.5)
///            Displays:
///              - Kitchen Environment Briefing
///              - Electrical Chimney Fire Eruption above stove
///              - Objective 01: Fire Alarm activation ([A] key / click)
///              - Objective 02: Power Cut ([L] key / click)
///              - Objective 03: Extinguisher selection (CO2 guidance popup, hover inspection, severe warning on wrong pick)
///              - Objective 04: P.A.S.S. procedure hologram
///              - Objective 05: Active extinguishing with realistic cryogenic CO2 vapor
///              - Mission Complete & Dynamic Performance Debrief (score based on response time & penalties!)
/// </summary>
public class TrainingHUD : MonoBehaviour
{
    // ─── Color Palette Matching Figma Dark-Glass Theme ───────────────────────
    static readonly Color CyanBorder   = new Color(0.341f, 0.902f, 0.949f, 1f);     // #57e6f2
    static readonly Color DarkGlass    = new Color(0.039f, 0.094f, 0.133f, 0.94f);  // #0A1822
    static readonly Color CardHeaderBg = new Color(0.065f, 0.135f, 0.185f, 0.95f);
    static readonly Color TextCyan     = new Color(0.341f, 0.902f, 0.949f, 1f);
    static readonly Color TextWhite    = new Color(0.96f,  0.96f,  0.97f,  1f);
    static readonly Color TextMuted    = new Color(0.72f,  0.75f,  0.80f,  1f);
    static readonly Color TextOrange   = new Color(1f,     0.65f,  0.20f,  1f);
    static readonly Color TextRed      = new Color(1f,     0.25f,  0.20f,  1f);
    static readonly Color TextGreen    = new Color(0.25f,  0.92f,  0.45f,  1f);
    static readonly Color BgSuccess    = new Color(0.08f,  0.30f,  0.14f,  0.92f);
    static readonly Color BgDanger     = new Color(0.45f,  0.06f,  0.06f,  0.96f);
    static readonly Color BgObjCard    = new Color(0.039f, 0.094f, 0.133f, 0.94f);

    // ─── State Machine ───────────────────────────────────────────────────────
    public enum TrainingState
    {
        WelcomeScreen = 0,            // Room 01: Welcome Trainee
        SelectEnvironment = 1,        // Room 01: Select Training Environment
        RedirectingToKitchen = 2,     // Cinematic Fade Transition from Room 01 to Room 02
        KitchenBriefing = 3,          // Room 02: Kitchen Briefing
        FireIgnition = 4,             // Room 02: Electrical Chimney Fire starts
        Obj01_Alarm = 5,              // Room 02: Activate Fire Alarm
        Obj02_PowerCut = 6,           // Room 02: Isolate Power
        Obj03_SelectExtinguisher = 7,  // Room 02: Select Extinguisher (CO2)
        Obj04_PASSProcedure = 8,      // Room 02: P.A.S.S. Hologram
        Obj05_Extinguishing = 9,      // Room 02: Discharge spray onto fire
        MissionComplete = 10          // Room 02: Performance Debrief
    }

    private TrainingState currentState = TrainingState.WelcomeScreen;
    public TrainingState State => currentState;
    private string currentRoomId = "ROOM 01";

    // Progress Flags
    private bool obj01Done = false;
    private bool obj02Done = false;
    private bool obj03Done = false;
    private bool fireSuppressed = false;

    // Extinguisher Selection & Warnings
    private ExtinguisherType selectedExt = ExtinguisherType.CO2;
    private bool showExtCard = false;
    private bool wrongExtAlert = false;
    private string wrongExtTitle = "";
    private string wrongExtMsg = "";
    private float wrongAlertTimer = 0f;
    private int wrongAttemptsCount = 0;
    private FireExtinguisher hoveredExtinguisher = null;

    // Fire Health
    private float fireStatusPct = 100f;

    // Time Tracking for Dynamic Score
    private float fireStartTime = 0f;
    private float fireFinalTime = 0f;
    private bool isStopwatchRunning = false;

    // Objective Complete Toast
    private bool showObjComplete = false;
    private string objCompleteMsg = "";
    private float objCompleteTimer = 0f;

    // Fullscreen Screen Transition Fade
    private float screenFadeAlpha = 0f;
    private bool isFading = false;

    // References
    private FireVFXController fireVFX;
    private FireAlarmController fireAlarm;
    private PowerSwitchController powerSwitch;
    private FireExtinguisher heldExt;
    private KitchenPlayerController playerController;

    // Textures for High-Resolution Clear UI
    private static Texture2D texDarkGlass;
    private static Texture2D texOrangeGradient;
    private static Texture2D texCyanBorder;
    private static Texture2D texFlameBadge;
    private static Texture2D texBlack;

    // ─── Unity Lifecycle ─────────────────────────────────────────────────────
    void Awake()
    {
        InitTextures();
    }

    void Start()
    {
        fireVFX          = FindObjectOfType<FireVFXController>();
        fireAlarm        = FindObjectOfType<FireAlarmController>();
        powerSwitch      = FindObjectOfType<PowerSwitchController>();
        playerController = FindObjectOfType<KitchenPlayerController>();

        // Wire event listeners
        if (fireAlarm != null)
        {
            fireAlarm.OnAlarmActivated += HandleAlarmActivated;
        }
        if (powerSwitch != null)
        {
            powerSwitch.OnPowerStateChanged += HandlePowerChanged;
        }
        if (fireVFX != null)
        {
            fireVFX.OnFireExtinguished += HandleFireExtinguished;
            fireVFX.ExtinguishFire(); // Fire starts off; ignites at FireIgnition state
        }

        // Start with the Figma Welcome Screen (Frame 4:50)
        currentRoomId = "ROOM 01";
        currentState  = TrainingState.WelcomeScreen;

        // Spawn 3D VR Holographic Training Board in Kitchen
        if (FindObjectOfType<VRTrainingBoard>() == null)
        {
            new GameObject("VR_Training_Hologram_Board").AddComponent<VRTrainingBoard>();
        }

        Debug.Log("[TrainingHUD] Initialized. Starting at Welcome Screen (Figma Workflow).");

        // Redesign cookware on the stove and room planters
        FireSafety.Environment.KitchenDecorRedesigner.RedesignAllPots();
    }

    void OnDestroy()
    {
        if (fireAlarm != null)   fireAlarm.OnAlarmActivated -= HandleAlarmActivated;
        if (powerSwitch != null) powerSwitch.OnPowerStateChanged -= HandlePowerChanged;
        if (fireVFX != null)     fireVFX.OnFireExtinguished -= HandleFireExtinguished;
    }

    void Update()
    {
        // Stopwatch tracking
        if (isStopwatchRunning)
        {
            fireFinalTime = Mathf.Max(0.1f, Time.time - fireStartTime);
        }

        // Track held extinguisher
        heldExt = null;
        foreach (var ext in FindObjectsOfType<FireExtinguisher>())
        {
            if (ext.IsHeld) { heldExt = ext; break; }
        }

        // Raycast detection for extinguisher inspection tooltips
        if (currentState == TrainingState.Obj03_SelectExtinguisher)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Ray ray = new Ray(cam.transform.position, cam.transform.forward);
                if (Physics.Raycast(ray, out RaycastHit hit, 5.0f))
                {
                    hoveredExtinguisher = hit.collider.GetComponentInParent<FireExtinguisher>();
                }
                else
                {
                    hoveredExtinguisher = null;
                }
            }
        }
        else
        {
            hoveredExtinguisher = null;
        }

        // Update fire health bar during extinguishing
        if (currentState == TrainingState.Obj05_Extinguishing && fireVFX != null)
        {
            fireStatusPct = Mathf.Round(fireVFX.FireIntensity * 100f);
            if (fireVFX.FireIntensity <= 0.01f && !fireSuppressed)
            {
                HandleFireExtinguished();
            }
        }

        // Timers
        if (wrongAlertTimer > 0f)
        {
            wrongAlertTimer -= Time.deltaTime;
            if (wrongAlertTimer <= 0f) wrongExtAlert = false;
        }

        if (objCompleteTimer > 0f)
        {
            objCompleteTimer -= Time.deltaTime;
            if (objCompleteTimer <= 0f) showObjComplete = false;
        }

        HandleInputHotkeys();
    }

    void HandleInputHotkeys()
    {
        bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        bool t     = Input.GetKeyDown(KeyCode.T); // Debug skip

        // Check VR Controller [A / Primary] or Trigger button
        bool vrAdvance = false;
        var rDevs = new List<UnityEngine.XR.InputDevice>();
        UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(UnityEngine.XR.InputDeviceCharacteristics.HeldInHand | UnityEngine.XR.InputDeviceCharacteristics.Right, rDevs);
        if (rDevs.Count > 0 && rDevs[0].isValid)
        {
            if (rDevs[0].TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool aBtn) && aBtn)
            {
                vrAdvance = true;
            }
            if (rDevs[0].TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool trig) && trig)
            {
                vrAdvance = true;
            }
        }

        // Only Enter key or VR controller primary button advances modal dialogs.
        // Clicks, Space (spray), and E (turn) are reserved for player interaction.
        bool proceed = enter || vrAdvance;

        if (t) { DebugSkip(); return; }

        switch (currentState)
        {
            case TrainingState.WelcomeScreen:
                if (proceed)
                {
                    AdvanceTo(TrainingState.SelectEnvironment);
                }
                break;

            case TrainingState.SelectEnvironment:
                if (proceed)
                {
                    RedirectToKitchen();
                }
                break;

            case TrainingState.KitchenBriefing:
                if (proceed)
                {
                    AdvanceTo(TrainingState.FireIgnition);
                }
                break;

            case TrainingState.FireIgnition:
                if (proceed)
                {
                    AdvanceTo(TrainingState.Obj01_Alarm);
                }
                break;

            case TrainingState.Obj03_SelectExtinguisher:
                if (showExtCard && proceed)
                {
                    ConfirmCO2Selection();
                }
                break;

            case TrainingState.Obj04_PASSProcedure:
                if (proceed)
                {
                    AdvanceTo(TrainingState.Obj05_Extinguishing);
                }
                break;
        }
    }

    // ─── Navigation & Teleport to Kitchen ────────────────────────────────────
    public void RedirectToKitchen()
    {
        if (isFading || currentState == TrainingState.RedirectingToKitchen) return;
        StartCoroutine(TransitionToKitchenRoutine());
    }

    private IEnumerator TransitionToKitchenRoutine()
    {
        isFading = true;
        currentState = TrainingState.RedirectingToKitchen;

        // 1. Fade screen out to black (0.35s)
        float elapsed = 0f;
        while (elapsed < 0.35f)
        {
            elapsed += Time.deltaTime;
            screenFadeAlpha = Mathf.Clamp01(elapsed / 0.35f);
            yield return null;
        }
        screenFadeAlpha = 1.0f;

        // 2. Teleport player and camera to Kitchen facing stove & fire
        VRCameraAnchor.SetKitchenAnchor();

        Vector3 kitchenPos = new Vector3(202.80f, 1.60f, 0.0f);
        Vector3 kitchenRot = new Vector3(0f, -90f, 0f);

        if (playerController != null)
        {
            playerController.Teleport(kitchenPos, kitchenRot);
        }
        else
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = kitchenPos;
                cam.transform.eulerAngles = kitchenRot;
            }
        }

        // 3. Switch telemetry to ROOM 02
        currentRoomId = "ROOM 02";
        currentState = TrainingState.KitchenBriefing;
        Debug.Log("[TrainingHUD] Trainee redirected to ROOM 02 (Kitchen Environment).");

        // 4. Fade back in from black (0.45s)
        elapsed = 0f;
        while (elapsed < 0.45f)
        {
            elapsed += Time.deltaTime;
            screenFadeAlpha = 1.0f - Mathf.Clamp01(elapsed / 0.45f);
            yield return null;
        }
        screenFadeAlpha = 0f;
        isFading = false;
    }

    void AdvanceTo(TrainingState next)
    {
        currentState = next;
        Debug.Log($"[TrainingHUD] State: {next}");

        if (next == TrainingState.FireIgnition)
        {
            // Start Stopwatch when fire ignites
            fireStartTime = Time.time;
            isStopwatchRunning = true;
            StartCoroutine(IgniteChimneyFireAfter(1.2f));
        }
        else if (next == TrainingState.Obj05_Extinguishing)
        {
            fireStatusPct = 100f;
            fireSuppressed = false;
        }
    }

    void DebugSkip()
    {
        int next = (int)currentState + 1;
        if (next == (int)TrainingState.RedirectingToKitchen) next++;
        if (next > (int)TrainingState.MissionComplete) next = 0;

        if (next >= (int)TrainingState.KitchenBriefing && currentRoomId == "ROOM 01")
        {
            RedirectToKitchen();
        }
        else
        {
            AdvanceTo((TrainingState)next);
        }
    }

    // ─── Event Callbacks ─────────────────────────────────────────────────────
    void HandleAlarmActivated()
    {
        if (currentState == TrainingState.Obj01_Alarm && !obj01Done)
        {
            obj01Done = true;
            StartCoroutine(ShowObjectiveCompleteToast("✓ FIRE ALARM ACTIVATED", 2.5f,
                () => AdvanceTo(TrainingState.Obj02_PowerCut)));
        }
    }

    void HandlePowerChanged(bool isOn)
    {
        if (!isOn && currentState == TrainingState.Obj02_PowerCut && !obj02Done)
        {
            obj02Done = true;
            StartCoroutine(ShowObjectiveCompleteToast("✓ POWER SUPPLY ISOLATED", 2.5f,
                () => AdvanceTo(TrainingState.Obj03_SelectExtinguisher)));
        }
    }

    void HandleFireExtinguished()
    {
        if (currentState == TrainingState.Obj05_Extinguishing && !fireSuppressed)
        {
            fireSuppressed = true;
            // Freeze final response time
            if (isStopwatchRunning)
            {
                fireFinalTime = Mathf.Max(1.0f, Time.time - fireStartTime);
                isStopwatchRunning = false;
            }

            StartCoroutine(ShowObjectiveCompleteToast("🔥 CHIMNEY FIRE EXTINGUISHED!", 3.0f,
                () => AdvanceTo(TrainingState.MissionComplete)));
        }
    }

    IEnumerator IgniteChimneyFireAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (fireVFX != null) fireVFX.IgniteFire();
        Debug.Log("[TrainingHUD] 🔥 Fire ignited at electrical chimney hood!");
    }

    IEnumerator ShowObjectiveCompleteToast(string msg, float duration, System.Action onFinished = null)
    {
        objCompleteMsg = msg;
        showObjComplete = true;
        objCompleteTimer = duration;

        yield return new WaitForSeconds(duration + 0.2f);
        showObjComplete = false;
        onFinished?.Invoke();
    }

    // ─── Public API for external callers ────────────────────────────────────

    /// <summary>True when a modal dialog is currently presented that requires trainee confirmation.</summary>
    public bool IsInModalState =>
        currentState == TrainingState.WelcomeScreen ||
        currentState == TrainingState.SelectEnvironment ||
        currentState == TrainingState.KitchenBriefing ||
        currentState == TrainingState.FireIgnition ||
        currentState == TrainingState.Obj04_PASSProcedure ||
        currentState == TrainingState.MissionComplete;

    /// <summary>Called by VRControllerManager when trigger / A button is pressed on a modal screen.</summary>
    public void OnVRTriggerPressed()
    {
        // Advance modal state only on screens that await a proceed action
        switch (currentState)
        {
            case TrainingState.WelcomeScreen:
                AdvanceTo(TrainingState.SelectEnvironment);
                break;
            case TrainingState.SelectEnvironment:
                RedirectToKitchen();
                break;
            case TrainingState.KitchenBriefing:
                AdvanceTo(TrainingState.FireIgnition);
                break;
            case TrainingState.FireIgnition:
                AdvanceTo(TrainingState.Obj01_Alarm);
                break;
            case TrainingState.Obj04_PASSProcedure:
                AdvanceTo(TrainingState.Obj05_Extinguishing);
                break;
            case TrainingState.MissionComplete:
                VRCameraAnchor.SetRoom01Anchor();
                currentRoomId = "ROOM 01";
                AdvanceTo(TrainingState.WelcomeScreen);
                break;
        }
    }

    /// <summary>Debug: skip current objective (called by [T] key).</summary>
    public void DebugSkipObjective() { DebugSkip(); }

    // ─── Extinguisher Selection & Safety Warnings ─────────────────────────────
    public void OnExtinguisherSelected(ExtinguisherType type)
    {
        if (currentState != TrainingState.Obj03_SelectExtinguisher) return;

        selectedExt = type;

        if (type == ExtinguisherType.CO2)
        {
            showExtCard = true;
            wrongExtAlert = false;
        }
        else
        {
            showExtCard = false;
            wrongExtAlert = true;
            wrongAlertTimer = 5.0f;
            wrongAttemptsCount++;

            switch (type)
            {
                case ExtinguisherType.Water:
                    wrongExtTitle = "⚡ CRITICAL SAFETY VIOLATION! ELECTROCUTION HAZARD";
                    wrongExtMsg = "Water is a powerful electrical conductor! Spraying water onto an energized electrical appliance or exhaust motor causes FATAL electric shock to the operator.";
                    break;

                case ExtinguisherType.Foam:
                    wrongExtTitle = "⚠ SAFETY HAZARD: CONDUCTIVE LIQUID";
                    wrongExtMsg = "Aqueous film-forming foam contains water and conducts electrical current. Risk of electric shock on live kitchen appliances.";
                    break;

                case ExtinguisherType.DryPowder:
                    wrongExtTitle = "⚠ WARNING: SEVERE CORROSIVE EQUIPMENT DAMAGE";
                    wrongExtMsg = "Dry chemical powder contains acidic ammonium phosphate salts that melt into circuits, permanently destroying electrical components and ventilation fans.";
                    break;

                case ExtinguisherType.WetChemical:
                    wrongExtTitle = "⚠ INCOMPATIBLE AGENT: CLASS F COOKING OIL ONLY";
                    wrongExtMsg = "Wet Chemical is formulated exclusively for saponifying hot commercial cooking grease/fat, not electrical motor and hood fires.";
                    break;

                default:
                    wrongExtTitle = "⚠ INCORRECT AGENT SELECTED";
                    wrongExtMsg = "This extinguisher is not rated for electrical fires. Select CO₂ (Carbon Dioxide).";
                    break;
            }

            Debug.LogWarning($"[TrainingHUD] Safety Violation: {type} selected. Wrong count: {wrongAttemptsCount}");

            // Drop any equipped wrong extinguisher
            if (heldExt != null)
            {
                heldExt.Drop();
            }
        }
    }

    void ConfirmCO2Selection()
    {
        if (!obj03Done && selectedExt == ExtinguisherType.CO2)
        {
            obj03Done = true;
            showExtCard = false;
            wrongExtAlert = false;

            StartCoroutine(ShowObjectiveCompleteToast("✓ CO₂ EXTINGUISHER SELECTED", 1.5f,
                () => AdvanceTo(TrainingState.Obj04_PASSProcedure)));
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  OnGUI — CRISP, HIGH-FIDELITY FIGMA RENDERING
    // ═════════════════════════════════════════════════════════════════════════
    void OnGUI()
    {
        // In VR Headset mode, the 3D Holographic VRTrainingBoard terminal handles all UI in world space!
        // Suppress 2D screen-space IMGUI in VR so it doesn't appear misaligned/distorted on one eye.
        if (UnityEngine.XR.XRSettings.isDeviceActive)
        {
            if (screenFadeAlpha > 0.001f)
            {
                GUI.color = new Color(0f, 0f, 0f, screenFadeAlpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), GetBlackTexture());
                GUI.color = Color.white;
            }
            return;
        }

        // 1. Persistent Top Telemetry Bar
        DrawTelemetryBar();

        // 2. Subtle center crosshair dot
        DrawCrosshair();

        // 3. State-Specific Screens
        switch (currentState)
        {
            case TrainingState.WelcomeScreen:            DrawWelcomeScreen();            break;
            case TrainingState.SelectEnvironment:        DrawSelectEnvironmentScreen();  break;
            case TrainingState.KitchenBriefing:          DrawKitchenBriefingScreen();    break;
            case TrainingState.FireIgnition:             DrawFireIgnitionScreen();       break;
            case TrainingState.Obj01_Alarm:              DrawObj01Screen();              break;
            case TrainingState.Obj02_PowerCut:           DrawObj02Screen();              break;
            case TrainingState.Obj03_SelectExtinguisher: DrawObj03Screen();              break;
            case TrainingState.Obj04_PASSProcedure:      DrawObj04Screen();              break;
            case TrainingState.Obj05_Extinguishing:      DrawObj05Screen();              break;
            case TrainingState.MissionComplete:          DrawMissionCompleteScreen();    break;
        }

        // 4. Critical Safety Warning Popup (when wrong extinguisher is picked)
        if (wrongExtAlert && wrongAlertTimer > 0f)
        {
            DrawSevereWarningPopup();
        }

        // 5. Objective Complete Green Toast Banner
        if (showObjComplete) DrawObjectiveToast();

        // 6. Fullscreen cinematic fade overlay (during Room 01 -> Room 02 teleport)
        if (screenFadeAlpha > 0.001f)
        {
            GUI.color = new Color(0f, 0f, 0f, screenFadeAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), GetBlackTexture());
            GUI.color = Color.white;
        }

        // 7. Bottom Key Prompt Bar
        DrawBottomStatusBar();
    }

    // ─── Screen 1: Welcome Trainee (Room 01) — Matches Image 1 ────────────────
    void DrawWelcomeScreen()
    {
        float W = Screen.width, H = Screen.height;
        float pw = Mathf.Min(640f, W - 60f);
        float ph = 350f;
        float px = (W - pw) * 0.5f;
        float py = (H - ph) * 0.5f - 20f;

        DrawHologramCard(px, py, pw, ph);

        // Header: Flame Badge + FIRE SAFETY TRAINING + MODULE 01 / ORIENTATION
        DrawFlameHeader(px + 24, py + 18, pw - 48, "FIRE SAFETY TRAINING", "MODULE 01 / ORIENTATION");
        DrawHorizontalRule(px + 24, py + 48, pw - 48, new Color(0.18f, 0.32f, 0.42f, 0.70f));

        // Center Title: "Welcome Trainee"
        Label(px, py + 72, pw, 38, "Welcome Trainee", StyleCenter(28, TextWhite, FontStyle.Bold));

        // Subtitle text
        Label(px + 45, py + 120, pw - 90, 48,
            "Learn how to identify fire hazards, respond to emergencies, and safely control different types of fires.",
            StyleCenter(14, TextMuted));

        // Button: [START TRAINING ->] (orange-to-red gradient pill button)
        float btnW = 280f, btnH = 50f;
        float btnX = px + (pw - btnW) * 0.5f;
        float btnY = py + 188f;

        if (DrawOrangeGradientButton(btnX, btnY, btnW, btnH, "START TRAINING  →"))
        {
            AdvanceTo(TrainingState.SelectEnvironment);
        }

        // Subtext below button: "Aim and press trigger to select"
        Label(px, py + 252f, pw, 22, "⭘  Aim and press trigger to select", StyleCenter(12, TextCyan));
    }

    // ─── Screen 2: Select Training Environment (Room 01) — Matches Image 2 ─────
    void DrawSelectEnvironmentScreen()
    {
        float W = Screen.width, H = Screen.height;
        float pw = Mathf.Min(640f, W - 60f);
        float ph = 440f;
        float px = (W - pw) * 0.5f;
        float py = (H - ph) * 0.5f - 20f;

        DrawHologramCard(px, py, pw, ph);

        // Header
        DrawFlameHeader(px + 24, py + 16, pw - 48, "FIRE SAFETY TRAINING", "MODULE 01 / ORIENTATION");
        DrawHorizontalRule(px + 24, py + 44, pw - 48, new Color(0.18f, 0.32f, 0.42f, 0.70f));

        // Title: "SELECT TRAINING ENVIRONMENT"
        Label(px, py + 56, pw, 32, "SELECT TRAINING ENVIRONMENT", StyleCenter(20, TextWhite, FontStyle.Bold));
        Label(px, py + 88, pw, 20, "⭘  Aim and press trigger to select", StyleCenter(11, TextCyan));

        // 4 Orange Gradient Buttons
        string[] scenarios = { "KITCHEN", "WAREHOUSE", "CHEMICAL LAB", "OIL REFINERY" };
        float btnW = 260f, btnH = 46f;
        float btnX = px + (pw - btnW) * 0.5f;

        for (int i = 0; i < scenarios.Length; i++)
        {
            float btnY = py + 120f + i * 58f;
            bool isKitchen = (i == 0);

            if (isKitchen)
            {
                if (DrawOrangeGradientButton(btnX, btnY, btnW, btnH, scenarios[i]))
                {
                    RedirectToKitchen();
                }
            }
            else
            {
                // Secondary / locked scenario buttons
                GUI.color = new Color(0.20f, 0.28f, 0.35f, 0.75f);
                GUI.DrawTexture(new Rect(btnX, btnY, btnW, btnH), GetDarkGlassTexture());
                GUI.color = new Color(0.35f, 0.50f, 0.60f, 0.50f);
                GUI.Box(new Rect(btnX, btnY, btnW, btnH), "");
                GUI.color = Color.white;

                Label(btnX, btnY, btnW, btnH, $"{scenarios[i]}  (Locked)", StyleCenter(14, new Color(0.55f, 0.60f, 0.68f)));
            }
        }
    }

    // ─── Screen 3: Kitchen Environment Briefing (Room 02 - Inside Kitchen) ────
    void DrawKitchenBriefingScreen()
    {
        float W = Screen.width, H = Screen.height;
        float pw = Mathf.Min(600f, W - 60f);
        float ph = 410f;
        float px = (W - pw) * 0.5f;
        float py = (H - ph) * 0.5f;

        DrawHologramCard(px, py, pw, ph);

        // Header
        Label(px, py + 14, pw, 28, "KITCHEN ENVIRONMENT BRIEFING", StyleCenter(20, TextCyan, FontStyle.Bold));
        DrawHorizontalRule(px + 24, py + 46, pw - 48, CyanBorder);

        // Inner Dark Briefing Card
        float cx = px + 24, cy = py + 58, cw = pw - 48, ch = 246f;
        GUI.color = CardHeaderBg;
        GUI.DrawTexture(new Rect(cx, cy, cw, ch), GetDarkGlassTexture());
        GUI.color = new Color(0.18f, 0.35f, 0.48f, 0.80f);
        GUI.Box(new Rect(cx, cy, cw, ch), "");
        GUI.color = Color.white;

        Label(cx + 16, cy + 12, cw - 32, 22, "⚡ Potential Fire:  Electrical chimney / exhaust hood", StyleLeft(14, TextOrange, FontStyle.Bold));
        DrawHorizontalRule(cx + 16, cy + 38, cw - 32, new Color(0.25f, 0.35f, 0.45f, 0.60f));

        Label(cx + 16, cy + 46, cw - 32, 20, "⚠ Major Hazards:", StyleLeft(13, TextRed, FontStyle.Bold));
        string[] hazards = { "• Cooking equipment & hot surfaces", "• Gas supply lines", "• Electrical appliances & exhaust motor" };
        for (int i = 0; i < hazards.Length; i++)
            Label(cx + 28, cy + 68 + i * 22, cw - 40, 20, hazards[i], StyleLeft(13, TextWhite));

        DrawHorizontalRule(cx + 16, cy + 142, cw - 32, new Color(0.25f, 0.35f, 0.45f, 0.60f));
        Label(cx + 16, cy + 150, cw - 32, 18, "YOUR OBJECTIVE:", StyleLeft(12, TextCyan, FontStyle.Bold));
        Label(cx + 16, cy + 170, cw - 32, 64,
            "Identify the fire, activate the emergency alarm, isolate the power supply, " +
            "select the appropriate CO₂ extinguisher, and extinguish the fire safely.",
            StyleWrap(12, TextWhite));

        // Guardian Boundary indicator
        Label(px, py + ph - 62, pw, 18, "🛡  GUARDIAN BOUNDARY ACTIVE  •  CLEAR PLAY AREA", StyleCenter(11, new Color(0.95f, 0.80f, 0.20f)));

        // Begin Button
        float btnW = 240f, btnH = 38f;
        if (DrawOrangeGradientButton(px + (pw - btnW) * 0.5f, py + ph - 44, btnW, btnH, "▶  BEGIN SIMULATION"))
        {
            AdvanceTo(TrainingState.FireIgnition);
        }
    }

    // ─── Fire Ignition Alert (Room 02) ────────────────────────────────────────
    void DrawFireIgnitionScreen()
    {
        float W = Screen.width, H = Screen.height;
        float pw = Mathf.Min(520f, W - 60f);
        float ph = 120f;
        float px = (W - pw) * 0.5f;
        float py = H * 0.22f;

        float pulse = Mathf.PingPong(Time.time * 3f, 1f);
        GUI.color = new Color(1f, 0.15f, 0.15f, 0.35f + pulse * 0.35f);
        GUI.DrawTexture(new Rect(px - 3, py - 3, pw + 6, ph + 6), GetCyanBorderTexture());
        GUI.color = Color.white;

        DrawHologramCard(px, py, pw, ph);
        Label(px, py + 12, pw, 32, "🔥  FIRE DETECTED!", StyleCenter(24, TextRed, FontStyle.Bold));
        Label(px, py + 48, pw, 22, "Electrical chimney / exhaust hood above stove ignited!", StyleCenter(14, TextOrange));
        Label(px, py + 78, pw, 20, "Press [Enter] or click to begin emergency response", StyleCenter(12, TextWhite));
    }

    // ─── Objective 01: Activate Alarm (Room 02) ───────────────────────────────
    void DrawObj01Screen()
    {
        DrawObjectiveHUDCard("OBJECTIVE 01", "ACTIVATE FIRE ALARM",
            "Locate the red manual fire alarm on the wall and trigger it.\n[A] key  or  Click the wall-mounted call point.");

        DrawInteractWorldHint(0.78f, 0.40f, "☝  Interact\nFire Alarm Panel →");
    }

    // ─── Objective 02: Isolate Power (Room 02) ────────────────────────────────
    void DrawObj02Screen()
    {
        DrawObjectiveHUDCard("OBJECTIVE 02", "ISOLATE ELECTRICAL SUPPLY",
            "Turn OFF the main circuit breaker before using extinguishing agents.\n[L] key  or  Click the power panel.");

        DrawInteractWorldHint(0.80f, 0.45f, "☝  Interact\nMain Breaker →");

        // Power Status readout
        float W = Screen.width;
        bool isOn = powerSwitch != null ? powerSwitch.IsPowerOn : true;
        DrawHologramCard(W - 230, 150, 215, 44);
        Label(W - 225, 156, 205, 30, isOn ? "MAIN POWER  [ON]" : "MAIN POWER  [OFF]",
            StyleCenter(14, isOn ? TextGreen : TextRed, FontStyle.Bold));
    }

    // ─── Objective 03: Select Extinguisher (Room 02) ───────────────────────────
    void DrawObj03Screen()
    {
        float W = Screen.width;

        DrawObjectiveHUDCard("OBJECTIVE 03", "SELECT FIRE EXTINGUISHER",
            "Inspect the wall station and select the safe agent for electrical fires.\nLook at an extinguisher or press [1 - 5].");

        // 1. Central Educational Guidance Card (pops up telling which to use!)
        float gw = Mathf.Min(500f, W * 0.45f);
        float gx = (W - gw) * 0.5f;
        float gy = 48f;
        DrawHologramCard(gx, gy, gw, 160f);

        Label(gx, gy + 8, gw, 22, "⚡ RECOMMENDED EQUIPMENT FOR ELECTRICAL FIRES", StyleCenter(13, TextOrange, FontStyle.Bold));
        DrawHorizontalRule(gx + 12, gy + 32, gw - 24, CyanBorder);

        Label(gx + 15, gy + 38, gw - 30, 22, "★ USE: CO₂ (CARBON DIOXIDE) — BLACK IDENTIFICATION BAND", StyleLeft(12, TextCyan, FontStyle.Bold));
        Label(gx + 15, gy + 62, gw - 30, 52,
            "• Clean non-conductive gas eliminates electric shock hazard\n" +
            "• Penetrates exhaust vents and leaves zero residue on circuitry\n" +
            "• Safe for live electrical equipment up to 1000V",
            StyleWrap(11, TextWhite));

        Label(gx + 15, gy + 122, gw - 30, 32,
            "⚠ DANGER: NEVER use WATER (conducts current) or DRY POWDER (ruins motor)",
            StyleWrap(10, TextRed));

        // 2. Extinguisher Selector Quick-Menu (Right Side)
        float pw = Mathf.Min(340f, W * 0.30f);
        float px = W - pw - 15f;
        DrawHologramCard(px, 50, pw, 185f);

        Label(px, 58, pw, 22, "WALL EXTINCTION STATION", StyleCenter(13, TextCyan, FontStyle.Bold));
        DrawHorizontalRule(px + 12, 82, pw - 24, CyanBorder);

        var types = new[] {
            ExtinguisherType.Water,      // [1]
            ExtinguisherType.DryPowder,  // [2]
            ExtinguisherType.CO2         // [3]
        };
        string[] names = {
            "[1]  Water  (Red Band - Class A)",
            "[2]  Dry Powder  (Blue Band - ABC)",
            "[3]  CO₂  (Black Band - ELECTRICAL)"
        };

        for (int i = 0; i < types.Length; i++)
        {
            float by = 90 + i * 28f;
            bool isCO2 = types[i] == ExtinguisherType.CO2;

            GUI.color = isCO2 ? new Color(0.12f, 0.42f, 0.65f, 0.85f) : new Color(0.15f, 0.22f, 0.30f, 0.75f);
            if (GUI.Button(new Rect(px + 10, by, pw - 20, 24), ""))
            {
                OnExtinguisherSelected(types[i]);
            }
            GUI.color = Color.white;
            Label(px + 12, by, pw - 24, 24, names[i], StyleLeft(11, isCO2 ? TextCyan : TextWhite));
        }

        // 3. Hover / Look-At Inspection Tooltip (aiming at an extinguisher in the scene)
        if (hoveredExtinguisher != null)
        {
            DrawHoverInspectionTooltip(hoveredExtinguisher);
        }

        // 4. CO2 Details Modal Card (upon choosing CO2)
        if (showExtCard) DrawCO2DetailsCard();
    }

    void DrawHoverInspectionTooltip(FireExtinguisher ext)
    {
        float W = Screen.width, H = Screen.height;
        float tw = 400f, th = 85f;
        float tx = (W - tw) * 0.5f;
        float ty = H * 0.60f;

        DrawHologramCard(tx, ty, tw, th);

        bool isCO2 = ext.Type == ExtinguisherType.CO2;
        if (isCO2)
        {
            Label(tx, ty + 8, tw, 22, "✓ RECOMMENDED FOR ELECTRICAL FIRES", StyleCenter(13, TextGreen, FontStyle.Bold));
            Label(tx + 12, ty + 32, tw - 24, 20, $"{ext.DisplayName.ToUpper()}", StyleCenter(12, TextCyan, FontStyle.Bold));
            Label(tx, ty + 54, tw, 20, "Press [3] or Left Click to equip", StyleCenter(11, TextWhite));
        }
        else
        {
            Label(tx, ty + 8, tw, 22, "⚠ NOT RECOMMENDED / DANGEROUS", StyleCenter(13, TextRed, FontStyle.Bold));
            Label(tx + 12, ty + 32, tw - 24, 20, $"{ext.DisplayName.ToUpper()}", StyleCenter(12, TextOrange));
            Label(tx + 10, ty + 54, tw - 20, 26, $"{ext.HazardWarning}", StyleCenter(10, TextWhite));
        }
    }

    void DrawCO2DetailsCard()
    {
        float W = Screen.width, H = Screen.height;
        float pw = Mathf.Min(460f, W - 60f);
        float ph = 320f;
        float px = (W - pw) * 0.5f;
        float py = (H - ph) * 0.5f;

        DrawHologramCard(px, py, pw, ph);
        Label(px, py + 12, pw, 26, "CO₂ (CARBON DIOXIDE) EXTINGUISHER", StyleCenter(18, TextCyan, FontStyle.Bold));
        DrawHorizontalRule(px + 20, py + 42, pw - 40, CyanBorder);

        string[] points = {
            "✓ Non-conductive inert gas eliminates electrocution risk",
            "✓ Zero chemical residue — protects sensitive circuitry",
            "✓ Smothers oxygen in electrical chimney motor & hood",
            "✓ Rated for Class B (liquids) & Live Electrical Equipment",
            "ⓘ Fast volumetric expansion: Maintain 2 - 3 meter safety distance"
        };

        for (int i = 0; i < points.Length; i++)
        {
            Color c = points[i].StartsWith("✓") ? TextGreen : new Color(0.95f, 0.78f, 0.30f);
            Label(px + 24, py + 56 + i * 30, pw - 48, 26, points[i], StyleLeft(13, c));
        }

        if (DrawOrangeGradientButton(px + (pw - 220f) * 0.5f, py + ph - 50, 220f, 38f, "→  PROCEED"))
        {
            ConfirmCO2Selection();
        }
    }

    // ─── Severe Safety Hazard Warning Popup (When wrong extinguisher chosen!) ──
    void DrawSevereWarningPopup()
    {
        float W = Screen.width;
        float pw = Mathf.Min(600f, W - 60f);
        float ph = 125f;
        float px = (W - pw) * 0.5f;
        float py = 90f;

        // Flashing pulsating red outline
        float pulse = Mathf.PingPong(Time.time * 4f, 1f);
        GUI.color = new Color(1f, 0.1f, 0.1f, 0.40f + pulse * 0.50f);
        GUI.DrawTexture(new Rect(px - 4, py - 4, pw + 8, ph + 8), GetCyanBorderTexture());

        // Dark red danger background
        GUI.color = BgDanger;
        GUI.DrawTexture(new Rect(px, py, pw, ph), GetDarkGlassTexture());
        GUI.color = Color.white;

        Label(px + 15, py + 10, pw - 30, 26, wrongExtTitle, StyleCenter(14, Color.white, FontStyle.Bold));
        DrawHorizontalRule(px + 20, py + 38, pw - 40, TextRed);

        Label(px + 20, py + 44, pw - 40, 48, wrongExtMsg, StyleWrap(12, new Color(1f, 0.95f, 0.95f)));

        Label(px + 20, py + 95, pw - 40, 20,
            $"PENALTY APPLIED: -15 POINTS TO EVALUATION SCORE  (Mistakes: {wrongAttemptsCount})",
            StyleCenter(11, new Color(1f, 0.85f, 0.20f), FontStyle.Bold));
    }

    // ─── Objective 04: P.A.S.S. Procedure Hologram (Room 02) ──────────────────
    void DrawObj04Screen()
    {
        float W = Screen.width, H = Screen.height;
        float pw = Mathf.Min(680f, W - 60f);
        float ph = 310f;
        float px = (W - pw) * 0.5f;
        float py = (H - ph) * 0.5f;

        DrawHologramCard(px, py, pw, ph);
        Label(px, py + 12, pw, 26, "EXTINGUISHER PROCEDURE  —  P.A.S.S.", StyleCenter(18, TextCyan, FontStyle.Bold));
        DrawHorizontalRule(px + 20, py + 42, pw - 40, CyanBorder);

        string[] steps = {
            "(1) PULL\nSAFETY PIN\n\n[P key]",
            "(2) AIM\nAT BASE\nOF FIRE",
            "(3) SQUEEZE\nHANDLE\n\n[Hold Space]",
            "(4) SWEEP\nSIDE TO\nSIDE"
        };
        Color[] cols = { TextOrange, TextRed, TextCyan, TextGreen };
        float cardW = (pw - 60f) / 4f;

        for (int i = 0; i < 4; i++)
        {
            float cx = px + 18 + i * (cardW + 8);
            GUI.color = CardHeaderBg;
            GUI.DrawTexture(new Rect(cx, py + 56, cardW, 170), GetDarkGlassTexture());
            GUI.color = cols[i];
            GUI.Box(new Rect(cx, py + 56, cardW, 170), "");
            GUI.color = Color.white;

            Label(cx, py + 66, cardW, 150, steps[i], StyleCenter(13, cols[i], FontStyle.Bold));
        }

        if (DrawOrangeGradientButton(px + (pw - 260f) * 0.5f, py + ph - 48, 260f, 38f, "▶  START EXTINGUISHING"))
        {
            AdvanceTo(TrainingState.Obj05_Extinguishing);
        }
    }

    // ─── Objective 05: Active Extinguishing HUD (Room 02) ─────────────────────
    void DrawObj05Screen()
    {
        float W = Screen.width, H = Screen.height;

        DrawObjectiveHUDCard("OBJECTIVE 05", "EXTINGUISH THE FIRE",
            "Aim at the BASE of the chimney fire. Sweep side to side.\n[P] Pull Pin  →  [Hold Space] Spray CO₂ cryogenic vapor");

        // Top-Right Fire Status HUD
        float barW = 280f, barH = 100f;
        float bx = W - barW - 15f;
        DrawHologramCard(bx, 50, barW, barH);
        Label(bx, 55, barW, 20, "🔥  FIRE STATUS", StyleCenter(13, TextCyan, FontStyle.Bold));

        float fill = Mathf.Clamp01(fireStatusPct / 100f);
        Color barCol = Color.Lerp(TextGreen, TextRed, fill);
        DrawProgressBar(bx + 15, 78, barW - 30, 16, fill, barCol);
        Label(bx, 96, barW, 20, $"{fireStatusPct:0}%", StyleCenter(13, barCol, FontStyle.Bold));

        // Live stopwatch
        Label(bx, 118, barW, 20, $"⏱ Time Elapsed: {fireFinalTime:F1}s", StyleCenter(11, TextMuted));

        // Center-Bottom Distance Indicator: ◀─── 2 – 3 METERS ───▶
        float dW = 320f, dX = (W - dW) * 0.5f;
        DrawHologramCard(dX, H - 105, dW, 40);
        Label(dX, H - 102, dW, 34, "◀─── 2 – 3 METERS ───▶", StyleCenter(14, TextGreen, FontStyle.Bold));

        // Bottom-Left Equipment Info
        if (heldExt != null)
        {
            DrawHologramCard(15, H - 78, 420, 64);
            Label(22, H - 76, 400, 20, $"HOLDING: {heldExt.DisplayName.ToUpper()}", StyleLeft(12, TextCyan, FontStyle.Bold));
            Label(22, H - 56, 400, 20, "[P] Pull Pin  →  [Hold Space / Click] Squeeze & Spray", StyleLeft(11, TextWhite));
            Label(22, H - 36, 400, 18, $"Agent remaining: {heldExt.RemainingAgentFraction * 100f:0}%", StyleLeft(11, TextGreen));
        }
        else
        {
            DrawHologramCard(15, H - 56, 400, 42);
            Label(22, H - 52, 390, 34, "Pick up the CO₂ Extinguisher: Press [3] or Click it on wall", StyleLeft(12, TextOrange, FontStyle.Bold));
        }
    }

    // ─── Mission Complete & Dynamic Performance Debrief (Room 02) ────────────
    void DrawMissionCompleteScreen()
    {
        float W = Screen.width, H = Screen.height;
        float pw = Mathf.Min(640f, W - 60f);
        float ph = 430f;
        float px = (W - pw) * 0.5f;
        float py = (H - ph) * 0.5f;

        DrawHologramCard(px, py, pw, ph);
        Label(px, py + 12, pw, 32, "✅  MISSION COMPLETE", StyleCenter(24, TextGreen, FontStyle.Bold));
        DrawHorizontalRule(px + 20, py + 48, pw - 40, TextGreen);

        Label(px, py + 56, pw, 22, "PERFORMANCE DEBRIEF & SCORE BREAKDOWN", StyleCenter(15, TextCyan, FontStyle.Bold));

        // Dynamic Score Calculation Based on Response Time & Penalties!
        int alarmScore = obj01Done ? 20 : 0;
        int powerScore = obj02Done ? 20 : 0;
        int selectionScore = Mathf.Max(0, 20 - (wrongAttemptsCount * 10));

        // Time-based score (Max 40 pts):
        // Benchmark: <= 12s is 40pts; decreases gradually as time increases!
        int timeScore = 0;
        if (fireFinalTime <= 12f)
        {
            timeScore = 40;
        }
        else if (fireFinalTime <= 25f)
        {
            timeScore = Mathf.RoundToInt(40f - (fireFinalTime - 12f) * 1.5f);
        }
        else if (fireFinalTime <= 45f)
        {
            timeScore = Mathf.RoundToInt(20f - (fireFinalTime - 25f) * 0.75f);
        }
        else
        {
            timeScore = 0;
        }
        timeScore = Mathf.Clamp(timeScore, 0, 40);

        int penaltyDeduction = wrongAttemptsCount * 15;
        int totalScore = Mathf.Clamp(alarmScore + powerScore + selectionScore + timeScore - penaltyDeduction, 0, 100);

        float dy = py + 86;
        DrawDebriefLine(px + 35, dy,       "Emergency Fire Alarm Activated",          obj01Done ? "+20 / 20" : "+0 / 20", obj01Done);
        DrawDebriefLine(px + 35, dy + 28,  "Main Power Breaker Isolated",              obj02Done ? "+20 / 20" : "+0 / 20", obj02Done);
        DrawDebriefLine(px + 35, dy + 56,  "Extinguisher Selection (CO₂ First Try)",   $"+{selectionScore} / 20", selectionScore > 0);
        DrawDebriefLine(px + 35, dy + 84,  $"Extinguishing Speed ({fireFinalTime:F1}s elapsed)", $"+{timeScore} / 40", timeScore >= 20);

        if (wrongAttemptsCount > 0)
        {
            DrawDebriefLine(px + 35, dy + 112, $"Safety Violations ({wrongAttemptsCount} wrong picks)", $"-{penaltyDeduction} pts", false, true);
        }
        else
        {
            DrawDebriefLine(px + 35, dy + 112, "Safety Adherence (Zero wrong agents)", "+0 (No penalties)", true);
        }

        DrawHorizontalRule(px + 20, py + ph - 110, pw - 40, new Color(0.25f, 0.35f, 0.45f));

        // Final Dynamic Score Display
        Color scoreColor = totalScore >= 90 ? TextGreen : (totalScore >= 75 ? TextCyan : (totalScore >= 60 ? TextOrange : TextRed));
        Label(px, py + ph - 100, pw, 32, $"FINAL SCORE: {totalScore} / 100", StyleCenter(22, scoreColor, FontStyle.Bold));

        // Rating Evaluation
        string grade = totalScore >= 90 ? "★ GRADE A: EXCEPTIONAL RESPONSE (Certified Fire Warden)" :
                      (totalScore >= 75 ? "★ GRADE B: QUALIFIED EMERGENCY RESPONDER" :
                      (totalScore >= 60 ? "★ GRADE C: MARGINAL PASS (Retraining on Speed Recommended)" :
                                          "⚠ FAILED: CRITICAL SAFETY VIOLATIONS (Mandatory Retake)"));

        Label(px, py + ph - 68, pw, 20, grade, StyleCenter(12, scoreColor));

        if (DrawOrangeGradientButton(px + (pw - 240f) * 0.5f, py + ph - 42, 240f, 32f, "↺  RESTART TRAINING"))
        {
            obj01Done = obj02Done = obj03Done = fireSuppressed = false;
            wrongAttemptsCount = 0;
            fireFinalTime = 0f;
            isStopwatchRunning = false;

            if (fireVFX != null) fireVFX.ExtinguishFire();
            currentRoomId = "ROOM 01";

            // Reset position to Room 01 (Orientation Studio)
            VRCameraAnchor.SetRoom01Anchor();
            if (playerController != null)
                playerController.Teleport(new Vector3(0f, 1.55f, 0.45f), Vector3.zero);

            AdvanceTo(TrainingState.WelcomeScreen);
        }
    }

    void DrawDebriefLine(float x, float y, string label, string scoreText, bool passed, bool isPenalty = false)
    {
        Color c = isPenalty ? TextRed : (passed ? TextGreen : new Color(0.6f, 0.6f, 0.65f));
        string mark = isPenalty ? "⚠" : (passed ? "✓" : "○");

        Label(x, y, 24, 22, mark, StyleLeft(14, c, FontStyle.Bold));
        Label(x + 24, y, 360, 22, label, StyleLeft(13, c));
        Label(x + 400, y, 140, 22, scoreText, StyleRight(13, c, FontStyle.Bold));
    }

    // ─── UI Widgets & Drawing Helpers ─────────────────────────────────────────

    void DrawTelemetryBar()
    {
        float W = Screen.width;

        // Top-Left: ● TRAINING SIMULATION • READY
        GUI.color = DarkGlass;
        GUI.DrawTexture(new Rect(10, 8, 290, 32), GetDarkGlassTexture());
        GUI.color = Color.white;
        Label(22, 14, 270, 20, "●  TRAINING SIMULATION  •  READY", StyleLeft(11, TextCyan, FontStyle.Bold));

        // Top-Right: ROOM 01/02 | HEADSET TRACKING ✓
        GUI.color = DarkGlass;
        GUI.DrawTexture(new Rect(W - 280, 8, 270, 32), GetDarkGlassTexture());
        GUI.color = Color.white;
        Label(W - 270, 14, 255, 20, $"{currentRoomId}  |  HEADSET TRACKING  ✓", StyleRight(11, TextCyan, FontStyle.Bold));
    }

    void DrawFlameHeader(float x, float y, float w, string title, string module)
    {
        // Red flame icon box
        GUI.color = new Color(0.92f, 0.18f, 0.15f, 0.95f);
        GUI.DrawTexture(new Rect(x, y, 22, 22), GetFlameBadgeTexture());
        GUI.color = Color.white;
        Label(x, y, 22, 22, "🔥", StyleCenter(12, TextWhite));

        // Title text
        Label(x + 28, y + 2, 240, 20, title, StyleLeft(11, TextWhite, FontStyle.Bold));

        // Module text on right
        Label(x + w - 200, y + 2, 200, 20, module, StyleRight(11, TextMuted));
    }

    void DrawObjectiveHUDCard(string tag, string title, string desc)
    {
        float pw = Mathf.Min(400f, Screen.width * 0.38f);
        float ph = 115f;
        float px = 15f, py = 50f;

        DrawHologramCard(px, py, pw, ph);
        Label(px + 12, py + 8, pw - 24, 18, tag, StyleLeft(11, TextCyan, FontStyle.Bold));
        Label(px + 12, py + 26, pw - 24, 22, title, StyleLeft(14, TextOrange, FontStyle.Bold));
        Label(px + 12, py + 48, pw - 24, 60, desc, StyleWrap(11, TextWhite));
    }

    void DrawInteractWorldHint(float xFrac, float yFrac, string text)
    {
        float W = Screen.width, H = Screen.height;
        float px = W * xFrac, py = H * yFrac;
        DrawHologramCard(px, py, 160, 48);
        Label(px, py, 160, 48, text, StyleCenter(12, TextCyan, FontStyle.Bold));
    }

    void DrawObjectiveToast()
    {
        float W = Screen.width, H = Screen.height;
        float pw = Mathf.Min(440f, W - 60f);
        float ph = 74f;
        float px = (W - pw) * 0.5f;
        float py = H * 0.74f;

        GUI.color = BgSuccess;
        GUI.DrawTexture(new Rect(px, py, pw, ph), GetDarkGlassTexture());
        GUI.color = TextGreen;
        GUI.Box(new Rect(px, py, pw, ph), "");
        GUI.color = Color.white;

        Label(px, py + 10, pw, 28, objCompleteMsg, StyleCenter(18, TextGreen, FontStyle.Bold));
        Label(px, py + 42, pw, 20, "OBJECTIVE COMPLETE", StyleCenter(12, TextWhite));
    }

    void DrawBottomStatusBar()
    {
        float W = Screen.width, H = Screen.height;
        string hint = currentState switch
        {
            TrainingState.WelcomeScreen            => "🎮 Press [A] / Trigger or Click [START TRAINING] to begin",
            TrainingState.SelectEnvironment        => "🎮 Press [A] / Trigger or Click [KITCHEN] to enter simulation",
            TrainingState.RedirectingToKitchen     => "Redirecting to Kitchen Environment...",
            TrainingState.KitchenBriefing          => "🎮 Press [A] / Trigger to acknowledge briefing",
            TrainingState.FireIgnition             => "🎮 Press [A] / Trigger to acknowledge fire alert",
            TrainingState.Obj01_Alarm              => "🖐️ Touch Alarm with Hand or Aim Laser + Trigger  |  [A] Key",
            TrainingState.Obj02_PowerCut           => "🖐️ Touch Power Breaker with Hand or Aim Laser + Trigger  |  [L] Key",
            TrainingState.Obj03_SelectExtinguisher => "🖐️ Grab CO₂ Extinguisher with Hand or Aim Laser + Trigger",
            TrainingState.Obj04_PASSProcedure      => "🎮 Press [A] / Trigger when ready to extinguish",
            TrainingState.Obj05_Extinguishing      => "🎮 Press [A] to Pull Pin  →  Hold Right Trigger to Spray CO₂",
            TrainingState.MissionComplete          => "🏆 Mission Complete! Press [A] / Trigger to restart.",
            _                                      => "🎮 Controls: Left Stick: Walk | Right Stick: Turn | [A]/Trigger: Interact"
        };

        GUI.color = new Color(0.02f, 0.05f, 0.08f, 0.70f);
        GUI.DrawTexture(new Rect(0, H - 26, W, 26), GetBlackTexture());
        GUI.color = Color.white;
        Label(0, H - 25, W, 22, hint, StyleCenter(11, TextMuted));
    }

    void DrawCrosshair()
    {
        float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
        GUI.color = new Color(1f, 1f, 1f, 0.65f);
        GUI.Label(new Rect(cx - 10, cy - 12, 20, 24), "·", StyleCenter(18, Color.white));
        GUI.color = Color.white;
    }

    void DrawHologramCard(float x, float y, float w, float h)
    {
        GUI.color = DarkGlass;
        GUI.DrawTexture(new Rect(x, y, w, h), GetDarkGlassTexture());

        GUI.color = CyanBorder;
        GUI.DrawTexture(new Rect(x, y, w, 2), GetCyanBorderTexture());
        GUI.DrawTexture(new Rect(x, y + h - 2, w, 2), GetCyanBorderTexture());
        GUI.DrawTexture(new Rect(x, y, 2, h), GetCyanBorderTexture());
        GUI.DrawTexture(new Rect(x + w - 2, y, 2, h), GetCyanBorderTexture());

        GUI.color = Color.white;
    }

    bool DrawOrangeGradientButton(float x, float y, float w, float h, string text)
    {
        GUI.DrawTexture(new Rect(x, y, w, h), GetOrangeGradientTexture());

        GUI.color = Color.clear;
        bool clicked = GUI.Button(new Rect(x, y, w, h), "");
        GUI.color = Color.white;

        Label(x, y, w, h, text, StyleCenter((int)(h * 0.38f), TextWhite, FontStyle.Bold));
        return clicked;
    }

    void DrawHorizontalRule(float x, float y, float w, Color col)
    {
        GUI.color = col;
        GUI.DrawTexture(new Rect(x, y, w, 1), GetCyanBorderTexture());
        GUI.color = Color.white;
    }

    void DrawProgressBar(float x, float y, float w, float h, float fill, Color col)
    {
        GUI.color = new Color(0.08f, 0.12f, 0.16f, 0.95f);
        GUI.DrawTexture(new Rect(x, y, w, h), GetBlackTexture());
        GUI.color = col;
        GUI.DrawTexture(new Rect(x + 2, y + 2, (w - 4f) * fill, h - 4f), GetCyanBorderTexture());
        GUI.color = Color.white;
    }

    void Label(float x, float y, float w, float h, string text, GUIStyle style)
    {
        GUI.Label(new Rect(x, y, w, h), text, style);
    }

    // ─── Style Factories ─────────────────────────────────────────────────────
    GUIStyle StyleCenter(int size, FontStyle fontStyle = FontStyle.Normal)
    {
        return StyleCenter(size, TextWhite, fontStyle);
    }

    GUIStyle StyleCenter(int size, Color col, FontStyle fontStyle = FontStyle.Normal)
    {
        var s = new GUIStyle(GUI.skin.label);
        s.fontSize = size;
        s.fontStyle = fontStyle;
        s.alignment = TextAnchor.MiddleCenter;
        s.wordWrap = false;
        s.normal.textColor = col;
        return s;
    }

    GUIStyle StyleLeft(int size, Color? col = null, FontStyle fontStyle = FontStyle.Normal)
    {
        var s = new GUIStyle(GUI.skin.label);
        s.fontSize = size;
        s.fontStyle = fontStyle;
        s.alignment = TextAnchor.MiddleLeft;
        s.wordWrap = false;
        s.normal.textColor = col ?? TextWhite;
        return s;
    }

    GUIStyle StyleRight(int size, Color? col = null, FontStyle fontStyle = FontStyle.Normal)
    {
        var s = new GUIStyle(GUI.skin.label);
        s.fontSize = size;
        s.fontStyle = fontStyle;
        s.alignment = TextAnchor.MiddleRight;
        s.wordWrap = false;
        s.normal.textColor = col ?? TextWhite;
        return s;
    }

    GUIStyle StyleWrap(int size, Color? col = null)
    {
        var s = new GUIStyle(GUI.skin.label);
        s.fontSize = size;
        s.alignment = TextAnchor.UpperLeft;
        s.wordWrap = true;
        s.normal.textColor = col ?? TextWhite;
        return s;
    }

    // ─── Texture Caching ─────────────────────────────────────────────────────
    private static void InitTextures()
    {
        if (texBlack == null)
        {
            texBlack = new Texture2D(1, 1);
            texBlack.SetPixel(0, 0, Color.black);
            texBlack.Apply();
        }
        if (texDarkGlass == null)
        {
            texDarkGlass = new Texture2D(1, 1);
            texDarkGlass.SetPixel(0, 0, DarkGlass);
            texDarkGlass.Apply();
        }
        if (texCyanBorder == null)
        {
            texCyanBorder = new Texture2D(1, 1);
            texCyanBorder.SetPixel(0, 0, CyanBorder);
            texCyanBorder.Apply();
        }
        if (texOrangeGradient == null)
        {
            texOrangeGradient = Resources.Load<Texture2D>("Textures/UI_OrangeGradient");
            if (texOrangeGradient == null)
            {
                int w = 128, h = 32;
                texOrangeGradient = new Texture2D(w, h, TextureFormat.RGBA32, false);
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float t = x / (float)w;
                        Color c = Color.Lerp(new Color(1f, 0.42f, 0.15f), new Color(1f, 0.22f, 0.12f), t);
                        texOrangeGradient.SetPixel(x, y, c);
                    }
                }
                texOrangeGradient.Apply();
            }
        }
        if (texFlameBadge == null)
        {
            texFlameBadge = new Texture2D(1, 1);
            texFlameBadge.SetPixel(0, 0, new Color(0.92f, 0.18f, 0.15f));
            texFlameBadge.Apply();
        }
    }

    private static Texture2D GetDarkGlassTexture()      { InitTextures(); return texDarkGlass; }
    private static Texture2D GetCyanBorderTexture()     { InitTextures(); return texCyanBorder; }
    private static Texture2D GetOrangeGradientTexture() { InitTextures(); return texOrangeGradient; }
    private static Texture2D GetFlameBadgeTexture()     { InitTextures(); return texFlameBadge; }
    private static Texture2D GetBlackTexture()          { InitTextures(); return texBlack; }
}
