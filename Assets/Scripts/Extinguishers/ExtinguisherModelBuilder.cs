using UnityEngine;

namespace FireSafety.Extinguishers
{
    /// <summary>
    /// Builds authentic, detailed 3D fire extinguisher models from procedural primitives.
    /// Each extinguisher includes:
    ///   - Red cylinder body with rounded neck and bottom base ring
    ///   - 360-degree high-visibility colored identification collar band
    ///   - Large, high-contrast front safety label with type, classes, and electrical rating
    ///   - 3D floating illuminated holographic nameplate badge above cylinder
    ///   - Valve head, fixed carry handle, and articulated squeeze lever
    ///   - Safety pull pin with yellow pull ring
    ///   - Pressure gauge with green zone indicator
    ///   - Authentic type-specific discharge apparatus:
    ///       * CO₂: Cryogenic flared discharge horn
    ///       * Dry Powder: Flexible reinforced hose with directional nozzle
    ///       * Water: Narrow high-speed liquid nozzle
    ///       * Foam: Air-aspirating foaming branchpipe
    ///       * Wet Chemical: Stainless-steel extended applicator lance
    ///   - Directional particle discharge effect oriented forward (+Z)
    ///   - Fully wired FireExtinguisher component with P.A.S.S. protocol
    /// </summary>
    public static class ExtinguisherModelBuilder
    {
        public static GameObject BuildExtinguisher(ExtinguisherType type, Vector3 worldPosition, Quaternion worldRotation)
        {
            GameObject root = new GameObject($"Extinguisher_{type}");
            root.transform.position = worldPosition;
            root.transform.rotation = worldRotation;

            // 1. Root Collider (covers cylinder height ~0.62m, radius ~0.10m)
            CapsuleCollider col = root.AddComponent<CapsuleCollider>();
            col.radius = 0.10f;
            col.height = 0.65f;
            col.center = new Vector3(0f, 0.32f, 0f);

            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = 4.5f;
            rb.isKinematic = true;
            rb.useGravity = false;

            AudioSource audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0.6f;
            audio.minDistance = 1.0f;
            audio.maxDistance = 12.0f;

            // Materials
            Material redBodyMat = CreateLitMaterial(new Color(0.85f, 0.08f, 0.08f), 0.20f, 0.70f); // Signal Red
            Material darkBaseMat = CreateLitMaterial(new Color(0.10f, 0.10f, 0.12f), 0.10f, 0.30f); // Base & grips
            Material chromeMat = CreateLitMaterial(new Color(0.88f, 0.88f, 0.90f), 0.85f, 0.85f);  // Chrome neck
            Material brassMat = CreateLitMaterial(new Color(0.85f, 0.72f, 0.25f), 0.75f, 0.65f);   // Brass valve
            Material blackRubber = CreateLitMaterial(new Color(0.12f, 0.12f, 0.14f), 0.05f, 0.25f);

            // 2. Base Ring (protects cylinder bottom)
            GameObject baseRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseRing.name = "BaseRing";
            baseRing.transform.SetParent(root.transform, false);
            baseRing.transform.localPosition = new Vector3(0f, 0.015f, 0f);
            baseRing.transform.localScale = new Vector3(0.19f, 0.015f, 0.19f);
            Object.Destroy(baseRing.GetComponent<Collider>());
            baseRing.GetComponent<Renderer>().sharedMaterial = darkBaseMat;

            // 3. Cylinder Body (Height 0.40m, Diameter 0.17m)
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "CylinderBody";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.23f, 0f);
            body.transform.localScale = new Vector3(0.17f, 0.20f, 0.17f);
            Object.Destroy(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().sharedMaterial = redBodyMat;

            // 4. Cylinder Top Dome
            GameObject dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.name = "TopDome";
            dome.transform.SetParent(root.transform, false);
            dome.transform.localPosition = new Vector3(0f, 0.43f, 0f);
            dome.transform.localScale = new Vector3(0.17f, 0.08f, 0.17f);
            Object.Destroy(dome.GetComponent<Collider>());
            dome.GetComponent<Renderer>().sharedMaterial = redBodyMat;

            // 5. 360-Degree High-Visibility Colored Identification Collar Band
            Color bandColor = GetExtinguisherColor(type);
            Material bandMat = CreateLitMaterial(bandColor, 0.15f, 0.50f);

            GameObject collarBand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            collarBand.name = "360_ColorCollarBand";
            collarBand.transform.SetParent(root.transform, false);
            collarBand.transform.localPosition = new Vector3(0f, 0.32f, 0f);
            collarBand.transform.localScale = new Vector3(0.174f, 0.045f, 0.174f); // Slightly proud of cylinder
            Object.Destroy(collarBand.GetComponent<Collider>());
            collarBand.GetComponent<Renderer>().sharedMaterial = bandMat;

            // 6. Valve Neck
            GameObject neck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            neck.name = "ValveNeck";
            neck.transform.SetParent(root.transform, false);
            neck.transform.localPosition = new Vector3(0f, 0.48f, 0f);
            neck.transform.localScale = new Vector3(0.045f, 0.03f, 0.045f);
            Object.Destroy(neck.GetComponent<Collider>());
            neck.GetComponent<Renderer>().sharedMaterial = chromeMat;

            // 7. Brass Valve Block
            GameObject valve = GameObject.CreatePrimitive(PrimitiveType.Cube);
            valve.name = "ValveBlock";
            valve.transform.SetParent(root.transform, false);
            valve.transform.localPosition = new Vector3(0f, 0.51f, 0f);
            valve.transform.localScale = new Vector3(0.04f, 0.035f, 0.05f);
            Object.Destroy(valve.GetComponent<Collider>());
            valve.GetComponent<Renderer>().sharedMaterial = brassMat;

            // 8. Carry Handle (Fixed Lower Handle, pointing -Z backward)
            GameObject carryHandle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            carryHandle.name = "CarryHandle";
            carryHandle.transform.SetParent(root.transform, false);
            carryHandle.transform.localPosition = new Vector3(0f, 0.52f, -0.055f);
            carryHandle.transform.localEulerAngles = new Vector3(10f, 0f, 0f);
            carryHandle.transform.localScale = new Vector3(0.028f, 0.012f, 0.10f);
            Object.Destroy(carryHandle.GetComponent<Collider>());
            carryHandle.GetComponent<Renderer>().sharedMaterial = darkBaseMat;

            // 9. Squeeze Lever (Upper Actuator, pivots on squeeze)
            GameObject squeezeLever = GameObject.CreatePrimitive(PrimitiveType.Cube);
            squeezeLever.name = "SqueezeLever";
            squeezeLever.transform.SetParent(root.transform, false);
            squeezeLever.transform.localPosition = new Vector3(0f, 0.555f, -0.055f);
            squeezeLever.transform.localEulerAngles = new Vector3(-8f, 0f, 0f);
            squeezeLever.transform.localScale = new Vector3(0.026f, 0.010f, 0.11f);
            Object.Destroy(squeezeLever.GetComponent<Collider>());
            squeezeLever.GetComponent<Renderer>().sharedMaterial = darkBaseMat;

            // 10. Safety Pin (pullable)
            GameObject pinObj = new GameObject("SafetyPin");
            pinObj.transform.SetParent(root.transform, false);
            pinObj.transform.localPosition = new Vector3(0f, 0.535f, -0.01f);

            GameObject pinStem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pinStem.name = "PinStem";
            pinStem.transform.SetParent(pinObj.transform, false);
            pinStem.transform.localEulerAngles = new Vector3(0f, 0f, 90f);
            pinStem.transform.localScale = new Vector3(0.006f, 0.025f, 0.006f);
            Object.Destroy(pinStem.GetComponent<Collider>());
            pinStem.GetComponent<Renderer>().sharedMaterial = chromeMat;

            GameObject pullRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pullRing.name = "PullRing";
            pullRing.transform.SetParent(pinObj.transform, false);
            pullRing.transform.localPosition = new Vector3(0.032f, 0f, 0f);
            pullRing.transform.localScale = new Vector3(0.035f, 0.035f, 0.008f);
            Object.Destroy(pullRing.GetComponent<Collider>());
            Material yellowPinMat = CreateLitMaterial(new Color(0.98f, 0.82f, 0.05f), 0.10f, 0.60f);
            pullRing.GetComponent<Renderer>().sharedMaterial = yellowPinMat;

            // 11. Pressure Gauge (front face of valve)
            GameObject gauge = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            gauge.name = "PressureGauge";
            gauge.transform.SetParent(root.transform, false);
            gauge.transform.localPosition = new Vector3(0f, 0.51f, 0.032f);
            gauge.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
            gauge.transform.localScale = new Vector3(0.026f, 0.008f, 0.026f);
            Object.Destroy(gauge.GetComponent<Collider>());
            Material gaugeMat = CreateLitMaterial(new Color(0.15f, 0.85f, 0.25f), 0.2f, 0.5f);
            gauge.GetComponent<Renderer>().sharedMaterial = gaugeMat;

            // 12. Front High-Visibility Safety Label (firmly on front surface +Z)
            AttachHighVisibilitySafetyLabel(root.transform, type, bandColor);

            // 13. 3D Floating Illuminated Holographic Nameplate Badge (above cylinder)
            AttachFloatingHolographicBadge(root.transform, type, bandColor);

            // 14. Type-Specific Discharge Apparatus (facing +Z forward toward fire)
            GameObject nozzleTipObj = new GameObject("NozzleTip");
            nozzleTipObj.transform.SetParent(root.transform, false);

            if (type == ExtinguisherType.CO2)
            {
                // CO2 Discharge Horn (large tapered black cryogenic horn)
                GameObject horn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                horn.name = "CO2_Horn";
                horn.transform.SetParent(root.transform, false);
                horn.transform.localPosition = new Vector3(0f, 0.45f, 0.18f);
                horn.transform.localEulerAngles = new Vector3(75f, 0f, 0f);
                horn.transform.localScale = new Vector3(0.085f, 0.13f, 0.085f);
                Object.Destroy(horn.GetComponent<Collider>());
                horn.GetComponent<Renderer>().sharedMaterial = blackRubber;

                nozzleTipObj.transform.localPosition = new Vector3(0f, 0.44f, 0.32f);
                nozzleTipObj.transform.localRotation = Quaternion.identity;
            }
            else if (type == ExtinguisherType.WetChemical)
            {
                // Wet Chemical extended applicator lance
                GameObject lance = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                lance.name = "WetChem_Lance";
                lance.transform.SetParent(root.transform, false);
                lance.transform.localPosition = new Vector3(0f, 0.46f, 0.18f);
                lance.transform.localEulerAngles = new Vector3(80f, 0f, 0f);
                lance.transform.localScale = new Vector3(0.012f, 0.12f, 0.012f);
                Object.Destroy(lance.GetComponent<Collider>());
                lance.GetComponent<Renderer>().sharedMaterial = chromeMat;

                GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tip.name = "Lance_Tip";
                tip.transform.SetParent(root.transform, false);
                tip.transform.localPosition = new Vector3(0f, 0.46f, 0.30f);
                tip.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
                tip.transform.localScale = new Vector3(0.022f, 0.015f, 0.022f);
                Object.Destroy(tip.GetComponent<Collider>());
                tip.GetComponent<Renderer>().sharedMaterial = brassMat;

                nozzleTipObj.transform.localPosition = new Vector3(0f, 0.46f, 0.32f);
                nozzleTipObj.transform.localRotation = Quaternion.identity;
            }
            else
            {
                // Hose & brass nozzle (Water, Foam, Dry Powder)
                GameObject hose = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                hose.name = "DischargeHose";
                hose.transform.SetParent(root.transform, false);
                hose.transform.localPosition = new Vector3(0f, 0.43f, 0.14f);
                hose.transform.localEulerAngles = new Vector3(70f, 0f, 0f);
                hose.transform.localScale = new Vector3(0.018f, 0.10f, 0.018f);
                Object.Destroy(hose.GetComponent<Collider>());
                hose.GetComponent<Renderer>().sharedMaterial = blackRubber;

                GameObject nozzle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                nozzle.name = "BrassNozzle";
                nozzle.transform.SetParent(root.transform, false);
                nozzle.transform.localPosition = new Vector3(0f, 0.42f, 0.25f);
                nozzle.transform.localEulerAngles = new Vector3(85f, 0f, 0f);
                nozzle.transform.localScale = new Vector3(0.022f, 0.025f, 0.022f);
                Object.Destroy(nozzle.GetComponent<Collider>());
                nozzle.GetComponent<Renderer>().sharedMaterial = brassMat;

                nozzleTipObj.transform.localPosition = new Vector3(0f, 0.42f, 0.28f);
                nozzleTipObj.transform.localRotation = Quaternion.identity;
            }

            // 15. Discharge Particle System with physical substance colors
            GameObject fxObj = new GameObject("DischargeFX");
            fxObj.transform.SetParent(nozzleTipObj.transform, false);
            fxObj.transform.localPosition = Vector3.zero;
            fxObj.transform.localRotation = Quaternion.identity;
            ParticleSystem ps = fxObj.AddComponent<ParticleSystem>();
            ExtinguisherDischargeEffect fx = fxObj.AddComponent<ExtinguisherDischargeEffect>();
            fx.ApplyTypeVisualSettings(type);

            // 16. FireExtinguisher Script Component
            FireExtinguisher ext = root.AddComponent<FireExtinguisher>();
            ConfigureExtinguisherFields(ext, type, nozzleTipObj.transform, pinObj.transform, squeezeLever.transform, fx, audio);

            return root;
        }

        private static void AttachHighVisibilitySafetyLabel(Transform parent, ExtinguisherType type, Color bandColor)
        {
            GameObject labelObj = new GameObject("SafetyLabel_Front");
            labelObj.transform.SetParent(parent, false);
            // Firmly in front of cylinder (radius 0.085m -> Z = 0.090m)
            labelObj.transform.localPosition = new Vector3(0f, 0.22f, 0.090f);
            labelObj.transform.localRotation = Quaternion.identity;

            // 1. Label Background Badge: Solid high-contrast dark metallic plaque
            GameObject badge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            badge.name = "Badge_Base";
            badge.transform.SetParent(labelObj.transform, false);
            badge.transform.localPosition = Vector3.zero;
            badge.transform.localScale = new Vector3(0.14f, 0.24f, 0.004f);
            Object.Destroy(badge.GetComponent<Collider>());
            badge.GetComponent<Renderer>().sharedMaterial = CreateLitMaterial(new Color(0.08f, 0.08f, 0.10f), 0.20f, 0.70f); // High-contrast dark charcoal

            // 2. High-Visibility Colored Header Banner Stripe
            GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.name = "Badge_HeaderStripe";
            stripe.transform.SetParent(labelObj.transform, false);
            stripe.transform.localPosition = new Vector3(0f, 0.082f, 0.003f);
            stripe.transform.localScale = new Vector3(0.138f, 0.060f, 0.003f);
            Object.Destroy(stripe.GetComponent<Collider>());
            stripe.GetComponent<Renderer>().sharedMaterial = CreateLitMaterial(bandColor, 0.15f, 0.60f);

            // 3. Header Title Text (Bold, high-contrast)
            string title = type switch
            {
                ExtinguisherType.CO2         => "CO₂ GAS",
                ExtinguisherType.Water       => "WATER",
                ExtinguisherType.Foam        => "FOAM",
                ExtinguisherType.DryPowder   => "DRY POWDER",
                ExtinguisherType.WetChemical => "WET CHEMICAL",
                _                            => type.ToString().ToUpper()
            };

            Color titleColor = (type == ExtinguisherType.Foam || type == ExtinguisherType.WetChemical) ? Color.black : Color.white;
            AddCrispText(labelObj.transform, "T_Title", title, titleColor,
                new Vector3(0f, 0.082f, 0.008f), 54, FontStyle.Bold, 0.0026f);

            // 4. Rating & Suitable Uses (Luminescent and easy to read)
            string rating = type switch
            {
                ExtinguisherType.CO2         => "CLASS B & ELEC\n✓ ELECTRICAL SAFE",
                ExtinguisherType.Water       => "CLASS A ONLY\n⚡ DANGER: SHOCK",
                ExtinguisherType.Foam        => "CLASS B LIQUIDS\n⚠ AVOID ELECTRICAL",
                ExtinguisherType.DryPowder   => "CLASS A · B · C\nMULTI-PURPOSE",
                ExtinguisherType.WetChemical => "CLASS F / K\nCOOKING OIL",
                _                            => "FIRE EXTINGUISHER"
            };

            Color ratingColor = type == ExtinguisherType.CO2 ? new Color(0.35f, 0.95f, 0.45f) :
                               (type == ExtinguisherType.Water ? new Color(1.0f, 0.35f, 0.35f) : new Color(0.40f, 0.85f, 1.0f));

            AddCrispText(labelObj.transform, "T_Rating", rating, ratingColor,
                new Vector3(0f, 0.022f, 0.008f), 22, FontStyle.Bold, 0.0022f);

            // 5. P.A.S.S. Instructions
            AddCrispText(labelObj.transform, "T_PASS", "P - PULL PIN [P]\nA - AIM AT BASE\nS - SQUEEZE [SPACE]\nS - SWEEP NOZZLE",
                new Color(0.92f, 0.94f, 0.96f),
                new Vector3(0f, -0.056f, 0.008f), 17, FontStyle.Bold, 0.0019f);
        }

        private static void AttachFloatingHolographicBadge(Transform parent, ExtinguisherType type, Color bandColor)
        {
            GameObject badgeRoot = new GameObject("HoloNameplate_Above");
            badgeRoot.transform.SetParent(parent, false);
            // Floating 0.68m above ground (directly above valve head)
            badgeRoot.transform.localPosition = new Vector3(0f, 0.68f, 0f);
            badgeRoot.transform.localRotation = Quaternion.identity;

            // Plaque background
            GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bg.name = "Holo_BG";
            bg.transform.SetParent(badgeRoot.transform, false);
            bg.transform.localPosition = Vector3.zero;
            bg.transform.localScale = new Vector3(0.24f, 0.065f, 0.004f);
            Object.Destroy(bg.GetComponent<Collider>());
            bg.GetComponent<Renderer>().sharedMaterial = CreateLitMaterial(new Color(0.04f, 0.08f, 0.12f, 0.95f), 0.2f, 0.5f);

            // Glowing border
            GameObject border = GameObject.CreatePrimitive(PrimitiveType.Cube);
            border.name = "Holo_Border";
            border.transform.SetParent(badgeRoot.transform, false);
            border.transform.localPosition = new Vector3(0f, 0f, 0.003f);
            border.transform.localScale = new Vector3(0.245f, 0.070f, 0.002f);
            Object.Destroy(border.GetComponent<Collider>());

            Material emissiveBorder = CreateLitMaterial(bandColor, 0f, 0.8f);
            emissiveBorder.EnableKeyword("_EMISSION");
            emissiveBorder.SetColor("_EmissionColor", bandColor * 1.5f);
            border.GetComponent<Renderer>().sharedMaterial = emissiveBorder;

            // Name Text
            string labelText = type switch
            {
                ExtinguisherType.CO2         => "CO₂ (ELECTRICAL)",
                ExtinguisherType.Water       => "WATER (CLASS A)",
                ExtinguisherType.Foam        => "FOAM (CLASS B)",
                ExtinguisherType.DryPowder   => "DRY POWDER",
                ExtinguisherType.WetChemical => "WET CHEMICAL",
                _                            => type.ToString().ToUpper()
            };

            AddCrispText(badgeRoot.transform, "Holo_Text", labelText, Color.white,
                new Vector3(0f, 0f, -0.006f), 40, FontStyle.Bold, 0.0035f);
        }

        private static void AddCrispText(Transform parent, string name, string text, Color color,
            Vector3 localPos, int fontSize, FontStyle style, float charSize)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // TextMesh faces forward when flipped

            TextMesh tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = fontSize;
            tm.fontStyle = style;
            tm.color = color;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = charSize;

            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sortingOrder = 5;
                // Force diffuse/lit visible text material that does not render washed out white
                if (mr.sharedMaterial != null)
                {
                    mr.sharedMaterial.color = color;
                }
            }
        }

        public static Color GetExtinguisherColor(ExtinguisherType type)
        {
            return type switch
            {
                ExtinguisherType.CO2         => new Color(0.12f, 0.12f, 0.14f), // Jet Black
                ExtinguisherType.Water       => new Color(0.85f, 0.10f, 0.10f), // Signal Red
                ExtinguisherType.Foam        => new Color(0.96f, 0.88f, 0.65f), // Rich Cream
                ExtinguisherType.DryPowder   => new Color(0.08f, 0.42f, 0.88f), // French Blue
                ExtinguisherType.WetChemical => new Color(0.98f, 0.82f, 0.05f), // Canary Yellow
                _                            => Color.white
            };
        }

        private static Material CreateLitMaterial(Color color, float metallic, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.color = color;
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }

        private static void ConfigureExtinguisherFields(
            FireExtinguisher ext,
            ExtinguisherType type,
            Transform nozzle,
            Transform pin,
            Transform lever,
            ExtinguisherDischargeEffect fx,
            AudioSource audio)
        {
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var t = typeof(FireExtinguisher);

            t.GetField("extinguisherType", flags)?.SetValue(ext, type);
            t.GetField("nozzleTip", flags)?.SetValue(ext, nozzle);
            t.GetField("safetyPin", flags)?.SetValue(ext, pin);
            t.GetField("handleLever", flags)?.SetValue(ext, lever);
            t.GetField("dischargeEffect", flags)?.SetValue(ext, fx);
            t.GetField("audioSource", flags)?.SetValue(ext, audio);

            // Auto-assign type-specific discharge and pin pull audio clips
            var allClips = Resources.FindObjectsOfTypeAll<AudioClip>();
            string dischargeClipName = type switch
            {
                ExtinguisherType.CO2 => "Gas",
                ExtinguisherType.DryPowder => "Powder",
                ExtinguisherType.Foam => "Foam",
                ExtinguisherType.Water => "Water",
                _ => "Gas"
            };

            AudioClip matchedDischarge = null;
            AudioClip matchedPin = null;
            foreach (var clip in allClips)
            {
                string n = clip.name;
                if (matchedDischarge == null && n.Contains("Extinguisher_Discharge") && n.Contains(dischargeClipName))
                    matchedDischarge = clip;
                if (matchedPin == null && n.Contains("Extinguisher_Pin_Pull"))
                    matchedPin = clip;
            }

            if (matchedDischarge != null) t.GetField("dischargeSound", flags)?.SetValue(ext, matchedDischarge);
            if (matchedPin != null) t.GetField("pinPullSound", flags)?.SetValue(ext, matchedPin);

            switch (type)
            {
                case ExtinguisherType.CO2:
                    t.GetField("displayName", flags)?.SetValue(ext, "CO₂ Gas Extinguisher (Black Band - Electrical Safe)");
                    t.GetField("classificationCode", flags)?.SetValue(ext, "Class B & Live Electrical (Class C)");
                    t.GetField("effectiveAgainst", flags)?.SetValue(ext, FireClassification.ClassB | FireClassification.Electrical);
                    t.GetField("suitableDescription", flags)?.SetValue(ext, "Clean inert carbon dioxide gas that leaves zero residue. Non-conductive and ideal for electrical appliances, motors, and switchboards.");
                    t.GetField("hazardWarning", flags)?.SetValue(ext, "Do not touch the horn during discharge to avoid cryogenic cold burns.");
                    break;
                case ExtinguisherType.Water:
                    t.GetField("displayName", flags)?.SetValue(ext, "Water Jet Extinguisher (Red Band - Class A Only)");
                    t.GetField("classificationCode", flags)?.SetValue(ext, "Class A Solid Combustibles Only");
                    t.GetField("effectiveAgainst", flags)?.SetValue(ext, FireClassification.ClassA);
                    t.GetField("suitableDescription", flags)?.SetValue(ext, "High-capacity cooling stream for paper, wood, and textiles.");
                    t.GetField("hazardWarning", flags)?.SetValue(ext, "⚡ EXTREME SHOCK HAZARD: Never use on live electrical fires! Risk of fatal electrocution.");
                    break;
                case ExtinguisherType.DryPowder:
                    t.GetField("displayName", flags)?.SetValue(ext, "ABC Dry Powder Extinguisher (Blue Band)");
                    t.GetField("classificationCode", flags)?.SetValue(ext, "Class A - B - C Multi-Purpose");
                    t.GetField("effectiveAgainst", flags)?.SetValue(ext, FireClassification.ClassA | FireClassification.ClassB | FireClassification.ClassC);
                    t.GetField("suitableDescription", flags)?.SetValue(ext, "Multi-purpose chemical powder that smothers flames across solids and flammable liquids.");
                    t.GetField("hazardWarning", flags)?.SetValue(ext, "⚠ Leaves abrasive, corrosive salt dust that permanently ruins electrical appliance circuits and ventilation motors.");
                    break;
                case ExtinguisherType.Foam:
                    t.GetField("displayName", flags)?.SetValue(ext, "AFFF Foam Extinguisher (Cream Band)");
                    t.GetField("classificationCode", flags)?.SetValue(ext, "Class A & Class B Flammable Liquids");
                    t.GetField("effectiveAgainst", flags)?.SetValue(ext, FireClassification.ClassA | FireClassification.ClassB);
                    t.GetField("suitableDescription", flags)?.SetValue(ext, "Forms a smothering blanket over flammable liquid spill fires.");
                    t.GetField("hazardWarning", flags)?.SetValue(ext, "⚠ Aqueous solution conducts electrical current at close range.");
                    break;
                case ExtinguisherType.WetChemical:
                    t.GetField("displayName", flags)?.SetValue(ext, "Wet Chemical Extinguisher (Yellow Band)");
                    t.GetField("classificationCode", flags)?.SetValue(ext, "Class F Cooking Oil & Fats");
                    t.GetField("effectiveAgainst", flags)?.SetValue(ext, FireClassification.ClassF_K);
                    t.GetField("suitableDescription", flags)?.SetValue(ext, "Potassium solution that creates a cooling soapy saponification blanket over burning grease fryers.");
                    t.GetField("hazardWarning", flags)?.SetValue(ext, "⚠ Formulated for high-temperature cooking oil, not electrical hood motors.");
                    break;
            }
        }
    }
}
