using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using FireSafety.Extinguishers;
using FireSafety.Fire;
using FireSafety.Training;

namespace FireSafety.Interactions
{
    /// <summary>
    /// VRControllerManager — handles Meta Quest 6-DoF tracking, controller lasers,
    /// extinguisher pickup, and snap-turning.
    ///
    /// When no HMD is detected (desktop editor play), this component is a no-op
    /// so the KitchenPlayerController handles everything.
    /// </summary>
    public class VRControllerManager : MonoBehaviour
    {
        [Header("Locomotion")]
        [SerializeField] private float vrMoveSpeed  = 2.4f;
        [SerializeField] private float snapTurnAngle = 45f;
        [SerializeField] private float turnCooldown  = 0.30f;

        [Header("Laser")]
        [SerializeField] private float    maxLaserDist  = 8f;
        [SerializeField] private Color    laserNormal   = new Color(0.2f, 0.8f, 1f, 0.65f);
        [SerializeField] private Color    laserHover    = new Color(0.2f, 1f, 0.4f, 0.90f);

        [Header("Hold Pose")]
        [SerializeField] private Vector3 holdOffset   = new Vector3(0.02f, -0.22f, 0.10f);
        [SerializeField] private Vector3 holdRotation = new Vector3(15f, 0f, 0f);

        // Kitchen bounds
        private const float MinX = 199.5f;
        private const float MaxX = 205.2f;
        private const float MinZ = -3.4f;
        private const float MaxZ =  3.4f;

        // XR devices
        private InputDevice _left, _right, _hmd;
        private bool _leftOk, _rightOk, _hmdOk;

        // Visual hands
        private GameObject _leftHand, _rightHand;
        private LineRenderer _rightLaser;
        private GameObject  _rightDot;

        // Interaction state
        private FireExtinguisher _heldExt = null;
        private float _lastTurn = 0f;
        private bool  _wasTrig  = false;
        private bool  _wasPin   = false;
        private bool  _wasDrop  = false;
        private bool  _discharging = false;

        public bool IsVRActive => _hmdOk || XRSettings.isDeviceActive;

        // ─── Unity Messages ───────────────────────────────────────────────────

        private void Start()
        {
            TryFindDevices();
            if (IsVRActive)
            {
                BuildHandRigs();
                EnsureInsideKitchen();
                Debug.Log("[VRControllerManager] VR mode active.");
            }
            else
            {
                Debug.Log("[VRControllerManager] No HMD detected — desktop mode.");
            }
        }

        private void Update()
        {
            TryFindDevices();

            if (!IsVRActive) return;    // let KitchenPlayerController handle everything

            HandleVRLocomotion();
            UpdateHandPoses();
            UpdateLaser();
            HandleVRInteraction();
            HandleExtinguisherControl();
        }

        // ─── Device Discovery ─────────────────────────────────────────────────

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

        // ─── Position Fix ─────────────────────────────────────────────────────

        /// <summary>
        /// When XR is active, the HMD tracking overrides the camera's world position.
        /// We counteract this by moving the parent transform so the camera ends up
        /// inside the kitchen. This runs once at Start.
        /// </summary>
        private void EnsureInsideKitchen()
        {
            Transform rig = transform.parent != null ? transform.parent : transform;
            if (rig.position.x < 100f)
            {
                rig.position = new Vector3(202.8f, 0f, 0f);
                rig.rotation = Quaternion.Euler(0f, -90f, 0f);
            }
        }

        private void LateUpdate()
        {
            if (IsVRActive)
            {
                Transform rig = transform.parent != null ? transform.parent : transform;
                if (rig.position.x < 100f)
                {
                    rig.position = new Vector3(202.8f, 0f, 0f);
                    rig.rotation = Quaternion.Euler(0f, -90f, 0f);
                }
            }
        }

        // ─── Locomotion ───────────────────────────────────────────────────────

        private void HandleVRLocomotion()
        {
            // ── Walk with left thumbstick ──
            Vector2 leftStick = Vector2.zero;
            if (_left.isValid) _left.TryGetFeatureValue(CommonUsages.primary2DAxis, out leftStick);

            if (leftStick.magnitude > 0.15f)
            {
                Vector3 fwd   = transform.forward; fwd.y = 0f; fwd.Normalize();
                Vector3 right = transform.right;   right.y = 0f; right.Normalize();
                Vector3 move  = (fwd * leftStick.y + right * leftStick.x) * vrMoveSpeed * Time.deltaTime;

                Vector3 np = transform.position + move;
                np.x = Mathf.Clamp(np.x, MinX, MaxX);
                np.z = Mathf.Clamp(np.z, MinZ, MaxZ);
                transform.position = np;
            }

            // ── Snap-turn with right thumbstick ──
            Vector2 rightStick = Vector2.zero;
            if (_right.isValid) _right.TryGetFeatureValue(CommonUsages.primary2DAxis, out rightStick);

            if (Time.time - _lastTurn > turnCooldown && Mathf.Abs(rightStick.x) > 0.6f)
            {
                float angle = Mathf.Sign(rightStick.x) * snapTurnAngle;
                transform.Rotate(0f, angle, 0f, Space.World);
                _lastTurn = Time.time;
                Haptic(_right, 0.3f, 0.1f);
            }
        }

        // ─── Hand Rigs ────────────────────────────────────────────────────────

        private void BuildHandRigs()
        {
            _leftHand  = BuildGlove("VR_LeftHand",  true);
            _rightHand = BuildGlove("VR_RightHand", false);

            // Right-hand laser pointer
            _rightLaser = _rightHand.AddComponent<LineRenderer>();
            _rightLaser.positionCount = 2;
            _rightLaser.startWidth    = 0.003f;
            _rightLaser.endWidth      = 0.001f;
            _rightLaser.material      = new Material(Shader.Find("Sprites/Default"));
            _rightLaser.startColor    = laserNormal;
            _rightLaser.endColor      = new Color(laserNormal.r, laserNormal.g, laserNormal.b, 0f);

            _rightDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _rightDot.name = "LaserDot";
            Destroy(_rightDot.GetComponent<Collider>());
            _rightDot.transform.localScale = Vector3.one * 0.015f;
            var dotMat = _rightDot.GetComponent<Renderer>().material;
            dotMat.color = laserHover;
        }

        private GameObject BuildGlove(string name, bool isLeft)
        {
            var root = new GameObject(name);
            root.transform.SetParent(transform, false);

            var palm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            palm.name = "Palm";
            Destroy(palm.GetComponent<Collider>());
            palm.transform.SetParent(root.transform, false);
            palm.transform.localScale    = new Vector3(0.08f, 0.025f, 0.11f);
            palm.transform.localPosition = Vector3.zero;
            var mat = palm.GetComponent<Renderer>().material;
            mat.color = new Color(0.18f, 0.22f, 0.28f);

            return root;
        }

        private void UpdateHandPoses()
        {
            UpdateHand(_leftHand,  _left,  true);
            UpdateHand(_rightHand, _right, false);
        }

        private void UpdateHand(GameObject hand, InputDevice dev, bool isLeft)
        {
            if (hand == null || !dev.isValid) return;

            Vector3    pos;
            Quaternion rot;
            if (dev.TryGetFeatureValue(CommonUsages.devicePosition, out pos) &&
                dev.TryGetFeatureValue(CommonUsages.deviceRotation, out rot))
            {
                hand.transform.position = pos;
                hand.transform.rotation = rot;
            }
        }

        // ─── Laser ────────────────────────────────────────────────────────────

        private void UpdateLaser()
        {
            if (_rightLaser == null || _rightHand == null) return;

            Ray ray = new Ray(_rightHand.transform.position, _rightHand.transform.forward);
            Vector3 endPos;
            Color col = laserNormal;

            if (Physics.Raycast(ray, out RaycastHit hit, maxLaserDist))
            {
                endPos = hit.point;
                bool hovering = hit.collider.GetComponentInParent<FireExtinguisher>()  != null
                             || hit.collider.GetComponentInParent<FireAlarmController>() != null
                             || hit.collider.GetComponentInParent<PowerSwitchController>() != null;
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

        // ─── Trigger Interaction ──────────────────────────────────────────────

        private void HandleVRInteraction()
        {
            if (_rightHand == null) return;

            float trig = 0f;
            if (_right.isValid) _right.TryGetFeatureValue(CommonUsages.trigger, out trig);
            bool trigDown = (trig > 0.5f) && !_wasTrig;
            _wasTrig = trig > 0.5f;

            if (!trigDown) return;

            Ray ray = new Ray(_rightHand.transform.position, _rightHand.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, maxLaserDist)) return;

            // 1. Extinguisher pickup
            var ext = hit.collider.GetComponentInParent<FireExtinguisher>();
            if (ext != null && !ext.IsHeld)
            {
                EquipExtinguisher(ext);
                Haptic(_right, 0.7f, 0.15f);
                return;
            }

            // 2. Fire alarm
            var alarm = hit.collider.GetComponentInParent<FireAlarmController>();
            if (alarm != null)
            {
                alarm.ToggleAlarm();
                Haptic(_right, 0.85f, 0.2f);
                return;
            }

            // 3. Power switch
            var psw = hit.collider.GetComponentInParent<PowerSwitchController>();
            if (psw != null)
            {
                psw.TogglePower();
                Haptic(_right, 0.85f, 0.2f);
                return;
            }
        }

        // ─── Extinguisher Control ─────────────────────────────────────────────

        public void EquipExtinguisher(FireExtinguisher ext)
        {
            if (_heldExt != null && _heldExt != ext) _heldExt.Drop();
            _heldExt = ext;

            if (_rightHand != null)
                ext.Pickup(_rightHand.transform);
            else
                ext.Pickup(transform);

            var hud = FindObjectOfType<TrainingHUD>();
            if (hud != null) hud.OnExtinguisherSelected(ext.Type);

            Debug.Log($"[VRControllerManager] Equipped {ext.DisplayName}");
        }

        private void HandleExtinguisherControl()
        {
            if (_heldExt == null) return;

            // Pull pin — [A] button
            bool pinBtn = false;
            if (_right.isValid) _right.TryGetFeatureValue(CommonUsages.primaryButton, out pinBtn);
            if (pinBtn && !_wasPin)
            {
                _heldExt.PullSafetyPin();
                Haptic(_right, 0.6f, 0.12f);
            }
            _wasPin = pinBtn;

            // Squeeze — right trigger
            float trig = 0f;
            if (_right.isValid) _right.TryGetFeatureValue(CommonUsages.trigger, out trig);

            // Desktop fallback while holding
            if (Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0)) trig = 1f;

            if (trig > 0.2f)
            {
                if (!_discharging) { _heldExt.SqueezeHandle(); _discharging = true; }
                Haptic(_right, Mathf.Lerp(0.25f, 0.65f, trig), 0.08f);
            }
            else if (_discharging)
            {
                _heldExt.ReleaseHandle();
                _discharging = false;
            }

            // Drop — [B] button
            bool dropBtn = false;
            if (_right.isValid) _right.TryGetFeatureValue(CommonUsages.secondaryButton, out dropBtn);
            if (dropBtn && !_wasDrop)
            {
                if (_discharging) { _heldExt.ReleaseHandle(); _discharging = false; }
                _heldExt.Drop();
                _heldExt = null;
                Haptic(_right, 0.4f, 0.1f);
            }
            _wasDrop = dropBtn;
        }

        // ─── Haptics ──────────────────────────────────────────────────────────

        private void Haptic(InputDevice dev, float amplitude, float duration)
        {
            if (dev.isValid) dev.SendHapticImpulse(0, amplitude, duration);
        }
    }
}
