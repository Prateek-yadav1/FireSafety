using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FireSafety.Fire;
using FireSafety.Interactions;

namespace FireSafety.Extinguishers
{
    /// <summary>
    /// Core extinguisher interaction component. Implements standard P.A.S.S. protocol
    /// (Pull pin, Aim nozzle, Squeeze handle, Sweep base). Manages discharge physics,
    /// fire classification compatibility checking, and educational feedback.
    ///
    /// Manages proper first-person hand positioning and orientation when equipped,
    /// ensuring the nozzle points forward towards the crosshair and the fire.
    /// </summary>
    public class FireExtinguisher : MonoBehaviour
    {
        [Header("Classification & Identity")]
        [SerializeField] private ExtinguisherType extinguisherType = ExtinguisherType.WetChemical;
        [SerializeField] private FireClassification effectiveAgainst = FireClassification.ClassF_K | FireClassification.ClassA;
        [SerializeField] private string displayName = "Wet Chemical Extinguisher";
        [SerializeField] private string classificationCode = "Class F/K & Class A";
        [TextArea(2, 3)]
        [SerializeField] private string suitableDescription = "Specifically engineered for commercial and domestic cooking oil/fat fires (Class F/K). Uses saponification to form a soapy cooling blanket without splashing.";
        [TextArea(2, 3)]
        [SerializeField] private string hazardWarning = "DO NOT USE WATER on burning cooking oil! Water causes instantaneous steam explosions.";

        [Header("Components & Transforms")]
        [SerializeField] private Transform nozzleTip;
        [SerializeField] private Transform safetyPin;
        [SerializeField] private Transform handleLever;
        [SerializeField] private ExtinguisherDischargeEffect dischargeEffect;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip dischargeSound;
        [SerializeField] private AudioClip pinPullSound;
        [SerializeField] private Rigidbody rigidBody;
        [SerializeField] private Collider mainCollider;

        [Header("Capacity & Extinguishing Rates")]
        [SerializeField] private float agentCapacitySeconds = 20f;
        [SerializeField] private float extinguishPower = 0.14f; // Gradual realistic suppression (takes 7-8s of sustained sweep)

        [Header("Desktop Test Controls")]
        [SerializeField] private bool enableDesktopHotkeys = true;

        // Runtime state
        private bool isSafetyPinRemoved = false;
        private bool isHandleSqueezed = false;
        private bool isHeld = false;
        private float remainingAgent;
        private Transform originalParent;
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private FirstPersonExtinguisherHands handRig;
        private Transform vrHandHolder = null;

        public void SetVRHandHolder(Transform holder)
        {
            vrHandHolder = holder;
        }

        // Events
        public event Action OnSafetyPinPulled;
        public event Action OnDischargeStarted;
        public event Action OnDischargeStopped;
        public event Action<FireExtinguisher> OnPickedUp;
        public event Action<FireExtinguisher> OnDropped;
        public event Action<string> OnSafetyViolationAlert;
        public event Action<FireVFXController> OnFireExtinguishedSuccessfully;

        // Public accessors
        public ExtinguisherType Type => extinguisherType;
        public FireClassification EffectiveAgainst => effectiveAgainst;
        public string DisplayName => displayName;
        public string ClassificationCode => classificationCode;
        public string SuitableDescription => suitableDescription;
        public string HazardWarning => hazardWarning;
        public bool IsSafetyPinRemoved => isSafetyPinRemoved;
        public bool IsDischarging => isHandleSqueezed && isSafetyPinRemoved && remainingAgent > 0f;
        public bool IsHeld => isHeld;
        public float RemainingAgentFraction => remainingAgent / agentCapacitySeconds;

        private void Awake()
        {
            remainingAgent = agentCapacitySeconds;
            originalPosition = transform.position;
            originalRotation = transform.rotation;
            originalParent = transform.parent;

            if (rigidBody == null) rigidBody = GetComponent<Rigidbody>();
            if (mainCollider == null) mainCollider = GetComponent<Collider>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 0.8f;
                audioSource.minDistance = 0.5f;
                audioSource.maxDistance = 8f;
                audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
                audioSource.playOnAwake = false;
            }

            // Auto-load discharge and pin-pull sounds if not manually assigned
            if (dischargeSound == null || pinPullSound == null)
            {
                AutoLoadExtinguisherSounds();
            }

            if (dischargeEffect == null) dischargeEffect = GetComponentInChildren<ExtinguisherDischargeEffect>();

            if (dischargeEffect != null)
            {
                dischargeEffect.ApplyTypeVisualSettings(extinguisherType);
            }
        }

        /// <summary>
        /// Auto-loads extinguisher-type-appropriate discharge and pin-pull audio clips.
        /// CO2 uses a high-pressure gas hiss; Water/Foam use liquid discharge sound; Powder uses turbulent burst.
        /// </summary>
        private void AutoLoadExtinguisherSounds()
        {
            string dischargeKeyword = extinguisherType switch
            {
                ExtinguisherType.CO2        => "Gas",
                ExtinguisherType.DryPowder  => "Powder",
                ExtinguisherType.Foam       => "Foam",
                ExtinguisherType.Water      => "Water",
                _                           => "Gas"
            };

            // 1. Try loading from Resources/Audio/
            if (dischargeSound == null)
            {
                dischargeSound = Resources.Load<AudioClip>($"Audio/Extinguisher_Discharge_{dischargeKeyword}");
                if (dischargeSound == null) dischargeSound = Resources.Load<AudioClip>("Audio/Extinguisher_Discharge_Gas");
                if (dischargeSound == null) dischargeSound = Resources.Load<AudioClip>($"Extinguisher_Discharge_{dischargeKeyword}");
            }

            if (pinPullSound == null)
            {
                pinPullSound = Resources.Load<AudioClip>("Audio/Extinguisher_Pin_Pull");
                if (pinPullSound == null) pinPullSound = Resources.Load<AudioClip>("Extinguisher_Pin_Pull");
            }

            // 2. Fallback to loaded clips
            if (dischargeSound == null || pinPullSound == null)
            {
                var allClips = Resources.FindObjectsOfTypeAll<AudioClip>();
                foreach (var clip in allClips)
                {
                    string n = clip.name;
                    if (dischargeSound == null && n.Contains("Extinguisher_Discharge") && n.Contains(dischargeKeyword))
                        dischargeSound = clip;
                    if (dischargeSound == null && n.Contains("Extinguisher_Discharge"))
                        dischargeSound = clip;
                    if (pinPullSound == null && n.Contains("Pin_Pull"))
                        pinPullSound = clip;
                }
            }

            // 3. Guaranteed synthetic fallback for pin pull (metal clink)
            if (pinPullSound == null)
            {
                int sampleRate = 44100;
                int samples = (int)(sampleRate * 0.18f);
                float[] data = new float[samples];
                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / sampleRate;
                    float ring1 = Mathf.Sin(2f * Mathf.PI * 3200f * t) * Mathf.Exp(-t * 45f);
                    float ring2 = Mathf.Sin(2f * Mathf.PI * 4800f * t) * Mathf.Exp(-t * 60f);
                    data[i] = Mathf.Clamp((ring1 * 0.6f + ring2 * 0.4f), -1f, 1f);
                }
                pinPullSound = AudioClip.Create("Pin_Pull_Synthetic", samples, 1, sampleRate, false);
                pinPullSound.SetData(data, 0);
            }

            // 4. Guaranteed synthetic fallback for discharge spray (pressurized gas hiss)
            if (dischargeSound == null)
            {
                int sampleRate = 44100;
                int samples = sampleRate * 2;
                float[] data = new float[samples];
                for (int i = 0; i < samples; i++)
                {
                    float noise = (Random.value * 2f - 1f) * 0.65f;
                    data[i] = noise;
                }
                dischargeSound = AudioClip.Create("Discharge_Synthetic", samples, 1, sampleRate, false);
                dischargeSound.SetData(data, 0);
            }
        }

        private void Start()
        {
            // Store original world transform after initialization in case it was placed dynamically
            originalPosition = transform.position;
            originalRotation = transform.rotation;
            originalParent = transform.parent;
        }

        private void Update()
        {
            if (IsDischarging)
            {
                ProcessDischarge();
            }

            if (enableDesktopHotkeys && isHeld)
            {
                HandleDesktopControls();
            }
        }

        /// <summary>
        /// Pulls the safety pin (Step 1 of P.A.S.S.) with visible hand animation.
        /// </summary>
        public bool PullSafetyPin()
        {
            if (isSafetyPinRemoved) return true;

            if (handRig != null && !handRig.IsPinPulling && safetyPin != null)
            {
                StartCoroutine(handRig.AnimatePinRemoval(safetyPin, () =>
                {
                    isSafetyPinRemoved = true;
                    if (audioSource != null && pinPullSound != null)
                    {
                        audioSource.PlayOneShot(pinPullSound);
                    }
                    OnSafetyPinPulled?.Invoke();
                    Debug.Log($"[FireExtinguisher] Safety pin PULLED step-by-step with animated hands on {displayName}");
                }));
                return true;
            }

            isSafetyPinRemoved = true;

            if (safetyPin != null)
            {
                safetyPin.gameObject.SetActive(false);
            }

            if (audioSource != null && pinPullSound != null)
            {
                audioSource.PlayOneShot(pinPullSound);
            }

            OnSafetyPinPulled?.Invoke();
            Debug.Log($"[FireExtinguisher] Safety pin PULLED on {displayName}");
            return true;
        }

        /// <summary>
        /// Squeezes the handle lever (Step 3 of P.A.S.S.).
        /// </summary>
        public void SqueezeHandle()
        {
            isHandleSqueezed = true;
            if (handRig != null) handRig.SetSqueezeAnimation(true);

            if (!isSafetyPinRemoved)
            {
                OnSafetyViolationAlert?.Invoke("Cannot discharge: Safety Pin is still locked! Pull safety pin first (press P).");
                Debug.Log("[FireExtinguisher] Safety pin locked! Press P first.");
                return;
            }

            if (remainingAgent <= 0f)
            {
                OnSafetyViolationAlert?.Invoke("Extinguisher is empty! Agent depleted.");
                return;
            }

            if (dischargeEffect != null)
            {
                dischargeEffect.StartDischarge();
            }

            if (audioSource != null)
            {
                if (dischargeSound != null) audioSource.clip = dischargeSound;
                audioSource.loop = true;
                if (!audioSource.isPlaying) audioSource.Play();
            }

            if (handleLever != null)
            {
                handleLever.localEulerAngles = new Vector3(8f, 0f, 0f);
            }

            OnDischargeStarted?.Invoke();
        }

        /// <summary>
        /// Releases the handle lever, stopping discharge.
        /// </summary>
        public void ReleaseHandle()
        {
            isHandleSqueezed = false;
            if (handRig != null) handRig.SetSqueezeAnimation(false);

            if (dischargeEffect != null)
            {
                dischargeEffect.StopDischarge();
            }

            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }

            if (handleLever != null)
            {
                handleLever.localEulerAngles = new Vector3(-12f, 0f, 0f);
            }

            OnDischargeStopped?.Invoke();
        }

        private void LateUpdate()
        {
            if (!isHeld) return;

            // 1. In VR: Extinguisher naturally follows the VR Controller Hand!
            if (vrHandHolder != null)
            {
                Vector3 holdPos = vrHandHolder.position
                    + vrHandHolder.forward * 0.10f
                    + vrHandHolder.up * -0.22f
                    + vrHandHolder.right * 0.02f;
                Quaternion holdRot = vrHandHolder.rotation * Quaternion.Euler(15f, 0f, 0f);

                transform.position = holdPos;
                transform.rotation = holdRot;

                if (dischargeEffect != null)
                {
                    dischargeEffect.transform.rotation = transform.rotation;
                }
                return;
            }

            // 2. Desktop Fallback: Position in first-person camera viewport
            if (Camera.main == null) return;
            Transform cam = Camera.main.transform;

            Vector3 handPos = cam.position
                + cam.right * 0.22f
                + cam.up * -0.28f
                + cam.forward * 0.54f;

            Quaternion handRot = cam.rotation * Quaternion.Euler(18f, -4f, 0f);

            transform.position = handPos;
            transform.rotation = handRot;

            if (dischargeEffect != null)
            {
                dischargeEffect.transform.rotation = Quaternion.LookRotation(cam.forward, cam.up);
            }
        }

        /// <summary>
        /// Picks up the extinguisher into the player's hands.
        /// </summary>
        public void Pickup(Transform holdParent)
        {
            if (isHeld) return;
            isHeld = true;

            // Check if held by VR Controller Hand
            bool isVRHand = holdParent != null && holdParent != Camera.main?.transform;
            if (isVRHand)
            {
                vrHandHolder = holdParent;
            }
            else
            {
                vrHandHolder = null;
            }

            if (rigidBody != null)
            {
                rigidBody.isKinematic = true;
                rigidBody.useGravity = false;
            }

            if (mainCollider != null)
            {
                mainCollider.enabled = false;
            }

            // Only attach animated desktop screen hands if not held by a VR controller hand
            if (!isVRHand)
            {
                if (handRig == null)
                {
                    handRig = gameObject.AddComponent<FirstPersonExtinguisherHands>();
                }
                handRig.Initialize(this);
            }
            else
            {
                if (handRig != null)
                {
                    DestroyImmediate(handRig);
                    handRig = null;
                }
            }

            OnPickedUp?.Invoke(this);
            Debug.Log($"[FireExtinguisher] Picked up {displayName}. (VR Hand: {isVRHand})");
        }

        private void OnMouseDown()
        {
            if (!isHeld)
            {
                Pickup(Camera.main != null ? Camera.main.transform : null);
            }
        }

        /// <summary>
        /// Drops the extinguisher upright onto the floor in front of the player.
        /// </summary>
        public void Drop()
        {
            if (isHandleSqueezed)
            {
                ReleaseHandle();
            }

            isHeld = false;
            vrHandHolder = null;

            if (Camera.main != null)
            {
                Transform cam = Camera.main.transform;
                Vector3 dropPos = cam.position + cam.forward * 0.75f;
                dropPos.y = 0.0f; // Sits neatly on floor
                transform.position = dropPos;
                transform.rotation = Quaternion.Euler(0f, cam.eulerAngles.y - 90f, 0f);
            }

            if (rigidBody != null)
            {
                rigidBody.isKinematic = true;
                rigidBody.useGravity = false;
            }

            if (mainCollider != null)
            {
                mainCollider.enabled = true;
            }

            if (handRig != null)
            {
                DestroyImmediate(handRig);
                handRig = null;
            }

            OnDropped?.Invoke(this);
            Debug.Log($"[FireExtinguisher] Dropped {displayName}");
        }

        /// <summary>
        /// Resets the extinguisher back to its original safety station position.
        /// </summary>
        public void ResetToStand()
        {
            if (isHandleSqueezed) ReleaseHandle();
            isHeld = false;
            vrHandHolder = null;
            isSafetyPinRemoved = false;
            remainingAgent = agentCapacitySeconds;

            if (safetyPin != null) safetyPin.gameObject.SetActive(true);

            if (handRig != null)
            {
                DestroyImmediate(handRig);
                handRig = null;
            }

            transform.position = originalPosition;
            transform.rotation = originalRotation;

            if (rigidBody != null)
            {
                rigidBody.isKinematic = true;
                rigidBody.useGravity = false;
            }

            if (mainCollider != null) mainCollider.enabled = true;

            Debug.Log($"[FireExtinguisher] Reset {displayName} to wall station");
        }

        /// <summary>
        /// Processes active discharge: raycasts forward from camera center toward crosshair.
        /// </summary>
        private void ProcessDischarge()
        {
            remainingAgent -= Time.deltaTime;
            if (remainingAgent <= 0f)
            {
                remainingAgent = 0f;
                ReleaseHandle();
                OnSafetyViolationAlert?.Invoke("Extinguisher agent fully depleted!");
                return;
            }

            Camera cam = Camera.main;

            Vector3 origin = dischargeEffect != null ? dischargeEffect.transform.position : (cam != null ? cam.transform.position : transform.position);
            Vector3 forward = dischargeEffect != null ? dischargeEffect.transform.forward : (cam != null ? cam.transform.forward : transform.forward);
            float range = dischargeEffect != null ? dischargeEffect.GetEffectiveRange() : 5.0f;

            // Sphere cast from crosshair
            Ray ray = new Ray(origin, forward);
            RaycastHit[] hits = Physics.SphereCastAll(ray, 0.65f, range + 2f);

            bool hitFire = false;
            foreach (var hit in hits)
            {
                FireVFXController fire = hit.collider.GetComponentInParent<FireVFXController>();
                if (fire != null && fire.IsBurning)
                {
                    EvaluateFireSuppression(fire);
                    hitFire = true;
                    break;
                }
            }

            // Fallback proximity check in case ray misses collider edge
            if (!hitFire)
            {
                FireVFXController[] fires = FindObjectsOfType<FireVFXController>();
                foreach (var fire in fires)
                {
                    if (fire.IsBurning)
                    {
                        Vector3 toFire = fire.transform.position - cam.transform.position;
                        if (toFire.magnitude < range + 1.5f && Vector3.Dot(cam.transform.forward, toFire.normalized) > 0.45f)
                        {
                            EvaluateFireSuppression(fire);
                            break;
                        }
                    }
                }
            }
        }

        private void EvaluateFireSuppression(FireVFXController fire)
        {
            // === ELECTRICAL CHIMNEY FIRE (Class B + Electrical) ===
            // CO2 is the CORRECT agent for this scenario.
            if (extinguisherType == ExtinguisherType.CO2)
            {
                // Correct agent – CO2 smothers electrical / Class B fires gradually and realistically
                float newIntensity = Mathf.Max(0f, fire.FireIntensity - (extinguishPower * Time.deltaTime));
                fire.SetFireIntensity(newIntensity);

                if (newIntensity <= 0.02f)
                {
                    fire.ExtinguishFire();
                    OnFireExtinguishedSuccessfully?.Invoke(fire);
                    Debug.Log("[FireExtinguisher] ★ FIRE EXTINGUISHED with CO₂! Electrical chimney fire suppressed. ★");
                }
                return;
            }

            if (extinguisherType == ExtinguisherType.Water)
            {
                OnSafetyViolationAlert?.Invoke("⚡ EXTREME HAZARD: WATER conducts electricity! Never use on live electrical fires. Risk of electrocution!");
                Debug.LogWarning("[FireExtinguisher] HAZARD: Water on electrical fire!");
                return; // Water cannot suppress electrical fire
            }
            else if (extinguisherType == ExtinguisherType.WetChemical)
            {
                OnSafetyViolationAlert?.Invoke("⚠ CAUTION: Wet Chemical is designed for Class F cooking oil fires, not electrical equipment.");
                fire.SetFireIntensity(Mathf.Max(0.6f, fire.FireIntensity - (extinguishPower * 0.10f * Time.deltaTime)));
                return;
            }
            else if (extinguisherType == ExtinguisherType.DryPowder)
            {
                OnSafetyViolationAlert?.Invoke("⚠ WARNING: Dry Powder damages sensitive electrical equipment and leaves corrosive residue.");
                // Partially suppresses but cannot fully extinguish
                fire.SetFireIntensity(Mathf.Max(0.25f, fire.FireIntensity - (extinguishPower * 0.40f * Time.deltaTime)));
            }
            else if (extinguisherType == ExtinguisherType.Foam)
            {
                OnSafetyViolationAlert?.Invoke("⚠ CAUTION: Foam conducts electricity at close range. CO₂ is the correct agent for electrical fires.");
                fire.SetFireIntensity(Mathf.Max(0.5f, fire.FireIntensity - (extinguishPower * 0.15f * Time.deltaTime)));
            }
        }

        private void HandleDesktopControls()
        {
            if (Input.GetKeyDown(KeyCode.P))
            {
                PullSafetyPin();
            }

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
            {
                SqueezeHandle();
            }
            else if (Input.GetKeyUp(KeyCode.Space) || Input.GetMouseButtonUp(0))
            {
                ReleaseHandle();
            }

            if (Input.GetKeyDown(KeyCode.G))
            {
                Drop();
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetToStand();
            }
        }
    }
}
