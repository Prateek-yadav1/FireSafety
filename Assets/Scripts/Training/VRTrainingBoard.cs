using System.Collections;
using UnityEngine;
using FireSafety.Extinguishers;
using FireSafety.Fire;
using FireSafety.Environment;

namespace FireSafety.Training
{
    /// <summary>
    /// Interactive 3D World-Space Holographic Training Terminal for Meta Quest VR.
    /// Provides crystal-clear, comfortable in-headset viewing for both Room 01 and Room 02:
    ///   - Room 01 (Orientation Studio): Displays Welcome Screen & Scenario Selection directly in front of trainee
    ///   - Room 02 (Kitchen): Displays Hazard Briefing, Fire Alert, Step-by-Step Objectives, P.A.S.S. Guide, and Debrief
    ///   - 100% Controller interactive: Trigger, [A]/[X] buttons, or Laser pointer to interact
    ///   - Zero keyboard dependency
    /// </summary>
    public class VRTrainingBoard : MonoBehaviour
    {
        private TextMesh titleText;
        private TextMesh objectiveHeader;
        private TextMesh objectiveBody;
        private TextMesh promptButtonText;
        private TextMesh statusLine;
        private TextMesh passGuideText;

        private GameObject boardRoot;
        private GameObject healthBarFill;
        private GameObject promptButtonBox;
        private BoxCollider buttonCollider;

        private FireVFXController fireVFX;
        private FireAlarmController fireAlarm;
        private PowerSwitchController powerSwitch;
        private TrainingHUD trainingHUD;

        // Palette matching Figma Dark-Glass & Hologram Theme
        private static readonly Color CyanAccent  = new Color(0.34f, 0.90f, 0.95f);
        private static readonly Color OrangeAlert = new Color(1.0f,  0.65f, 0.20f);
        private static readonly Color RedAlert    = new Color(1.0f,  0.25f, 0.20f);
        private static readonly Color GreenSafe   = new Color(0.25f, 0.92f, 0.45f);
        private static readonly Color WhiteText   = new Color(0.95f, 0.95f, 0.98f);
        private static readonly Color MutedText   = new Color(0.70f, 0.74f, 0.80f);

        private bool _isRoom01 = true;

        private void Start()
        {
            fireVFX     = FindObjectOfType<FireVFXController>();
            fireAlarm   = FindObjectOfType<FireAlarmController>();
            powerSwitch = FindObjectOfType<PowerSwitchController>();
            trainingHUD = FindObjectOfType<TrainingHUD>();

            BuildBoardVisuals();
            UpdateBoardRoomPlacement();
        }

        private void BuildBoardVisuals()
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            // 1. Sleek Glass Terminal Frame
            GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "Board_Frame";
            frame.transform.SetParent(transform, false);
            frame.transform.localPosition = Vector3.zero;
            frame.transform.localScale = new Vector3(1.35f, 0.95f, 0.02f);
            Destroy(frame.GetComponent<Collider>());

            Material frameMat = new Material(litShader);
            frameMat.color = new Color(0.04f, 0.08f, 0.12f, 0.95f);
            frameMat.SetFloat("_Metallic", 0.75f);
            frameMat.SetFloat("_Smoothness", 0.85f);
            frame.GetComponent<Renderer>().sharedMaterial = frameMat;

            // Cyan Border Trim
            GameObject trim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trim.name = "Board_BorderTrim";
            trim.transform.SetParent(transform, false);
            trim.transform.localPosition = new Vector3(0f, 0f, 0.006f);
            trim.transform.localScale = new Vector3(1.38f, 0.98f, 0.008f);
            Destroy(trim.GetComponent<Collider>());
            Material trimMat = new Material(litShader);
            trimMat.color = CyanAccent;
            trimMat.EnableKeyword("_EMISSION");
            trimMat.SetColor("_EmissionColor", CyanAccent * 0.9f);
            trim.GetComponent<Renderer>().sharedMaterial = trimMat;

            // 2. Title Text
            titleText = CreateTextMesh("Title", new Vector3(0f, 0.38f, -0.015f), 0.016f, TextAnchor.MiddleCenter, FontStyle.Bold, CyanAccent);
            titleText.text = "VR FIRE SAFETY TRAINING • ORIENTATION";

            // 3. Current Objective Header
            objectiveHeader = CreateTextMesh("ObjHeader", new Vector3(0f, 0.25f, -0.015f), 0.014f, TextAnchor.MiddleCenter, FontStyle.Bold, OrangeAlert);
            objectiveHeader.text = "WELCOME TRAINEE";

            // 4. Objective Instructions Body
            objectiveBody = CreateTextMesh("ObjBody", new Vector3(0f, 0.08f, -0.015f), 0.011f, TextAnchor.MiddleCenter, FontStyle.Normal, WhiteText);
            objectiveBody.text = "Learn emergency response protocols, identify hazards,\nand safely control commercial kitchen fires.";

            // 5. Fire Intensity Health Bar Background
            GameObject barBg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barBg.name = "HealthBar_Bg";
            barBg.transform.SetParent(transform, false);
            barBg.transform.localPosition = new Vector3(0f, -0.08f, -0.015f);
            barBg.transform.localScale = new Vector3(1.05f, 0.035f, 0.005f);
            Destroy(barBg.GetComponent<Collider>());
            Material barBgMat = new Material(litShader);
            barBgMat.color = new Color(0.12f, 0.14f, 0.18f);
            barBg.GetComponent<Renderer>().sharedMaterial = barBgMat;

            // Health Bar Fill
            healthBarFill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            healthBarFill.name = "HealthBar_Fill";
            healthBarFill.transform.SetParent(barBg.transform, false);
            healthBarFill.transform.localPosition = Vector3.zero;
            healthBarFill.transform.localScale = new Vector3(1f, 0.85f, 1.2f);
            Destroy(healthBarFill.GetComponent<Collider>());
            Material fillMat = new Material(litShader);
            fillMat.color = OrangeAlert;
            fillMat.EnableKeyword("_EMISSION");
            fillMat.SetColor("_EmissionColor", OrangeAlert * 1.5f);
            healthBarFill.GetComponent<Renderer>().sharedMaterial = fillMat;
            barBg.SetActive(false); // only visible during extinguishing

            // 6. Status Line (Alarm / Power / Time)
            statusLine = CreateTextMesh("StatusLine", new Vector3(0f, -0.15f, -0.015f), 0.010f, TextAnchor.MiddleCenter, FontStyle.Bold, MutedText);
            statusLine.text = "VR 6-DOF HEAD TRACKING ACTIVE";

            // 7. Interactive Action Button
            promptButtonBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            promptButtonBox.name = "Board_ActionButton";
            promptButtonBox.transform.SetParent(transform, false);
            promptButtonBox.transform.localPosition = new Vector3(0f, -0.28f, -0.016f);
            promptButtonBox.transform.localScale = new Vector3(0.95f, 0.14f, 0.015f);

            buttonCollider = promptButtonBox.GetComponent<BoxCollider>();
            if (buttonCollider == null) buttonCollider = promptButtonBox.AddComponent<BoxCollider>();

            Material btnMat = new Material(litShader);
            btnMat.color = new Color(1.0f, 0.42f, 0.12f);
            btnMat.EnableKeyword("_EMISSION");
            btnMat.SetColor("_EmissionColor", new Color(0.9f, 0.35f, 0.10f) * 0.7f);
            promptButtonBox.GetComponent<Renderer>().sharedMaterial = btnMat;

            promptButtonText = CreateTextMesh("BtnText", new Vector3(0f, -0.28f, -0.026f), 0.013f, TextAnchor.MiddleCenter, FontStyle.Bold, WhiteText);
            promptButtonText.text = "START TRAINING  →";

            // 8. Controller Help Footer
            passGuideText = CreateTextMesh("FooterHelp", new Vector3(0f, -0.40f, -0.015f), 0.009f, TextAnchor.MiddleCenter, FontStyle.Normal, CyanAccent);
            passGuideText.text = "🎮 Press [A] Button or Squeeze Trigger to Proceed";
        }

        private TextMesh CreateTextMesh(string name, Vector3 localPos, float charSize, TextAnchor anchor, FontStyle style, Color col)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;

            TextMesh tm = go.AddComponent<TextMesh>();
            tm.fontSize = 28;
            tm.characterSize = charSize;
            tm.anchor = anchor;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = style;
            tm.color = col;

            return tm;
        }

        private void Update()
        {
            UpdateBoardRoomPlacement();
            OrientBoardTowardsPlayer();
            UpdateLiveTelemetry();
        }

        private void OrientBoardTowardsPlayer()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 toCam = cam.transform.position - transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 0.05f)
            {
                // Text is on the -Z local face of the board.
                // Setting forward (+Z) to point away from camera (-toCam)
                // makes the -Z face point directly at the player camera.
                transform.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
            }
        }

        private void UpdateBoardRoomPlacement()
        {
            Camera cam = Camera.main;
            bool inKitchen = (cam != null ? cam.transform.position.x : transform.position.x) > 100f;

            if (inKitchen && _isRoom01)
            {
                // Transitioned to Kitchen: place cleanly to the right of the fire hazard,
                // facing trainee at comfortable viewing angle
                _isRoom01 = false;
                transform.position = new Vector3(201.35f, 1.50f, 0.85f);
                OrientBoardTowardsPlayer();
            }
            else if (!inKitchen && !_isRoom01)
            {
                // In Room 01 (Orientation Studio)
                _isRoom01 = true;
                transform.position = new Vector3(0f, 1.48f, 1.65f);
                transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            }
            else if (_isRoom01 && transform.position.x > 50f)
            {
                // Ensure initial position in Room 01
                transform.position = new Vector3(0f, 1.48f, 1.65f);
                transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            }
        }

        private void UpdateLiveTelemetry()
        {
            if (fireVFX == null) fireVFX = FindObjectOfType<FireVFXController>();
            if (fireAlarm == null) fireAlarm = FindObjectOfType<FireAlarmController>();
            if (powerSwitch == null) powerSwitch = FindObjectOfType<PowerSwitchController>();
            if (trainingHUD == null) trainingHUD = FindObjectOfType<TrainingHUD>();

            if (trainingHUD == null) return;

            var state = trainingHUD.State;

            switch (state)
            {
                case TrainingHUD.TrainingState.WelcomeScreen:
                    titleText.text = "VR FIRE SAFETY TRAINING • ORIENTATION";
                    objectiveHeader.text = "WELCOME TRAINEE";
                    objectiveHeader.color = CyanAccent;
                    objectiveBody.text = "Learn how to identify fire hazards, respond to emergencies,\nand safely control commercial kitchen fires.";
                    promptButtonText.text = "START TRAINING  →";
                    passGuideText.text = "🎮 Press [A] Button or Squeeze Trigger to Begin";
                    statusLine.text = "VR 6-DOF TRACKING ACTIVE • READY";
                    break;

                case TrainingHUD.TrainingState.SelectEnvironment:
                    titleText.text = "SELECT TRAINING ENVIRONMENT";
                    objectiveHeader.text = "▶  KITCHEN ENVIRONMENT  (ACTIVE)";
                    objectiveHeader.color = OrangeAlert;
                    objectiveBody.text = "Commercial Kitchen: Electrical Chimney Fire Scenario\n• Warehouse (Locked)   • Chemical Lab (Locked)";
                    promptButtonText.text = "ENTER KITCHEN SCENARIO  →";
                    passGuideText.text = "🎮 Press [A] or Squeeze Trigger to Enter Kitchen";
                    statusLine.text = "TARGET: ROOM 02 • COMMERCIAL KITCHEN";
                    break;

                case TrainingHUD.TrainingState.KitchenBriefing:
                    titleText.text = "VR FIRE SAFETY TRAINING • KITCHEN";
                    objectiveHeader.text = "KITCHEN ENVIRONMENT BRIEFING";
                    objectiveHeader.color = CyanAccent;
                    objectiveBody.text = "High-risk commercial kitchen environment.\nBe prepared for electrical appliance and ventilation fires.";
                    promptButtonText.text = "ACKNOWLEDGE BRIEFING  →";
                    passGuideText.text = "🎮 Press [A] Button or Squeeze Trigger to Continue";
                    statusLine.text = "ROOM 02 • SYSTEM NOMINAL";
                    break;

                case TrainingHUD.TrainingState.FireIgnition:
                    titleText.text = "⚠️ EMERGENCY ALERT ⚠️";
                    objectiveHeader.text = "FIRE DETECTED IN KITCHEN!";
                    objectiveHeader.color = RedAlert;
                    objectiveBody.text = "Electrical exhaust chimney hood above stove has ignited!\nThick smoke and sparks detected.";
                    promptButtonText.text = "COMMENCE EMERGENCY RESPONSE  →";
                    passGuideText.text = "🎮 Press [A] Button or Squeeze Trigger to Respond";
                    statusLine.text = "<color=#FF3020>FIRE STATUS: ACTIVE FLAMES</color>";
                    break;

                case TrainingHUD.TrainingState.Obj01_Alarm:
                    titleText.text = "VR FIRE SAFETY • STEP 1";
                    objectiveHeader.text = "OBJECTIVE 1: SOUND FIRE ALARM";
                    objectiveHeader.color = RedAlert;
                    objectiveBody.text = "Reach hand to touch the Red Wall Unit near the door,\nor point VR Laser and squeeze Trigger!";
                    promptButtonText.text = "ALARM: AWAITING ACTIVATION";
                    passGuideText.text = "🖐️ Direct Hand Touch or Aim Laser + Trigger";
                    bool isAlarmOn = fireAlarm != null && fireAlarm.IsAlarmActive;
                    statusLine.text = isAlarmOn ? "<color=#FF4030>ALARM: SOUNDING 🚨</color>" : "<color=#40EA70>ALARM: READY TO ACTIVATE</color>";
                    break;

                case TrainingHUD.TrainingState.Obj02_PowerCut:
                    titleText.text = "VR FIRE SAFETY • STEP 2";
                    objectiveHeader.text = "OBJECTIVE 2: ISOLATE ELECTRICAL POWER";
                    objectiveHeader.color = OrangeAlert;
                    objectiveBody.text = "Reach hand to touch the Power Breaker Box on the wall,\nor point VR Laser and squeeze Trigger to cut power!";
                    promptButtonText.text = "POWER: LIVE (CUT TO PREVENT ELECTROCUTION)";
                    passGuideText.text = "🖐️ Direct Hand Touch or Aim Laser + Trigger";
                    bool isPowerOff = powerSwitch != null && !powerSwitch.IsPowerOn;
                    statusLine.text = isPowerOff ? "<color=#40EA70>POWER: ISOLATED ⚡</color>" : "<color=#FF9020>POWER: LIVE (DANGER)</color>";
                    break;

                case TrainingHUD.TrainingState.Obj03_SelectExtinguisher:
                    titleText.text = "VR FIRE SAFETY • STEP 3";
                    objectiveHeader.text = "OBJECTIVE 3: SELECT CO2 EXTINGUISHER";
                    objectiveHeader.color = CyanAccent;
                    objectiveBody.text = "Select the Black-banded CO2 Extinguisher from the wall station.\n⚠️ DO NOT USE WATER OR FOAM ON ELECTRICAL FIRES!";
                    promptButtonText.text = "GRAB CO2 EXTINGUISHER";
                    passGuideText.text = "🖐️ Reach Hand to Grab or Aim Laser + Trigger";
                    statusLine.text = "AGENT REQUIREMENT: CARBON DIOXIDE (CO₂)";
                    break;

                case TrainingHUD.TrainingState.Obj04_PASSProcedure:
                    titleText.text = "VR FIRE SAFETY • STEP 4";
                    objectiveHeader.text = "P.A.S.S. SAFETY PROTOCOL";
                    objectiveHeader.color = GreenSafe;
                    objectiveBody.text = "P - PULL PIN: Press [A] on controller or reach left hand to pin\nA - AIM: Point nozzle at base of fire\nS - SQUEEZE: Hold Right Trigger to spray\nS - SWEEP: Sweep vapor across fire";
                    promptButtonText.text = "PROCEED TO EXTINGUISH  →";
                    passGuideText.text = "🎮 Press [A] Button or Squeeze Trigger when Ready";
                    statusLine.text = "SAFETY PIN: READY TO PULL";
                    break;

                case TrainingHUD.TrainingState.Obj05_Extinguishing:
                    titleText.text = "VR FIRE SAFETY • STEP 5";
                    objectiveHeader.text = "SUPPRESS CHIMNEY FIRE";
                    objectiveHeader.color = OrangeAlert;
                    float intensity = fireVFX != null && fireVFX.IsBurning ? fireVFX.FireIntensity : 0f;
                    int pct = Mathf.RoundToInt(intensity * 100f);
                    objectiveBody.text = $"Aim extinguisher nozzle directly at the chimney fire.\nHold Right Trigger to discharge cryogenic CO2 vapor!\nFire Intensity: {pct}%";
                    promptButtonText.text = $"FIRE INTENSITY: {pct}%";
                    passGuideText.text = "🎮 Squeeze Right Trigger to Spray CO₂";
                    statusLine.text = $"SUPPRESSION IN PROGRESS • {pct}% REMAINING";
                    break;

                case TrainingHUD.TrainingState.MissionComplete:
                    titleText.text = "🏆 TRAINING MISSION COMPLETE 🏆";
                    objectiveHeader.text = "PERFORMANCE EVALUATION: GRADE A+";
                    objectiveHeader.color = GreenSafe;
                    objectiveBody.text = "Chimney fire suppressed with CO2 extinguisher!\nAll safety protocols successfully executed.\n- Alarm Sounded\n- Power Cut\n- Correct Agent Selected\n- Zero Electrocution Violations";
                    promptButtonText.text = "RETURN TO ORIENTATION ROOM  →";
                    passGuideText.text = "🎮 Press [A] Button or Squeeze Trigger to Restart";
                    statusLine.text = "<color=#40EA70>STATUS: 100% SUCCESS • CERTIFIED</color>";
                    break;
            }
        }
    }
}
