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

        // Helper forwarder attached to specific body part rigidbodies and colliders
        public class JoyhubCollisionForwarder : MonoBehaviour {
            public JoyhubHaptics parentPlugin;
            public string bodyPartName = "";

            void OnCollisionEnter(Collision collision) {
                if (parentPlugin != null && enabled && collision != null) {
                    parentPlugin.HandleCollisionOrTrigger(bodyPartName, collision.collider, collision.relativeVelocity.magnitude);
                }
            }

            void OnCollisionStay(Collision collision) {
                if (parentPlugin != null && enabled && collision != null) {
                    parentPlugin.HandleCollisionOrTrigger(bodyPartName, collision.collider, collision.relativeVelocity.magnitude);
                }
            }

            void OnTriggerEnter(Collider other) {
                if (parentPlugin != null && enabled && other != null) {
                    parentPlugin.HandleCollisionOrTrigger(bodyPartName, other, 1.0f);
                }
            }

            void OnTriggerStay(Collider other) {
                if (parentPlugin != null && enabled && other != null) {
                    parentPlugin.HandleCollisionOrTrigger(bodyPartName, other, 1.0f);
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

        // Body Part Filters (What triggers haptics)
        private JSONStorableBool filterPenisJSON;
        private JSONStorableBool filterVaginaJSON;
        private JSONStorableBool filterBreastsJSON;
        private JSONStorableBool filterMouthJSON;
        private JSONStorableBool filterHandsJSON;
        private JSONStorableBool filterOtherJSON;

        // Touch Source Filters (What is allowed to touch)
        private JSONStorableStringChooser allowedTouchingAtomJSON;
        private JSONStorableBool sourcePersonsJSON;
        private JSONStorableBool sourceToysJSON;
        private JSONStorableBool sourceCustomObjectsJSON;
        private JSONStorableBool ignoreSelfTouchJSON;
        private JSONStorableBool ignoreClothingHairJSON;
        private JSONStorableBool ignoreEnvironmentJSON;

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
        private string lastTouchingSource = "None";
        private float currentImpactBonus = 0f;
        private float releaseFadeIntensity = 0f;
        private float burstIntensity = 0f;
        private float lastFinalIntensity = 0f;

        private List<JoyhubCollisionForwarder> activeForwarders = new List<JoyhubCollisionForwarder>();
        private HashSet<Collider> myTargetColliders = new HashSet<Collider>();

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

        private List<string> GetAllSceneAtomNames() {
            List<string> list = new List<string> { "Any Allowed Atom" };
            try {
                List<Atom> atoms = SuperController.singleton.GetAtoms();
                if (atoms != null) {
                    foreach (Atom a in atoms) {
                        if (a != null) {
                            list.Add(a.name);
                        }
                    }
                }
            } catch (Exception) { }
            return list;
        }

        public override void Init() {
            try {
                SuperController.LogMessage("Joyhub Advanced Haptics (Dedicated Toys & Objects) Loading...");

                // ==================== LEFT COLUMN (rightSide = false) ====================
                CreateSectionHeader("Master Plugin Control", false);
                enabledJSON = new JSONStorableBool("Master Enabled", true);
                RegisterBool(enabledJSON);
                CreateToggle(enabledJSON, false);

                List<string> persons = GetScenePersonNames();
                string defaultPerson = (containingAtom != null && containingAtom.type == "Person") ? containingAtom.name : (persons.Count > 0 ? persons[0] : "None");
                personSelectorJSON = new JSONStorableStringChooser("Target Person Atom", persons, defaultPerson, "Target Person", OnPersonSelected);
                RegisterStringChooser(personSelectorJSON);
                
                UIDynamicPopup personPopup = CreateScrollablePopup(personSelectorJSON, false);
                if (personPopup != null) {
                    personPopup.popupPanelHeight = 320f;
                }

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
                filterPenisJSON = new JSONStorableBool("Body Part: Male Penis & Glans", true);
                RegisterBool(filterPenisJSON);
                CreateToggle(filterPenisJSON, true);

                filterVaginaJSON = new JSONStorableBool("Body Part: Female Vagina & Labia", false);
                RegisterBool(filterVaginaJSON);
                CreateToggle(filterVaginaJSON, true);

                filterBreastsJSON = new JSONStorableBool("Body Part: Breasts & Chest", false);
                RegisterBool(filterBreastsJSON);
                CreateToggle(filterBreastsJSON, true);

                filterMouthJSON = new JSONStorableBool("Body Part: Mouth, Lips & Head", false);
                RegisterBool(filterMouthJSON);
                CreateToggle(filterMouthJSON, true);

                filterHandsJSON = new JSONStorableBool("Body Part: General Hand & Arm Touch", false);
                RegisterBool(filterHandsJSON);
                CreateToggle(filterHandsJSON, true);

                filterOtherJSON = new JSONStorableBool("Body Part: All Other Parts", false);
                RegisterBool(filterOtherJSON);
                CreateToggle(filterOtherJSON, true);

                CreateSectionHeader("Touch Source & Collider Filters", true);
                List<string> allAtoms = GetAllSceneAtomNames();
                allowedTouchingAtomJSON = new JSONStorableStringChooser("Allowed Touching Atom", allAtoms, "Any Allowed Atom", "Allowed Atom");
                RegisterStringChooser(allowedTouchingAtomJSON);
                
                UIDynamicPopup touchingPopup = CreateScrollablePopup(allowedTouchingAtomJSON, true);
                if (touchingPopup != null) {
                    touchingPopup.popupPanelHeight = 320f;
                }

                sourcePersonsJSON = new JSONStorableBool("Allow: Other Characters (Persons)", true);
                RegisterBool(sourcePersonsJSON);
                CreateToggle(sourcePersonsJSON, true);

                sourceToysJSON = new JSONStorableBool("Allow: Toys & Dildos", true);
                RegisterBool(sourceToysJSON);
                CreateToggle(sourceToysJSON, true);

                sourceCustomObjectsJSON = new JSONStorableBool("Allow: General Props & Custom Objects", false);
                RegisterBool(sourceCustomObjectsJSON);
                CreateToggle(sourceCustomObjectsJSON, true);

                ignoreSelfTouchJSON = new JSONStorableBool("Ignore: Self-Collisions (Own Body)", true);
                RegisterBool(ignoreSelfTouchJSON);
                CreateToggle(ignoreSelfTouchJSON, true);

                ignoreClothingHairJSON = new JSONStorableBool("Ignore: Clothing & Hair Collisions", true);
                RegisterBool(ignoreClothingHairJSON);
                CreateToggle(ignoreClothingHairJSON, true);

                ignoreEnvironmentJSON = new JSONStorableBool("Ignore: Floor & Furniture Collisions", true);
                RegisterBool(ignoreEnvironmentJSON);
                CreateToggle(ignoreEnvironmentJSON, true);

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

                List<string> allAtoms = GetAllSceneAtomNames();
                if (allowedTouchingAtomJSON != null) {
                    allowedTouchingAtomJSON.choices = allAtoms;
                    if (!allAtoms.Contains(allowedTouchingAtomJSON.val)) {
                        allowedTouchingAtomJSON.val = "Any Allowed Atom";
                    }
                }
            } catch (Exception ex) {
                SuperController.LogError("RefreshPersonList Error: " + ex);
            }
        }

        private void AutoConfigureGenderFilters(Atom target) {
            if (target == null) return;
            try {
                bool isMale = false;
                DAZCharacterSelector charSelector = target.GetComponentInChildren<DAZCharacterSelector>();
                if (charSelector != null) {
                    isMale = (charSelector.gender == DAZCharacterSelector.Gender.Male);
                }
                else {
                    // Anatomy fallback check
                    foreach (Collider c in target.GetComponentsInChildren<Collider>(true)) {
                        if (c != null && (c.name.ToLower().Contains("penis") || c.name.ToLower().Contains("autocollidergen"))) {
                            isMale = true;
                            break;
                        }
                    }
                }

                if (isMale) {
                    if (filterPenisJSON != null) filterPenisJSON.val = true;
                    if (filterVaginaJSON != null) filterVaginaJSON.val = false;
                }
                else {
                    if (filterPenisJSON != null) filterPenisJSON.val = false;
                    if (filterVaginaJSON != null) filterVaginaJSON.val = true;
                }
            }
            catch (Exception ex) {
                SuperController.LogError("AutoConfigureGenderFilters Error: " + ex);
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
                    AutoConfigureGenderFilters(activeTargetAtom);
                    SuperController.LogMessage(string.Format("JoyhubHaptics: Bound sensors to Atom '{0}' ({1} colliders/triggers)", activeTargetAtom.name, myTargetColliders.Count));
                }
            }
            catch (Exception ex) {
                SuperController.LogError("BindToTargetAtom Error: " + ex);
            }
        }

        private void SetupBodyPartForwarders(Atom target) {
            try {
                if (target == null) return;
                myTargetColliders.Clear();

                Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
                foreach (Collider col in colliders) {
                    if (col == null || col.gameObject == null) continue;

                    myTargetColliders.Add(col);

                    JoyhubCollisionForwarder fwd = col.gameObject.GetComponent<JoyhubCollisionForwarder>();
                    if (fwd == null) {
                        fwd = col.gameObject.AddComponent<JoyhubCollisionForwarder>();
                    }
                    fwd.parentPlugin = this;
                    fwd.bodyPartName = col.gameObject.name;
                    activeForwarders.Add(fwd);
                }

                Rigidbody[] rbs = target.GetComponentsInChildren<Rigidbody>(true);
                foreach (Rigidbody rb in rbs) {
                    if (rb == null || rb.gameObject == null) continue;

                    Collider c = rb.GetComponent<Collider>();
                    if (c != null) myTargetColliders.Add(c);

                    JoyhubCollisionForwarder fwd = rb.gameObject.GetComponent<JoyhubCollisionForwarder>();
                    if (fwd == null) {
                        fwd = rb.gameObject.AddComponent<JoyhubCollisionForwarder>();
                        fwd.parentPlugin = this;
                        fwd.bodyPartName = rb.gameObject.name;
                        activeForwarders.Add(fwd);
                    }
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
                myTargetColliders.Clear();
            } catch (Exception) { }
        }

        // Dedicated Male Genital Detection (Penis, Glans, Shaft, Testicles, AutoColliderGen1-3)
        private bool IsMaleGenitalName(string name) {
            if (string.IsNullOrEmpty(name)) return false;
            string lower = name.ToLower();

            if (lower == "genesis" || lower == "genesis8" || lower == "genesis8female" ||
                lower == "genesis8male" || lower == "genesis2" || lower == "genesis3" ||
                lower.StartsWith("generic")) return false;

            return lower.Contains("autocollidergen") || lower.StartsWith("gen1") || lower.StartsWith("gen2") ||
                   lower.StartsWith("gen3") || lower.StartsWith("gen4") || lower.StartsWith("gen5") ||
                   lower.StartsWith("gen6") || lower.Contains("male_gen") || lower.Contains("penis") ||
                   lower.Contains("glans") || lower.Contains("gland") || lower.Contains("shaft") ||
                   lower.Contains("testes") || lower.Contains("scrotum") || lower.Contains("foreskin") ||
                   lower.Contains("urethra") || lower.Contains("penishead") || lower.Contains("penisbase") ||
                   lower.Contains("penistip") || lower.Contains("p_base") || lower.Contains("p_mid") ||
                   lower.Contains("p_tip") || lower.Contains("dick") || lower.Contains("cock") || lower.Contains("phallus");
        }

        // Dedicated Female Genital Detection (Vagina, Labia, Clitoris, Pelvic Triggers)
        private bool IsFemaleGenitalName(string name) {
            if (string.IsNullOrEmpty(name)) return false;
            string lower = name.ToLower();

            if (lower == "genesis" || lower == "genesis8" || lower == "genesis8female" ||
                lower == "genesis8male" || lower == "genesis2" || lower == "genesis3" ||
                lower.StartsWith("generic")) return false;

            return lower.Contains("labia") || lower.Contains("vagina") || lower.Contains("clit") ||
                   lower.Contains("cervix") || lower.Contains("gspot") || lower.Contains("pelvis") ||
                   lower.Contains("crotch") || lower.Contains("labiatrig") || lower.Contains("vaginatrig");
        }

        private bool IsHandName(string name) {
            if (string.IsNullOrEmpty(name)) return false;
            string lower = name.ToLower();
            return lower.Contains("hand") || lower.Contains("finger") || lower.Contains("forearm") ||
                   lower.Contains("arm") || lower.Contains("wrist") || lower.Contains("thumb") ||
                   lower.Contains("index") || lower.Contains("mid") || lower.Contains("ring") ||
                   lower.Contains("pinky") || lower.Contains("palm");
        }

        private bool IsToyName(string name) {
            if (string.IsNullOrEmpty(name)) return false;
            string lower = name.ToLower();
            return lower.Contains("dildo") || lower.Contains("toy") || lower.Contains("plug") ||
                   lower.Contains("vibe") || lower.Contains("vibrat") || lower.Contains("fleshlight") ||
                   lower.Contains("stroker") || lower.Contains("egg") || lower.Contains("wand") ||
                   lower.Contains("strap") || lower.Contains("penetrat") || lower.Contains("bead") ||
                   lower.Contains("bullet") || lower.Contains("masturbat");
        }

        private bool IsBodyPartEnabled(string partName, GameObject otherObj) {
            string lower = (partName != null) ? partName.ToLower() : "";
            string otherLower = (otherObj != null) ? otherObj.name.ToLower() : "";

            // 1. MALE PENIS & GLANS
            if (IsMaleGenitalName(partName) || IsMaleGenitalName(otherLower)) {
                if (filterPenisJSON != null && filterPenisJSON.val) {
                    return true;
                }
            }

            // 2. FEMALE VAGINA & LABIA
            if (IsFemaleGenitalName(partName) || IsFemaleGenitalName(otherLower)) {
                if (filterVaginaJSON != null && filterVaginaJSON.val) {
                    return true;
                }
            }

            // 3. BREASTS & CHEST
            if (lower.Contains("breast") || lower.Contains("nipple") || lower.Contains("chest") ||
                lower.Contains("boob") || lower.Contains("areola") || lower.Contains("pec") ||
                otherLower.Contains("breast") || otherLower.Contains("nipple")) {
                if (filterBreastsJSON != null && filterBreastsJSON.val) {
                    return true;
                }
            }

            // 4. MOUTH & HEAD
            if (lower.Contains("mouth") || lower.Contains("lip") || lower.Contains("tongue") ||
                lower.Contains("jaw") || lower.Contains("neck") || lower.Contains("face") ||
                (lower.Contains("head") && !lower.Contains("penis")) ||
                otherLower.Contains("mouth") || otherLower.Contains("lip") || otherLower.Contains("tongue")) {
                if (filterMouthJSON != null && filterMouthJSON.val) {
                    return true;
                }
            }

            // 5. GENERAL HAND & ARM TOUCH
            if (IsHandName(partName) || IsHandName(otherLower)) {
                if (filterHandsJSON != null && filterHandsJSON.val) {
                    return true;
                }
            }

            return (filterOtherJSON != null && filterOtherJSON.val);
        }

        private bool IsTouchingSourceAllowed(Collider otherCollider, GameObject otherObj, out string sourceName) {
            sourceName = "Unknown";
            if (otherObj == null || otherCollider == null) return false;

            // 1. 100% Guaranteed Self-Collision Filter via Collider HashSet
            if (ignoreSelfTouchJSON == null || ignoreSelfTouchJSON.val) {
                if (myTargetColliders.Contains(otherCollider)) {
                    return false; // Direct self-collider on same character!
                }
            }

            Atom target = activeTargetAtom ?? containingAtom;

            // 2. Find parent Atom of colliding object
            Atom incomingAtom = otherObj.GetComponentInParent<Atom>();
            if (incomingAtom == null && otherCollider != null) {
                incomingAtom = otherCollider.GetComponentInParent<Atom>();
            }

            if (incomingAtom == null) {
                Transform p = otherObj.transform;
                while (p != null) {
                    Atom a = p.GetComponent<Atom>();
                    if (a != null) { incomingAtom = a; break; }
                    p = p.parent;
                }
            }

            // Check if incoming object belongs to same target atom
            if (target != null) {
                if (incomingAtom == target || (incomingAtom != null && incomingAtom.name == target.name) || otherObj.transform.IsChildOf(target.transform)) {
                    if (ignoreSelfTouchJSON == null || ignoreSelfTouchJSON.val) {
                        return false; // Skip self-collisions
                    }
                }
            }

            string atomName = (incomingAtom != null) ? incomingAtom.name : otherObj.name;
            string atomType = (incomingAtom != null) ? incomingAtom.type : "Object";
            sourceName = atomName;

            if (allowedTouchingAtomJSON != null && allowedTouchingAtomJSON.val != "Any Allowed Atom") {
                if (atomName != allowedTouchingAtomJSON.val) {
                    return false;
                }
            }

            string objNameLower = otherObj.name.ToLower();
            string atomNameLower = atomName.ToLower();

            if (ignoreClothingHairJSON != null && ignoreClothingHairJSON.val) {
                if (atomType == "Clothing" || atomType == "Hair" ||
                    objNameLower.Contains("cloth") || objNameLower.Contains("hair") ||
                    objNameLower.Contains("pant") || objNameLower.Contains("skirt") ||
                    objNameLower.Contains("bra") || objNameLower.Contains("dress") ||
                    objNameLower.Contains("shoe") || objNameLower.Contains("heel") ||
                    objNameLower.Contains("underwear") || objNameLower.Contains("strap") ||
                    atomNameLower.Contains("cloth") || atomNameLower.Contains("hair")) {
                    return false;
                }
            }

            if (ignoreEnvironmentJSON != null && ignoreEnvironmentJSON.val) {
                if (objNameLower.Contains("floor") || objNameLower.Contains("bed") ||
                    objNameLower.Contains("chair") || objNameLower.Contains("cushion") ||
                    objNameLower.Contains("ground") || objNameLower.Contains("sofa") ||
                    atomNameLower.Contains("furniture") || atomNameLower.Contains("room") ||
                    atomNameLower.Contains("environment")) {
                    return false;
                }
            }

            // Differentiate Person vs. Toy vs. General Custom Object
            if (atomType == "Person") {
                if (sourcePersonsJSON != null && !sourcePersonsJSON.val) return false;
            }
            else if (IsToyName(atomNameLower) || IsToyName(objNameLower)) {
                if (sourceToysJSON != null && !sourceToysJSON.val) return false;
            }
            else {
                if (sourceCustomObjectsJSON != null && !sourceCustomObjectsJSON.val) return false;
            }

            return true;
        }

        public void HandleCollisionOrTrigger(string partName, Collider otherCollider, float relativeVelocity) {
            if (enabledJSON == null || !enabledJSON.val) return;
            if (otherCollider == null) return;

            GameObject otherObj = otherCollider.gameObject;
            string sourceName;
            if (!IsTouchingSourceAllowed(otherCollider, otherObj, out sourceName)) {
                return;
            }

            if (!IsBodyPartEnabled(partName, otherObj)) return;

            RegisterActiveTouch(partName, otherObj.name, sourceName, relativeVelocity);
        }

        private void RegisterActiveTouch(string partName, string colliderName, string sourceName, float relativeVelocity) {
            isTouching = true;
            lastTouchTime = Time.time;
            lastTouchedPart = string.Format("{0} -> {1}", partName, colliderName);
            lastTouchingSource = sourceName;

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
                udpClient = new LoveUdpClientWrapper();
                currentPort = port;
            }
            catch (Exception ex) {
                SuperController.LogError("JoyhubHaptics UDP Init Error: " + ex);
            }
        }

        private class LoveUdpClientWrapper : UdpClient { }

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

                if (currentImpactBonus > 0f) {
                    currentImpactBonus = Mathf.Max(0f, currentImpactBonus - Time.deltaTime * 35f);
                }
                if (burstIntensity > 0f) {
                    burstIntensity = Mathf.Max(0f, burstIntensity - Time.deltaTime * 25f);
                }

                if (Time.time - lastTouchTime > 0.08f) {
                    if (isTouching) {
                        isTouching = false;
                        releaseFadeIntensity = lastFinalIntensity;
                    }
                }

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
                    float duringBase = (duringVibeJSON != null) ? duringVibeJSON.val : 15f;

                    float velocityIntensity = 0f;
                    Atom target = activeTargetAtom ?? containingAtom;
                    if (target != null && motionSensitivityJSON != null && motionSensitivityJSON.val > 0f) {
                        Vector3 currentPos = target.transform.position;
                        float speed = (currentPos - lastPosition).magnitude / Mathf.Max(0.0001f, Time.deltaTime);
                        lastPosition = currentPos;
                        velocityIntensity = speed * motionSensitivityJSON.val * 15f;
                    }

                    finalIntensity = duringBase + velocityIntensity + currentImpactBonus + burstIntensity;

                    float ch2Factor = (duringCh2JSON != null ? duringCh2JSON.val : 15f) / 100f;
                    float ch3Factor = (duringCh3JSON != null ? duringCh3JSON.val : 0f) / 100f;
                    float ch4Factor = (duringCh4JSON != null ? duringCh4JSON.val : 0f) / 100f;
                    ch2Val = finalIntensity * ch2Factor;
                    ch3Val = finalIntensity * ch3Factor;
                    ch4Val = finalIntensity * ch4Factor;

                    activeHeat = duringHeatJSON != null && duringHeatJSON.val;
                    activeLight = duringLightJSON != null && duringLightJSON.val;
                    activeSuck = duringSuckJSON != null ? Mathf.RoundToInt(duringSuckJSON.val) : 0;
                    activeSqueeze = duringSqueezeJSON != null ? Mathf.RoundToInt(duringSqueezeJSON.val) : 0;
                    activePump = duringPumpJSON != null && duringPumpJSON.val;
                }
                else if (isFadingActive) {
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

                float maxClamp = (maxSpeedClampJSON != null) ? maxSpeedClampJSON.val : 100f;
                finalIntensity = Mathf.Clamp(finalIntensity, 0f, maxClamp);
                lastFinalIntensity = finalIntensity;

                int m1 = Mathf.RoundToInt(finalIntensity);
                int m2 = Mathf.RoundToInt(Mathf.Clamp(ch2Val, 0f, maxClamp));
                int m3 = Mathf.RoundToInt(Mathf.Clamp(ch3Val, 0f, maxClamp));
                int m4 = Mathf.RoundToInt(Mathf.Clamp(ch4Val, 0f, maxClamp));

                string jsonPacket = string.Format(
                    "{{\"vibe\":[{0},{1},{2},{3}],\"heat\":{4},\"light\":{5},\"suck\":{6},\"squeeze\":{7},\"pump\":{8}}}",
                    m1, m2, m3, m4,
                    activeHeat ? "true" : "false",
                    activeLight ? "true" : "false",
                    activeSuck, activeSqueeze,
                    activePump ? "true" : "false"
                );

                SendRawPacket(jsonPacket);

                if (statusJSON != null) {
                    string stateStr;
                    if (isTouching) {
                        stateStr = string.Format("[DURING: {0} ({1})]", lastTouchedPart, lastTouchingSource);
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
