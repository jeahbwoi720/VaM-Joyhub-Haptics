using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using SimpleJSON;

namespace MVRPlugin {

    // Main MVRScript class MUST be first in file for VaM dynamic compiler
    public class JoyhubHaptics : MVRScript {

        // Helper forwarder attached to specific body part rigidbodies
        public class JoyhubCollisionForwarder : MonoBehaviour {
            public JoyhubHaptics parentPlugin;
            public string bodyPartName = "";

            void OnCollisionEnter(Collision collision) {
                if (parentPlugin != null && enabled) {
                    parentPlugin.HandleCollision(bodyPartName, collision);
                }
            }

            void OnCollisionStay(Collision collision) {
                if (parentPlugin != null && enabled) {
                    parentPlugin.HandleCollision(bodyPartName, collision);
                }
            }

            void OnTriggerEnter(Collider other) {
                if (parentPlugin != null && enabled) {
                    parentPlugin.HandleTrigger(bodyPartName, other);
                }
            }

            void OnTriggerStay(Collider other) {
                if (parentPlugin != null && enabled) {
                    parentPlugin.HandleTrigger(bodyPartName, other);
                }
            }
        }

        // ==================== LEFT COLUMN UI CONTROLS ====================
        private JSONStorableBool enabledJSON;
        private JSONStorableStringChooser personSelectorJSON;

        // Before Touch (Idle) Feature Controls
        private JSONStorableFloat beforeVibeJSON;
        private JSONStorableBool beforeHeatJSON;
        private JSONStorableBool beforeLightJSON;
        private JSONStorableFloat beforeSuckJSON;
        private JSONStorableFloat beforeSqueezeJSON;
        private JSONStorableBool beforePumpJSON;

        // Dynamics, Hardness & Waveforms
        private JSONStorableBool enableImpactForceJSON;
        private JSONStorableFloat impactSensitivityJSON;
        private JSONStorableBool pulseEnabledJSON;
        private JSONStorableFloat pulseMinJSON;
        private JSONStorableFloat pulseMaxJSON;
        private JSONStorableFloat pulseSpeedJSON;
        private JSONStorableFloat motionSensitivityJSON;
        private JSONStorableBool enableFadeJSON;
        private JSONStorableFloat touchFadeSpeedJSON;
        private JSONStorableFloat maxSpeedClampJSON;
        private JSONStorableFloat manualVibeJSON;

        // ==================== RIGHT COLUMN UI CONTROLS ====================
        // During Touch Feature Controls
        private JSONStorableFloat duringVibeJSON;
        private JSONStorableFloat duringCh2JSON;
        private JSONStorableFloat duringCh3JSON;
        private JSONStorableFloat duringCh4JSON;
        private JSONStorableBool duringHeatJSON;
        private JSONStorableBool duringLightJSON;
        private JSONStorableFloat duringSuckJSON;
        private JSONStorableFloat duringSqueezeJSON;
        private JSONStorableBool duringPumpJSON;

        // Body Part Filters
        private JSONStorableBool filterGenitalsJSON;
        private JSONStorableBool filterBreastsJSON;
        private JSONStorableBool filterMouthJSON;
        private JSONStorableBool filterHandsJSON;
        private JSONStorableBool filterOtherJSON;

        // Network & Actions
        private JSONStorableFloat portJSON;
        private JSONStorableString statusJSON;
        private JSONStorableAction burstAction;
        private JSONStorableAction stopAction;

        private UdpClient udpClient;
        private string host = "127.0.0.1";
        private int currentPort = 8888;

        private Atom activeTargetAtom;
        private Vector3 lastPosition;
        private float lastSendTime = 0f;
        private float sendInterval = 0.04f; // 25 Hz updates

        private bool isTouching = false;
        private float lastTouchTime = 0f;
        private string lastTouchedPart = "None";
        private float currentImpactBonus = 0f;
        private float releaseFadeIntensity = 0f;
        private float burstIntensity = 0f;
        private float lastFinalIntensity = 0f;

        private List<JoyhubCollisionForwarder> activeForwarders = new List<JoyhubCollisionForwarder>();

        private void CreateSectionHeader(string title, bool rightSide) {
            try {
                UIDynamic spacer = CreateSpacer(rightSide);
                if (spacer != null) spacer.height = 10f;

                string formatted = string.Format("<b><color=#000000>■  {0}  ■</color></b>", title.ToUpper());
                JSONStorableString headerParam = new JSONStorableString(title + "_hdr", formatted);
                UIDynamicTextField tf = CreateTextField(headerParam, rightSide);
                if (tf != null) {
                    tf.height = 34f;
                }
            } catch (Exception) { }
        }

        private List<string> GetScenePersonNames() {
            List<string> list = new List<string>();
            try {
                List<Atom> atoms = SuperController.singleton.GetAtoms();
                if (atoms != null) {
                    foreach (Atom a in atoms) {
                        if (a != null && a.type == "Person") {
                            list.Add(a.name);
                        }
                    }
                }
            } catch (Exception) { }
            if (list.Count == 0 && containingAtom != null) {
                list.Add(containingAtom.name);
            }
            return list;
        }

        public override void Init() {
            try {
                SuperController.LogMessage("Joyhub Advanced Haptics (Reactive Real-time) Loading...");

                // ==================== LEFT COLUMN (rightSide = false) ====================
                CreateSectionHeader("Master Plugin Control", false);
                enabledJSON = new JSONStorableBool("Master Enabled", true);
                RegisterBool(enabledJSON);
                CreateToggle(enabledJSON, false);

                List<string> persons = GetScenePersonNames();
                string defaultPerson = (containingAtom != null && containingAtom.type == "Person") ? containingAtom.name : (persons.Count > 0 ? persons[0] : "None");
                personSelectorJSON = new JSONStorableStringChooser("Target Person Atom", persons, defaultPerson, "Target Person", OnPersonSelected);
                RegisterStringChooser(personSelectorJSON);
                CreatePopup(personSelectorJSON, false);

                UIDynamicButton refreshAtomsBtn = CreateButton("🔄 Refresh Scene Characters", false);
                if (refreshAtomsBtn != null && refreshAtomsBtn.button != null) {
                    refreshAtomsBtn.button.onClick.AddListener(RefreshPersonList);
                }

                CreateSectionHeader("Before Touch (Idle / Approach)", false);
                beforeVibeJSON = new JSONStorableFloat("Before Touch: Vibe % (Idle)", 0f, 0f, 100f, true);
                RegisterFloat(beforeVibeJSON);
                CreateSlider(beforeVibeJSON, false);

                beforeHeatJSON = new JSONStorableBool("Before Touch: Heating [On/Off]", false);
                RegisterBool(beforeHeatJSON);
                CreateToggle(beforeHeatJSON, false);

                beforeLightJSON = new JSONStorableBool("Before Touch: LED Lights [On/Off]", false);
                RegisterBool(beforeLightJSON);
                CreateToggle(beforeLightJSON, false);

                beforeSuckJSON = new JSONStorableFloat("Before Touch: Suction (0-5)", 0f, 0f, 5f, true);
                RegisterFloat(beforeSuckJSON);
                CreateSlider(beforeSuckJSON, false);

                beforeSqueezeJSON = new JSONStorableFloat("Before Touch: Squeeze (0-5)", 0f, 0f, 5f, true);
                RegisterFloat(beforeSqueezeJSON);
                CreateSlider(beforeSqueezeJSON, false);

                beforePumpJSON = new JSONStorableBool("Before Touch: Fluid Pump [On/Off]", false);
                RegisterBool(beforePumpJSON);
                CreateToggle(beforePumpJSON, false);

                CreateSectionHeader("Dynamics, Hardness & Waveforms", false);
                enableImpactForceJSON = new JSONStorableBool("Dynamic Touch Hardness Boost", true);
                RegisterBool(enableImpactForceJSON);
                CreateToggle(enableImpactForceJSON, false);

                impactSensitivityJSON = new JSONStorableFloat("Touch Hardness Sensitivity %", 50f, 0f, 100f, true);
                RegisterFloat(impactSensitivityJSON);
                CreateSlider(impactSensitivityJSON, false);

                pulseEnabledJSON = new JSONStorableBool("Pulse Waveform Mode (Idle)", false);
                RegisterBool(pulseEnabledJSON);
                CreateToggle(pulseEnabledJSON, false);

                pulseMinJSON = new JSONStorableFloat("Pulse Min Vibe %", 15f, 0f, 100f, true);
                RegisterFloat(pulseMinJSON);
                CreateSlider(pulseMinJSON, false);

                pulseMaxJSON = new JSONStorableFloat("Pulse Max Vibe %", 80f, 0f, 100f, true);
                RegisterFloat(pulseMaxJSON);
                CreateSlider(pulseMaxJSON, false);

                pulseSpeedJSON = new JSONStorableFloat("Pulse Frequency (Hz)", 1.0f, 0.1f, 5.0f, true);
                RegisterFloat(pulseSpeedJSON);
                CreateSlider(pulseSpeedJSON, false);

                motionSensitivityJSON = new JSONStorableFloat("Motion Velocity Sensitivity", 1.5f, 0f, 10f, true);
                RegisterFloat(motionSensitivityJSON);
                CreateSlider(motionSensitivityJSON, false);

                enableFadeJSON = new JSONStorableBool("Enable Touch Release Fade", true);
                RegisterBool(enableFadeJSON);
                CreateToggle(enableFadeJSON, false);

                touchFadeSpeedJSON = new JSONStorableFloat("Touch Release Fade Speed", 3.0f, 0.5f, 15f, true);
                RegisterFloat(touchFadeSpeedJSON);
                CreateSlider(touchFadeSpeedJSON, false);

                maxSpeedClampJSON = new JSONStorableFloat("Max Output Clamp %", 100f, 0f, 100f, true);
                RegisterFloat(maxSpeedClampJSON);
                CreateSlider(maxSpeedClampJSON, false);

                manualVibeJSON = new JSONStorableFloat("Manual Test Override %", 0f, 0f, 100f, true);
                RegisterFloat(manualVibeJSON);
                CreateSlider(manualVibeJSON, false);

                // ==================== RIGHT COLUMN (rightSide = true) ====================
                CreateSectionHeader("During Touch (Active Contact)", true);
                duringVibeJSON = new JSONStorableFloat("During Touch: Vibe % (Ch 1)", 15f, 0f, 100f, true);
                RegisterFloat(duringVibeJSON);
                CreateSlider(duringVibeJSON, true);

                duringCh2JSON = new JSONStorableFloat("During Touch: Motor 2 (Ch 2) %", 15f, 0f, 100f, true);
                RegisterFloat(duringCh2JSON);
                CreateSlider(duringCh2JSON, true);

                duringCh3JSON = new JSONStorableFloat("During Touch: Motor 3 (Ch 3) %", 0f, 0f, 100f, true);
                RegisterFloat(duringCh3JSON);
                CreateSlider(duringCh3JSON, true);

                duringCh4JSON = new JSONStorableFloat("During Touch: Motor 4 (Ch 4) %", 0f, 0f, 100f, true);
                RegisterFloat(duringCh4JSON);
                CreateSlider(duringCh4JSON, true);

                duringHeatJSON = new JSONStorableBool("During Touch: Heating [On/Off]", true);
                RegisterBool(duringHeatJSON);
                CreateToggle(duringHeatJSON, true);

                duringLightJSON = new JSONStorableBool("During Touch: LED Lights [On/Off]", true);
                RegisterBool(duringLightJSON);
                CreateToggle(duringLightJSON, true);

                duringSuckJSON = new JSONStorableFloat("During Touch: Suction (0-5)", 3f, 0f, 5f, true);
                RegisterFloat(duringSuckJSON);
                CreateSlider(duringSuckJSON, true);

                duringSqueezeJSON = new JSONStorableFloat("During Touch: Squeeze (0-5)", 2f, 0f, 5f, true);
                RegisterFloat(duringSqueezeJSON);
                CreateSlider(duringSqueezeJSON, true);

                duringPumpJSON = new JSONStorableBool("During Touch: Fluid Pump [On/Off]", false);
                RegisterBool(duringPumpJSON);
                CreateToggle(duringPumpJSON, true);

                CreateSectionHeader("Targeted Body Parts", true);
                filterGenitalsJSON = new JSONStorableBool("Body Part: Genitals & Pelvis", true);
                RegisterBool(filterGenitalsJSON);
                CreateToggle(filterGenitalsJSON, true);

                filterBreastsJSON = new JSONStorableBool("Body Part: Breasts & Chest", false);
                RegisterBool(filterBreastsJSON);
                CreateToggle(filterBreastsJSON, true);

                filterMouthJSON = new JSONStorableBool("Body Part: Mouth, Lips & Head", false);
                RegisterBool(filterMouthJSON);
                CreateToggle(filterMouthJSON, true);

                filterHandsJSON = new JSONStorableBool("Body Part: Hands & Arms", false);
                RegisterBool(filterHandsJSON);
                CreateToggle(filterHandsJSON, true);

                filterOtherJSON = new JSONStorableBool("Body Part: All Other Parts", false);
                RegisterBool(filterOtherJSON);
                CreateToggle(filterOtherJSON, true);

                CreateSectionHeader("Actions & Telemetry", true);
                portJSON = new JSONStorableFloat("Bridge Port", 8888f, 1000f, 65535f, false);
                RegisterFloat(portJSON);
                CreateSlider(portJSON, true);

                burstAction = new JSONStorableAction("TriggerBurst", TriggerBurstAction);
                RegisterAction(burstAction);

                stopAction = new JSONStorableAction("StopAll", StopAllAction);
                RegisterAction(stopAction);

                UIDynamicButton burstBtn = CreateButton("⚡ Test Trigger Burst (100%)", true);
                if (burstBtn != null && burstBtn.button != null) {
                    burstBtn.button.onClick.AddListener(TriggerBurstAction);
                }

                UIDynamicButton stopBtn = CreateButton("🛑 STOP ALL MOTORS & FEATURES", true);
                if (stopBtn != null && stopBtn.button != null) {
                    stopBtn.button.onClick.AddListener(StopAllAction);
                }

                statusJSON = new JSONStorableString("Live Status", "Ready (Streaming to 127.0.0.1:8888)");
                CreateTextField(statusJSON, true);

                InitUDP((int)portJSON.val);
                BindToTargetAtom(defaultPerson);
            }
            catch (Exception ex) {
                SuperController.LogError("JoyhubHaptics Init Error: " + ex);
            }
        }

        private void OnPersonSelected(string personName) {
            BindToTargetAtom(personName);
        }

        private void RefreshPersonList() {
            try {
                List<string> persons = GetScenePersonNames();
                if (personSelectorJSON != null) {
                    personSelectorJSON.choices = persons;
                    if (!persons.Contains(personSelectorJSON.val) && persons.Count > 0) {
                        personSelectorJSON.val = persons[0];
                    }
                }
            } catch (Exception ex) {
                SuperController.LogError("RefreshPersonList Error: " + ex);
            }
        }

        private void BindToTargetAtom(string personName) {
            try {
                CleanupForwarders();

                Atom target = null;
                if (!string.IsNullOrEmpty(personName) && personName != "None") {
                    target = SuperController.singleton.GetAtomByUid(personName);
                }
                if (target == null) {
                    target = containingAtom;
                }

                activeTargetAtom = target;
                if (activeTargetAtom != null) {
                    lastPosition = activeTargetAtom.transform.position;
                    SetupBodyPartForwarders(activeTargetAtom);
                    SuperController.LogMessage(string.Format("JoyhubHaptics: Bound sensors to Atom '{0}' ({1} colliders)", activeTargetAtom.name, activeForwarders.Count));
                }
            }
            catch (Exception ex) {
                SuperController.LogError("BindToTargetAtom Error: " + ex);
            }
        }

        private void SetupBodyPartForwarders(Atom target) {
            try {
                if (target == null) return;

                Rigidbody[] rbs = target.GetComponentsInChildren<Rigidbody>(true);
                foreach (Rigidbody rb in rbs) {
                    if (rb == null || rb.gameObject == null) continue;

                    JoyhubCollisionForwarder fwd = rb.gameObject.GetComponent<JoyhubCollisionForwarder>();
                    if (fwd == null) {
                        fwd = rb.gameObject.AddComponent<JoyhubCollisionForwarder>();
                    }
                    fwd.parentPlugin = this;
                    fwd.bodyPartName = rb.gameObject.name;
                    activeForwarders.Add(fwd);
                }
            }
            catch (Exception ex) {
                SuperController.LogError("SetupBodyPartForwarders Error: " + ex);
            }
        }

        private void CleanupForwarders() {
            try {
                foreach (var fwd in activeForwarders) {
                    if (fwd != null) {
                        Destroy(fwd);
                    }
                }
                activeForwarders.Clear();
            } catch (Exception) { }
        }

        private bool IsBodyPartEnabled(string partName) {
            if (string.IsNullOrEmpty(partName)) return true;
            string lower = partName.ToLower();

            if (lower.Contains("pelvis") || lower.Contains("labia") || lower.Contains("vagina") ||
                lower.Contains("penis") || lower.Contains("testes") || lower.Contains("anus") ||
                lower.Contains("glute") || lower.Contains("genital")) {
                return (filterGenitalsJSON != null && filterGenitalsJSON.val);
            }

            if (lower.Contains("breast") || lower.Contains("nipple") || lower.Contains("chest")) {
                return (filterBreastsJSON != null && filterBreastsJSON.val);
            }

            if (lower.Contains("head") || lower.Contains("mouth") || lower.Contains("lip") ||
                lower.Contains("tongue") || lower.Contains("jaw") || lower.Contains("neck") || lower.Contains("face")) {
                return (filterMouthJSON != null && filterMouthJSON.val);
            }

            if (lower.Contains("hand") || lower.Contains("finger") || lower.Contains("forearm") ||
                lower.Contains("arm") || lower.Contains("wrist")) {
                return (filterHandsJSON != null && filterHandsJSON.val);
            }

            return (filterOtherJSON != null && filterOtherJSON.val);
        }

        public void HandleCollision(string partName, Collision collision) {
            if (enabledJSON == null || !enabledJSON.val) return;
            if (!IsBodyPartEnabled(partName)) return;

            Atom target = activeTargetAtom ?? containingAtom;
            if (collision != null && collision.collider != null && target != null) {
                if (collision.collider.transform.IsChildOf(target.transform)) {
                    return;
                }
            }

            float relVel = (collision != null) ? collision.relativeVelocity.magnitude : 1.0f;
            RegisterActiveTouch(partName, relVel);
        }

        public void HandleTrigger(string partName, Collider other) {
            if (enabledJSON == null || !enabledJSON.val) return;
            if (!IsBodyPartEnabled(partName)) return;

            Atom target = activeTargetAtom ?? containingAtom;
            if (other != null && target != null) {
                if (other.transform.IsChildOf(target.transform)) {
                    return;
                }
            }

            RegisterActiveTouch(partName, 1.0f);
        }

        private void RegisterActiveTouch(string partName, float relativeVelocity) {
            isTouching = true;
            lastTouchTime = Time.time;
            lastTouchedPart = partName;

            // Compute dynamic impact spike
            if (enableImpactForceJSON != null && enableImpactForceJSON.val) {
                float sensMultiplier = (impactSensitivityJSON != null ? impactSensitivityJSON.val : 50f) / 50f;
                float bonus = Mathf.Clamp((relativeVelocity - 0.3f) * 15f * sensMultiplier, 0f, 35f);
                currentImpactBonus = Mathf.Max(currentImpactBonus, bonus);
            }
        }

        public void TriggerBurstAction() {
            burstIntensity = 100f;
        }

        public void StopAllAction() {
            burstIntensity = 0f;
            currentImpactBonus = 0f;
            releaseFadeIntensity = 0f;
            isTouching = false;
            if (manualVibeJSON != null) manualVibeJSON.val = 0f;
            SendRawPacket("STOP");
        }

        private void InitUDP(int port) {
            try {
                if (udpClient != null) {
                    udpClient.Close();
                }
                udpClient = new UdpClient();
                currentPort = port;
            }
            catch (Exception ex) {
                SuperController.LogError("JoyhubHaptics UDP Init Error: " + ex);
            }
        }

        void Start() {
            Atom target = activeTargetAtom ?? containingAtom;
            if (target != null) {
                lastPosition = target.transform.position;
            }
        }

        void Update() {
            try {
                if (enabledJSON == null || !enabledJSON.val) {
                    return;
                }

                if (portJSON != null && (int)portJSON.val != currentPort) {
                    InitUDP((int)portJSON.val);
                }

                bool isFadingEnabled = enableFadeJSON == null || enableFadeJSON.val;

                // Decay impact bonus quickly over time
                if (currentImpactBonus > 0f) {
                    currentImpactBonus = Mathf.Max(0f, currentImpactBonus - Time.deltaTime * 35f);
                }
                if (burstIntensity > 0f) {
                    burstIntensity = Mathf.Max(0f, burstIntensity - Time.deltaTime * 25f);
                }

                // Check touch release timeout (0.08s after last physical contact frame)
                if (Time.time - lastTouchTime > 0.08f) {
                    if (isTouching) {
                        isTouching = false;
                        // Start release fade from current active intensity
                        releaseFadeIntensity = lastFinalIntensity;
                    }
                }

                // Smoothly decay release fade down to 0
                if (!isTouching && releaseFadeIntensity > 0f) {
                    if (!isFadingEnabled) {
                        releaseFadeIntensity = 0f;
                    }
                    else {
                        float decayStep = Time.deltaTime * (touchFadeSpeedJSON != null ? touchFadeSpeedJSON.val : 3f) * 25f;
                        releaseFadeIntensity = Mathf.Max(0f, releaseFadeIntensity - decayStep);
                    }
                }

                if (Time.time - lastSendTime < sendInterval) {
                    return;
                }
                lastSendTime = Time.time;

                float finalIntensity = 0f;
                float beforeBase = (beforeVibeJSON != null) ? beforeVibeJSON.val : 0f;
                bool isFadingActive = !isTouching && isFadingEnabled && (releaseFadeIntensity > beforeBase);

                // Active features selection
                bool activeHeat;
                bool activeLight;
                int activeSuck;
                int activeSqueeze;
                bool activePump;
                float ch2Val, ch3Val, ch4Val;

                if (manualVibeJSON != null && manualVibeJSON.val > 0f) {
                    finalIntensity = manualVibeJSON.val;
                    ch2Val = finalIntensity;
                    ch3Val = 0f;
                    ch4Val = 0f;
                    activeHeat = duringHeatJSON != null && duringHeatJSON.val;
                    activeLight = duringLightJSON != null && duringLightJSON.val;
                    activeSuck = duringSuckJSON != null ? Mathf.RoundToInt(duringSuckJSON.val) : 0;
                    activeSqueeze = duringSqueezeJSON != null ? Mathf.RoundToInt(duringSqueezeJSON.val) : 0;
                    activePump = duringPumpJSON != null && duringPumpJSON.val;
                }
                else if (isTouching) {
                    // === ACTIVELY TOUCHING / CONTINUOUS COLLISION ===
                    // Directly reflects the During Touch slider in REAL TIME!
                    float duringBase = (duringVibeJSON != null) ? duringVibeJSON.val : 15f;

                    // Motion Velocity Tracking
                    float velocityIntensity = 0f;
                    Atom target = activeTargetAtom ?? containingAtom;
                    if (target != null && motionSensitivityJSON != null && motionSensitivityJSON.val > 0f) {
                        Vector3 currentPos = target.transform.position;
                        float speed = (currentPos - lastPosition).magnitude / Mathf.Max(0.0001f, Time.deltaTime);
                        lastPosition = currentPos;
                        velocityIntensity = speed * motionSensitivityJSON.val * 15f;
                    }

                    finalIntensity = duringBase + velocityIntensity + currentImpactBonus + burstIntensity;

                    // Individual channel sliders scale from final intensity
                    float ch2Factor = (duringCh2JSON != null ? duringCh2JSON.val : 15f) / 100f;
                    float ch3Factor = (duringCh3JSON != null ? duringCh3JSON.val : 0f) / 100f;
                    float ch4Factor = (duringCh4JSON != null ? duringCh4JSON.val : 0f) / 100f;
                    ch2Val = finalIntensity * ch2Factor;
                    ch3Val = finalIntensity * ch3Factor;
                    ch4Val = finalIntensity * ch4Factor;

                    // Active hardware features read live during sliders/toggles!
                    activeHeat = duringHeatJSON != null && duringHeatJSON.val;
                    activeLight = duringLightJSON != null && duringLightJSON.val;
                    activeSuck = duringSuckJSON != null ? Mathf.RoundToInt(duringSuckJSON.val) : 0;
                    activeSqueeze = duringSqueezeJSON != null ? Mathf.RoundToInt(duringSqueezeJSON.val) : 0;
                    activePump = duringPumpJSON != null && duringPumpJSON.val;
                }
                else if (isFadingActive) {
                    // === RELEASING & FADING DOWN TO BEFORE-TOUCH LEVEL ===
                    finalIntensity = releaseFadeIntensity;
                    ch2Val = finalIntensity;
                    ch3Val = 0f;
                    ch4Val = 0f;

                    activeHeat = beforeHeatJSON != null && beforeHeatJSON.val;
                    activeLight = beforeLightJSON != null && beforeLightJSON.val;
                    activeSuck = beforeSuckJSON != null ? Mathf.RoundToInt(beforeSuckJSON.val) : 0;
                    activeSqueeze = beforeSqueezeJSON != null ? Mathf.RoundToInt(beforeSqueezeJSON.val) : 0;
                    activePump = beforePumpJSON != null && beforePumpJSON.val;
                }
                else {
                    // === BEFORE TOUCH (IDLE / PROXIMITY) ===
                    if (pulseEnabledJSON != null && pulseEnabledJSON.val) {
                        float minP = pulseMinJSON != null ? pulseMinJSON.val : 15f;
                        float maxP = pulseMaxJSON != null ? pulseMaxJSON.val : 80f;
                        float freq = pulseSpeedJSON != null ? pulseSpeedJSON.val : 1.0f;
                        float wave = (Mathf.Sin(Time.time * freq * Mathf.PI * 2f) + 1f) * 0.5f;
                        finalIntensity = Mathf.Lerp(minP, maxP, wave);
                    }
                    else {
                        finalIntensity = beforeBase;
                    }

                    ch2Val = finalIntensity;
                    ch3Val = 0f;
                    ch4Val = 0f;

                    activeHeat = beforeHeatJSON != null && beforeHeatJSON.val;
                    activeLight = beforeLightJSON != null && beforeLightJSON.val;
                    activeSuck = beforeSuckJSON != null ? Mathf.RoundToInt(beforeSuckJSON.val) : 0;
                    activeSqueeze = beforeSqueezeJSON != null ? Mathf.RoundToInt(beforeSqueezeJSON.val) : 0;
                    activePump = beforePumpJSON != null && beforePumpJSON.val;
                }

                // Apply max speed clamp
                float maxClamp = (maxSpeedClampJSON != null) ? maxSpeedClampJSON.val : 100f;
                finalIntensity = Mathf.Clamp(finalIntensity, 0f, maxClamp);
                lastFinalIntensity = finalIntensity;

                int m1 = Mathf.RoundToInt(finalIntensity);
                int m2 = Mathf.RoundToInt(Mathf.Clamp(ch2Val, 0f, maxClamp));
                int m3 = Mathf.RoundToInt(Mathf.Clamp(ch3Val, 0f, maxClamp));
                int m4 = Mathf.RoundToInt(Mathf.Clamp(ch4Val, 0f, maxClamp));

                // Transmit JSON state
                string jsonPacket = string.Format(
                    "{{\"vibe\":[{0},{1},{2},{3}],\"heat\":{4},\"light\":{5},\"suck\":{6},\"squeeze\":{7},\"pump\":{8}}}",
                    m1, m2, m3, m4,
                    activeHeat ? "true" : "false",
                    activeLight ? "true" : "false",
                    activeSuck, activeSqueeze,
                    activePump ? "true" : "false"
                );

                SendRawPacket(jsonPacket);

                // Update Status Display
                if (statusJSON != null) {
                    string stateStr;
                    if (isTouching) {
                        stateStr = string.Format("[DURING: {0}]", lastTouchedPart);
                    }
                    else if (isFadingActive) {
                        stateStr = "[RELEASE FADING...]";
                    }
                    else {
                        stateStr = "[BEFORE TOUCH (IDLE)]";
                    }

                    string atomName = activeTargetAtom != null ? activeTargetAtom.name : "None";
                    statusJSON.val = string.Format("{0} [{1}] -> Vibe:{2}% | H:{3} L:{4} S:{5} | Port {6}",
                        stateStr,
                        atomName,
                        m1,
                        activeHeat ? "ON" : "OFF",
                        activeLight ? "ON" : "OFF",
                        activeSuck,
                        currentPort);
                }
            }
            catch (Exception e) {
                SuperController.LogError("JoyhubHaptics Update Error: " + e);
            }
        }

        private void SendRawPacket(string payload) {
            try {
                if (udpClient == null) {
                    InitUDP(currentPort);
                }
                byte[] bytes = Encoding.UTF8.GetBytes(payload);
                udpClient.Send(bytes, bytes.Length, host, currentPort);
            }
            catch (Exception) { }
        }

        void OnDestroy() {
            try {
                CleanupForwarders();

                if (udpClient != null) {
                    SendRawPacket("STOP");
                    udpClient.Close();
                    udpClient = null;
                }
            }
            catch (Exception) { }
        }
    }
}
