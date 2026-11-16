using System.Collections.Generic;
using UnityEngine;
using FireSafety.Environment;

namespace FireSafety.Extinguishers
{
    /// <summary>
    /// Coordinates the interactive fire extinguisher training station in the Kitchen (Room 02).
    /// Features exactly 3 wall-mounted fire extinguishers with professional positioning:
    ///   - [1] WATER       (Signal Red Band   - Class A, electrocution hazard on live electrical equipment)
    ///   - [2] DRY POWDER  (French Blue Band  - Class ABC, corrosive powder residue)
    ///   - [3] CO₂ GAS     (Jet Black Band    - Class B & Electrical, CORRECT safe agent)
    /// All 3 are mounted firmly on heavy-duty wall brackets at 1.10m ergonomic height,
    /// with illuminated identification plaques mounted directly above at eye level (1.62m).
    /// </summary>
    public class ExtinguisherStation : MonoBehaviour
    {
        [Header("Manual Wall Mounting Position Coordinates")]
        [Tooltip("X coordinate of the mounting wall (default East wall: 205.15)")]
        public float wallX = 205.15f;

        [Tooltip("Y coordinate / height of extinguisher carry handles (default: 1.10m)")]
        public float wallY = 1.10f;

        [Tooltip("Z coordinate of the first extinguisher (away from the door: default 1.70m)")]
        public float wallZ_Start = 1.70f;

        [Tooltip("Spacing in meters between each of the 3 extinguishers (default: 0.55m)")]
        public float wallZ_Step = 0.55f;

        public const float WallX = 205.15f;
        public const float WallY = 1.10f;
        public const float WallZ_Start = 1.70f; // Unit 1 (Water) at Z = 1.70, clear of door (door is at Z ~ -0.08)
        public const float WallZ_Step = 0.55f;   // Unit 2 (Powder) at Z = 2.25, Unit 3 (CO2) at Z = 2.80

        [Header("Extinguishers")]
        [SerializeField] private List<FireExtinguisher> extinguishers = new List<FireExtinguisher>();
        private int currentFocusIndex = 0;

        public IReadOnlyList<FireExtinguisher> Extinguishers => extinguishers;
        public FireExtinguisher ActiveExtinguisher =>
            extinguishers.Count > 0 && currentFocusIndex < extinguishers.Count
                ? extinguishers[currentFocusIndex]
                : null;

        private void Start()
        {
            // Redesign kitchen cookware on stove and planter pots
            KitchenDecorRedesigner.RedesignAllPots();

            // Rebuild wall station if count != 3 or any missing
            FindSceneExtinguishers();
            if (extinguishers.Count != 3)
            {
                BuildWallStation(gameObject, wallX, wallY, wallZ_Start, wallZ_Step);
                FindSceneExtinguishers();
            }
        }

        public void FindSceneExtinguishers()
        {
            extinguishers.Clear();
            var found = FindObjectsOfType<FireExtinguisher>();
            foreach (var ext in found)
            {
                // Only include extinguishers in the Kitchen (X > 100)
                if (ext.transform.position.x > 100f)
                {
                    extinguishers.Add(ext);
                }
            }
            // Sort by Z position so [0] = Water (-0.50), [1] = Dry Powder (0.15), [2] = CO2 (0.80)
            extinguishers.Sort((a, b) => a.transform.position.z.CompareTo(b.transform.position.z));
            Debug.Log($"[ExtinguisherStation] Found {extinguishers.Count} wall-mounted kitchen extinguisher(s).");
        }

        public void SelectExtinguisher(int index)
        {
            if (extinguishers.Count == 0) return;
            currentFocusIndex = Mathf.Clamp(index, 0, extinguishers.Count - 1);
        }

        public void CycleNext()
        {
            if (extinguishers.Count == 0) return;
            currentFocusIndex = (currentFocusIndex + 1) % extinguishers.Count;
        }

        public static void BuildWallStation(GameObject parent = null, float customX = WallX, float customY = WallY, float customZStart = WallZ_Start, float customZStep = WallZ_Step)
        {
            if (parent == null)
            {
                parent = GameObject.Find("FireExtinguisher_WallStation");
                if (parent == null) parent = new GameObject("FireExtinguisher_WallStation");
            }
            parent.transform.position = new Vector3(customX, 0f, 0f);

            // Clean up any old/extra children in the station
            var oldChildren = new List<GameObject>();
            foreach (Transform t in parent.transform) oldChildren.Add(t.gameObject);
            foreach (var go in oldChildren) DestroyImmediate(go);

            // Clean up any standalone kitchen extinguishers outside this station
            foreach (var ext in FindObjectsOfType<FireExtinguisher>())
            {
                if (ext.transform.position.x > 100f && ext.transform.parent != parent.transform)
                {
                    DestroyImmediate(ext.gameObject);
                }
            }

            // Exactly 3 Fire Extinguishers
            var extTypes = new[]
            {
                ExtinguisherType.Water,       // Unit 1 (Red Band)   - Class A (⚡ DANGER: NO ELECTRICAL)
                ExtinguisherType.DryPowder,   // Unit 2 (Blue Band)  - Class ABC (⚠ CORROSIVE RESIDUE)
                ExtinguisherType.CO2          // Unit 3 (Black Band) - Class B & Electrical (✓ CORRECT AGENT)
            };

            for (int i = 0; i < extTypes.Length; i++)
            {
                float zPos = customZStart + i * customZStep;
                Vector3 worldPos = new Vector3(customX - 0.18f, customY, zPos);
                // Face into the room towards -X (West)
                Quaternion rot = Quaternion.Euler(0f, -90f, 0f);

                GameObject ext = ExtinguisherModelBuilder.BuildExtinguisher(extTypes[i], worldPos, rot);
                ext.transform.SetParent(parent.transform, true);

                // Heavy-duty wall bracket attached to the wall holding cylinder neck
                CreateWallBracket(parent.transform, new Vector3(customX - 0.05f, customY + 0.12f, zPos));

                // Illuminated plaque mounted directly above cylinder at eye level (1.62m)
                CreateWallPlaque(parent.transform, new Vector3(customX - 0.05f, customY + 0.52f, zPos), extTypes[i]);
            }

            Debug.Log($"[ExtinguisherStation] ✓ 3 wall-mounted extinguishers successfully created at X={customX:F2}, Y={customY:F2}, Z={customZStart:F2}..{(customZStart + 2*customZStep):F2}.");
        }

        private static void CreateWallBracket(Transform parent, Vector3 pos)
        {
            GameObject bracket = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bracket.name = "WallBracket";
            bracket.transform.SetParent(parent, false);
            bracket.transform.position = pos;
            bracket.transform.localScale = new Vector3(0.06f, 0.45f, 0.14f);
            bracket.GetComponent<Renderer>().sharedMaterial = CreateMat(new Color(0.20f, 0.20f, 0.22f), 0.7f, 0.4f);
            var c = bracket.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);

            // Wall mounting collar clamp
            GameObject clamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            clamp.name = "Clamp";
            clamp.transform.SetParent(bracket.transform, false);
            clamp.transform.localPosition = new Vector3(-0.06f, 0.08f, 0f);
            clamp.transform.localScale = new Vector3(0.12f, 0.04f, 0.16f);
            clamp.GetComponent<Renderer>().sharedMaterial = CreateMat(new Color(0.15f, 0.15f, 0.18f), 0.8f, 0.5f);
            var c2 = clamp.GetComponent<Collider>();
            if (c2 != null) DestroyImmediate(c2);
        }

        private static void CreateWallPlaque(Transform parent, Vector3 pos, ExtinguisherType type)
        {
            GameObject label = new GameObject($"Label_{type}");
            label.transform.SetParent(parent, false);
            label.transform.position = pos;
            label.transform.eulerAngles = new Vector3(0f, -90f, 0f); // Face into room (-X)

            GameObject plaque = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plaque.name = "Plaque";
            plaque.transform.SetParent(label.transform, false);
            plaque.transform.localPosition = Vector3.zero;
            plaque.transform.localScale = new Vector3(0.42f, 0.16f, 0.02f);
            var c = plaque.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);

            Color plaqueColor = type switch
            {
                ExtinguisherType.CO2        => new Color(0.08f, 0.08f, 0.10f),
                ExtinguisherType.Water      => new Color(0.85f, 0.10f, 0.10f),
                ExtinguisherType.DryPowder  => new Color(0.08f, 0.42f, 0.88f),
                _                           => new Color(0.20f, 0.20f, 0.22f),
            };

            plaque.GetComponent<Renderer>().sharedMaterial = CreateMat(plaqueColor, 0.1f, 0.5f);

            // Glowing border trim
            GameObject border = GameObject.CreatePrimitive(PrimitiveType.Cube);
            border.name = "PlaqueBorder";
            border.transform.SetParent(label.transform, false);
            border.transform.localPosition = new Vector3(0f, 0f, 0.008f);
            border.transform.localScale = new Vector3(0.43f, 0.17f, 0.005f);
            var c2 = border.GetComponent<Collider>();
            if (c2 != null) DestroyImmediate(c2);

            Color borderColor = type == ExtinguisherType.CO2 ? new Color(0.34f, 0.90f, 0.95f) : Color.white;
            Material borderMat = CreateMat(borderColor, 0f, 0.8f);
            borderMat.EnableKeyword("_EMISSION");
            borderMat.SetColor("_EmissionColor", borderColor * 1.5f);
            border.GetComponent<Renderer>().sharedMaterial = borderMat;

            string mainTitle = type switch
            {
                ExtinguisherType.Water      => "[1] WATER EXTINGUISHER\n⚡ DANGER: NO ELECTRICAL",
                ExtinguisherType.DryPowder  => "[2] DRY POWDER\nCLASS A·B·C MULTI-PURPOSE",
                ExtinguisherType.CO2        => "[3] CO₂ (CARBON DIOXIDE)\n✓ CLASS B & ELECTRICAL SAFE",
                _                           => "FIRE EXTINGUISHER",
            };

            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(label.transform, false);
            textGO.transform.localPosition = new Vector3(0f, 0f, 0.015f);
            textGO.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            TextMesh tm = textGO.AddComponent<TextMesh>();
            tm.text = mainTitle;
            tm.fontSize = 58;
            tm.characterSize = 0.003f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;
            tm.fontStyle = FontStyle.Bold;

            MeshRenderer mr = textGO.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sortingOrder = 5;
                if (mr.sharedMaterial != null)
                {
                    mr.sharedMaterial.color = Color.white;
                }
            }
        }

        private static Material CreateMat(Color col, float metallic, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.color = col;
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }
    }
}
