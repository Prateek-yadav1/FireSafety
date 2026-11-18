using System.Collections;
using UnityEngine;
using FireSafety.Extinguishers;
using FireSafety.Fire;
using FireSafety.Environment;

namespace FireSafety.Training
{
    /// <summary>
    /// Interactive 3D World-Space Holographic Training Terminal for VR.
    /// Floats comfortably inside the kitchen environment so VR users can read objectives,
    /// track fire suppression, view P.A.S.S. instructions, and interact via VR Controller Laser.
    /// </summary>
    public class VRTrainingBoard : MonoBehaviour
    {
        private TextMesh titleText;
        private TextMesh objectiveHeader;
        private TextMesh objectiveBody;
        private TextMesh statusLine;
        private TextMesh passGuideText;
        private TextMesh timerScoreText;

        private GameObject boardRoot;
        private GameObject healthBarFill;
        private FireVFXController fireVFX;
        private FireAlarmController fireAlarm;
        private PowerSwitchController powerSwitch;
        private TrainingHUD trainingHUD;

        // Colors
        private static readonly Color CyanAccent  = new Color(0.34f, 0.90f, 0.95f);
        private static readonly Color OrangeAlert = new Color(1.0f,  0.65f, 0.20f);
        private static readonly Color RedAlert    = new Color(1.0f,  0.25f, 0.20f);
        private static readonly Color GreenSafe   = new Color(0.25f, 0.92f, 0.45f);
        private static readonly Color WhiteText   = new Color(0.95f, 0.95f, 0.98f);
        private static readonly Color MutedText   = new Color(0.70f, 0.74f, 0.80f);

        private void Start()
        {
            fireVFX     = FindObjectOfType<FireVFXController>();
            fireAlarm   = FindObjectOfType<FireAlarmController>();
            powerSwitch = FindObjectOfType<PowerSwitchController>();
            trainingHUD = FindObjectOfType<TrainingHUD>();

            BuildBoardVisuals();
        }

        private void BuildBoardVisuals()
        {
            // Positioned inside kitchen: near prep island/wall, clearly visible to player at (202.8, 1.55, 0.5)
            // Board positioned to player's left and forward.
            // Player is at (202.8, 1.6, 0) facing -X (yaw=-90).
            // Board faces TOWARD player: Y=155° (front face points in +X,-Z direction toward player).
            transform.position = new Vector3(201.75f, 1.55f, 1.35f);
            transform.rotation = Quaternion.Euler(0f, 155f, 0f); // 155° = board front faces toward player position

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");

            // 1. Sleek Glass Terminal Frame
            GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "Board_Frame";
            frame.transform.SetParent(transform, false);
            frame.transform.localPosition = Vector3.zero;
            frame.transform.localScale = new Vector3(1.15f, 0.85f, 0.02f);
            Destroy(frame.GetComponent<Collider>());

            Material frameMat = new Material(litShader);
            frameMat.color = new Color(0.04f, 0.08f, 0.12f); // Deep dark teal glass
            frameMat.SetFloat("_Metallic", 0.80f);
            frameMat.SetFloat("_Smoothness", 0.85f);
            frame.GetComponent<Renderer>().sharedMaterial = frameMat;

            // Cyan Border Trim
            GameObject trim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trim.name = "Board_BorderTrim";
            trim.transform.SetParent(transform, false);
            trim.transform.localPosition = new Vector3(0f, 0f, 0.005f);
            trim.transform.localScale = new Vector3(1.18f, 0.88f, 0.008f);
            Destroy(trim.GetComponent<Collider>());
            Material trimMat = new Material(litShader);
            trimMat.color = CyanAccent;
            trimMat.EnableKeyword("_EMISSION");
            trimMat.SetColor("_EmissionColor", CyanAccent * 0.8f);
            trim.GetComponent<Renderer>().sharedMaterial = trimMat;

            // 2. Title Text
            titleText = CreateTextMesh("Title", new Vector3(0f, 0.35f, -0.015f), 0.016f, TextAnchor.MiddleCenter, FontStyle.Bold, CyanAccent);
            titleText.text = "VR FIRE SAFETY TRAINING • KITCHEN";

            // 3. Current Objective Header
            objectiveHeader = CreateTextMesh("ObjHeader", new Vector3(0f, 0.24f, -0.015f), 0.014f, TextAnchor.MiddleCenter, FontStyle.Bold, OrangeAlert);
            objectiveHeader.text = "OBJECTIVE: ELECTRICAL CHIMNEY FIRE";

            // 4. Objective Instructions Body
            objectiveBody = CreateTextMesh("ObjBody", new Vector3(0f, 0.12f, -0.015f), 0.011f, TextAnchor.MiddleCenter, FontStyle.Normal, WhiteText);
            objectiveBody.text = "1. Sound Fire Alarm (Wall Unit)\n2. Isolate Power (Main Switch)\n3. Use CO2 Extinguisher (Black Band)";

            // 5. Fire Intensity Health Bar Background
            GameObject barBg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barBg.name = "HealthBar_Bg";
            barBg.transform.SetParent(transform, false);
            barBg.transform.localPosition = new Vector3(0f, -0.02f, -0.015f);
            barBg.transform.localScale = new Vector3(0.90f, 0.04f, 0.005f);
            Destroy(barBg.GetComponent<Collider>());
            Material barBgMat = new Material(litShader);
            barBgMat.color = new Color(0.12f, 0.14f, 0.18f);
            barBg.GetComponent<Renderer>().sharedMaterial = barBgMat;

            // Fire Intensity Health Bar Fill
            healthBarFill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            healthBarFill.name = "HealthBar_Fill";
            healthBarFill.transform.SetParent(barBg.transform, false);
            healthBarFill.transform.localPosition = Vector3.zero;
            healthBarFill.transform.localScale = new Vector3(1f, 0.85f, 1.2f);
            Destroy(healthBarFill.GetComponent<Collider>());
            Material fillMat = new Material(litShader);
            fillMat.color = RedAlert;
            fillMat.EnableKeyword("_EMISSION");
            fillMat.SetColor("_EmissionColor", RedAlert * 1.5f);
            healthBarFill.GetComponent<Renderer>().sharedMaterial = fillMat;

            // 6. P.A.S.S. Guide
            passGuideText = CreateTextMesh("PASS_Guide", new Vector3(0f, -0.15f, -0.015f), 0.010f, TextAnchor.MiddleCenter, FontStyle.Normal, MutedText);
            passGuideText.text = "P.A.S.S. PROCEDURE:\n[P] Pull Pin (A Button)  |  [A] Aim Nozzle (Point Controller)\n[S] Squeeze Handle (Trigger)  |  [S] Sweep Across Base";

            // 7. Status Telemetry
            statusLine = CreateTextMesh("StatusLine", new Vector3(0f, -0.28f, -0.015f), 0.011f, TextAnchor.MiddleCenter, FontStyle.Bold, GreenSafe);
            statusLine.text = "ALARM: STANDBY   |   POWER: ONLINE   |   EXTINGUISHER: READY";

            // 8. Box Collider for laser pointer interactions
            BoxCollider col = gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(1.2f, 0.9f, 0.08f);
            col.center = Vector3.zero;
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
            UpdateLiveTelemetry();
        }

        private void UpdateLiveTelemetry()
        {
            if (fireVFX == null) fireVFX = FindObjectOfType<FireVFXController>();
            if (fireAlarm == null) fireAlarm = FindObjectOfType<FireAlarmController>();
            if (powerSwitch == null) powerSwitch = FindObjectOfType<PowerSwitchController>();
            if (trainingHUD == null) trainingHUD = FindObjectOfType<TrainingHUD>();

            // 1. Health Bar & Fire Intensity
            float intensity = fireVFX != null && fireVFX.IsBurning ? fireVFX.FireIntensity : 0f;
            if (healthBarFill != null)
            {
                healthBarFill.transform.localScale = new Vector3(Mathf.Clamp01(intensity), 0.85f, 1.2f);
                healthBarFill.transform.localPosition = new Vector3((intensity - 1f) * 0.45f, 0f, 0f);
            }

            // 2. Status Line
            bool isAlarmOn = fireAlarm != null && fireAlarm.IsAlarmActive;
            bool isPowerOff = powerSwitch != null && !powerSwitch.IsPowerOn;

            string alarmStr = isAlarmOn ? "<color=#FF4030>ALARM: ACTIVE 🚨</color>" : "<color=#40EA70>ALARM: SAFE</color>";
            string powerStr = isPowerOff ? "<color=#40EA70>POWER: ISOLATED ⚡</color>" : "<color=#FF9020>POWER: LIVE</color>";

            statusLine.text = $"{alarmStr}   |   {powerStr}";

            // 3. Dynamic Objectives Update
            if (trainingHUD != null)
            {
                var state = trainingHUD.State;
                switch (state)
                {
                    case TrainingHUD.TrainingState.Obj01_Alarm:
                        objectiveHeader.text = "STEP 1: ACTIVATE FIRE ALARM";
                        objectiveHeader.color = RedAlert;
                        objectiveBody.text = "Point VR Controller Laser at the Red Wall Unit near the door\nand squeeze Index Trigger to sound the alarm!";
                        break;

                    case TrainingHUD.TrainingState.Obj02_PowerCut:
                        objectiveHeader.text = "STEP 2: ISOLATE ELECTRICAL POWER";
                        objectiveHeader.color = OrangeAlert;
                        objectiveBody.text = "Point VR Controller Laser at the Power Switch Box on the wall\nand squeeze Trigger to cut main power!";
                        break;

                    case TrainingHUD.TrainingState.Obj03_SelectExtinguisher:
                        objectiveHeader.text = "STEP 3: SELECT CO2 EXTINGUISHER";
                        objectiveHeader.color = CyanAccent;
                        objectiveBody.text = "Point at the Black-banded CO2 Extinguisher on the wall.\nSqueeze Trigger or Grip to pick it up!";
                        break;

                    case TrainingHUD.TrainingState.Obj04_PASSProcedure:
                    case TrainingHUD.TrainingState.Obj05_Extinguishing:
                        objectiveHeader.text = "STEP 4 & 5: P.A.S.S. DISCHARGE";
                        objectiveHeader.color = GreenSafe;
                        objectiveBody.text = "1. Press [A] Button on controller to PULL PIN\n2. Point extinguisher nozzle directly at chimney fire\n3. Squeeze Index Trigger to SPRAY CO2 VAPOR!";
                        break;

                    case TrainingHUD.TrainingState.MissionComplete:
                        objectiveHeader.text = "🏆 TRAINING MISSION COMPLETE!";
                        objectiveHeader.color = GreenSafe;
                        objectiveBody.text = "Electrical fire successfully suppressed using CO2!\nAll safety protocols (Alarm, Power, P.A.S.S.) followed.";
                        break;
                }
            }
        }
    }
}
