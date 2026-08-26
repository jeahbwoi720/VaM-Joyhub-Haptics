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
        private JSONStorableFloat beforeTouchVibeJSON;
        private JSONStorableFloat duringTouchVibeJSON;
        private JSONStorableFloat ch2MultiplierJSON;
        private JSONStorableFloat ch3MultiplierJSON;
        private JSONStorableFloat ch4MultiplierJSON;

        private JSONStorableBool pulseEnabledJSON;
        private JSONStorableFloat pulseMinJSON;
        private JSONStorableFloat pulseMaxJSON;
        private JSONStorableFloat pulseSpeedJSON;

        private JSONStorableFloat motionSensitivityJSON;
        private JSONStorableFloat touchFadeSpeedJSON;
        private JSONStorableFloat maxSpeedClampJSON;
        private JSONStorableFloat manualVibeJSON;

        // ==================== RIGHT COLUMN UI CONTROLS ====================
        private JSONStorableBool heatJSON;
        private JSONStorableBool lightJSON;
        private JSONStorableFloat suckJSON;
        private JSONStorableFloat squeezeJSON;
        private JSONStorableBool pumpJSON;

        private JSONStorableBool filterGenitalsJSON;
        private JSONStorableBool filterBreastsJSON;
        private JSONStorableBool filterMouthJSON;
        private JSONStorableBool filterHandsJSON;
        private JSONStorableBool filterOtherJSON;

        private JSONStorableFloat portJSON;
        private JSONStorableString statusJSON;
        private JSONStorableAction burstAction;
        private JSONStorableAction stopAction;

        // Network & Timing
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

        // Feature dirty states
        private bool lastHeat = false;
        private bool lastLight = false;
        private int lastSuck = 0;
        private int lastSqueeze = 0;
        private bool lastPump = false;

        private List<JoyhubCollisionForwarder> activeForwarders = new List<JoyhubCollisionForwarder>();

        public override void Init() {
            try {
                SuperController.LogMessage("Joyhub Full Haptics Dashboard Loading...");

                // ==================== LEFT COLUMN (rightSide = false) ====================
                enabledJSON = new JSONStorableBool("Master Enabled", true);
                RegisterBool(enabledJSON);
                CreateToggle(enabledJSON, false);

                beforeTouchVibeJSON = new JSONStorableFloat("Before Touch Vibe % (Idle)", 0f, 0f, 100f, true);
                RegisterFloat(beforeTouchVibeJSON);
                CreateSlider(beforeTouchVibeJSON, false);

                duringTouchVibeJSON = new JSONStorableFloat("During Touch Vibe % (Ch 1)", 80f, 0f, 100f, true);
                RegisterFloat(duringTouchVibeJSON);
                CreateSlider(duringTouchVibeJSON, false);

                ch2MultiplierJSON = new JSONStorableFloat("Motor 2 (Ch 2) Power %", 80f, 0f, 100f, true);
                RegisterFloat(ch2MultiplierJSON);
                CreateSlider(ch2MultiplierJSON, false);

                ch3MultiplierJSON = new JSONStorableFloat("Motor 3 (Ch 3) Power %", 0f, 0f, 100f, true);
                RegisterFloat(ch3MultiplierJSON);
                CreateSlider(ch3MultiplierJSON, false);

                ch4MultiplierJSON = new JSONStorableFloat("Motor 4 (Ch 4) Power %", 0f, 0f, 100f, true);
                RegisterFloat(ch4MultiplierJSON);
                CreateSlider(ch4MultiplierJSON, false);

                pulseEnabledJSON = new JSONStorableBool("Pulse Waveform Enabled", false);
                RegisterBool(pulseEnabledJSON);
                CreateToggle(pulseEnabledJSON, false);

                pulseMinJSON = new JSONStorableFloat("Pulse Min Vibe %", 20f, 0f, 100f, true);
                RegisterFloat(pulseMinJSON);
                CreateSlider(pulseMinJSON, false);

                pulseMaxJSON = new JSONStorableFloat("Pulse Max Vibe %", 90f, 0f, 100f, true);
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
                heatJSON = new JSONStorableBool("Device Heating [On/Off]", false, SyncFeaturesBool);
                RegisterBool(heatJSON);
                CreateToggle(heatJSON, true);

                lightJSON = new JSONStorableBool("Device LED Lights [On/Off]", false, SyncFeaturesBool);
                RegisterBool(lightJSON);
                CreateToggle(lightJSON, true);

                suckJSON = new JSONStorableFloat("Suction Level (0-5)", 0f, 0f, 5f, true);
                RegisterFloat(suckJSON);
                CreateSlider(suckJSON, true);

                squeezeJSON = new JSONStorableFloat("Squeezing Level (0-5)", 0f, 0f, 5f, true);
                RegisterFloat(squeezeJSON);
                CreateSlider(squeezeJSON, true);

                pumpJSON = new JSONStorableBool("Fluid Pump [On/Off]", false, SyncFeaturesBool);
                RegisterBool(pumpJSON);
                CreateToggle(pumpJSON, true);

                filterGenitalsJSON = new JSONStorableBool("Body Part: Genitals & Pelvis", true);
                RegisterBool(filterGenitalsJSON);
                CreateToggle(filterGenitalsJSON, true);

                filterBreastsJSON = new JSONStorableBool("Body Part: Breasts & Chest", true);
                RegisterBool(filterBreastsJSON);
                CreateToggle(filterBreastsJSON, true);

                filterMouthJSON = new JSONStorableBool("Body Part: Mouth, Lips & Head", true);
                RegisterBool(filterMouthJSON);
                CreateToggle(filterMouthJSON, true);

                filterHandsJSON = new JSONStorableBool("Body Part: Hands & Arms", true);
                RegisterBool(filterHandsJSON);
                CreateToggle(filterHandsJSON, true);

                filterOtherJSON = new JSONStorableBool("Body Part: All Other Parts", false);
                RegisterBool(filterOtherJSON);
                CreateToggle(filterOtherJSON, true);

                portJSON = new JSONStorableFloat("Bridge Port", 8888f, 1000f, 65535f, false);
                RegisterFloat(portJSON);
                CreateSlider(portJSON, true);

                // Actions & Buttons
                burstAction = new JSONStorableAction("TriggerBurst", TriggerBurstAction);
                RegisterAction(burstAction);

                stopAction = new JSONStorableAction("StopAll", StopAllAction);
                RegisterAction(stopAction);

                UIDynamicButton burstBtn = CreateButton("Test Trigger Burst (100%)", true);
                if (burstBtn != null && burstBtn.button != null) {
                    burstBtn.button.onClick.AddListener(TriggerBurstAction);
                }

                UIDynamicButton stopBtn = CreateButton("STOP ALL MOTORS & FEATURES", true);
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
            float targetValue = (duringTouchVibeJSON != null ? duringTouchVibeJSON.val : 80f) + impactBonus;

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
            if (heatJSON != null) heatJSON.val = false;
            if (lightJSON != null) lightJSON.val = false;
            if (suckJSON != null) suckJSON.val = 0f;
            if (squeezeJSON != null) squeezeJSON.val = 0f;
            if (pumpJSON != null) pumpJSON.val = false;
            if (pulseEnabledJSON != null) pulseEnabledJSON.val = false;

            SendRawPacket("STOP");
        }

        private void SyncFeaturesBool(bool val) {
            // Triggered automatically on UI changes
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
                float decayStep = Time.deltaTime * touchFadeSpeedJSON.val * 25f;
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

                // 1. Manual Override Check
                if (manualVibeJSON != null && manualVibeJSON.val > 0f) {
                    finalIntensity = manualVibeJSON.val;
                }
                // 2. Pulse Waveform Generator
                else if (pulseEnabledJSON != null && pulseEnabledJSON.val) {
                    float minP = pulseMinJSON != null ? pulseMinJSON.val : 20f;
                    float maxP = pulseMaxJSON != null ? pulseMaxJSON.val : 90f;
                    float freq = pulseSpeedJSON != null ? pulseSpeedJSON.val : 1.0f;
                    float wave = (Mathf.Sin(Time.time * freq * Mathf.PI * 2f) + 1f) * 0.5f;
                    finalIntensity = Mathf.Lerp(minP, maxP, wave);
                }
                // 3. Normal Touch + Motion Velocity
                else {
                    float beforeTouchBase = (beforeTouchVibeJSON != null) ? beforeTouchVibeJSON.val : 0f;

                    float velocityIntensity = 0f;
                    if (containingAtom != null && motionSensitivityJSON != null && motionSensitivityJSON.val > 0f) {
                        Vector3 currentPos = containingAtom.transform.position;
                        float speed = (currentPos - lastPosition).magnitude / Mathf.Max(0.0001f, Time.deltaTime);
                        lastPosition = currentPos;
                        velocityIntensity = speed * motionSensitivityJSON.val * 15f;
                    }

                    float activeTouchVal = Mathf.Max(touchIntensity, burstIntensity);
                    finalIntensity = Mathf.Max(beforeTouchBase + velocityIntensity, activeTouchVal);
                }

                // Apply max clamp
                float maxClamp = (maxSpeedClampJSON != null) ? maxSpeedClampJSON.val : 100f;
                finalIntensity = Mathf.Clamp(finalIntensity, 0f, maxClamp);

                // Multi-Channel Speeds
                int m1 = Mathf.RoundToInt(finalIntensity);
                int m2 = Mathf.RoundToInt(finalIntensity * (ch2MultiplierJSON != null ? ch2MultiplierJSON.val / 100f : 0.8f));
                int m3 = Mathf.RoundToInt(finalIntensity * (ch3MultiplierJSON != null ? ch3MultiplierJSON.val / 100f : 0f));
                int m4 = Mathf.RoundToInt(finalIntensity * (ch4MultiplierJSON != null ? ch4MultiplierJSON.val / 100f : 0f));

                // Send Full JSON State Packet to Bridge
                bool curHeat = heatJSON != null && heatJSON.val;
                bool curLight = lightJSON != null && lightJSON.val;
                int curSuck = suckJSON != null ? Mathf.RoundToInt(suckJSON.val) : 0;
                int curSqueeze = squeezeJSON != null ? Mathf.RoundToInt(squeezeJSON.val) : 0;
                bool curPump = pumpJSON != null && pumpJSON.val;

                string jsonPacket = string.Format(
                    "{{\"vibe\":[{0},{1},{2},{3}],\"heat\":{4},\"light\":{5},\"suck\":{6},\"squeeze\":{7},\"pump\":{8}}}",
                    m1, m2, m3, m4,
                    curHeat ? "true" : "false",
                    curLight ? "true" : "false",
                    curSuck, curSqueeze,
                    curPump ? "true" : "false"
                );

                SendRawPacket(jsonPacket);

                // Update Status Display
                if (statusJSON != null) {
                    string touchIndicator = (isTouching || touchIntensity > 5f) ? string.Format(" [TOUCH: {0}]", lastTouchedPart) : " [IDLE]";
                    statusJSON.val = string.Format("Vibe: {0}%{1} | H:{2} L:{3} S:{4} | Port {5}",
                        m1, touchIndicator,
                        curHeat ? "ON" : "OFF",
                        curLight ? "ON" : "OFF",
                        curSuck,
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
