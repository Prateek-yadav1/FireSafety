using System.Collections;
using UnityEngine;
using FireSafety.Extinguishers;

namespace FireSafety.Interactions
{
    /// <summary>
    /// Renders animated first-person trainee hands and procedural handling effects:
    ///   1. Step 1 (HOLD): Right hand grips carry handle; Left hand grips nozzle/horn aiming at fire base.
    ///   2. Step 2 (PULL PIN): Left hand reaches over, clasps the pull-ring, twists, and pulls the safety pin out cleanly.
    ///   3. Step 3 (AIM NOZZLE): Left hand raises the flexible horn/hose towards the fire base with slight dynamic sway.
    ///   4. Step 4 (SQUEEZE LEVER): Right hand visibly clamps down the top squeeze lever with realistic recoil/kick.
    /// </summary>
    public class FirstPersonExtinguisherHands : MonoBehaviour
    {
        [Header("Materials")]
        private Material gloveMat;
        private Material cuffMat;
        private Material chromeMat;

        [Header("Hand Rig Roots")]
        private GameObject handsRoot;
        private Transform rightHandGroup;
        private Transform leftHandGroup;
        private Transform leftFingersGroup;
        private Transform pullPinTransform;

        [Header("State")]
        private FireExtinguisher targetExtinguisher;
        private bool isPinPulling = false;
        private bool isAiming = false;

        public bool IsPinPulling => isPinPulling;

        public void Initialize(FireExtinguisher ext)
        {
            targetExtinguisher = ext;
            BuildHandModels();
        }

        private void BuildHandModels()
        {
            if (handsRoot != null) DestroyImmediate(handsRoot);

            handsRoot = new GameObject("FP_Hands_Rig");
            handsRoot.transform.SetParent(transform, false);
            handsRoot.transform.localPosition = Vector3.zero;
            handsRoot.transform.localRotation = Quaternion.identity;

            // Firefighter / Safety Leather Glove Materials
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");

            gloveMat = new Material(litShader);
            gloveMat.color = new Color(0.82f, 0.70f, 0.48f); // Golden tan leather
            gloveMat.SetFloat("_Metallic", 0.05f);
            gloveMat.SetFloat("_Smoothness", 0.35f);

            cuffMat = new Material(litShader);
            cuffMat.color = new Color(0.18f, 0.20f, 0.24f); // Heavy Kevlar cuff
            cuffMat.SetFloat("_Metallic", 0.15f);
            cuffMat.SetFloat("_Smoothness", 0.40f);

            chromeMat = new Material(litShader);
            chromeMat.color = new Color(0.9f, 0.9f, 0.92f);
            chromeMat.SetFloat("_Metallic", 0.95f);
            chromeMat.SetFloat("_Smoothness", 0.90f);

            // ─── 1. RIGHT HAND (Grips Extinguisher Handle & Squeeze Lever) ────────
            GameObject rh = new GameObject("RightHand_CarryGrip");
            rh.transform.SetParent(handsRoot.transform, false);
            // Positioned at carry handle: Y ~ 0.52m, Z ~ -0.055m
            rh.transform.localPosition = new Vector3(0.01f, 0.52f, -0.06f);
            rh.transform.localEulerAngles = new Vector3(15f, -10f, -20f);
            rightHandGroup = rh.transform;

            // Palm
            CreateLimbPart(rh.transform, "Palm", new Vector3(0.02f, 0f, 0f), new Vector3(0.065f, 0.045f, 0.085f), gloveMat);
            // Thumb gripping upper lever
            CreateLimbPart(rh.transform, "Thumb", new Vector3(-0.02f, 0.025f, 0.01f), new Vector3(0.022f, 0.022f, 0.055f), gloveMat);
            // Fingers curling around lower handle
            CreateLimbPart(rh.transform, "Fingers", new Vector3(0.015f, -0.025f, 0.01f), new Vector3(0.060f, 0.035f, 0.055f), gloveMat);
            // Forearm sleeve
            CreateLimbPart(rh.transform, "Forearm", new Vector3(0.08f, -0.07f, -0.12f), new Vector3(0.085f, 0.085f, 0.22f), cuffMat);

            // ─── 2. LEFT HAND (Pulls Pin / Aims Discharge Nozzle/Horn) ───────────
            GameObject lh = new GameObject("LeftHand_NozzleAim");
            lh.transform.SetParent(handsRoot.transform, false);
            // Positioned supporting nozzle: Y ~ 0.44m, Z ~ 0.22m
            lh.transform.localPosition = new Vector3(-0.06f, 0.44f, 0.20f);
            lh.transform.localEulerAngles = new Vector3(25f, 15f, 20f);
            leftHandGroup = lh.transform;

            CreateLimbPart(lh.transform, "Palm", new Vector3(-0.01f, 0f, 0f), new Vector3(0.065f, 0.045f, 0.085f), gloveMat);
            GameObject lf = new GameObject("FingersGrip");
            lf.transform.SetParent(lh.transform, false);
            leftFingersGroup = lf.transform;
            CreateLimbPart(lf.transform, "Fingers", new Vector3(0.02f, 0.01f, 0.02f), new Vector3(0.055f, 0.035f, 0.06f), gloveMat);
            CreateLimbPart(lh.transform, "Thumb", new Vector3(-0.02f, 0.02f, 0.01f), new Vector3(0.022f, 0.022f, 0.05f), gloveMat);
            CreateLimbPart(lh.transform, "Forearm", new Vector3(-0.09f, -0.08f, -0.12f), new Vector3(0.085f, 0.085f, 0.22f), cuffMat);
        }

        private GameObject CreateLimbPart(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPos;
            obj.transform.localScale = scale;
            DestroyImmediate(obj.GetComponent<Collider>());
            obj.GetComponent<Renderer>().sharedMaterial = mat;
            return obj;
        }

        /// <summary>
        /// Plays the full visible hand animation: reaching out, gripping the yellow ring,
        /// twisting the pin stem, and smoothly pulling it out.
        /// </summary>
        public IEnumerator AnimatePinRemoval(Transform pinObject, System.Action onComplete)
        {
            isPinPulling = true;
            pullPinTransform = pinObject;

            Vector3 startHandPos = leftHandGroup.localPosition;
            Quaternion startHandRot = leftHandGroup.localRotation;

            // Target position: right at the safety pin ring (X ~ 0.035m, Y ~ 0.535m, Z ~ -0.01m)
            Vector3 pinReachPos = new Vector3(0.045f, 0.535f, -0.01f);
            Quaternion pinReachRot = Quaternion.Euler(-10f, 65f, 40f);

            // Phase 1: Hand reaches from nozzle over to the safety pin (0.35s)
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 3.0f;
                leftHandGroup.localPosition = Vector3.Lerp(startHandPos, pinReachPos, Mathf.SmoothStep(0f, 1f, t));
                leftHandGroup.localRotation = Quaternion.Slerp(startHandRot, pinReachRot, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            // Phase 2: Hand clasps ring and twists pin to break the tamper seal (0.25s)
            float twist = 0f;
            while (twist < 1f)
            {
                twist += Time.deltaTime * 4.0f;
                if (pullPinTransform != null)
                {
                    pullPinTransform.localEulerAngles = new Vector3(0f, 0f, 90f + Mathf.Sin(twist * Mathf.PI) * 25f);
                }
                yield return null;
            }

            // Phase 3: Hand firmly pulls pin outward to the right (+X) out of the valve head (0.40s)
            Vector3 pinStartLocal = pullPinTransform != null ? pullPinTransform.localPosition : Vector3.zero;
            Vector3 pinOutLocal = pinStartLocal + new Vector3(0.18f, 0.04f, -0.03f);
            Vector3 handPullOut = pinReachPos + new Vector3(0.20f, 0.04f, -0.03f);

            float pull = 0f;
            while (pull < 1f)
            {
                pull += Time.deltaTime * 2.8f;
                float ease = Mathf.SmoothStep(0f, 1f, pull);
                if (pullPinTransform != null)
                {
                    pullPinTransform.localPosition = Vector3.Lerp(pinStartLocal, pinOutLocal, ease);
                }
                leftHandGroup.localPosition = Vector3.Lerp(pinReachPos, handPullOut, ease);
                yield return null;
            }

            if (pullPinTransform != null)
            {
                pullPinTransform.gameObject.SetActive(false);
            }

            // Phase 4: Left hand transitions smoothly forward into dedicated AIMING stance on the nozzle (0.45s)
            Vector3 aimNozzlePos = new Vector3(0.0f, 0.45f, 0.28f); // Firmly gripping the forward horn/nozzle
            Quaternion aimNozzleRot = Quaternion.Euler(30f, 0f, 0f);

            float aimT = 0f;
            while (aimT < 1f)
            {
                aimT += Time.deltaTime * 2.5f;
                float ease = Mathf.SmoothStep(0f, 1f, aimT);
                leftHandGroup.localPosition = Vector3.Lerp(handPullOut, aimNozzlePos, ease);
                leftHandGroup.localRotation = Quaternion.Slerp(pinReachRot, aimNozzleRot, ease);
                yield return null;
            }

            isPinPulling = false;
            isAiming = true;
            onComplete?.Invoke();
        }

        private void Update()
        {
            if (isPinPulling || leftHandGroup == null) return;

            // Realistic continuous handling dynamics:
            // When aiming or discharging, hands actively track the nozzle forward with micro-sway
            float sway = Mathf.Sin(Time.time * 2.5f) * 0.003f;
            if (isAiming)
            {
                leftHandGroup.localPosition = new Vector3(sway, 0.45f, 0.28f);
                leftHandGroup.localEulerAngles = new Vector3(30f + sway * 10f, 0f, 0f);
            }
        }

        public void SetSqueezeAnimation(bool squeezed)
        {
            if (rightHandGroup == null) return;

            // Clamps fingers down on squeeze lever
            float targetPitch = squeezed ? 28f : 15f;
            rightHandGroup.localEulerAngles = new Vector3(targetPitch, -10f, -20f);
        }
    }
}
