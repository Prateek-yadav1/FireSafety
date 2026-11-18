using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using FireSafety.Extinguishers;
using FireSafety.Fire;
using FireSafety.Training;

namespace FireSafety.Interactions
{
    /// <summary>
    /// VRControllerManager — Comprehensive Meta Quest 6-DoF tracking and interaction manager:
    ///   - Full 6-DoF physical head tracking (HMD rotation & translation applied directly to Camera)
    ///   - 100% Controller-driven interactions (Joysticks, [A]/[B]/[X]/[Y], Triggers, Grips)
    ///   - Zero keyboard dependency in VR mode
    ///   - Dual-hand natural proximity touch & grab
    ///   - Right-stick smooth & snap turning around player position + quick 180° turnaround
    ///   - High-precision controller laser pointer for 3D UI & distance interaction
    /// </summary>
    public class VRControllerManager : MonoBehaviour
    {
        [Header("Locomotion")]
        [SerializeField] private float vrMoveSpeed    = 2.8f;
        [SerializeField] private float snapTurnAngle  = 45f;
        [SerializeField] private float turnCooldown   = 0.28f;

        [Header("Laser Pointer")]
        [SerializeField] private float maxLaserDist   = 8.0f;
        [SerializeField] private Color laserNormal    = new Color(0.2f, 0.8f, 1f, 0.65f);
        [SerializeField] private Color laserHover     = new Color(0.2f, 1f, 0.4f, 0.90f);

        [Header("Extinguisher Hold Pose")]
        [SerializeField] private Vector3 holdOffset   = new Vector3(0.02f, -0.22f, 0.10f);
        [SerializeField] private Vector3 holdRotation = new Vector3(15f, 0f, 0f);

        // Room bounds
        private const float Room01MinX = -3.2f;
        private const float Room01MaxX =  3.2f;
        private const float Room01MinZ = -1.2f;
        private const float Room01MaxZ =  4.8f;

        private const float KitchenMinX = 199.5f;
        private const float KitchenMaxX = 205.2f;
        private const float KitchenMinZ = -3.4f;
        private const float KitchenMaxZ =  3.4f;

        // XR devices
        private InputDevice _left, _right, _hmd;
        private bool _leftOk, _rightOk, _hmdOk;

        // Visual hands & laser
        private GameObject   _leftHand, _rightHand;
        private LineRenderer _rightLaser;
        private GameObject   _rightDot;

        // Interaction state
        private FireExtinguisher _heldExt = null;
        private float _lastTurn           = 0f;
        private bool  _wasTrigRight       = false;
        private bool  _wasTrigLeft        = false;
        private bool  _wasPrimaryRight    = false;
        private bool  _wasPrimaryLeft     = false;
        private bool  _wasSecRight        = false;
        private bool  _wasSecLeft         = false;
        private bool  _wasPin             = false;
        private bool  _discharging        = false;

        private float _lastAlarmTouchTime = 0f;
        private float _lastPowerTouchTime = 0f;

        public bool IsVRActive => _hmdOk || XRSettings.isDeviceActive;

        // ─── Lifecycle ─────────────────────────────────────────────────────────

        private void OnEnable()
        {
            Application.onBeforeRender += OnBeforeRender;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= OnBeforeRender;
        }

        private void Start()
        {
            TryFindDevices();
            if (IsVRActive)
            {
                BuildHandRigs();
                Debug.Log("[VRControllerManager] Meta Quest VR active. Hands & 6-DoF tracking initialized.");
            }
        }

        private void Update()
        {
            TryFindDevices();
            if (!IsVRActive) return;

            if (_leftHand == null || _rightHand == null)
            {
                BuildHandRigs();
            }

            UpdateHeadPose();
            HandleVRLocomotion();
            UpdateHandPoses();
            UpdateLaser();
            HandleControllerButtonInteractions();
            HandleDirectHandProximity();
            HandleExtinguisherControl();
        }

        private void OnBeforeRender()
        {
            if (IsVRActive)
            {
                UpdateHeadPose();
            }
        }

        // ─── Device Discovery ──────────────────────────────────────────────────

        private void TryFindDevices()
        {
            if (!_hmdOk)
            {
                var list = new List<InputDevice>();
                InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, list);
                if (list.Count > 0) { _hmd = list[0]; _hmdOk = _hmd.isValid; }
            }
            if (!_leftOk)
            {
                var list = new List<InputDevice>();
                InputDevices.GetDevicesWithCharacteristics(
                    InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller, list);
                if (list.Count > 0) { _left = list[0]; _leftOk = _left.isValid; }
            }
            if (!_rightOk)
            {
                var list = new List<InputDevice>();
                InputDevices.GetDevicesWithCharacteristics(
                    InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, list);
                if (list.Count > 0) { _right = list[0]; _rightOk = _right.isValid; }
            }
        }

        // ─── 6-DoF Head Tracking ───────────────────────────────────────────────

        private void UpdateHeadPose()
        {
            Camera cam = Camera.main ?? GetComponent<Camera>();
            if (cam == null) return;

            Quaternion hmdRot = Quaternion.identity;
            Vector3    hmdPos = Vector3.zero;
            bool gotRot = false;
            bool gotPos = false;

            // 1. Query HMD device
            if (_hmdOk && _hmd.isValid)
            {
                gotRot = _hmd.TryGetFeatureValue(CommonUsages.deviceRotation, out hmdRot);
                gotPos = _hmd.TryGetFeatureValue(CommonUsages.devicePosition, out hmdPos);
            }

            // 2. Fallback to CenterEye XRNode
            if (!gotRot)
            {
                InputDevice centerEye = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
                if (centerEye.isValid)
                {
                    gotRot = centerEye.TryGetFeatureValue(CommonUsages.deviceRotation, out hmdRot);
                    gotPos = centerEye.TryGetFeatureValue(CommonUsages.devicePosition, out hmdPos);
                }
            }

            // 3. Fallback to legacy XR InputTracking
            if (!gotRot)
            {
                hmdRot = InputTracking.GetLocalRotation(XRNode.CenterEye);
                if (hmdRot != Quaternion.identity) gotRot = true;
                hmdPos = InputTracking.GetLocalPosition(XRNode.CenterEye);
                if (hmdPos != Vector3.zero) gotPos = true;
            }

            // Apply 6-DoF rotation and position to Camera
            if (gotRot)
            {
                cam.transform.localRotation = hmdRot;
            }
            if (gotPos)
            {
                // The XR rig root is at floor level (Y=0).
                // HMD devicePosition.y is the head height above the tracking floor (~1.5-1.7m when standing).
                // Do NOT add a fixed eye-height offset — that doubles the height and puts camera in the roof!
                // Just use the raw HMD position relative to the rig floor.
                bool inKitchen = (cam.transform.parent != null ? cam.transform.parent.position.x : cam.transform.position.x) > 100f;
                float baseZ = inKitchen ? 0f : 0.45f;
                cam.transform.localPosition = new Vector3(hmdPos.x, hmdPos.y, baseZ + hmdPos.z);
            }
        }

        // ─── Locomotion & Turning ──────────────────────────────────────────────

        private void HandleVRLocomotion()
        {
            Transform xrRoot = GetXRRoot();
            if (xrRoot == null) return;

            bool inKitchen = xrRoot.position.x > 100f;
            Camera cam = Camera.main ?? GetComponent<Camera>();

            // ── Left Stick: Walk (Forward/Back/Left/Right) ──
            Vector2 leftStick = Vector2.zero;
            if (_left.isValid) _left.TryGetFeatureValue(CommonUsages.primary2DAxis, out leftStick);

            if (leftStick.sqrMagnitude > 0.02f)
            {
                Vector3 fwd   = cam != null ? cam.transform.forward : xrRoot.forward;
                Vector3 right = cam != null ? cam.transform.right   : xrRoot.right;
                fwd.y = 0f;   fwd.Normalize();
                right.y = 0f; right.Normalize();

                Vector3 move = (fwd * leftStick.y + right * leftStick.x) * vrMoveSpeed * Time.deltaTime;
                Vector3 np   = xrRoot.position + move;

                if (inKitchen)
                {
                    np.x = Mathf.Clamp(np.x, KitchenMinX, KitchenMaxX);
                    np.z = Mathf.Clamp(np.z, KitchenMinZ, KitchenMaxZ);
                }
                else
                {
                    np.x = Mathf.Clamp(np.x, Room01MinX, Room01MaxX);
                    np.z = Mathf.Clamp(np.z, Room01MinZ, Room01MaxZ);
                }
                np.y = 0f;
                xrRoot.position = np;
            }

            // ── Right Stick: Turning & 180° Turnaround ──
            Vector2 rightStick = Vector2.zero;
            if (_right.isValid) _right.TryGetFeatureValue(CommonUsages.primary2DAxis, out rightStick);

            Vector3 pivot = cam != null ? cam.transform.position : xrRoot.position;
            pivot.y = 0f;

            // 1. Quick 180° Turnaround (pull right stick straight down)
            if (rightStick.y < -0.65f && Time.time - _lastTurn > turnCooldown)
            {
                xrRoot.RotateAround(pivot, Vector3.up, 180f);
                _lastTurn = Time.time;
                Haptic(_right, 0.40f, 0.12f);
            }
            // 2. Snap Turn Left / Right
            else if (Mathf.Abs(rightStick.x) > 0.55f && Time.time - _lastTurn > turnCooldown)
            {
                float angle = Mathf.Sign(rightStick.x) * snapTurnAngle;
                xrRoot.RotateAround(pivot, Vector3.up, angle);
                _lastTurn = Time.time;
                Haptic(_right, 0.30f, 0.08f);
            }
        }

        // ─── Find XR Rig Root ──────────────────────────────────────────────────

        private Transform GetXRRoot()
        {
            Camera cam = Camera.main ?? GetComponent<Camera>();
            if (cam == null) return null;

            Transform t = cam.transform;
            while (t.parent != null) t = t.parent;
            return t;
        }

        // ─── Hand Rigs ─────────────────────────────────────────────────────────

        private void BuildHandRigs()
        {
            Transform xrRoot = GetXRRoot();

            if (_leftHand == null)
                _leftHand = BuildGlove("VR_LeftHand", true, xrRoot);

            if (_rightHand == null)
            {
                _rightHand = BuildGlove("VR_RightHand", false, xrRoot);
                BuildLaserPointer(_rightHand.transform);
            }
        }

        private GameObject BuildGlove(string name, bool isLeft, Transform parent)
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null)
            {
                if (parent != null && existing.transform.parent != parent)
                    existing.transform.SetParent(parent, false);
                return existing;
            }

            GameObject hand = new GameObject(name);
            if (parent != null) hand.transform.SetParent(parent, false);

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            // Tactical firefighter palm
            GameObject palm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            palm.name = "Palm";
            palm.transform.SetParent(hand.transform, false);
            palm.transform.localPosition = new Vector3(isLeft ? -0.015f : 0.015f, -0.02f, 0.04f);
            palm.transform.localScale    = new Vector3(0.085f, 0.045f, 0.10f);
            Destroy(palm.GetComponent<Collider>());

            Material mat = new Material(litShader);
            mat.color = new Color(0.18f, 0.19f, 0.22f); // Kevlar dark grey
            mat.SetFloat("_Metallic", 0.35f);
            mat.SetFloat("_Smoothness", 0.40f);
            palm.GetComponent<Renderer>().sharedMaterial = mat;

            // Hi-Vis yellow safety strip
            GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = "SafetyTrim";
            strip.transform.SetParent(palm.transform, false);
            strip.transform.localPosition = new Vector3(0f, 0.52f, 0f);
            strip.transform.localScale    = new Vector3(1.02f, 0.18f, 0.60f);
            Destroy(strip.GetComponent<Collider>());

            Material trimMat = new Material(litShader);
            trimMat.color = new Color(1.0f, 0.85f, 0.05f); // Hi-vis fluorescent yellow
            trimMat.EnableKeyword("_EMISSION");
            trimMat.SetColor("_EmissionColor", new Color(0.85f, 0.70f, 0.05f));
            strip.GetComponent<Renderer>().sharedMaterial = trimMat;

            // Proximity Sphere Trigger Collider
            SphereCollider sc = hand.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 0.18f;
            sc.center = new Vector3(0f, -0.02f, 0.04f);

            return hand;
        }

        private void BuildLaserPointer(Transform parent)
        {
            if (_rightLaser != null) return;

            GameObject laserGO = new GameObject("VR_LaserPointer");
            laserGO.transform.SetParent(parent, false);
            laserGO.transform.localPosition = new Vector3(0f, -0.01f, 0.08f);

            _rightLaser = laserGO.AddComponent<LineRenderer>();
            _rightLaser.useWorldSpace   = true;
            _rightLaser.startWidth      = 0.006f;
            _rightLaser.endWidth        = 0.002f;
            _rightLaser.positionCount   = 2;

            Shader s = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            _rightLaser.material = new Material(s);

            _rightDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _rightDot.name = "LaserReticle";
            _rightDot.transform.localScale = Vector3.one * 0.024f;
            Destroy(_rightDot.GetComponent<Collider>());
            _rightDot.GetComponent<Renderer>().sharedMaterial = new Material(s);
            _rightDot.SetActive(false);
        }

        private void UpdateHandPoses()
        {
            Transform xrRoot = GetXRRoot();

            if (_leftHand != null && xrRoot != null && _leftHand.transform.parent != xrRoot)
                _leftHand.transform.SetParent(xrRoot, false);
            if (_rightHand != null && xrRoot != null && _rightHand.transform.parent != xrRoot)
                _rightHand.transform.SetParent(xrRoot, false);

            if (_leftHand != null && _left.isValid)
            {
                if (_left.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 lPos) &&
                    _left.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion lRot))
                {
                    _leftHand.transform.localPosition = lPos;
                    _leftHand.transform.localRotation = lRot;
                }
            }

            if (_rightHand != null && _right.isValid)
            {
                if (_right.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 rPos) &&
                    _right.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rRot))
                {
                    _rightHand.transform.localPosition = rPos;
                    _rightHand.transform.localRotation = rRot;

                    if (_heldExt != null)
                    {
                        _heldExt.transform.position = _rightHand.transform.TransformPoint(holdOffset);
                        _heldExt.transform.rotation = _rightHand.transform.rotation * Quaternion.Euler(holdRotation);
                    }
                }
            }
        }

        private void UpdateLaser()
        {
            if (_rightLaser == null || _rightHand == null) return;

            Ray ray = new Ray(_rightHand.transform.position, _rightHand.transform.forward);
            Vector3 endPos;
            Color col = laserNormal;

            if (Physics.Raycast(ray, out RaycastHit hit, maxLaserDist))
            {
                endPos = hit.point;
                bool hovering = hit.collider.GetComponentInParent<FireExtinguisher>() != null ||
                                hit.collider.GetComponentInParent<FireAlarmController>() != null ||
                                hit.collider.GetComponentInParent<PowerSwitchController>() != null ||
                                hit.collider.GetComponentInParent<VRTrainingBoard>() != null;
                if (hovering) col = laserHover;
                if (_rightDot != null) { _rightDot.SetActive(true); _rightDot.transform.position = endPos; }
            }
            else
            {
                endPos = ray.origin + ray.direction * maxLaserDist;
                if (_rightDot != null) _rightDot.SetActive(false);
            }

            _rightLaser.startColor = col;
            _rightLaser.endColor   = new Color(col.r, col.g, col.b, 0f);
            _rightLaser.SetPosition(0, _rightHand.transform.position);
            _rightLaser.SetPosition(1, endPos);
        }

        // ─── Controller Buttons Interaction ([A], [B], [X], [Y], Triggers) ─────

        private void HandleControllerButtonInteractions()
        {
            // Triggers
            float trigRightVal = 0f; if (_right.isValid) _right.TryGetFeatureValue(CommonUsages.trigger, out trigRightVal);
            float trigLeftVal  = 0f; if (_left.isValid)  _left.TryGetFeatureValue(CommonUsages.trigger, out trigLeftVal);

            bool trigRightDown = (trigRightVal > 0.5f) && !_wasTrigRight;
            bool trigLeftDown  = (trigLeftVal  > 0.5f) && !_wasTrigLeft;
            _wasTrigRight = trigRightVal > 0.5f;
            _wasTrigLeft  = trigLeftVal  > 0.5f;

            // [A] Button (Right) & [X] Button (Left)
            bool primaryRight = false; if (_right.isValid) _right.TryGetFeatureValue(CommonUsages.primaryButton, out primaryRight);
            bool primaryLeft  = false; if (_left.isValid)  _left.TryGetFeatureValue(CommonUsages.primaryButton, out primaryLeft);
            bool primaryRightDown = primaryRight && !_wasPrimaryRight;
            bool primaryLeftDown  = primaryLeft  && !_wasPrimaryLeft;
            _wasPrimaryRight = primaryRight;
            _wasPrimaryLeft  = primaryLeft;

            // [B] Button (Right) & [Y] Button (Left)
            bool secRight = false; if (_right.isValid) _right.TryGetFeatureValue(CommonUsages.secondaryButton, out secRight);
            bool secLeft  = false; if (_left.isValid)  _left.TryGetFeatureValue(CommonUsages.secondaryButton, out secLeft);
            bool secRightDown = secRight && !_wasSecRight;
            bool secLeftDown  = secLeft  && !_wasSecLeft;
            _wasSecRight = secRight;
            _wasSecLeft  = secLeft;

            bool advancePressed = trigRightDown || trigLeftDown || primaryRightDown || primaryLeftDown;

            // 1. Advance Training HUD & VRTrainingBoard modal screens
            var hud = FindObjectOfType<TrainingHUD>();
            if (hud != null && hud.IsInModalState && advancePressed)
            {
                hud.OnVRTriggerPressed();
                Haptic(_right, 0.55f, 0.10f);
                Haptic(_left,  0.55f, 0.10f);
                return;
            }

            // 2. Laser distance click on interactive objects (Right Trigger)
            if (trigRightDown && _rightHand != null)
            {
                Ray ray = new Ray(_rightHand.transform.position, _rightHand.transform.forward);
                if (Physics.Raycast(ray, out RaycastHit hit, maxLaserDist))
                {
                    // VR Board button click
                    var board = hit.collider.GetComponentInParent<VRTrainingBoard>();
                    if (board != null && hud != null && hud.IsInModalState)
                    {
                        hud.OnVRTriggerPressed();
                        Haptic(_right, 0.60f, 0.12f);
                        return;
                    }

                    // Extinguisher grab
                    var ext = hit.collider.GetComponentInParent<FireExtinguisher>();
                    if (ext != null && !ext.IsHeld && _heldExt == null)
                    {
                        EquipExtinguisher(ext);
                        Haptic(_right, 0.70f, 0.15f);
                        return;
                    }

                    // Fire Alarm toggle
                    var alarm = hit.collider.GetComponentInParent<FireAlarmController>();
                    if (alarm != null)
                    {
                        alarm.ToggleAlarm();
                        Haptic(_right, 0.85f, 0.20f);
                        return;
                    }

                    // Power Switch toggle
                    var psw = hit.collider.GetComponentInParent<PowerSwitchController>();
                    if (psw != null)
                    {
                        psw.TogglePower();
                        Haptic(_right, 0.85f, 0.20f);
                        return;
                    }
                }
            }

            // 3. Drop Extinguisher with [B] or [Y]
            if ((secRightDown || secLeftDown) && _heldExt != null)
            {
                if (_discharging) { _heldExt.ReleaseHandle(); _discharging = false; }
                _heldExt.Drop();
                _heldExt = null;
                Haptic(_right, 0.40f, 0.10f);
            }
        }

        // ─── Direct Physical Hand Proximity Touch & Grab ───────────────────────

        private void HandleDirectHandProximity()
        {
            if (_leftHand != null)  CheckHandTouch(_leftHand.transform, _left, true);
            if (_rightHand != null) CheckHandTouch(_rightHand.transform, _right, false);
        }

        private void CheckHandTouch(Transform handTransform, InputDevice device, bool isLeft)
        {
            float trigVal = 0f; if (device.isValid) device.TryGetFeatureValue(CommonUsages.trigger, out trigVal);
            float gripVal = 0f; if (device.isValid) device.TryGetFeatureValue(CommonUsages.grip, out gripVal);
            bool handAction = trigVal > 0.45f || gripVal > 0.45f;

            Collider[] hits = Physics.OverlapSphere(handTransform.position, 0.35f);
            foreach (var col in hits)
            {
                // Fire Alarm Physical Touch
                var alarm = col.GetComponentInParent<FireAlarmController>();
                if (alarm != null && Time.time - _lastAlarmTouchTime > 1.0f)
                {
                    float dist = Vector3.Distance(handTransform.position, col.ClosestPoint(handTransform.position));
                    if (dist < 0.20f || (dist < 0.35f && handAction))
                    {
                        alarm.ToggleAlarm();
                        _lastAlarmTouchTime = Time.time;
                        Haptic(device, 0.95f, 0.25f);
                        Debug.Log($"[VRControllerManager] {(isLeft ? "Left" : "Right")} hand activated Fire Alarm!");
                        return;
                    }
                }

                // Power Switch Physical Touch
                var psw = col.GetComponentInParent<PowerSwitchController>();
                if (psw != null && Time.time - _lastPowerTouchTime > 1.0f)
                {
                    float dist = Vector3.Distance(handTransform.position, col.ClosestPoint(handTransform.position));
                    if (dist < 0.20f || (dist < 0.35f && handAction))
                    {
                        psw.TogglePower();
                        _lastPowerTouchTime = Time.time;
                        Haptic(device, 0.95f, 0.25f);
                        Debug.Log($"[VRControllerManager] {(isLeft ? "Left" : "Right")} hand toggled Power Breaker!");
                        return;
                    }
                }

                // Fire Extinguisher Direct Grab
                if (_heldExt == null && handAction)
                {
                    var ext = col.GetComponentInParent<FireExtinguisher>();
                    if (ext != null && !ext.IsHeld)
                    {
                        EquipExtinguisher(ext);
                        Haptic(device, 0.75f, 0.18f);
                        Debug.Log($"[VRControllerManager] Hand grabbed {ext.DisplayName}!");
                        return;
                    }
                }
            }
        }

        // ─── Extinguisher Operation ────────────────────────────────────────────

        public void EquipExtinguisher(FireExtinguisher ext)
        {
            if (_heldExt != null && _heldExt != ext) _heldExt.Drop();
            _heldExt = ext;
            ext.Pickup(_rightHand != null ? _rightHand.transform : transform);

            var hud = FindObjectOfType<TrainingHUD>();
            if (hud != null) hud.OnExtinguisherSelected(ext.Type);
            Debug.Log($"[VRControllerManager] Equipped {ext.DisplayName}");
        }

        private void HandleExtinguisherControl()
        {
            if (_heldExt == null) return;

            // 1. Pull Pin:
            // Squeeze [A] on Right or [X] on Left, OR reach left hand near pin and squeeze grip/trigger!
            bool pinBtn = false;
            if (_right.isValid) _right.TryGetFeatureValue(CommonUsages.primaryButton, out pinBtn);
            if (!pinBtn && _left.isValid) _left.TryGetFeatureValue(CommonUsages.primaryButton, out pinBtn);

            bool leftHandNearPin = false;
            if (_leftHand != null)
            {
                float distToExt = Vector3.Distance(_leftHand.transform.position, _heldExt.transform.position);
                if (distToExt < 0.32f)
                {
                    float lTrig = 0f; if (_left.isValid) _left.TryGetFeatureValue(CommonUsages.trigger, out lTrig);
                    float lGrip = 0f; if (_left.isValid) _left.TryGetFeatureValue(CommonUsages.grip, out lGrip);
                    if (lTrig > 0.40f || lGrip > 0.40f) leftHandNearPin = true;
                }
            }

            if ((pinBtn || leftHandNearPin) && !_wasPin)
            {
                _heldExt.PullSafetyPin();
                Haptic(_right, 0.70f, 0.18f);
                Haptic(_left,  0.70f, 0.18f);
            }
            _wasPin = pinBtn || leftHandNearPin;

            // 2. Spray CO2: Right index trigger
            float trig = 0f;
            if (_right.isValid) _right.TryGetFeatureValue(CommonUsages.trigger, out trig);

            if (trig > 0.25f)
            {
                if (!_discharging) { _heldExt.SqueezeHandle(); _discharging = true; }
                Haptic(_right, Mathf.Lerp(0.35f, 0.90f, trig), 0.08f);
            }
            else if (_discharging)
            {
                _heldExt.ReleaseHandle();
                _discharging = false;
            }
        }

        // ─── Haptics ───────────────────────────────────────────────────────────

        private void Haptic(InputDevice dev, float amplitude, float duration)
        {
            if (dev.isValid) dev.SendHapticImpulse(0, amplitude, duration);
        }
    }
}
