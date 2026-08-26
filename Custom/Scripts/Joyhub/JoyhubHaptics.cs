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
                    parentPlugin.HandleTouch(bodyPartName, collision.relativeVelocity.magnitude);
                }
            }

            void OnCollisionStay(Collision collision) {
                if (parentPlugin != null && enabled) {
                    parentPlugin.HandleTouch(bodyPartName, collision.relativeVelocity.magnitude);
                }
            }

            void OnTriggerEnter(Collider other) {
                if (parentPlugin != null && enabled) {
                    parentPlugin.HandleTouch(bodyPartName, 1.0f);
                }
            }

            void OnTriggerStay(Collider other) {
                if (parentPlugin != null && enabled) {
                    parentPlugin.HandleTouch(bodyPartName, 1.0f);
                }
            }
        }

        // ==================== LEFT COLUMN UI CONTROLS ====================
        private JSONStorableBool enabledJSON;

        // Before Touch (Idle) Feature Controls
        private JSONStorableFloat beforeVibeJSON;
        private JSONStorableBool beforeHeatJSON;
        private JSONStorableBool beforeLightJSON;
        private JSONStorableFloat beforeSuckJSON;
        private JSONStorableFloat beforeSqueezeJSON;
        private JSONStorableBool beforePumpJSON;

        // Dynamics & Waveforms
        private JSONStorableBool pulseEnabledJSON;
        private JSONStorableFloat pulseMinJSON;
        private JSONStorableFloat pulseMaxJSON;
        private JSONStorableFloat pulseSpeedJSON;
        private JSONStorableFloat motionSensitivityJSON;
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

        private Vector3 lastPosition;
        private float lastSendTime = 0f;
        private float sendInterval = 0.04f; // 25 Hz updates

        private bool isTouching = false;
        private float touchIntensity = 0f;
        private float lastTouchTime = 0f;
        private string lastTouchedPart = "None";
        private float burstIntensity = 0f;

        private List<JoyhubCollisionForwarder> activeForwarders = new List<JoyhubCollisionForwarder>();

        // Helper to create clean visual section headers with Unity Rich Text
        private void CreateSectionHeader(string title, string hexColor, bool rightSide) {
            try {
                UIDynamic spacer = CreateSpacer(rightSide);
                if (spacer != null) spacer.height = 12f;

                string formatted = string.Format("<b><color={0}>━━━ {1} ━━━</color></b>", hexColor, title.ToUpper());
                JSONStorableString headerParam = new JSONStorableString(title + "_hdr", formatted);
                UIDynamicTextField tf = CreateTextField(headerParam, rightSide);
                if (tf != null) {
                    tf.height = 36f;
                }
            } catch (Exception) { }
        }

        public override void Init() {
            try {
                SuperController.LogMessage("Joyhub Advanced Haptics Dashboard Loading...");

                // ==================== LEFT COLUMN (rightSide = false) ====================
                CreateSectionHeader("Master Plugin Control", "#33CCFF", false);
                enabledJSON = new JSONStorableBool("Master Enabled", true);
                RegisterBool(enabledJSON);
                CreateToggle(enabledJSON, false);

                CreateSectionHeader("Before Touch (Idle / Approach)", "#44BBFF", false);
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

                CreateSectionHeader("Dynamics & Waveforms", "#CC77FF", false);
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
                CreateSectionHeader("During Touch (Active Contact)", "#FF6688", true);
                duringVibeJSON = new JSONStorableFloat("During Touch: Vibe % (Ch 1)", 85f, 0f, 100f, true);
                RegisterFloat(duringVibeJSON);
                CreateSlider(duringVibeJSON, true);

                duringCh2JSON = new JSONStorableFloat("During Touch: Motor 2 (Ch 2) %", 85f, 0f, 100f, true);
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

                CreateSectionHeader("Targeted Body Parts", "#FFAA33", true);
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

                CreateSectionHeader("Actions & Telemetry", "#33DD88", true);
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
                SetupBodyPartForwarders();
            }
            catch (Exception ex) {
                SuperController.LogError("JoyhubHaptics Init Error: " + ex);
            }
        }

        private void SetupBodyPartForwarders() {
            try {
                if (containingAtom == null) return;

                Rigidbody[] rbs = containingAtom.GetComponentsInChildren<Rigidbody>(true);
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
                SuperController.LogMessage(string.Format("JoyhubHaptics: Mapped {0} body part colliders", activeForwarders.Count));
            }
            catch (Exception ex) {
                SuperController.LogError("SetupBodyPartForwarders Error: " + ex);
            }
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

        public void HandleTouch(string partName, float relativeVelocity) {
            if (enabledJSON == null || !enabledJSON.val) return;
            if (!IsBodyPartEnabled(partName)) return;

            isTouching = true;
            lastTouchTime = Time.time;
            lastTouchedPart = partName;

            float impactBonus = Mathf.Clamp((relativeVelocity - 0.5f) * 10f, 0f, 20f);
            float targetValue = (duringVibeJSON != null ? duringVibeJSON.val : 85f) + impactBonus;

            touchIntensity = Mathf.Max(touchIntensity, Mathf.Clamp(targetValue, 0f, 100f));
        }

        public void TriggerBurstAction() {
            burstIntensity = 100f;
        }

        public void StopAllAction() {
            burstIntensity = 0f;
            touchIntensity = 0f;
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
            if (containingAtom != null) {
                lastPosition = containingAtom.transform.position;
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

                // Check touch release timeout
                if (Time.time - lastTouchTime > 0.12f) {
                    isTouching = false;
                }

                // Decay touch and burst
                float decayStep = Time.deltaTime * (touchFadeSpeedJSON != null ? touchFadeSpeedJSON.val : 3f) * 25f;
                if (!isTouching && touchIntensity > 0f) {
                    touchIntensity = Mathf.Max(0f, touchIntensity - decayStep);
                }
                if (burstIntensity > 0f) {
                    burstIntensity = Mathf.Max(0f, burstIntensity - decayStep);
                }

                if (Time.time - lastSendTime < sendInterval) {
                    return;
                }
                lastSendTime = Time.time;

                float finalIntensity = 0f;
                bool isCurrentlyActiveTouch = isTouching || touchIntensity > 5f || burstIntensity > 5f;

                // Active features selection (Before vs. During Touch)
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
                else if (isCurrentlyActiveTouch) {
                    // DURING TOUCH STATE
                    float duringBase = duringVibeJSON != null ? duringVibeJSON.val : 85f;

                    // Motion Velocity Tracking
                    float velocityIntensity = 0f;
                    if (containingAtom != null && motionSensitivityJSON != null && motionSensitivityJSON.val > 0f) {
                        Vector3 currentPos = containingAtom.transform.position;
                        float speed = (currentPos - lastPosition).magnitude / Mathf.Max(0.0001f, Time.deltaTime);
                        lastPosition = currentPos;
                        velocityIntensity = speed * motionSensitivityJSON.val * 15f;
                    }

                    float maxTouchVal = Mathf.Max(touchIntensity, burstIntensity);
                    finalIntensity = Mathf.Max(duringBase + velocityIntensity, maxTouchVal);

                    ch2Val = duringCh2JSON != null ? duringCh2JSON.val : finalIntensity;
                    ch3Val = duringCh3JSON != null ? duringCh3JSON.val : 0f;
                    ch4Val = duringCh4JSON != null ? duringCh4JSON.val : 0f;

                    activeHeat = duringHeatJSON != null && duringHeatJSON.val;
                    activeLight = duringLightJSON != null && duringLightJSON.val;
                    activeSuck = duringSuckJSON != null ? Mathf.RoundToInt(duringSuckJSON.val) : 0;
                    activeSqueeze = duringSqueezeJSON != null ? Mathf.RoundToInt(duringSqueezeJSON.val) : 0;
                    activePump = duringPumpJSON != null && duringPumpJSON.val;
                }
                else {
                    // BEFORE TOUCH (IDLE / PROXIMITY STATE)
                    if (pulseEnabledJSON != null && pulseEnabledJSON.val) {
                        float minP = pulseMinJSON != null ? pulseMinJSON.val : 15f;
                        float maxP = pulseMaxJSON != null ? pulseMaxJSON.val : 80f;
                        float freq = pulseSpeedJSON != null ? pulseSpeedJSON.val : 1.0f;
                        float wave = (Mathf.Sin(Time.time * freq * Mathf.PI * 2f) + 1f) * 0.5f;
                        finalIntensity = Mathf.Lerp(minP, maxP, wave);
                    }
                    else {
                        finalIntensity = beforeVibeJSON != null ? beforeVibeJSON.val : 0f;
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
                    string stateStr = isCurrentlyActiveTouch ? string.Format("[DURING: {0}]", lastTouchedPart) : "[BEFORE TOUCH (IDLE)]";
                    statusJSON.val = string.Format("{0} -> Vibe:{1}% | H:{2} L:{3} S:{4} | Port {5}",
                        stateStr,
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
                foreach (var fwd in activeForwarders) {
                    if (fwd != null) {
                        Destroy(fwd);
                    }
                }
                activeForwarders.Clear();

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
