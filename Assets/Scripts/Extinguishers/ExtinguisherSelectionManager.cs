using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace FireSafety.Extinguishers
{
    /// <summary>
    /// Coordinates the interactive equipment selection station on the equipment table.
    /// Presents all 5 extinguisher types with their fire ratings, application guidelines,
    /// and hazards, enabling the trainee to select and equip the correct safety tool.
    /// </summary>
    public class ExtinguisherSelectionManager : MonoBehaviour
    {
        [Header("Extinguishers")]
        [SerializeField] private List<FireExtinguisher> availableExtinguishers = new List<FireExtinguisher>();
        [SerializeField] private int selectedIndex = 0;

        [Header("Information Panel UI (World Space)")]
        [SerializeField] private TextMeshPro typeTitleText;
        [SerializeField] private TextMeshPro classRatingsText;
        [SerializeField] private TextMeshPro suitableForText;
        [SerializeField] private TextMeshPro hazardWarningText;
        [SerializeField] private TextMeshPro passInstructionText;

        [Header("Equipment Placement")]
        [SerializeField] private Transform activeInspectionAnchor;
        [SerializeField] private Transform traineeHandAnchor;

        // Events
        public event Action<FireExtinguisher> OnExtinguisherSelected;
        public event Action<FireExtinguisher> OnExtinguisherEquipped;

        public FireExtinguisher SelectedExtinguisher =>
            availableExtinguishers.Count > 0 && selectedIndex < availableExtinguishers.Count
                ? availableExtinguishers[selectedIndex]
                : null;

        private void Start()
        {
            if (availableExtinguishers.Count == 0)
            {
                availableExtinguishers.AddRange(FindObjectsOfType<FireExtinguisher>());
            }

            UpdateSelectionDisplay();
        }

        private void Update()
        {
            if (UnityEngine.XR.XRSettings.isDeviceActive) return;

            // Desktop quick cycling hotkeys for testing
            if (Input.GetKeyDown(KeyCode.E))
            {
                NextExtinguisher();
            }
            else if (Input.GetKeyDown(KeyCode.Q))
            {
                PreviousExtinguisher();
            }
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                EquipSelectedExtinguisher();
            }
        }

        public void SelectExtinguisher(int index)
        {
            if (availableExtinguishers.Count == 0) return;

            selectedIndex = Mathf.Clamp(index, 0, availableExtinguishers.Count - 1);
            UpdateSelectionDisplay();
            OnExtinguisherSelected?.Invoke(SelectedExtinguisher);
        }

        public void SelectExtinguisher(ExtinguisherType type)
        {
            for (int i = 0; i < availableExtinguishers.Count; i++)
            {
                if (availableExtinguishers[i].Type == type)
                {
                    SelectExtinguisher(i);
                    return;
                }
            }
        }

        public void NextExtinguisher()
        {
            if (availableExtinguishers.Count == 0) return;
            selectedIndex = (selectedIndex + 1) % availableExtinguishers.Count;
            UpdateSelectionDisplay();
            OnExtinguisherSelected?.Invoke(SelectedExtinguisher);
        }

        public void PreviousExtinguisher()
        {
            if (availableExtinguishers.Count == 0) return;
            selectedIndex = (selectedIndex - 1 + availableExtinguishers.Count) % availableExtinguishers.Count;
            UpdateSelectionDisplay();
            OnExtinguisherSelected?.Invoke(SelectedExtinguisher);
        }

        /// <summary>
        /// Equips the currently selected extinguisher into the trainee's hand or inspection stand.
        /// </summary>
        public void EquipSelectedExtinguisher()
        {
            FireExtinguisher ext = SelectedExtinguisher;
            if (ext == null) return;

            Transform targetAnchor = traineeHandAnchor != null ? traineeHandAnchor : activeInspectionAnchor;
            if (targetAnchor != null)
            {
                ext.Pickup(targetAnchor);
            }

            OnExtinguisherEquipped?.Invoke(ext);
        }

        private void UpdateSelectionDisplay()
        {
            FireExtinguisher ext = SelectedExtinguisher;
            if (ext == null) return;

            if (typeTitleText != null)
            {
                typeTitleText.text = ext.DisplayName;
            }

            if (classRatingsText != null)
            {
                classRatingsText.text = $"<b>Approved Classes:</b> {ext.ClassificationCode}";
            }

            if (suitableForText != null)
            {
                suitableForText.text = $"<b>Suitable Applications:</b>\n{ext.SuitableDescription}";
            }

            if (hazardWarningText != null)
            {
                hazardWarningText.text = $"<color=#FF4444><b>CRITICAL HAZARD WARNING:</b>\n{ext.HazardWarning}</color>";
            }

            if (passInstructionText != null)
            {
                passInstructionText.text = "<b>P.A.S.S. Protocol:</b>\n1. <b>[P]</b>ull safety pin\n2. <b>[A]</b>im nozzle at base of fire\n3. <b>[S]</b>queeze handle lever\n4. <b>[S]</b>weep side to side";
            }
        }
    }
}
