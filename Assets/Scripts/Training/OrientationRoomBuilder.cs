using UnityEngine;
using FireSafety.Extinguishers;

namespace FireSafety.Training
{
    /// <summary>
    /// Builds Room 01 (Orientation Room / Studio) matching the user's Figma designs.
    /// Includes:
    ///   - Concrete studio room (walls, floor, ceiling)
    ///   - Overhead horizontal strip light fixture
    ///   - Industrial workbench table
    ///   - Table equipment: Folded Fire Blanket, Center Fire Extinguisher, Yellow Safety Helmet, Leather Gloves, Alarm Box
    ///   - 3D World callout plaques: [FIRE BLANKET] (cyan), [EXTINGUISHER] (red), [SAFETY HELMET] (orange)
    /// </summary>
    public static class OrientationRoomBuilder
    {
        public const float RoomOriginX = 0f;
        public const float RoomOriginY = 0f;
        public const float RoomOriginZ = 0f;

        public static readonly Vector3 PlayerSpawnPos = new Vector3(0f, 1.55f, 0.45f);
        public static readonly Vector3 PlayerSpawnRot = Vector3.zero;

        public static GameObject BuildOrientationRoom()
        {
            GameObject existing = GameObject.Find("Room01_OrientationStudio");
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            GameObject root = new GameObject("Room01_OrientationStudio");
            root.transform.position = Vector3.zero;

            // Shared materials
            Material wallMat = CreateLitMaterial(new Color(0.24f, 0.25f, 0.26f), 0.05f, 0.20f);
            Material floorMat = CreateLitMaterial(new Color(0.18f, 0.19f, 0.20f), 0.10f, 0.35f);
            Material ceilingMat = CreateLitMaterial(new Color(0.15f, 0.15f, 0.16f), 0.05f, 0.15f);
            Material metalDark = CreateLitMaterial(new Color(0.18f, 0.18f, 0.20f), 0.70f, 0.60f);
            Material metalTrim = CreateLitMaterial(new Color(0.32f, 0.33f, 0.35f), 0.85f, 0.75f);
            Material emissiveLightMat = CreateEmissiveMaterial(new Color(1f, 0.98f, 0.95f) * 2.8f);

            // 1. Room Shell (width 7.0m, height 3.6m, depth 7.0m)
            CreateBox(root.transform, "Floor", new Vector3(0f, -0.05f, 2.0f), new Vector3(8.0f, 0.1f, 8.0f), floorMat);
            CreateBox(root.transform, "Ceiling", new Vector3(0f, 3.65f, 2.0f), new Vector3(8.0f, 0.1f, 8.0f), ceilingMat);
            CreateBox(root.transform, "BackWall", new Vector3(0f, 1.8f, -2.0f), new Vector3(8.0f, 3.6f, 0.1f), wallMat);
            CreateBox(root.transform, "FrontWall", new Vector3(0f, 1.8f, 6.0f), new Vector3(8.0f, 3.6f, 0.1f), wallMat);
            CreateBox(root.transform, "LeftWall", new Vector3(-4.0f, 1.8f, 2.0f), new Vector3(0.1f, 3.6f, 8.0f), wallMat);
            CreateBox(root.transform, "RightWall", new Vector3(4.0f, 1.8f, 2.0f), new Vector3(0.1f, 3.6f, 8.0f), wallMat);

            // 2. Overhead Strip Light Fixture (matching neon fixture in screenshots)
            GameObject lightFixture = new GameObject("OverheadLightFixture");
            lightFixture.transform.SetParent(root.transform, false);
            lightFixture.transform.localPosition = new Vector3(0f, 3.55f, 2.3f);

            // Fixture casing
            CreateBox(lightFixture.transform, "Housing", Vector3.zero, new Vector3(2.4f, 0.08f, 0.45f), metalDark);
            // Emissive diffuser panel
            CreateBox(lightFixture.transform, "Diffuser", new Vector3(0f, -0.035f, 0f), new Vector3(2.25f, 0.02f, 0.38f), emissiveLightMat);

            // Point/Spot Light
            GameObject lightGO = new GameObject("StudioLight");
            lightGO.transform.SetParent(lightFixture.transform, false);
            lightGO.transform.localPosition = new Vector3(0f, -0.1f, 0f);
            Light lt = lightGO.AddComponent<Light>();
            lt.type = LightType.Spot;
            lt.spotAngle = 105f;
            lt.innerSpotAngle = 70f;
            lt.range = 7.5f;
            lt.intensity = 2.4f;
            lt.color = new Color(0.98f, 0.98f, 1.0f);
            lt.shadows = LightShadows.Soft;
            lightGO.transform.localEulerAngles = new Vector3(90f, 0f, 0f); // point straight down

            // Soft fill ambient light
            GameObject ambientFill = new GameObject("AmbientFill");
            ambientFill.transform.SetParent(lightFixture.transform, false);
            ambientFill.transform.localPosition = new Vector3(0f, -0.2f, 0f);
            Light fillLt = ambientFill.AddComponent<Light>();
            fillLt.type = LightType.Point;
            fillLt.range = 8.0f;
            fillLt.intensity = 1.0f;
            fillLt.color = new Color(0.85f, 0.88f, 0.95f);
            fillLt.shadows = LightShadows.None;

            // 3. Equipment Workbench Table (matching user screenshot)
            GameObject table = new GameObject("EquipmentTable");
            table.transform.SetParent(root.transform, false);
            table.transform.localPosition = new Vector3(0f, 0f, 2.55f);

            // Table top
            CreateBox(table.transform, "TableTop", new Vector3(0f, 0.81f, 0f), new Vector3(2.35f, 0.08f, 0.85f), metalDark);
            // Table top rim with industrial bevel/trim
            CreateBox(table.transform, "FrontRim", new Vector3(0f, 0.77f, 0.42f), new Vector3(2.36f, 0.06f, 0.025f), metalTrim);
            CreateBox(table.transform, "BackRim", new Vector3(0f, 0.77f, -0.42f), new Vector3(2.36f, 0.06f, 0.025f), metalTrim);
            CreateBox(table.transform, "LeftRim", new Vector3(-1.17f, 0.77f, 0f), new Vector3(0.025f, 0.06f, 0.85f), metalTrim);
            CreateBox(table.transform, "RightRim", new Vector3(1.17f, 0.77f, 0f), new Vector3(0.025f, 0.06f, 0.85f), metalTrim);

            // Table legs (4 sturdy steel legs)
            float legX = 1.08f, legZ = 0.34f, legH = 0.77f;
            CreateBox(table.transform, "Leg_FL", new Vector3(-legX, legH * 0.5f, legZ), new Vector3(0.09f, legH, 0.09f), metalTrim);
            CreateBox(table.transform, "Leg_FR", new Vector3(legX, legH * 0.5f, legZ), new Vector3(0.09f, legH, 0.09f), metalTrim);
            CreateBox(table.transform, "Leg_BL", new Vector3(-legX, legH * 0.5f, -legZ), new Vector3(0.09f, legH, 0.09f), metalTrim);
            CreateBox(table.transform, "Leg_BR", new Vector3(legX, legH * 0.5f, -legZ), new Vector3(0.09f, legH, 0.09f), metalTrim);

            // 4. Equipment Items on the Table (exact positions from Image 1)
            float tableSurfaceY = 0.85f;

            // (A) Center: Red Fire Extinguisher (standing upright display model)
            Vector3 extPos = new Vector3(0f, tableSurfaceY, 2.55f);
            GameObject extObj = ExtinguisherModelBuilder.BuildExtinguisher(ExtinguisherType.CO2, extPos, Quaternion.Euler(0f, 180f, 0f));
            extObj.transform.SetParent(table.transform, true);
            extObj.transform.localScale = Vector3.one * 0.95f;
            // Remove interactive extinguisher component so it functions purely as an orientation showcase prop
            var propComp = extObj.GetComponent<FireExtinguisher>();
            if (propComp != null) Object.DestroyImmediate(propComp);

            // (B) Left: Folded Fire Blanket (white folded textile package with cyan edges)
            BuildFireBlanketProp(table.transform, new Vector3(-0.48f, tableSurfaceY, 2.55f));

            // (C) Right: Yellow Safety Helmet (standard industrial hard hat)
            BuildSafetyHelmetProp(table.transform, new Vector3(0.55f, tableSurfaceY, 2.55f));

            // (D) Leather work gloves
            BuildGlovesProp(table.transform, new Vector3(0.24f, tableSurfaceY, 2.52f));

            // (E) Red alarm bell/callpoint box
            BuildAlarmBoxProp(table.transform, new Vector3(-0.85f, tableSurfaceY, 2.50f));

            // 5. 3D World Callout Tags below table rim (matching dotted tags in Image 1)
            float tagY = 0.72f;
            float tagZ = 2.55f + 0.44f;
            CreateWorldCalloutTag(root.transform, new Vector3(-0.48f, tagY, tagZ), "FIRE BLANKET", new Color(0.25f, 0.90f, 0.95f));
            CreateWorldCalloutTag(root.transform, new Vector3(0f, tagY, tagZ), "EXTINGUISHER", new Color(0.95f, 0.25f, 0.20f));
            CreateWorldCalloutTag(root.transform, new Vector3(0.55f, tagY, tagZ), "SAFETY HELMET", new Color(1.0f, 0.65f, 0.15f));

            Debug.Log("[OrientationRoomBuilder] ✓ Room 01 (Orientation Studio) built successfully at (0, 0, 0).");
            return root;
        }

        private static void BuildFireBlanketProp(Transform parent, Vector3 worldPos)
        {
            GameObject root = new GameObject("FireBlanket_Prop");
            root.transform.SetParent(parent, false);
            root.transform.position = worldPos;

            Material clothMat = CreateLitMaterial(new Color(0.92f, 0.93f, 0.95f), 0.05f, 0.25f);
            Material cyanStripeMat = CreateLitMaterial(new Color(0.25f, 0.85f, 0.95f), 0.15f, 0.45f);

            // Layered folded blanket cloth
            CreateBox(root.transform, "BlanketFold1", new Vector3(0f, 0.02f, 0f), new Vector3(0.36f, 0.038f, 0.26f), clothMat);
            CreateBox(root.transform, "BlanketFold2", new Vector3(0f, 0.055f, 0.01f), new Vector3(0.34f, 0.032f, 0.24f), clothMat);
            // Cyan quick-release pull tape
            CreateBox(root.transform, "CyanTape", new Vector3(0f, 0.075f, 0.04f), new Vector3(0.28f, 0.008f, 0.06f), cyanStripeMat);
            CreateBox(root.transform, "PullTab", new Vector3(0f, 0.04f, 0.14f), new Vector3(0.04f, 0.01f, 0.08f), cyanStripeMat);
        }

        private static void BuildSafetyHelmetProp(Transform parent, Vector3 worldPos)
        {
            GameObject root = new GameObject("SafetyHelmet_Prop");
            root.transform.SetParent(parent, false);
            root.transform.position = worldPos;

            Material helmetYellow = CreateLitMaterial(new Color(0.98f, 0.82f, 0.12f), 0.10f, 0.80f); // High-gloss safety yellow
            Material darkTrim = CreateLitMaterial(new Color(0.12f, 0.12f, 0.12f), 0.10f, 0.30f);

            // Helmet Dome (hemisphere made with squashed sphere)
            GameObject dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.name = "HelmetDome";
            dome.transform.SetParent(root.transform, false);
            dome.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            dome.transform.localScale = new Vector3(0.24f, 0.17f, 0.27f);
            dome.GetComponent<Renderer>().sharedMaterial = helmetYellow;
            Object.DestroyImmediate(dome.GetComponent<Collider>());

            // Helmet Brim / Visor
            GameObject brim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            brim.name = "HelmetBrim";
            brim.transform.SetParent(root.transform, false);
            brim.transform.localPosition = new Vector3(0f, 0.045f, 0.02f);
            brim.transform.localScale = new Vector3(0.27f, 0.012f, 0.30f);
            brim.GetComponent<Renderer>().sharedMaterial = helmetYellow;
            Object.DestroyImmediate(brim.GetComponent<Collider>());

            // Front ridge
            CreateBox(root.transform, "CrownRidge", new Vector3(0f, 0.15f, 0f), new Vector3(0.035f, 0.04f, 0.22f), helmetYellow);
        }

        private static void BuildGlovesProp(Transform parent, Vector3 worldPos)
        {
            GameObject root = new GameObject("Gloves_Prop");
            root.transform.SetParent(parent, false);
            root.transform.position = worldPos;

            Material leatherMat = CreateLitMaterial(new Color(0.78f, 0.65f, 0.45f), 0.05f, 0.25f);
            CreateBox(root.transform, "Glove1", new Vector3(-0.06f, 0.015f, 0f), new Vector3(0.12f, 0.025f, 0.22f), leatherMat);
            CreateBox(root.transform, "Glove2", new Vector3(0.06f, 0.018f, 0.02f), new Vector3(0.12f, 0.025f, 0.22f), leatherMat);
        }

        private static void BuildAlarmBoxProp(Transform parent, Vector3 worldPos)
        {
            GameObject root = new GameObject("AlarmBox_Prop");
            root.transform.SetParent(parent, false);
            root.transform.position = worldPos;

            Material redMat = CreateLitMaterial(new Color(0.85f, 0.15f, 0.15f), 0.15f, 0.50f);
            Material bellBrass = CreateLitMaterial(new Color(0.85f, 0.75f, 0.25f), 0.85f, 0.80f);

            CreateBox(root.transform, "Box", new Vector3(0f, 0.08f, 0f), new Vector3(0.18f, 0.16f, 0.10f), redMat);
            GameObject dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.transform.SetParent(root.transform, false);
            dome.transform.localPosition = new Vector3(0f, 0.08f, 0.055f);
            dome.transform.localScale = new Vector3(0.10f, 0.10f, 0.04f);
            dome.GetComponent<Renderer>().sharedMaterial = bellBrass;
            Object.DestroyImmediate(dome.GetComponent<Collider>());
        }

        private static void CreateWorldCalloutTag(Transform parent, Vector3 pos, string label, Color color)
        {
            GameObject tagRoot = new GameObject($"Tag_{label}");
            tagRoot.transform.SetParent(parent, false);
            tagRoot.transform.position = pos;
            tagRoot.transform.eulerAngles = Vector3.zero;

            // Background plaque
            GameObject plaque = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plaque.transform.SetParent(tagRoot.transform, false);
            plaque.transform.localPosition = Vector3.zero;
            plaque.transform.localScale = new Vector3(0.24f, 0.045f, 0.005f);
            plaque.GetComponent<Renderer>().sharedMaterial = CreateLitMaterial(new Color(0.04f, 0.08f, 0.12f, 0.95f), 0.2f, 0.5f);
            Object.DestroyImmediate(plaque.GetComponent<Collider>());

            // Glowing border
            GameObject border = GameObject.CreatePrimitive(PrimitiveType.Cube);
            border.transform.SetParent(tagRoot.transform, false);
            border.transform.localPosition = new Vector3(0f, 0f, 0.003f);
            border.transform.localScale = new Vector3(0.245f, 0.048f, 0.001f);
            border.GetComponent<Renderer>().sharedMaterial = CreateEmissiveMaterial(color * 1.5f);
            Object.DestroyImmediate(border.GetComponent<Collider>());

            // Text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(tagRoot.transform, false);
            textObj.transform.localPosition = new Vector3(0f, 0f, -0.006f);
            TextMesh tm = textObj.AddComponent<TextMesh>();
            tm.text = label;
            tm.fontSize = 42;
            tm.characterSize = 0.0055f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            tm.color = color;
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static Material CreateLitMaterial(Color color, float metallic, float smoothness)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }

        private static Material CreateEmissiveMaterial(Color color)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color);
            return mat;
        }
    }
}
