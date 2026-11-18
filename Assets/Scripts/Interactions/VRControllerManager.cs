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
    /// laser pointer, extinguisher pickup, and snap-turning.
    ///
    /// When no HMD is detected (desktop editor play), this component is a no-op
    /// so the KitchenPlayerController handles everything.
    ///
    /// VR position anchoring is handled by VRCameraAnchor via Application.onBeforeRender.
    /// </summary>
    public class VRControllerManager : MonoBehaviour
    {
        [Header("Locomotion")]
        [SerializeField] private float vrMoveSpeed   = 2.4f;
        [SerializeField] private float snapTurnAngle = 45f;
        [SerializeField] private float turnCooldown  = 0.30f;

        [Header("Laser")]
        [SerializeField] private float maxLaserDist = 8f;
        [SerializeField] private Color laserNormal  = new Color(0.2f, 0.8f, 1f, 0.65f);
        [SerializeField] private Color laserHover   = new Color(0.2f, 1f,   0.4f, 0.90f);

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
        private float _lastTurn  = 0f;
        private bool  _wasTrig   = false;
        private bool  _wasPin    = false;
        private bool  _wasDrop   = false;
        private bool  _discharging = false;

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
                Debug.Log("[VRControllerManager] No HMD detected — desktop mode, all VR input skipped.");
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
            // VR position is primarily managed by VRCameraAnchor.
            // For fine locomotion within the kitchen, we move the rig root.
            Transform xrRoot = GetXRRoot();
            if (xrRoot == null) return;

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
                np.x = Mathf.Clamp(np.x, MinX, MaxX);
                np.z = Mathf.Clamp(np.z, MinZ, MaxZ);
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
                Haptic(_right, 0.3f, 0.1f);
            }
        }

        // ─── Find XR Rig Root ──────────────────────────────────────────────────

        private Transform GetXRRoot()
        {
            // VRCameraAnchor found the root — reuse its logic
            Camera cam = Camera.main;
            if (cam == null) return null;

            Transform t = cam.transform;
            while (t.parent != null) t = t.parent;
            return t;
        }

        // ─── Hand Rigs ─────────────────────────────────────────────────────────

        private void BuildHandRigs()
        {
            _leftHand  = BuildGlove("VR_LeftHand",  true);
            _rightHand = BuildGlove("VR_RightHand", false);

            // Right hand: laser pointer
            _rightLaser = _rightHand.AddComponent<LineRenderer>();
            _rightLaser.material     = new Material(Shader.Find("Sprites/Default"));
            _rightLaser.startWidth   = 0.004f;
            _rightLaser.endWidth     = 0.001f;
            _rightLaser.positionCount = 2;
            _rightLaser.startColor   = laserNormal;
            _rightLaser.endColor     = new Color(laserNormal.r, laserNormal.g, laserNormal.b, 0f);
            _rightLaser.useWorldSpace = true;

            // Laser dot
            _rightDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _rightDot.name = "LaserDot";
            Destroy(_rightDot.GetComponent<Collider>());
            _rightDot.transform.localScale = Vector3.one * 0.025f;
            var dotMat = _rightDot.GetComponent<Renderer>().material;
            dotMat.color = laserHover;
            dotMat.EnableKeyword("_EMISSION");
            dotMat.SetColor("_EmissionColor", laserHover * 2f);
        }

        private GameObject BuildGlove(string name, bool isLeft)
        {
            var go = new GameObject(name);

            // Palm cube
            var palm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            palm.name = "Palm";
            palm.transform.SetParent(go.transform, false);
            palm.transform.localScale = new Vector3(0.08f, 0.025f, 0.10f);
            Destroy(palm.GetComponent<Collider>());

            // Finger cylinder
            var finger = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            finger.name = "Finger";
            finger.transform.SetParent(go.transform, false);
            finger.transform.localPosition = new Vector3(isLeft ? -0.015f : 0.015f, 0f, 0.05f);
            finger.transform.localScale    = new Vector3(0.012f, 0.03f, 0.012f);
            finger.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Destroy(finger.GetComponent<Collider>());

            // Dark grey glove material
            var mat = new Material(Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.12f, 0.12f, 0.14f);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;

            return go;
        }

        private void UpdateHandPoses()
        {
            if (_leftHand != null && _left.isValid)
            {
                Vector3 lPos; Quaternion lRot;
                if (_left.TryGetFeatureValue(CommonUsages.devicePosition, out lPos) &&
                    _left.TryGetFeatureValue(CommonUsages.deviceRotation, out lRot))
                {
                    _leftHand.transform.position = lPos;
                    _leftHand.transform.rotation = lRot;
                }
            }

            if (_rightHand != null && _right.isValid)
            {
                Vector3 rPos; Quaternion rRot;
                if (_right.TryGetFeatureValue(CommonUsages.devicePosition, out rPos) &&
                    _right.TryGetFeatureValue(CommonUsages.deviceRotation, out rRot))
                {
                    if (_heldExt != null)
                    {
                        // Offset for natural hold position
                        Vector3 offset = _rightHand.transform.TransformDirection(holdOffset);
                        _heldExt.transform.position = rPos + offset;
                        _heldExt.transform.rotation = rRot * Quaternion.Euler(holdRotation);
                    }
                    _rightHand.transform.position = rPos;
                    _rightHand.transform.rotation = rRot;
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

        // ─── Trigger Interaction ───────────────────────────────────────────────

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
            if (ext != null && !ext.IsHeld) { EquipExtinguisher(ext); Haptic(_right, 0.7f, 0.15f); return; }

            // 2. Fire alarm
            var alarm = hit.collider.GetComponentInParent<FireAlarmController>();
            if (alarm != null) { alarm.ToggleAlarm(); Haptic(_right, 0.85f, 0.2f); return; }

            // 3. Power switch
            var psw = hit.collider.GetComponentInParent<PowerSwitchController>();
            if (psw != null) { psw.TogglePower(); Haptic(_right, 0.85f, 0.2f); return; }

            // 4. Advance HUD modal via VR trigger
            var hud = FindObjectOfType<TrainingHUD>();
            if (hud != null) hud.OnVRTriggerPressed();
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

            // Pull pin — [A] button (primaryButton)
            bool pinBtn = false;
            if (_right.isValid) _right.TryGetFeatureValue(CommonUsages.primaryButton, out pinBtn);
            if (pinBtn && !_wasPin) { _heldExt.PullSafetyPin(); Haptic(_right, 0.6f, 0.12f); }
            _wasPin = pinBtn;

            // Squeeze — right trigger
            float trig = 0f;
            if (_right.isValid) _right.TryGetFeatureValue(CommonUsages.trigger, out trig);
            if (Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0)) trig = 1f; // desktop fallback

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

            // Drop — [B] button (secondaryButton)
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

        // ─── Haptics ───────────────────────────────────────────────────────────

        private void Haptic(InputDevice dev, float amplitude, float duration)
        {
            if (dev.isValid) dev.SendHapticImpulse(0, amplitude, duration);
        }
    }
}
