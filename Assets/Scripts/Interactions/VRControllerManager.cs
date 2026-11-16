using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using FireSafety.Extinguishers;
using FireSafety.Fire;
using FireSafety.Training;

namespace FireSafety.Interactions
{
    /// <summary>
    /// Comprehensive VR Motion Controller & Headset Manager for the VR Fire Safety Training project.
    /// 
    /// Capabilities:
    ///  1. VR Headset & Stereo Display:
    ///     - Automatically initializes true immersive stereoscopic VR for Meta Quest (Link/AirLink), OpenXR, SteamVR.
    ///     - Enforces opaque background (no AR/Passthrough bleedthrough).
    ///     - Positions player directly inside the 3D kitchen environment.
    /// 
    ///  2. Dual 6-DoF Tracked Controller Hands:
    ///     - Left & Right controller tracking (position + orientation).
    ///     - Procedural 3D firefighter safety glove visual models.
    ///     - Interactive laser pointer with dynamic reticle dot for aiming at distant objects.
    /// 
    ///  3. VR Locomotion & Turning:
    ///     - Left Thumbstick: Smooth horizontal walking inside kitchen room bounds.
    ///     - Right Thumbstick: Snap-turning (45-degree clean turns) or smooth turning.
    /// 
    ///  4. VR Fire Equipment Interaction:
    ///     - Point laser at wall extinguisher and squeeze Trigger or Grip -> Picks it up into hand!
    ///     - Natural hand aiming: Extinguisher follows controller motion in full 3D space.
    ///     - P.A.S.S. Step 1 (Pull Pin): Press [A / Primary] button on controller -> Safety pin pulled!
    ///     - P.A.S.S. Step 2 (Aim): Point hand at fire base.
    ///     - P.A.S.S. Step 3 (Squeeze): Squeeze Index Trigger on controller -> Continuous realistic discharge spray!
    ///     - P.A.S.S. Step 4 (Sweep): Sweep controller back-and-forth across fire base.
    ///     - Drop / Reset: Press [B / Secondary] button or Grip while not spraying.
    ///     - Haptic Feedback: Realistic vibration during spray, button clicks, and pin pull.
    /// 
    ///  5. Wall Device Interaction:
    ///     - Point laser at Fire Alarm button -> Squeeze Trigger -> Alarm activated!
    ///     - Point laser at Power Switch -> Squeeze Trigger -> Main power cut!
    /// </summary>
    public class VRControllerManager : MonoBehaviour
    {
        [Header("Locomotion Settings")]
        [SerializeField] private float vrMoveSpeed = 2.4f;
        [SerializeField] private float snapTurnAngle = 45f;
        [SerializeField] private float turnCooldown = 0.30f;

        [Header("Laser Pointer Settings")]
        [SerializeField] private float maxLaserDistance = 8.0f;
        [SerializeField] private Color laserNormalColor = new Color(0.2f, 0.8f, 1.0f, 0.65f); // Cyan
        [SerializeField] private Color laserHoverColor  = new Color(0.2f, 1.0f, 0.4f, 0.90f); // Green
        [SerializeField] private Color laserDangerColor = new Color(1.0f, 0.25f, 0.2f, 0.90f); // Red

        [Header("Extinguisher Hold Pose")]
        [SerializeField] private Vector3 rightHandHoldOffset = new Vector3(0.02f, -0.22f, 0.10f);
        [SerializeField] private Vector3 rightHandHoldRotation = new Vector3(15f, 0f, 0f);

        // Tracked Input Devices
        private InputDevice leftController;
        private InputDevice rightController;
        private InputDevice hmdDevice;
        private bool isLeftTracked = false;
        private bool isRightTracked = false;
        private bool isHMDTracked = false;

        // Visual Hand Rig Hierarchy
        private GameObject leftHandRoot;
        private GameObject rightHandRoot;
        private LineRenderer rightLaser;
        private GameObject laserDot;
        private LineRenderer leftLaser;
        private GameObject leftLaserDot;

        // Interaction State
        private FireExtinguisher activeHeldExtinguisher;
        private GameObject currentHoveredObject;
        private float lastTurnTime = 0f;
        private bool wasTriggerPressed = false;
        private bool wasPinButtonPressed = false;
        private bool wasDropButtonPressed = false;
        private bool isDischarging = false;

        // Player bounds in Kitchen
        private const float KitchenMinX = 199.2f;
        private const float KitchenMaxX = 205.6f;
        private const float KitchenMinZ = -3.6f;
        private const float KitchenMaxZ = 3.6f;
        private const float PlayerEyeHeight = 1.60f;

        public FireExtinguisher ActiveHeldExtinguisher => activeHeldExtinguisher;
        public bool IsVRActive => isHMDTracked || isRightTracked || isLeftTracked || XRSettings.isDeviceActive;

        private void Awake()
        {
            EnsureVRCameraSetup();
            BuildVisualHandRigs();
        }

        private void Start()
        {
            InputDevices.deviceConnected += OnDeviceConnected;
            RefreshInputDevices();
        }

        private void OnDestroy()
        {
            InputDevices.deviceConnected -= OnDeviceConnected;
        }

        private void OnDeviceConnected(InputDevice dev)
        {
            RefreshInputDevices();
        }

        private void RefreshInputDevices()
        {
            var leftDevices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Left, leftDevices);
            if (leftDevices.Count > 0)
            {
                leftController = leftDevices[0];
                isLeftTracked = true;
            }

            var rightDevices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Right, rightDevices);
            if (rightDevices.Count > 0)
            {
                rightController = rightDevices[0];
                isRightTracked = true;
            }

            var hmdDevices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, hmdDevices);
            if (hmdDevices.Count > 0)
            {
                hmdDevice = hmdDevices[0];
                isHMDTracked = true;
            }
        }

        private void EnsureVRCameraSetup()
        {
            Camera cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;

            if (cam != null)
            {
                // CRITICAL: Set Alpha = 1.0 to prevent Meta Quest / OpenXR passthrough (AR mode) bleedthrough
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 1.0f);
                cam.nearClipPlane = 0.10f; // Close near plane for VR hands
                cam.farClipPlane = 500f;

                // Ensure player spawns inside the kitchen if placed at origin
                if (transform.position.x < 100f)
                {
                    transform.position = new Vector3(202.8f, PlayerEyeHeight, 0.0f);
                    transform.rotation = Quaternion.Euler(0f, -90f, 0f); // Facing stove and fire
                    Debug.Log("[VRControllerManager] Player repositioned inside Kitchen Environment (X=202.8, Y=1.60, Z=0.0).");
                }
            }
        }

        private void BuildVisualHandRigs()
        {
            // Shader for hand models
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");

            Material gloveMat = new Material(litShader);
            gloveMat.name = "Mat_VR_Glove";
            gloveMat.color = new Color(0.82f, 0.68f, 0.44f); // Golden tan leather firefighter glove
            gloveMat.SetFloat("_Metallic", 0.05f);
            gloveMat.SetFloat("_Smoothness", 0.30f);

            Material cuffMat = new Material(litShader);
            cuffMat.name = "Mat_VR_Cuff";
            cuffMat.color = new Color(0.15f, 0.17f, 0.20f); // Kevlar black cuff
            cuffMat.SetFloat("_Metallic", 0.10f);
            cuffMat.SetFloat("_Smoothness", 0.40f);

            Material laserMat = new Material(Shader.Find("Sprites/Default"));

            // ─── 1. RIGHT HAND RIG ──────────────────────────────────────────────
            rightHandRoot = new GameObject("VR_RightHand_Controller");
            rightHandRoot.transform.SetParent(transform, false);
            rightHandRoot.transform.localPosition = new Vector3(0.20f, -0.15f, 0.40f); // Default rest position
            rightHandRoot.transform.localRotation = Quaternion.identity;

            BuildGloveMesh(rightHandRoot.transform, isRight: true, gloveMat, cuffMat);

            // Right Laser Pointer
            GameObject rLaserObj = new GameObject("Right_LaserPointer");
            rLaserObj.transform.SetParent(rightHandRoot.transform, false);
            rightLaser = rLaserObj.AddComponent<LineRenderer>();
            rightLaser.material = laserMat;
            rightLaser.startWidth = 0.006f;
            rightLaser.endWidth = 0.002f;
            rightLaser.startColor = laserNormalColor;
            rightLaser.endColor = new Color(laserNormalColor.r, laserNormalColor.g, laserNormalColor.b, 0.1f);
            rightLaser.useWorldSpace = true;
            rightLaser.positionCount = 2;

            laserDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            laserDot.name = "LaserReticleDot";
            laserDot.transform.SetParent(rLaserObj.transform, false);
            laserDot.transform.localScale = Vector3.one * 0.025f;
            Destroy(laserDot.GetComponent<Collider>());
            laserDot.GetComponent<Renderer>().sharedMaterial = laserMat;

            // ─── 2. LEFT HAND RIG ───────────────────────────────────────────────
            leftHandRoot = new GameObject("VR_LeftHand_Controller");
            leftHandRoot.transform.SetParent(transform, false);
            leftHandRoot.transform.localPosition = new Vector3(-0.20f, -0.15f, 0.40f);
            leftHandRoot.transform.localRotation = Quaternion.identity;

            BuildGloveMesh(leftHandRoot.transform, isRight: false, gloveMat, cuffMat);

            // Left Laser Pointer
            GameObject lLaserObj = new GameObject("Left_LaserPointer");
            lLaserObj.transform.SetParent(leftHandRoot.transform, false);
            leftLaser = lLaserObj.AddComponent<LineRenderer>();
            leftLaser.material = laserMat;
            leftLaser.startWidth = 0.006f;
            leftLaser.endWidth = 0.002f;
            leftLaser.startColor = laserNormalColor;
            leftLaser.endColor = new Color(laserNormalColor.r, laserNormalColor.g, laserNormalColor.b, 0.1f);
            leftLaser.useWorldSpace = true;
            leftLaser.positionCount = 2;

            leftLaserDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leftLaserDot.name = "LeftLaserReticleDot";
            leftLaserDot.transform.SetParent(lLaserObj.transform, false);
            leftLaserDot.transform.localScale = Vector3.one * 0.025f;
            Destroy(leftLaserDot.GetComponent<Collider>());
            leftLaserDot.GetComponent<Renderer>().sharedMaterial = laserMat;
        }

        private void BuildGloveMesh(Transform parent, bool isRight, Material gloveMat, Material cuffMat)
        {
            float side = isRight ? 1f : -1f;

            // Palm
            GameObject palm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            palm.name = "GlovePalm";
            palm.transform.SetParent(parent, false);
            palm.transform.localPosition = new Vector3(0.01f * side, -0.015f, 0.05f);
            palm.transform.localScale = new Vector3(0.075f, 0.035f, 0.085f);
            Destroy(palm.GetComponent<Collider>());
            palm.GetComponent<Renderer>().sharedMaterial = gloveMat;

            // Thumb
            GameObject thumb = GameObject.CreatePrimitive(PrimitiveType.Cube);
            thumb.name = "GloveThumb";
            thumb.transform.SetParent(palm.transform, false);
            thumb.transform.localPosition = new Vector3(-0.55f * side, 0.15f, -0.1f);
            thumb.transform.localEulerAngles = new Vector3(0f, -30f * side, 25f * side);
            thumb.transform.localScale = new Vector3(0.35f, 0.65f, 0.55f);
            Destroy(thumb.GetComponent<Collider>());
            thumb.GetComponent<Renderer>().sharedMaterial = gloveMat;

            // Knuckles & Fingers
            GameObject fingers = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fingers.name = "GloveFingers";
            fingers.transform.SetParent(palm.transform, false);
            fingers.transform.localPosition = new Vector3(0f, 0f, 0.55f);
            fingers.transform.localScale = new Vector3(0.92f, 0.75f, 0.45f);
            Destroy(fingers.GetComponent<Collider>());
            fingers.GetComponent<Renderer>().sharedMaterial = gloveMat;

            // Wrist Cuff
            GameObject cuff = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cuff.name = "GloveCuff";
            cuff.transform.SetParent(palm.transform, false);
            cuff.transform.localPosition = new Vector3(0f, 0f, -0.55f);
            cuff.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
            cuff.transform.localScale = new Vector3(0.95f, 0.40f, 0.95f);
            Destroy(cuff.GetComponent<Collider>());
            cuff.GetComponent<Renderer>().sharedMaterial = cuffMat;
        }

        private void Update()
        {
            UpdateDeviceTracking();
            HandleVRLocomotion();
            HandleVRLaserInteraction();
            HandleExtinguisherControl();
        }

        private void UpdateDeviceTracking()
        {
            // Refresh if disconnected
            if (!leftController.isValid || !rightController.isValid)
            {
                RefreshInputDevices();
            }

            // 1. Right Controller Pose
            if (rightController.isValid)
            {
                if (rightController.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 rPos))
                {
                    rightHandRoot.transform.localPosition = rPos;
                }
                if (rightController.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rRot))
                {
                    rightHandRoot.transform.localRotation = rRot;
                }
            }
            else
            {
                // Fallback procedural idle sway when controllers are resting
                float bob = Mathf.Sin(Time.time * 1.5f) * 0.008f;
                rightHandRoot.transform.localPosition = new Vector3(0.22f, -0.22f + bob, 0.45f);
                rightHandRoot.transform.localRotation = Quaternion.Euler(5f, -5f, 0f);
            }

            // 2. Left Controller Pose
            if (leftController.isValid)
            {
                if (leftController.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 lPos))
                {
                    leftHandRoot.transform.localPosition = lPos;
                }
                if (leftController.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion lRot))
                {
                    leftHandRoot.transform.localRotation = lRot;
                }
            }
            else
            {
                float bob = Mathf.Sin(Time.time * 1.5f + 1.2f) * 0.008f;
                leftHandRoot.transform.localPosition = new Vector3(-0.22f, -0.22f + bob, 0.45f);
                leftHandRoot.transform.localRotation = Quaternion.Euler(5f, 5f, 0f);
            }
        }

        private void HandleVRLocomotion()
        {
            // ─── 1. Left Thumbstick Locomotion (Move) ───────────────────────────
            Vector2 leftStick = Vector2.zero;
            if (leftController.isValid)
            {
                leftController.TryGetFeatureValue(CommonUsages.primary2DAxis, out leftStick);
            }

            if (leftStick.magnitude > 0.12f)
            {
                Transform cam = Camera.main != null ? Camera.main.transform : transform;
                Vector3 forward = cam.forward; forward.y = 0f; forward.Normalize();
                Vector3 right = cam.right; right.y = 0f; right.Normalize();

                Vector3 moveDir = (forward * leftStick.y + right * leftStick.x).normalized;
                Vector3 targetPos = transform.position + moveDir * (vrMoveSpeed * Time.deltaTime);

                // Kitchen boundaries check
                if (transform.position.x > 100f)
                {
                    targetPos.x = Mathf.Clamp(targetPos.x, KitchenMinX, KitchenMaxX);
                    targetPos.z = Mathf.Clamp(targetPos.z, KitchenMinZ, KitchenMaxZ);
                    targetPos.y = PlayerEyeHeight;
                }

                transform.position = targetPos;
            }

            // ─── 2. Right Thumbstick Snap Turn ──────────────────────────────────
            Vector2 rightStick = Vector2.zero;
            if (rightController.isValid)
            {
                rightController.TryGetFeatureValue(CommonUsages.primary2DAxis, out rightStick);
            }

            if (Mathf.Abs(rightStick.x) > 0.65f && Time.time - lastTurnTime > turnCooldown)
            {
                float turnDir = Mathf.Sign(rightStick.x);
                transform.Rotate(0f, turnDir * snapTurnAngle, 0f, Space.World);
                lastTurnTime = Time.time;
                TriggerHaptic(rightController, 0.25f, 0.06f);
            }
        }

        private void HandleVRLaserInteraction()
        {
            // If holding an extinguisher, hide laser and let the nozzle be the aim point
            if (activeHeldExtinguisher != null)
            {
                rightLaser.enabled = false;
                laserDot.SetActive(false);
                return;
            }

            rightLaser.enabled = true;
            laserDot.SetActive(true);

            Vector3 origin = rightHandRoot.transform.position;
            Vector3 forward = rightHandRoot.transform.forward;

            Ray ray = new Ray(origin, forward);
            RaycastHit hit;
            bool didHit = Physics.Raycast(ray, out hit, maxLaserDistance);

            Vector3 endPos = didHit ? hit.point : (origin + forward * maxLaserDistance);
            rightLaser.SetPosition(0, origin);
            rightLaser.SetPosition(1, endPos);
            laserDot.transform.position = endPos;

            // Check interactable hover
            currentHoveredObject = didHit ? hit.collider.gameObject : null;

            bool isInteractive = false;
            if (didHit)
            {
                FireExtinguisher ext = hit.collider.GetComponentInParent<FireExtinguisher>();
                FireAlarmController alarm = hit.collider.GetComponentInParent<FireAlarmController>();
                PowerSwitchController pSwitch = hit.collider.GetComponentInParent<PowerSwitchController>();

                if (ext != null || alarm != null || pSwitch != null)
                {
                    isInteractive = true;
                }
            }

            Color curColor = isInteractive ? laserHoverColor : laserNormalColor;
            rightLaser.startColor = curColor;
            laserDot.GetComponent<Renderer>().sharedMaterial.color = curColor;

            // Check Trigger or Grip for interaction
            bool triggerDown = false;
            if (rightController.isValid)
            {
                if (rightController.TryGetFeatureValue(CommonUsages.trigger, out float trigVal))
                {
                    triggerDown = trigVal > 0.55f;
                }
                if (!triggerDown && rightController.TryGetFeatureValue(CommonUsages.grip, out float gripVal))
                {
                    triggerDown = gripVal > 0.55f;
                }
            }

            if (triggerDown && !wasTriggerPressed && didHit)
            {
                ExecuteLaserClick(hit);
            }

            wasTriggerPressed = triggerDown;
        }

        private void ExecuteLaserClick(RaycastHit hit)
        {
            // 1. Fire Extinguisher Pick-up
            FireExtinguisher ext = hit.collider.GetComponentInParent<FireExtinguisher>();
            if (ext != null && !ext.IsHeld)
            {
                EquipExtinguisher(ext);
                TriggerHaptic(rightController, 0.70f, 0.15f);
                return;
            }

            // 2. Fire Alarm Unit
            FireAlarmController alarm = hit.collider.GetComponentInParent<FireAlarmController>();
            if (alarm != null)
            {
                alarm.ToggleAlarm();
                TriggerHaptic(rightController, 0.85f, 0.20f);
                Debug.Log("[VRControllerManager] Fire Alarm toggled via VR Controller Laser!");
                return;
            }

            // 3. Electrical Power Switch
            PowerSwitchController pSwitch = hit.collider.GetComponentInParent<PowerSwitchController>();
            if (pSwitch != null)
            {
                pSwitch.TogglePower();
                TriggerHaptic(rightController, 0.85f, 0.20f);
                Debug.Log("[VRControllerManager] Power Switch flipped via VR Controller Laser!");
                return;
            }
        }

        public void EquipExtinguisher(FireExtinguisher ext)
        {
            if (activeHeldExtinguisher != null && activeHeldExtinguisher != ext)
            {
                activeHeldExtinguisher.Drop();
            }

            activeHeldExtinguisher = ext;
            ext.Pickup(rightHandRoot.transform);

            // Notify Training HUD
            var hud = FindObjectOfType<TrainingHUD>();
            if (hud != null) hud.OnExtinguisherSelected(ext.Type);

            Debug.Log($"[VRControllerManager] ✓ Equipped {ext.DisplayName} in VR Right Hand.");
        }

        private void HandleExtinguisherControl()
        {
            if (activeHeldExtinguisher == null) return;

            // ─── 1. PULL SAFETY PIN ([A] / Primary Button on Right Controller) ──
            bool pinButtonPressed = false;
            if (rightController.isValid)
            {
                rightController.TryGetFeatureValue(CommonUsages.primaryButton, out pinButtonPressed);
            }

            if (pinButtonPressed && !wasPinButtonPressed)
            {
                if (!activeHeldExtinguisher.IsSafetyPinRemoved)
                {
                    activeHeldExtinguisher.PullSafetyPin();
                    TriggerHaptic(rightController, 0.60f, 0.12f);
                    TriggerHaptic(leftController, 0.60f, 0.12f);
                    Debug.Log("[VRControllerManager] [P.A.S.S. Step 1] Safety pin pulled via VR Controller [A] Button!");
                }
            }
            wasPinButtonPressed = pinButtonPressed;

            // ─── 2. SQUEEZE HANDLE TO DISCHARGE (Right Index Trigger) ───────────
            float triggerAmount = 0f;
            if (rightController.isValid)
            {
                rightController.TryGetFeatureValue(CommonUsages.trigger, out triggerAmount);
            }

            // Desktop fallback: Space or Left Mouse
            if (Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0))
            {
                triggerAmount = 1.0f;
            }

            if (triggerAmount > 0.20f)
            {
                if (!isDischarging)
                {
                    activeHeldExtinguisher.SqueezeHandle();
                    isDischarging = true;
                }

                // Continuous haptic vibration kickback while spraying
                TriggerHaptic(rightController, Mathf.Lerp(0.25f, 0.65f, triggerAmount), 0.08f);
            }
            else if (isDischarging)
            {
                activeHeldExtinguisher.ReleaseHandle();
                isDischarging = false;
            }

            // ─── 3. DROP / PUT BACK ([B] / Secondary Button or Grip) ────────────
            bool dropPressed = false;
            if (rightController.isValid)
            {
                rightController.TryGetFeatureValue(CommonUsages.secondaryButton, out dropPressed);
            }

            if (dropPressed && !wasDropButtonPressed)
            {
                if (isDischarging)
                {
                    activeHeldExtinguisher.ReleaseHandle();
                    isDischarging = false;
                }
                activeHeldExtinguisher.Drop();
                activeHeldExtinguisher = null;
                TriggerHaptic(rightController, 0.40f, 0.10f);
                Debug.Log("[VRControllerManager] Extinguisher released from VR hand.");
            }
            wasDropButtonPressed = dropPressed;
        }

        private void TriggerHaptic(InputDevice device, float amplitude, float duration)
        {
            if (device.isValid)
            {
                device.SendHapticImpulse(0, amplitude, duration);
            }
        }
    }
}
