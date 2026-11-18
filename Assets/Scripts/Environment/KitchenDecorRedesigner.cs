using UnityEngine;

namespace FireSafety.Environment
{
    /// <summary>
    /// Redesigns all kitchen pots to ensure professional, realistic visual quality:
    ///   1. Realistic Cooking Cookware on the Stove:
    ///      - Heavy-duty Brushed Stainless Steel Stockpot directly under the fire chimney
    ///        with induction base, riveted loop handles, and domed lid.
    ///      - Modern Matte Cast-Iron Sauté Skillet on adjacent burner with long stay-cool handle.
    ///   2. High-End Modern Architectural Planter Pots:
    ///      - Elegant fluted matte-ceramic planter on a mid-century walnut tripod stand with lush tropical Monstera foliage.
    ///      - Minimalist dining table succulent centerpiece planter.
    ///   3. Deactivates any low-quality / glitchy legacy pot models (e.g. Sketchfab_model.003/.005/.006)
    ///      so they don't clutter the floor or clip with the wall-mounted extinguishers.
    /// </summary>
    public static class KitchenDecorRedesigner
    {
        public static void RedesignAllPots()
        {
            // 1. Remove / hide legacy low-quality pot models
            HideLegacyPotModels();

            // 2. Build or refresh modern cooking cookware on the stove
            BuildStoveCookware();

            // 3. Build or refresh high-end modern planter pots
            BuildModernPlanters();

            Debug.Log("[KitchenDecorRedesigner] ✓ All kitchen cookware and planter pots redesigned successfully.");
        }

        // ─── 1. Clean up old/glitchy pot meshes ────────────────────────────────
        private static void HideLegacyPotModels()
        {
            string[] legacyPotNames = {
                "Plant_Pot_Old",
                "Legacy_Pot_Old"
            };

            foreach (var name in legacyPotNames)
            {
                var objs = GameObject.FindObjectsOfType<GameObject>();
                foreach (var go in objs)
                {
                    if (go.name.Contains(name) && !go.name.Contains("Redesigned"))
                    {
                        var renderer = go.GetComponent<Renderer>();
                        if (renderer != null) renderer.enabled = false;
                        var col = go.GetComponent<Collider>();
                        if (col != null) col.enabled = false;
                    }
                }
            }
        }

        // ─── 2. Stove Cooking Cookware ────────────────────────────────────────
        private static void BuildStoveCookware()
        {
            GameObject existing = GameObject.Find("Stove_Cookware_Redesigned");
            if (existing != null) Object.DestroyImmediate(existing);

            GameObject root = new GameObject("Stove_Cookware_Redesigned");
            root.transform.position = Vector3.zero;

            // Materials
            Material brushedSteelMat = CreateLitMat(new Color(0.85f, 0.87f, 0.90f), 0.92f, 0.85f);
            Material darkBaseMat     = CreateLitMat(new Color(0.25f, 0.25f, 0.28f), 0.95f, 0.65f);
            Material castIronMat     = CreateLitMat(new Color(0.12f, 0.12f, 0.14f), 0.35f, 0.40f);
            Material chromeHandleMat = CreateLitMat(new Color(0.92f, 0.92f, 0.95f), 0.98f, 0.92f);
            Material copperRimMat    = CreateLitMat(new Color(0.85f, 0.52f, 0.32f), 0.90f, 0.80f);
            Material glassLidMat     = CreateLitMat(new Color(0.20f, 0.25f, 0.30f, 0.60f), 0.20f, 0.95f);
            Material cookingOilMat   = CreateLitMat(new Color(0.65f, 0.42f, 0.08f), 0.10f, 0.90f);

            // ── POT A: Professional Stainless-Steel Stockpot (Under Fire Chimney on front burner) ──
            // Stove front-left burner: X~200.18, Y=0.88 (counter surface), Z~0.13
            // Pots sit ON the stove surface — base at Y=0.88, body rises upward
            Vector3 potCenter = new Vector3(200.18f, 0.88f, 0.13f);
            GameObject stockpot = new GameObject("StainlessSteel_Stockpot");
            stockpot.transform.SetParent(root.transform, false);
            stockpot.transform.position = potCenter;

            // 1. Induction Base Ring
            GameObject baseRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseRing.name = "InductionBase";
            baseRing.transform.SetParent(stockpot.transform, false);
            baseRing.transform.localPosition = new Vector3(0f, 0.012f, 0f);
            baseRing.transform.localScale = new Vector3(0.32f, 0.012f, 0.32f);
            baseRing.GetComponent<Renderer>().sharedMaterial = darkBaseMat;
            RemoveCollider(baseRing);

            // 2. Main Pot Body Cylinder
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "PotBody";
            body.transform.SetParent(stockpot.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.10f, 0f);
            body.transform.localScale = new Vector3(0.30f, 0.085f, 0.30f);
            body.GetComponent<Renderer>().sharedMaterial = brushedSteelMat;
            RemoveCollider(body);

            // 3. Top Rim Collar
            GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rim.name = "PotRim";
            rim.transform.SetParent(stockpot.transform, false);
            rim.transform.localPosition = new Vector3(0f, 0.188f, 0f);
            rim.transform.localScale = new Vector3(0.312f, 0.006f, 0.312f);
            rim.GetComponent<Renderer>().sharedMaterial = chromeHandleMat;
            RemoveCollider(rim);

            // 4. Hot Oil Surface inside pot
            GameObject oil = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            oil.name = "CookingOilSurface";
            oil.transform.SetParent(stockpot.transform, false);
            oil.transform.localPosition = new Vector3(0f, 0.155f, 0f);
            oil.transform.localScale = new Vector3(0.29f, 0.002f, 0.29f);
            oil.GetComponent<Renderer>().sharedMaterial = cookingOilMat;
            RemoveCollider(oil);

            // 5. Dual Riveted Loop Handles (Opposite Sides)
            CreatePotLoopHandle(stockpot.transform, new Vector3(0.165f, 0.14f, 0f), chromeHandleMat);
            CreatePotLoopHandle(stockpot.transform, new Vector3(-0.165f, 0.14f, 0f), chromeHandleMat);

            // ── POT B: Cast-Iron Sauté Skillet (Adjacent rear burner) ───────────────────
            // Rear-left burner, offset back along Z axis from front burner
            Vector3 panCenter = new Vector3(200.18f, 0.88f, -0.22f);
            GameObject skillet = new GameObject("CastIron_Skillet");
            skillet.transform.SetParent(root.transform, false);
            skillet.transform.position = panCenter;

            // Pan Body (Shallow flared)
            GameObject panBody = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            panBody.name = "PanBody";
            panBody.transform.SetParent(skillet.transform, false);
            panBody.transform.localPosition = new Vector3(0f, 0.035f, 0f);
            panBody.transform.localScale = new Vector3(0.28f, 0.035f, 0.28f);
            panBody.GetComponent<Renderer>().sharedMaterial = castIronMat;
            RemoveCollider(panBody);

            // Copper Accent Ring
            GameObject panRim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            panRim.name = "CopperRim";
            panRim.transform.SetParent(skillet.transform, false);
            panRim.transform.localPosition = new Vector3(0f, 0.071f, 0f);
            panRim.transform.localScale = new Vector3(0.284f, 0.003f, 0.284f);
            panRim.GetComponent<Renderer>().sharedMaterial = copperRimMat;
            RemoveCollider(panRim);

            // Long Stay-Cool Ergonomic Handle (extending toward room / +X)
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handle.name = "SkilletHandle";
            handle.transform.SetParent(skillet.transform, false);
            handle.transform.localPosition = new Vector3(0.23f, 0.075f, 0f);
            handle.transform.localEulerAngles = new Vector3(0f, 0f, -80f);
            handle.transform.localScale = new Vector3(0.022f, 0.11f, 0.022f);
            handle.GetComponent<Renderer>().sharedMaterial = chromeHandleMat;
            RemoveCollider(handle);
        }

        private static void CreatePotLoopHandle(Transform parent, Vector3 localPos, Material mat)
        {
            GameObject handle = new GameObject("LoopHandle");
            handle.transform.SetParent(parent, false);
            handle.transform.localPosition = localPos;

            // Horizontal grip bar
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bar.name = "Grip";
            bar.transform.SetParent(handle.transform, false);
            bar.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
            bar.transform.localScale = new Vector3(0.016f, 0.035f, 0.016f);
            bar.GetComponent<Renderer>().sharedMaterial = mat;
            RemoveCollider(bar);

            // Two mounting bracket arms
            for (int s = -1; s <= 1; s += 2)
            {
                GameObject arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
                arm.name = "MountArm";
                arm.transform.SetParent(handle.transform, false);
                arm.transform.localPosition = new Vector3(0f, 0f, s * 0.035f);
                arm.transform.localScale = new Vector3(0.03f, 0.012f, 0.012f);
                arm.GetComponent<Renderer>().sharedMaterial = mat;
                RemoveCollider(arm);
            }
        }

        // ─── 3. Modern Architectural Planter Pots ─────────────────────────────
        private static void BuildModernPlanters()
        {
            GameObject existing = GameObject.Find("Planters_Redesigned");
            if (existing != null) Object.DestroyImmediate(existing);

            GameObject root = new GameObject("Planters_Redesigned");
            root.transform.position = Vector3.zero;

            Material ceramicWhiteMat = CreateLitMat(new Color(0.94f, 0.92f, 0.88f), 0.05f, 0.60f); // Matte ceramic
            Material walnutWoodMat   = CreateLitMat(new Color(0.38f, 0.24f, 0.14f), 0.05f, 0.45f); // Rich walnut
            Material darkSoilMat     = CreateLitMat(new Color(0.16f, 0.12f, 0.08f), 0.02f, 0.15f); // Rich potting soil
            Material lushLeafMat     = CreateLitMat(new Color(0.12f, 0.52f, 0.22f), 0.05f, 0.55f); // Vibrant monstera green
            Material deepLeafMat     = CreateLitMat(new Color(0.08f, 0.38f, 0.16f), 0.05f, 0.50f); // Forest green underlayer
            Material stemMat         = CreateLitMat(new Color(0.18f, 0.42f, 0.16f), 0.05f, 0.40f);

            // ── PLANTER A: Luxury Corner Architectural Planter ────────────────────
            // Corner between painting and kitchen counter: X: 199.35, Y: 0.00, Z: 2.15
            Vector3 cornerPos = new Vector3(199.35f, 0.0f, 2.15f);
            GameObject cornerPlanter = new GameObject("Corner_LuxuryPlanter");
            cornerPlanter.transform.SetParent(root.transform, false);
            cornerPlanter.transform.position = cornerPos;

            // 1. Walnut Wood Tripod Stand (3 angled legs)
            float standRadius = 0.18f;
            for (int i = 0; i < 3; i++)
            {
                float angle = i * 120f * Mathf.Deg2Rad;
                Vector3 legPos = new Vector3(Mathf.Cos(angle) * standRadius, 0.20f, Mathf.Sin(angle) * standRadius);
                GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                leg.name = $"StandLeg_{i}";
                leg.transform.SetParent(cornerPlanter.transform, false);
                leg.transform.localPosition = legPos;
                leg.transform.localScale = new Vector3(0.022f, 0.20f, 0.022f);
                leg.GetComponent<Renderer>().sharedMaterial = walnutWoodMat;
                RemoveCollider(leg);
            }

            // Stand cross bracket
            GameObject cross = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cross.name = "StandCross";
            cross.transform.SetParent(cornerPlanter.transform, false);
            cross.transform.localPosition = new Vector3(0f, 0.36f, 0f);
            cross.transform.localScale = new Vector3(0.32f, 0.015f, 0.32f);
            cross.GetComponent<Renderer>().sharedMaterial = walnutWoodMat;
            RemoveCollider(cross);

            // 2. Ceramic Planter Cylinder Pot
            GameObject pot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pot.name = "CeramicPot";
            pot.transform.SetParent(cornerPlanter.transform, false);
            pot.transform.localPosition = new Vector3(0f, 0.58f, 0f);
            pot.transform.localScale = new Vector3(0.34f, 0.20f, 0.34f);
            pot.GetComponent<Renderer>().sharedMaterial = ceramicWhiteMat;
            RemoveCollider(pot);

            // Pot top rim collar
            GameObject potRim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            potRim.name = "PotCollar";
            potRim.transform.SetParent(cornerPlanter.transform, false);
            potRim.transform.localPosition = new Vector3(0f, 0.785f, 0f);
            potRim.transform.localScale = new Vector3(0.355f, 0.012f, 0.355f);
            potRim.GetComponent<Renderer>().sharedMaterial = ceramicWhiteMat;
            RemoveCollider(potRim);

            // Potting Soil Bed
            GameObject soil = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            soil.name = "SoilBed";
            soil.transform.SetParent(cornerPlanter.transform, false);
            soil.transform.localPosition = new Vector3(0f, 0.76f, 0f);
            soil.transform.localScale = new Vector3(0.325f, 0.015f, 0.325f);
            soil.GetComponent<Renderer>().sharedMaterial = darkSoilMat;
            RemoveCollider(soil);

            // 3. Lush Tropical Indoor Foliage (Multi-tiered fan leaves)
            float[] leafAngles = { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f };
            float[] leafPitches = { 35f, 48f, 28f, 52f, 32f, 46f, 30f, 50f };
            float[] leafHeights = { 0.95f, 1.10f, 0.90f, 1.15f, 0.92f, 1.08f, 0.88f, 1.12f };

            for (int l = 0; l < leafAngles.Length; l++)
            {
                float rad = leafAngles[l] * Mathf.Deg2Rad;
                Vector3 stemBase = new Vector3(0f, 0.77f, 0f);
                Vector3 leafTip = new Vector3(
                    Mathf.Cos(rad) * 0.32f,
                    leafHeights[l],
                    Mathf.Sin(rad) * 0.32f
                );

                // Leaf stem
                GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stem.name = $"Stem_{l}";
                stem.transform.SetParent(cornerPlanter.transform, false);
                stem.transform.localPosition = (stemBase + leafTip) * 0.5f;
                stem.transform.up = (leafTip - stemBase).normalized;
                stem.transform.localScale = new Vector3(0.012f, (leafTip - stemBase).magnitude * 0.5f, 0.012f);
                stem.GetComponent<Renderer>().sharedMaterial = stemMat;
                RemoveCollider(stem);

                // Broad fan leaf
                GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                leaf.name = $"MonsteraLeaf_{l}";
                leaf.transform.SetParent(cornerPlanter.transform, false);
                leaf.transform.localPosition = leafTip;
                leaf.transform.localEulerAngles = new Vector3(leafPitches[l], leafAngles[l], 15f);
                leaf.transform.localScale = new Vector3(0.24f, 0.008f, 0.38f);
                leaf.GetComponent<Renderer>().sharedMaterial = (l % 2 == 0) ? lushLeafMat : deepLeafMat;
                RemoveCollider(leaf);
            }

            // ── PLANTER B: Dining Table Succulent Centerpiece ───────────────────────
            Vector3 tablePos = new Vector3(202.45f, 0.78f, -1.80f);
            GameObject tablePlanter = new GameObject("Table_SucculentPlanter");
            tablePlanter.transform.SetParent(root.transform, false);
            tablePlanter.transform.position = tablePos;

            // Low ceramic bowl
            GameObject bowl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bowl.name = "CeramicBowl";
            bowl.transform.SetParent(tablePlanter.transform, false);
            bowl.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            bowl.transform.localScale = new Vector3(0.22f, 0.03f, 0.22f);
            bowl.GetComponent<Renderer>().sharedMaterial = ceramicWhiteMat;
            RemoveCollider(bowl);

            // Soil / pebble bed
            GameObject pebbles = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pebbles.name = "PebbleBed";
            pebbles.transform.SetParent(tablePlanter.transform, false);
            pebbles.transform.localPosition = new Vector3(0f, 0.058f, 0f);
            pebbles.transform.localScale = new Vector3(0.205f, 0.005f, 0.205f);
            pebbles.GetComponent<Renderer>().sharedMaterial = darkSoilMat;
            RemoveCollider(pebbles);

            // Succulent rosettes
            for (int s = 0; s < 5; s++)
            {
                float sa = s * 72f * Mathf.Deg2Rad;
                float dist = s == 0 ? 0f : 0.055f;
                GameObject rosette = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rosette.name = $"SucculentRosette_{s}";
                rosette.transform.SetParent(tablePlanter.transform, false);
                rosette.transform.localPosition = new Vector3(Mathf.Cos(sa) * dist, 0.075f, Mathf.Sin(sa) * dist);
                rosette.transform.localScale = new Vector3(0.065f, 0.035f, 0.065f);
                rosette.GetComponent<Renderer>().sharedMaterial = lushLeafMat;
                RemoveCollider(rosette);
            }
        }

        private static void RemoveCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }

        private static Material CreateLitMat(Color col, float metallic, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material m = new Material(shader);
            m.color = col;
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
            return m;
        }
    }
}
