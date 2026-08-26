# Virt-a-Mate (VaM) Joyhub Bluetooth Haptics Integration 🎮💥

Full wireless Bluetooth Low Energy (BLE) interactive haptics integration for **Virt-a-Mate (VaM)** and **Joyhub** compatible devices.

---

## 🌟 Features

* **4-Channel Multi-Motor Vibration**: Primary motor with individual Channel 2, 3, and 4 power scaling.
* **Physics Touch & Collision Triggers**: Real-time physics contacts and penetrations dynamically boost vibration intensity.
* **Before-Touch vs. During-Touch States**:
  * *Before Touch (Idle)*: Configurable baseline idle/anticipation hum.
  * *During Touch*: Target vibration speed when physical contact occurs.
  * *Release Fade*: Smooth decay when contact breaks.
* **Targeted Body Part Filtering**: Selectively enable/disable haptics for:
  * Genitals & Pelvis (Vagina, Labia, Penis, Anus, Pelvis)
  * Breasts & Chest
  * Mouth, Lips & Head
  * Hands & Arms
  * Other Body Parts
* **Hardware Feature Controls**:
  * 🔥 **Heating Control** (On / Off)
  * 💡 **LED Lighting** (On / Off)
  * 🌀 **Suction Intensity** (Levels 0 – 5)
  * 🤏 **Squeezing / Clamping** (Levels 0 – 5)
  * 💦 **Fluid Pump** (On / Off)
* **Automated Pulse Waveform Generator**: Configurable sine wave pulsing (Min %, Max %, Frequency Hz).
* **VaM Action & Timeline Integration**: TriggerBurst (100% burst) and StopAll actions callable by any VaM Trigger or Timeline animation.
* **2-Column Clean In-Game Dashboard**: Organized dual-column UI inside Virt-a-Mate.
* **Device Memory & Auto-Reconnect**: Memorizes your paired device and automatically reconnects in background on signal drops.

---

## 📁 Repository Structure

`
VaM-Joyhub-Haptics/
├── Custom/
│   └── Scripts/
│       └── Joyhub/
│           └── JoyhubHaptics.cs         # Virt-a-Mate C# Plugin (MVRScript)
├── joyhub_vam_bridge.py                 # Python BLE Bluetooth Bridge
├── Joyhub_VaM_Bridge.bat                # 1-Click Launcher for Bridge
├── joyhub_controller.py                 # Standalone Interactive CLI Controller
├── JoyhubController.bat                 # Standalone CLI Launcher
├── requirements.txt                     # Python Dependencies (bleak)
└── README.md
`

---

## 🚀 Installation & Quick Start

### 1. Prerequisites
* **Python 3.10+** installed on Windows.
* Install required Python library:
  `powershell
  pip install -r requirements.txt
  `

### 2. Install the VaM Plugin
Copy the Custom/Scripts/Joyhub/ folder into your Virt-a-Mate root directory:
`
<VaM_Directory>/Custom/Scripts/Joyhub/JoyhubHaptics.cs
`

### 3. Connect & Play
1. Turn on your Joyhub Bluetooth device.
2. Double-click **Joyhub_VaM_Bridge.bat** (or run python joyhub_vam_bridge.py).
3. Select your device from the list on first launch (it will be remembered automatically afterwards).
4. Launch **Virt-a-Mate**, select your **Person** or **Toy** Atom, go to the **Plugins** tab, click **Add Plugin**, and browse to Custom/Scripts/Joyhub/JoyhubHaptics.cs.
5. Enjoy real-time, zero-latency motion and collision haptics!

---

## 🛠️ Standalone CLI Controller

If you want to control or test your device outside of VaM:
* Run JoyhubController.bat (or python joyhub_controller.py) for a rich interactive terminal shell (ibe <0-100>, pulse, heat on/off, light on/off, suck 1-5, squeeze 1-5, pump on/off, stop, aw <hex>).

---

## 📄 License
MIT License.
