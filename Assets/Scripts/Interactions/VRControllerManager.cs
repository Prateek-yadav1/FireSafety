using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using FireSafety.Extinguishers;
using FireSafety.Fire;
using FireSafety.Training;

namespace FireSafety.Interactions
{
    /// <summary>
    /// VRControllerManager — handles Meta Quest 6-DoF tracking, controller input,
    /// laser pointer, natural hand proximity touch & grab, and snap-turning.
    ///
    /// Hands are parented to the XR Rig Root so their local coordinates correspond
    /// directly to the physical room in both Room 01 (Orientation Studio) and Room 02 (Kitchen).
    /// </summary>
    public class VRControllerManager : MonoBehaviour
    {
        [Header("Locomotion")]
        [SerializeField] private float vrMoveSpeed   = 2.6f;
        [SerializeField] private float snapTurnAngle = 45f;
        [SerializeField] private float turnCooldown  = 0.30f;

        [Header("Laser")]
        [SerializeField] private float maxLaserDist = 8f;
        [SerializeField] private Color laserNormal  = new Color(0.2f, 0.8f, 1f, 0.65f);
        [SerializeField] private Color laserHover   = new Color(0.2f, 1f,   0.4f, 0.90f);

        [Header("Hold Pose")]
        [SerializeField] private Vector3 holdOffset   = new Vector3(0.02f, -0.22f, 0.10f);
        [SerializeField] private Vector3 holdRotation = new Vector3(15f, 0f, 0f);

        // Room 01 bounds
        private const float Room01MinX = -3.2f;
        private const float Room01MaxX =  3.2f;
        private const float Room01MinZ = -1.2f;
        private const float Room01MaxZ =  4.8f;

        // Kitchen bounds
        private const float KitchenMinX = 199.5f;
        private const float KitchenMaxX = 205.2f;
        private const float KitchenMinZ = -3.4f;
        private const float KitchenMaxZ =  3.4f;

        // XR devices
        private InputDevice _left, _right, _hmd;
        private bool _leftOk, _rightOk, _hmdOk;

        // Visual hands
        private GameObject _leftHand, _rightHand;
        private LineRenderer _rightLaser;
        private GameObject  _rightDot;

        // Interaction state
        private FireExtinguisher _heldExt = null;
        private float _lastTurn  = 0f;
        private bool  _wasTrigRight = false;
        private bool  _wasTrigLeft  = false;
        private bool  _wasPrimaryRight = false;
        private bool  _wasPrimaryLeft  = false;
        private bool  _wasPin    = false;
        private bool  _wasDrop   = false;
        private bool  _discharging = false;

        private float _lastAlarmInteractTime = 0f;
        private float _lastPowerInteractTime = 0f;

        public bool IsVRActive => _hmdOk || XRSettings.isDeviceActive;

        // ─── Unity Messages ────────────────────────────────────────────────────

        private void Start()
        {
            TryFindDevices();
            if (IsVRActive)
            {
                BuildHandRigs();
                Debug.Log("[VRControllerManager] VR mode active. Hand rigs built.");
            }
            else
            {
                Debug.Log("[VRControllerManager] No HMD detected — desktop mode active.");
            }
        }

        private void Update()
        {
            TryFindDevices();
            if (!IsVRActive) return;

            // Ensure hands are built once VR becomes active
            if (_leftHand == null || _rightHand == null)
            {
                BuildHandRigs();
            }

            HandleVRLocomotion();
            UpdateHandPoses();
            UpdateLaser();
            HandleModalAndLaserInteraction();
            HandleDirectHandProximity();
            HandleExtinguisherControl();
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

        // ─── Locomotion ────────────────────────────────────────────────────────

        private void HandleVRLocomotion()
        {
            Transform xrRoot = GetXRRoot();
            if (xrRoot == null) return;

            bool inKitchen = xrRoot.position.x > 100f;

            // ── Walk with left thumbstick ──
            Vector2 leftStick = Vector2.zero;
            if (_left.isValid) _left.TryGetFeatureValue(CommonUsages.primary2DAxis, out leftStick);

            if (leftStick.magnitude > 0.15f)
            {
                Camera cam = Camera.main;
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

                xrRoot.position = np;
            }

            // ── Snap-turn with right thumbstick ──
            Vector2 rightStick = Vector2.zero;
            if (_right.isValid) _right.TryGetFeatureValue(CommonUsages.primary2DAxis, out rightStick);

            if (Time.time - _lastTurn > turnCooldown && Mathf.Abs(rightStick.x) > 0.6f)
            {
                float angle = Mathf.Sign(rightStick.x) * snapTurnAngle;
                xrRoot.Rotate(0f, angle, 0f, Space.World);
                _lastTurn = Time.time;
                Haptic(_right, 0.35f, 0.1f);
            }
        }

        // ─── Find XR Rig Root ──────────────────────────────────────────────────

        private Transform GetXRRoot()
        {
            Camera cam = Camera.main;
            if (cam == null) cam = GetComponent<Camera>();
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

                // Right hand: laser pointer
                _rightLaser = _rightHand.AddComponent<LineRenderer>();
                _rightLaser.material      = new Material(Shader.Find("Sprites/Default"));
                _rightLaser.startWidth    = 0.005f;
                _rightLaser.endWidth      = 0.001f;
                _rightLaser.positionCount = 2;
                _rightLaser.startColor    = laserNormal;
                _rightLaser.endColor      = new Color(laserNormal.r, laserNormal.g, laserNormal.b, 0f);
                _rightLaser.useWorldSpace = true;

                // Laser dot
                _rightDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _rightDot.name = "LaserDot";
                Destroy(_rightDot.GetComponent<Collider>());
                _rightDot.transform.localScale = Vector3.one * 0.03f;
                var dotMat = _rightDot.GetComponent<Renderer>().material;
                dotMat.color = laserHover;
                dotMat.EnableKeyword("_EMISSION");
                dotMat.SetColor("_EmissionColor", laserHover * 2f);
            }
        }

        private GameObject BuildGlove(string name, bool isLeft, Transform parent)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);

            // Palm cube
            var palm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            palm.name = "Palm";
            palm.transform.SetParent(go.transform, false);
            palm.transform.localScale = new Vector3(0.085f, 0.035f, 0.11f);
            Destroy(palm.GetComponent<Collider>());

            // Finger cylinder
            var finger = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            finger.name = "Finger";
            finger.transform.SetParent(go.transform, false);
            finger.transform.localPosition = new Vector3(isLeft ? -0.015f : 0.015f, 0f, 0.06f);
            finger.transform.localScale    = new Vector3(0.014f, 0.035f, 0.014f);
            finger.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Destroy(finger.GetComponent<Collider>());

            // Glove material (URP Lit / Standard)
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = isLeft ? new Color(0.18f, 0.22f, 0.28f) : new Color(0.15f, 0.17f, 0.20f);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;

            return go;
        }

        private void UpdateHandPoses()
        {
            Transform xrRoot = GetXRRoot();

            // Keep hands parented to xrRoot
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
                        // Transform hold offset from right hand
                        _heldExt.transform.position = _rightHand.transform.TransformPoint(holdOffset);
                        _heldExt.transform.rotation = _rightHand.transform.rotation * Quaternion.Euler(holdRotation);
                    }
                }
            }
        }

        // ─── Laser Pointer ─────────────────────────────────────────────────────

        private void UpdateLaser()
        {
            if (_rightHand == null || _rightLaser == null) return;

            Ray ray = new Ray(_rightHand.transform.position, _rightHand.transform.forward);
            Vector3 endPos;
            Color col = laserNormal;

            if (Physics.Raycast(ray, out RaycastHit hit, maxLaserDist))
            {
                endPos = hit.point;
                bool hovering = hit.collider.GetComponentInParent<FireExtinguisher>() != null ||
                                hit.collider.GetComponentInParent<FireAlarmController>() != null ||
                                hit.collider.GetComponentInParent<PowerSwitchController>() != null;
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

        // ─── Modal HUD & Laser Distance Interaction ────────────────────────────

        private void HandleModalAndLaserInteraction()
        {
            // Read controller buttons
            float trigRightVal = 0f; if (_right.isValid) _right.TryGetFeatureValue(CommonUsages.trigger, out trigRightVal);
            float trigLeftVal  = 0f; if (_left.isValid)  _left.TryGetFeatureValue(CommonUsages.trigger, out trigLeftVal);

            bool trigRightDown = (trigRightVal > 0.5f) && !_wasTrigRight;
            bool trigLeftDown  = (trigLeftVal  > 0.5f) && !_wasTrigLeft;
            _wasTrigRight = trigRightVal > 0.5f;
            _wasTrigLeft  = trigLeftVal  > 0.5f;

            bool primaryRight = false; if (_right.isValid) _right.TryGetFeatureValue(CommonUsages.primaryButton, out primaryRight);
            bool primaryLeft  = false; if (_left.isValid)  _left.TryGetFeatureValue(CommonUsages.primaryButton, out primaryLeft);
            bool primaryRightDown = primaryRight && !_wasPrimaryRight;
            bool primaryLeftDown  = primaryLeft  && !_wasPrimaryLeft;
            _wasPrimaryRight = primaryRight;
            _wasPrimaryLeft  = primaryLeft;

            bool anyActionDown = trigRightDown || trigLeftDown || primaryRightDown || primaryLeftDown;

            // 1. Advance HUD Modal (if a modal is currently up)
            var hud = FindObjectOfType<TrainingHUD>();
            if (hud != null && hud.IsInModalState)
            {
                if (anyActionDown)
                {
                    hud.OnVRTriggerPressed();
                    Haptic(_right, 0.6f, 0.12f);
                    Haptic(_left, 0.6f, 0.12f);
                    return;
                }
            }

            // 2. Laser distance raycast interaction (triggered via right trigger)
            if (trigRightDown && _rightHand != null)
            {
                Ray ray = new Ray(_rightHand.transform.position, _rightHand.transform.forward);
                if (Physics.Raycast(ray, out RaycastHit hit, maxLaserDist))
                {
                    var ext = hit.collider.GetComponentInParent<FireExtinguisher>();
                    if (ext != null && !ext.IsHeld && _heldExt == null)
                    {
                        EquipExtinguisher(ext);
                        Haptic(_right, 0.7f, 0.15f);
                        return;
                    }

                    var alarm = hit.collider.GetComponentInParent<FireAlarmController>();
                    if (alarm != null)
                    {
                        alarm.ToggleAlarm();
                        Haptic(_right, 0.85f, 0.2f);
                        return;
                    }

                    var psw = hit.collider.GetComponentInParent<PowerSwitchController>();
                    if (psw != null)
                    {
                        psw.TogglePower();
                        Haptic(_right, 0.85f, 0.2f);
                        return;
                    }
                }
            }
        }

        // ─── Direct Hand Proximity Touch & Grab ────────────────────────────────

        private void HandleDirectHandProximity()
        {
            if (_rightHand != null) CheckHandProximity(_rightHand.transform, _right, false);
            if (_leftHand != null)  CheckHandProximity(_leftHand.transform,  _left,  true);
        }

        private void CheckHandProximity(Transform handTransform, InputDevice device, bool isLeft)
        {
            // Read hand grip and trigger inputs
            float grip = 0f;
            if (device.isValid) device.TryGetFeatureValue(CommonUsages.grip, out grip);
            float trig = 0f;
            if (device.isValid) device.TryGetFeatureValue(CommonUsages.trigger, out trig);

            bool handAction = (grip > 0.4f) || (trig > 0.4f);

            // Proximity sphere query (radius 0.35m around the hand)
            Collider[] hits = Physics.OverlapSphere(handTransform.position, 0.35f);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i];
                if (col == null) continue;

                // ── A. Fire Alarm ──
                var alarm = col.GetComponentInParent<FireAlarmController>();
                if (alarm != null && Time.time - _lastAlarmInteractTime > 0.8f)
                {
                    float dist = Vector3.Distance(handTransform.position, col.ClosestPoint(handTransform.position));
                    if (dist < 0.15f || (dist < 0.35f && handAction))
                    {
                        alarm.ToggleAlarm();
                        _lastAlarmInteractTime = Time.time;
                        Haptic(device, 0.9f, 0.22f);
                        Debug.Log($"[VRControllerManager] {(isLeft ? "Left" : "Right")} hand activated Fire Alarm!");
                        return;
                    }
                }

                // ── B. Main Power / Light Switch ──
                var psw = col.GetComponentInParent<PowerSwitchController>();
                if (psw != null && Time.time - _lastPowerInteractTime > 0.8f)
                {
                    float dist = Vector3.Distance(handTransform.position, col.ClosestPoint(handTransform.position));
                    if (dist < 0.15f || (dist < 0.35f && handAction))
                    {
                        psw.TogglePower();
                        _lastPowerInteractTime = Time.time;
                        Haptic(device, 0.9f, 0.22f);
                        Debug.Log($"[VRControllerManager] {(isLeft ? "Left" : "Right")} hand toggled Power Switch!");
                        return;
                    }
                }

                // ── C. Fire Extinguisher Direct Grab ──
                if (_heldExt == null && handAction)
                {
                    var ext = col.GetComponentInParent<FireExtinguisher>();
                    if (ext != null && !ext.IsHeld)
                    {
                        EquipExtinguisher(ext);
                        Haptic(device, 0.75f, 0.18f);
                        Debug.Log($"[VRControllerManager] Direct hand grab equipped {ext.DisplayName}!");
                        return;
                    }
                }
            }
        }

        // ─── Extinguisher Control ──────────────────────────────────────────────

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
            // Either by pressing Primary button ([A] on right or [X] on left),
            // OR by reaching free left hand near the extinguisher and squeezing trigger/grip!
            bool pinBtn = false;
            if (_right.isValid) _right.TryGetFeatureValue(CommonUsages.primaryButton, out pinBtn);
            if (!pinBtn && _left.isValid) _left.TryGetFeatureValue(CommonUsages.primaryButton, out pinBtn);

            bool leftHandNearPin = false;
            if (_leftHand != null)
            {
                float distToExt = Vector3.Distance(_leftHand.transform.position, _heldExt.transform.position);
                if (distToExt < 0.30f)
                {
                    float lTrig = 0f; if (_left.isValid) _left.TryGetFeatureValue(CommonUsages.trigger, out lTrig);
                    float lGrip = 0f; if (_left.isValid) _left.TryGetFeatureValue(CommonUsages.grip, out lGrip);
                    if (lTrig > 0.4f || lGrip > 0.4f) leftHandNearPin = true;
                }
            }

            if ((pinBtn || leftHandNearPin) && !_wasPin)
            {
                _heldExt.PullSafetyPin();
                Haptic(_right, 0.65f, 0.15f);
                Haptic(_left, 0.65f, 0.15f);
            }
            _wasPin = pinBtn || leftHandNearPin;

            // 2. Squeeze Handle & Spray: right trigger
            float trig = 0f;
            if (_right.isValid) _right.TryGetFeatureValue(CommonUsages.trigger, out trig);
            if (Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0)) trig = 1f; // desktop fallback

            if (trig > 0.2f)
            {
                if (!_discharging) { _heldExt.SqueezeHandle(); _discharging = true; }
                Haptic(_right, Mathf.Lerp(0.3f, 0.8f, trig), 0.08f);
            }
            else if (_discharging)
            {
                _heldExt.ReleaseHandle();
                _discharging = false;
            }

            // 3. Drop Extinguisher: secondary button ([B] on right / [Y] on left)
            bool dropBtn = false;
            if (_right.isValid) _right.TryGetFeatureValue(CommonUsages.secondaryButton, out dropBtn);
            if (!dropBtn && _left.isValid) _left.TryGetFeatureValue(CommonUsages.secondaryButton, out dropBtn);

            if (dropBtn && !_wasDrop)
            {
                if (_discharging) { _heldExt.ReleaseHandle(); _discharging = false; }
                _heldExt.Drop();
                _heldExt = null;
                Haptic(_right, 0.4f, 0.1f);
            }
            _wasDrop = dropBtn;
        }

        // ─── Haptics ───────────────────────────────────────────────────────────

        private void Haptic(InputDevice dev, float amplitude, float duration)
        {
            if (dev.isValid) dev.SendHapticImpulse(0, amplitude, duration);
        }
    }
}
