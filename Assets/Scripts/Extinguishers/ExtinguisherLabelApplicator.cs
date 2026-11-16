using UnityEngine;
using FireSafety.Extinguishers;

/// <summary>
/// Initializes the authentic fire extinguisher safety station in the kitchen.
///
/// Ensures:
///   1. Kitchen plant pots (Sketchfab_model.003, .005, .006) are cleanly restored and NOT used as extinguishers.
///   2. Three authentic, realistic fire extinguishers are placed standing neatly along the wall next to the door:
///        - WET CHEMICAL (Yellow Band - Class F/K Kitchen Oil)
///        - DRY POWDER   (Blue Band   - Class ABC Multi-Purpose)
///        - CO₂ GAS      (Black Band  - Class B & Electrical)
///   3. Each extinguisher has full P.A.S.S. protocol, proper forward nozzle orientation,
///      and accurate first-person holding transforms.
/// </summary>
public class ExtinguisherLabelApplicator : MonoBehaviour
{
    // Positions along the kitchen wall to the left of the door (facing -X into the room)
    private static readonly Vector3[] StandPositions =
    {
        new Vector3(205.15f, 0.0f, 0.55f), // Wet Chemical (nearest door)
        new Vector3(205.15f, 0.0f, 0.95f), // Dry Powder (middle)
        new Vector3(205.15f, 0.0f, 1.35f)  // CO2 (left)
    };

    private static readonly ExtinguisherType[] Types =
    {
        ExtinguisherType.WetChemical,
        ExtinguisherType.DryPowder,
        ExtinguisherType.CO2
    };

    private void Start()
    {
        RestorePlantPots();

        // ExtinguisherStation owns wall-station generation when present.
        if (GetComponent<ExtinguisherStation>() == null)
        {
            ExtinguisherStation.BuildWallStation(gameObject);
        }

        EnsureFloorCollider();
    }

    /// <summary>
    /// Strips any previously attached extinguisher components from the kitchen plant pots.
    /// Preserves existing kitchen decoration visuals intact.
    /// </summary>
    private void RestorePlantPots()
    {
        string[] potRoots = { "Sketchfab_model.003", "Sketchfab_model.005", "Sketchfab_model.006" };

        foreach (var potName in potRoots)
        {
            GameObject pot = GameObject.Find(potName);
            if (pot == null)
            {
                foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                {
                    if (t.name == potName && t.gameObject.scene.isLoaded)
                    {
                        pot = t.gameObject;
                        break;
                    }
                }
            }

            if (pot != null)
            {
                // Remove any components mistakenly attached to the pots
                var ext = pot.GetComponent<FireExtinguisher>();
                if (ext != null) Destroy(ext);

                var fx = pot.GetComponentInChildren<ExtinguisherDischargeEffect>();
                if (fx != null) Destroy(fx.gameObject);

                Transform label = pot.transform.Find("ExtSafetyLabel");
                if (label != null) Destroy(label.gameObject);

                Transform safetyLabel = pot.transform.Find("SafetyLabel");
                if (safetyLabel != null) Destroy(safetyLabel.gameObject);
            }
        }
    }

    private void EnsureFloorCollider()
    {
        if (GameObject.Find("Kitchen_Floor_Collider") != null) return;

        GameObject floorCol = new GameObject("Kitchen_Floor_Collider");
        floorCol.transform.position = new Vector3(202.45f, -0.02f, 0.0f);
        BoxCollider bc = floorCol.AddComponent<BoxCollider>();
        bc.size = new Vector3(14.0f, 0.04f, 14.0f);
    }
}
