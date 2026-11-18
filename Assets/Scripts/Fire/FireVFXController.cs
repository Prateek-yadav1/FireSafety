using System;
using System.Collections;
using UnityEngine;
using UnityEngine.VFX;

namespace FireSafety.Fire
{
    /// <summary>
    /// Controls the Visual Effect Graph fire simulation, smoke, sparks,
    /// dynamic fire illumination, and crackling audio feedback.
    /// Supports smooth ignition, extinguishing, and real-time intensity control.
    /// Includes desktop test shortcuts for verification without a VR headset.
    /// </summary>
    [RequireComponent(typeof(VisualEffect))]
    public class FireVFXController : MonoBehaviour
    {
        [Header("VFX Graph Reference")]
        [SerializeField] private VisualEffect visualEffect;

        [Header("Illumination & Audio Feedback")]
        [SerializeField] private Light fireLight;
        [SerializeField] private AudioSource audioSource;

        [Header("Base Emission Tuning (Stove Size)")]
        [Tooltip("Nominal flame particle spawn rate for full intensity")]
        [SerializeField] private float baseFlamesRate = 18.0f;
        [Tooltip("Nominal smoke particle spawn rate for full intensity")]
        [SerializeField] private float baseSmokeRate = 10.0f;
        [Tooltip("Nominal sparks particle spawn rate for full intensity")]
        [SerializeField] private float baseSparksRate = 15.0f;
        [Tooltip("Base light intensity for point light")]
        [SerializeField] private float baseLightIntensity = 1.8f;

        [Header("State")]
        [SerializeField] private bool igniteOnStart = true;
        [SerializeField] private float extinguishDuration = 1.5f;

        [Header("Desktop / Editor Testing")]
        [Tooltip("Enable keyboard shortcuts (F: toggle fire, 0-3: set intensity)")]
        [SerializeField] private bool enableDesktopTestHotkeys = true;

        private bool isBurning = false;
        private float currentFireIntensity = 1.0f;
        private float currentSmokeIntensity = 1.0f;
        private Coroutine transitionCoroutine;

        // Light flicker variables
        private float flickerNoiseOffset;

        // Events
        public event Action OnFireIgnited;
        public event Action OnFireExtinguished;
        public event Action<float> OnFireIntensityChanged;

        public bool IsBurning => isBurning;
        public float FireIntensity => currentFireIntensity;
        public float SmokeIntensity => currentSmokeIntensity;

        private void Awake()
        {
            if (visualEffect == null)
            {
                visualEffect = GetComponent<VisualEffect>();
            }

            if (fireLight == null)
            {
                fireLight = GetComponentInChildren<Light>();
            }

            if (audioSource == null)
            {
                audioSource = GetComponentInChildren<AudioSource>();
                if (audioSource == null)
                {
                    // Create a dedicated AudioSource for fire crackling
                    GameObject audioGO = new GameObject("FireAudio");
                    audioGO.transform.SetParent(transform, false);
                    audioSource = audioGO.AddComponent<AudioSource>();
                }
            }

            // Auto-configure fire audio source for realistic 3D fire sound
            audioSource.loop = true;
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.50f;      // Balanced spatial blend for strong room presence & spatial direction
            audioSource.minDistance = 1.0f;
            audioSource.maxDistance = 25f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.volume = 1.0f;
            audioSource.pitch = UnityEngine.Random.Range(0.95f, 1.05f);

            // Auto-load fire crackling clip
            if (audioSource.clip == null)
            {
                audioSource.clip = LoadOrGenerateFireClip();
            }

            flickerNoiseOffset = UnityEngine.Random.Range(0f, 1000f);
        }

        private static AudioClip LoadOrGenerateFireClip()
        {
            AudioClip clip = Resources.Load<AudioClip>("Audio/Fire_Crackling");
            if (clip == null) clip = Resources.Load<AudioClip>("Fire_Crackling");
            if (clip == null)
            {
                var allClips = Resources.FindObjectsOfTypeAll<AudioClip>();
                foreach (var c in allClips)
                {
                    string n = c.name.ToLower();
                    if (n.Contains("fire") || n.Contains("crackle") || n.Contains("crackling") || n.Contains("flame"))
                    {
                        clip = c;
                        break;
                    }
                }
            }
            if (clip == null)
            {
                // Procedural fire crackling loop
                int sampleRate = 44100;
                int samples = sampleRate * 3;
                float[] data = new float[samples];
                float roarPhase = 0f;
                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / sampleRate;
                    roarPhase += (80f + Mathf.PerlinNoise(t * 4f, 0f) * 60f) / sampleRate;
                    float roar = Mathf.Sin(2f * Mathf.PI * roarPhase) * 0.45f;
                    float noise = (UnityEngine.Random.value * 2f - 1f) * 0.35f;
                    float crackle = (UnityEngine.Random.value < 0.004f) ? (UnityEngine.Random.value * 2f - 1f) * 0.9f : 0f;
                    data[i] = Mathf.Clamp(roar + noise + crackle, -1f, 1f);
                }
                clip = AudioClip.Create("Procedural_Fire_Crackling", samples, 1, sampleRate, false);
                clip.SetData(data, 0);
            }
            return clip;
        }

        private void Start()
        {
            // Ensure a trigger collider exists so extinguisher raycasts can detect this fire
            if (GetComponent<Collider>() == null)
            {
                SphereCollider sc = gameObject.AddComponent<SphereCollider>();
                sc.isTrigger = true;
                sc.radius = 0.5f;
                sc.center = Vector3.zero;
                Debug.Log("[FireVFXController] Added trigger SphereCollider for extinguisher detection.");
            }

            if (igniteOnStart)
            {
                IgniteFire();
            }
            else
            {
                ExtinguishFireImmediate();
            }
        }

        private void Update()
        {
            UpdateLightFlicker();

            if (enableDesktopTestHotkeys)
            {
                HandleDesktopTestInputs();
            }
        }

        /// <summary>
        /// Ignites the fire effect, restoring nominal particle rates, light, and audio.
        /// </summary>
        public void IgniteFire()
        {
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }

            isBurning = true;
            currentFireIntensity = 1.0f;
            currentSmokeIntensity = 1.0f;

            if (visualEffect != null)
            {
                visualEffect.enabled = true;
                visualEffect.Play();
                ApplyRatesToVFX(currentFireIntensity, currentSmokeIntensity);
            }

            if (fireLight != null)
            {
                fireLight.enabled = true;
                fireLight.intensity = baseLightIntensity;
            }

            if (audioSource != null)
            {
                if (audioSource.clip == null) audioSource.clip = LoadOrGenerateFireClip();
                audioSource.volume = 1.0f;
                if (!audioSource.isPlaying)
                {
                    audioSource.Play();
                }
            }

            OnFireIgnited?.Invoke();
            OnFireIntensityChanged?.Invoke(currentFireIntensity);
        }

        /// <summary>
        /// Extinguishes the fire smoothly over extinguishDuration.
        /// </summary>
        public void ExtinguishFire()
        {
            if (!isBurning) return;

            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }

            transitionCoroutine = StartCoroutine(SmoothExtinguishRoutine(extinguishDuration));
        }

        /// <summary>
        /// Immediately shuts down fire, smoke, light, and audio without delay.
        /// </summary>
        public void ExtinguishFireImmediate()
        {
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
                transitionCoroutine = null;
            }

            isBurning = false;
            currentFireIntensity = 0f;
            currentSmokeIntensity = 0f;

            ApplyRatesToVFX(0f, 0f);

            if (visualEffect != null)
            {
                visualEffect.Stop();
            }

            if (fireLight != null)
            {
                fireLight.enabled = false;
                fireLight.intensity = 0f;
            }

            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }

            OnFireExtinguished?.Invoke();
            OnFireIntensityChanged?.Invoke(0f);
        }

        /// <summary>
        /// Adjusts fire intensity [0.0 - 1.0]. Scales flames rate, sparks, light, and audio volume.
        /// </summary>
        public void SetFireIntensity(float intensity)
        {
            currentFireIntensity = Mathf.Clamp01(intensity);

            if (currentFireIntensity <= 0.001f)
            {
                if (isBurning)
                {
                    isBurning = false;
                    if (visualEffect != null) visualEffect.Stop();
                    if (fireLight != null) fireLight.enabled = false;
                    if (audioSource != null && audioSource.isPlaying) audioSource.Stop();
                    OnFireExtinguished?.Invoke();
                }
            }
            else if (!isBurning)
            {
                isBurning = true;
                if (visualEffect != null) visualEffect.Play();
                if (fireLight != null) fireLight.enabled = true;
                if (audioSource != null && !audioSource.isPlaying) audioSource.Play();
                OnFireIgnited?.Invoke();
            }

            ApplyRatesToVFX(currentFireIntensity, currentSmokeIntensity);

            if (audioSource != null)
            {
                audioSource.volume = currentFireIntensity;
            }

            OnFireIntensityChanged?.Invoke(currentFireIntensity);
        }

        /// <summary>
        /// Adjusts smoke intensity [0.0 - 1.0].
        /// </summary>
        public void SetSmokeIntensity(float intensity)
        {
            currentSmokeIntensity = Mathf.Clamp01(intensity);
            ApplyRatesToVFX(currentFireIntensity, currentSmokeIntensity);
        }

        /// <summary>
        /// Dispatches verified rate parameters to the VisualEffect Graph instance safely.
        /// </summary>
        private void ApplyRatesToVFX(float fireScale, float smokeScale)
        {
            if (visualEffect == null) return;

            float flamesRate = baseFlamesRate * fireScale;
            float smokeRate = baseSmokeRate * smokeScale;
            float sparksRate = baseSparksRate * fireScale;

            // Target verified exposed parameter names in Bonfire / Flames graph
            SetVFXFloatSafely("Flames Rate", flamesRate);
            SetVFXFloatSafely("FireIntensity", fireScale);
            SetVFXFloatSafely("Smoke Rate", smokeRate);
            SetVFXFloatSafely("SmokeIntensity", smokeScale);
            SetVFXFloatSafely("Sparks Rate", sparksRate);
            SetVFXFloatSafely("SparksRate", sparksRate);

            // Ensure spawn area is scaled for stove burner
            if (visualEffect.HasFloat("Spawn Area"))
            {
                visualEffect.SetFloat("Spawn Area", 0.18f);
            }
        }

        private void SetVFXFloatSafely(string paramName, float value)
        {
            if (visualEffect.HasFloat(paramName))
            {
                visualEffect.SetFloat(paramName, value);
            }
        }

        private IEnumerator SmoothExtinguishRoutine(float duration)
        {
            float startFire = currentFireIntensity;
            float startSmoke = currentSmokeIntensity;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Smooth drop for flames
                float fireFactor = Mathf.Lerp(startFire, 0f, t * t);
                // Smoke lingers slightly longer during extinguishing
                float smokeFactor = Mathf.Lerp(startSmoke, 0f, t);

                SetFireIntensity(fireFactor);
                SetSmokeIntensity(smokeFactor);

                yield return null;
            }

            ExtinguishFireImmediate();
            transitionCoroutine = null;
        }

        private void UpdateLightFlicker()
        {
            if (fireLight == null || !fireLight.enabled || currentFireIntensity <= 0.001f)
            {
                return;
            }

            // Organic light flicker using Perlin noise
            float time = Time.time * 8.0f + flickerNoiseOffset;
            float noise = Mathf.PerlinNoise(time, 0.5f);
            float flicker = Mathf.Lerp(0.85f, 1.15f, noise);
            fireLight.intensity = baseLightIntensity * currentFireIntensity * flicker;
        }

        private void HandleDesktopTestInputs()
        {
            // [F] toggles fire on/off
            if (Input.GetKeyDown(KeyCode.F))
            {
                if (isBurning) ExtinguishFire();
                else IgniteFire();
                Debug.Log($"[FireVFXController] F → Fire {(isBurning ? "ON" : "OFF")}");
            }
            // NOTE: Keys 1/2/3 are reserved for extinguisher quick-equip.
            // Use F to toggle fire, intensity is controlled by the extinguisher spray.
        }
    }
}
