using UnityEngine;

namespace FireSafety.Extinguishers
{
    /// <summary>
    /// Controls the directional discharge particle stream and hit impact
    /// effects for all 5 extinguisher types with realistic visual substance:
    ///
    ///   - CO₂: Frosty white cryogenic vapor cloud (dry ice mist, rapid expansion, fast evaporation).
    ///   - Water: High-speed translucent blue-white jet stream with droplet dispersion.
    ///   - Foam: Thick, creamy yellow-tinted dense foam lather and accumulating bubbles.
    ///   - Dry Powder: Opaque chalky pale-blue dust cloud that billows and obscures vision.
    ///   - Wet Chemical: Fine golden-amber saponification spray mist from extended lance.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class ExtinguisherDischargeEffect : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private ExtinguisherType extinguisherType = ExtinguisherType.CO2;
        [SerializeField] private ParticleSystem dischargeParticles;
        [SerializeField] private Transform nozzleTip;

        [Header("Discharge Tuning")]
        [SerializeField] private float streamSpeed = 12f;
        [SerializeField] private float streamRange = 4.5f;
        [SerializeField] private float coneAngle = 8f;

        private ParticleSystem.MainModule mainModule;
        private ParticleSystem.ShapeModule shapeModule;
        private ParticleSystem.EmissionModule emissionModule;
        private ParticleSystem.ColorOverLifetimeModule colorModule;
        private ParticleSystem.SizeOverLifetimeModule sizeModule;

        private static Material co2ParticleMat;
        private static Material waterParticleMat;
        private static Material foamParticleMat;
        private static Material powderParticleMat;
        private static Material wetChemParticleMat;
        private static Texture2D softPuffTex;

        private void Awake()
        {
            if (dischargeParticles == null)
            {
                dischargeParticles = GetComponent<ParticleSystem>();
            }

            if (nozzleTip == null)
            {
                nozzleTip = transform;
            }

            ConfigureParticleModules();
            ApplyTypeVisualSettings(extinguisherType);
            StopDischarge();
        }

        private void ConfigureParticleModules()
        {
            if (dischargeParticles == null) return;

            mainModule = dischargeParticles.main;
            mainModule.playOnAwake = false;
            mainModule.simulationSpace = ParticleSystemSimulationSpace.World;
            mainModule.scalingMode = ParticleSystemScalingMode.Hierarchy;

            shapeModule = dischargeParticles.shape;
            shapeModule.shapeType = ParticleSystemShapeType.Cone;

            emissionModule = dischargeParticles.emission;
            
            // Color over lifetime: fade in quickly, fade out gracefully
            colorModule = dischargeParticles.colorOverLifetime;
            colorModule.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.95f, 0.10f),
                    new GradientAlphaKey(0.85f, 0.65f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            colorModule.color = grad;

            // Size over lifetime: expand as it leaves the nozzle
            sizeModule = dischargeParticles.sizeOverLifetime;
            sizeModule.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.0f, 0.25f);
            curve.AddKey(0.35f, 0.85f);
            curve.AddKey(1.0f, 1.60f);
            sizeModule.size = new ParticleSystem.MinMaxCurve(1.0f, curve);
        }

        /// <summary>
        /// Configures distinct visual parameters and realistic substance colors based on physics.
        /// </summary>
        public void ApplyTypeVisualSettings(ExtinguisherType type)
        {
            extinguisherType = type;

            if (dischargeParticles == null)
                dischargeParticles = GetComponent<ParticleSystem>();
            if (dischargeParticles == null) return;

            mainModule = dischargeParticles.main;
            shapeModule = dischargeParticles.shape;
            emissionModule = dischargeParticles.emission;
            ParticleSystemRenderer psRenderer = dischargeParticles.GetComponent<ParticleSystemRenderer>();

            Material mat = GetOrCreateMaterial(type);
            if (psRenderer != null && mat != null)
            {
                psRenderer.sharedMaterial = mat;
                psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                psRenderer.sortMode = ParticleSystemSortMode.Distance;
            }

            switch (type)
            {
                case ExtinguisherType.CO2:
                    // Cold cryogenic vapor cloud: high volume expansion, fast dissipation
                    streamSpeed = 13.5f;
                    streamRange = 3.6f;
                    coneAngle = 20.0f;
                    mainModule.startLifetime = streamRange / streamSpeed;
                    mainModule.startSpeed = streamSpeed;
                    mainModule.startSize = 0.38f;
                    // Frosty dense white with subtle sub-zero cyan tint
                    mainModule.startColor = new Color(0.94f, 0.98f, 1.0f, 0.95f);
                    mainModule.gravityModifier = -0.04f; // Cold gas rises/swirls slightly
                    emissionModule.rateOverTime = 420f;
                    shapeModule.angle = coneAngle;
                    shapeModule.radius = 0.05f;
                    break;

                case ExtinguisherType.Water:
                    // Clean translucent water jet stream: narrow, high speed, slight downward arc
                    streamSpeed = 17.0f;
                    streamRange = 5.5f;
                    coneAngle = 4.2f;
                    mainModule.startLifetime = streamRange / streamSpeed;
                    mainModule.startSpeed = streamSpeed;
                    mainModule.startSize = 0.09f;
                    // Clear liquid blue-white droplets
                    mainModule.startColor = new Color(0.78f, 0.92f, 1.0f, 0.65f);
                    mainModule.gravityModifier = 0.15f; // Gravity pulls stream down
                    emissionModule.rateOverTime = 320f;
                    shapeModule.angle = coneAngle;
                    shapeModule.radius = 0.015f;
                    break;

                case ExtinguisherType.Foam:
                    // Creamy thick yellowish-white lather: heavy clumps
                    streamSpeed = 11.0f;
                    streamRange = 4.2f;
                    coneAngle = 8.5f;
                    mainModule.startLifetime = streamRange / streamSpeed;
                    mainModule.startSpeed = streamSpeed;
                    mainModule.startSize = 0.26f;
                    // Rich pale cream/ivory
                    mainModule.startColor = new Color(1.0f, 0.98f, 0.86f, 0.95f);
                    mainModule.gravityModifier = 0.08f;
                    emissionModule.rateOverTime = 300f;
                    shapeModule.angle = coneAngle;
                    shapeModule.radius = 0.035f;
                    break;

                case ExtinguisherType.DryPowder:
                    // Dense chalky pale-blue dust cloud that billows and lingers
                    streamSpeed = 11.5f;
                    streamRange = 4.2f;
                    coneAngle = 16.5f;
                    mainModule.startLifetime = streamRange / streamSpeed * 1.3f;
                    mainModule.startSpeed = streamSpeed;
                    mainModule.startSize = 0.42f;
                    // Milky pale blue-white powder
                    mainModule.startColor = new Color(0.92f, 0.95f, 1.0f, 0.95f);
                    mainModule.gravityModifier = 0.01f;
                    emissionModule.rateOverTime = 450f;
                    shapeModule.angle = coneAngle;
                    shapeModule.radius = 0.035f;
                    break;

                case ExtinguisherType.WetChemical:
                    // Golden-amber translucent fine saponification mist
                    streamSpeed = 10.0f;
                    streamRange = 3.2f;
                    coneAngle = 11.5f;
                    mainModule.startLifetime = streamRange / streamSpeed;
                    mainModule.startSpeed = streamSpeed;
                    mainModule.startSize = 0.16f;
                    // Amber/golden soap solution mist
                    mainModule.startColor = new Color(0.98f, 0.90f, 0.65f, 0.80f);
                    mainModule.gravityModifier = 0.06f;
                    emissionModule.rateOverTime = 300f;
                    shapeModule.angle = coneAngle;
                    shapeModule.radius = 0.025f;
                    break;
            }
        }

        private static Material GetOrCreateMaterial(ExtinguisherType type)
        {
            ref Material cached = ref co2ParticleMat;
            Color tint = Color.white;

            switch (type)
            {
                case ExtinguisherType.CO2:
                    cached = ref co2ParticleMat;
                    tint = new Color(0.95f, 0.98f, 1.0f, 0.92f);
                    break;
                case ExtinguisherType.Water:
                    cached = ref waterParticleMat;
                    tint = new Color(0.80f, 0.92f, 1.0f, 0.65f);
                    break;
                case ExtinguisherType.Foam:
                    cached = ref foamParticleMat;
                    tint = new Color(1.0f, 0.98f, 0.86f, 0.92f);
                    break;
                case ExtinguisherType.DryPowder:
                    cached = ref powderParticleMat;
                    tint = new Color(0.92f, 0.95f, 1.0f, 0.95f);
                    break;
                case ExtinguisherType.WetChemical:
                    cached = ref wetChemParticleMat;
                    tint = new Color(0.98f, 0.90f, 0.65f, 0.80f);
                    break;
            }

            if (cached != null) return cached;

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Mobile/Particles/Alpha Blended");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");

            Material mat = new Material(shader);
            mat.name = $"ExtinguisherFX_{type}";

            Texture2D tex = GetSoftParticleTexture();
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);

            // Configure URP Particle blending
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1.0f); // Transparent
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0.0f);     // Alpha blend
            if (mat.HasProperty("_SoftParticlesEnabled")) mat.SetFloat("_SoftParticlesEnabled", 1.0f);

            cached = mat;
            return cached;
        }

        private static Texture2D GetSoftParticleTexture()
        {
            if (softPuffTex != null) return softPuffTex;

            // Try loading from Assets first
            Texture2D loaded = Resources.Load<Texture2D>("Textures/Particles/SoftSmokePuff");
            if (loaded != null)
            {
                softPuffTex = loaded;
                return softPuffTex;
            }

            // Procedural smooth radial falloff texture fallback
            int size = 64;
            softPuffTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            softPuffTex.name = "ProceduralSoftPuff";
            softPuffTex.wrapMode = TextureWrapMode.Clamp;
            softPuffTex.filterMode = FilterMode.Bilinear;

            float center = size / 2.0f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                    float alpha = Mathf.Clamp01(1.0f - dist);
                    alpha = Mathf.SmoothStep(0f, 1f, alpha);
                    softPuffTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
                }
            }
            softPuffTex.Apply();
            return softPuffTex;
        }

        public void StartDischarge()
        {
            if (dischargeParticles != null && !dischargeParticles.isPlaying)
            {
                dischargeParticles.Play();
            }
        }

        public void StopDischarge()
        {
            if (dischargeParticles != null && dischargeParticles.isPlaying)
            {
                dischargeParticles.Stop();
            }
        }

        public float GetEffectiveRange() => streamRange;
        public Vector3 GetDischargeOrigin() => nozzleTip != null ? nozzleTip.position : transform.position;
        public Vector3 GetDischargeForward() => nozzleTip != null ? nozzleTip.forward : transform.forward;
    }
}
