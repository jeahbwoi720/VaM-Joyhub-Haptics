import asyncio
import socket
import sys
import time
import json
from pathlib import Path
from bleak import BleakScanner, BleakClient

# ==================== Config Persistence ====================
CONFIG_FILE = Path(__file__).parent / "joyhub_config.json"

def load_saved_device():
    if CONFIG_FILE.exists():
        try:
            with open(CONFIG_FILE, "r", encoding="utf-8") as f:
                return json.load(f)
        except Exception:
            return None
    return None

def save_device(address: str, name: str):
    try:
        with open(CONFIG_FILE, "w", encoding="utf-8") as f:
            json.dump({"address": address, "name": name}, f, indent=2)
    except Exception as e:
        print(f"[Warning] Could not save device config: {e}")

# ==================== BLE Command Framing ====================
CMD_HEADER = "a0"
CMD_TRAILER_AA = "aa"
CMD_TRAILER_FF = "ff"

OP_MOTOR_WAVE = "03"
OP_HEATING = "04"
OP_SUCKING = "07"
OP_SQUEEZING = "0d"
OP_LIGHTING = "14"
OP_SQUIRTING = "24"
OP_RIPPLING = "25"
OP_RANGE = "26"

def build_vibe_packet(speeds: list[int]) -> bytes:
    padded = (speeds + [0, 0, 0, 0])[:4]
    hex_payload = "".join(f"{max(0, min(255, s)):02x}" for s in padded)
    return bytes.fromhex(f"{CMD_HEADER}{OP_MOTOR_WAVE}{hex_payload}{CMD_TRAILER_AA}")

def build_feature_packet(op: str, state_on: bool, level: int = 1) -> bytes:
    if state_on and level > 0:
        return bytes.fromhex(f"{CMD_HEADER}{op}0100{level:02x}{CMD_TRAILER_FF}")
    return bytes.fromhex(f"{CMD_HEADER}{op}00000000")

# ==================== Shared State ====================
current_vibe_speeds = [0, 0, 0, 0]
feature_queue = []
last_packet_time = 0
packet_count = 0

heat_state = False
light_state = False
suck_level = 0
squeeze_level = 0
pump_state = False

class VamUdpProtocol(asyncio.DatagramProtocol):
    def datagram_received(self, data: bytes, addr):
        global current_vibe_speeds, feature_queue, last_packet_time, packet_count
        global heat_state, light_state, suck_level, squeeze_level, pump_state
        try:
            text = data.decode("utf-8", errors="ignore").strip()
            last_packet_time = time.time()
            packet_count += 1

            # Format 1: JSON packet
            if text.startswith("{"):
                d = json.loads(text)
                if "vibe" in d:
                    v = d["vibe"]
                    if isinstance(v, list):
                        current_vibe_speeds = [int(x * 255 / 100) for x in (v + [0, 0, 0, 0])[:4]]
                    else:
                        val = int(v * 255 / 100)
                        current_vibe_speeds = [val, val, 0, 0]

                if "heat" in d and d["heat"] != heat_state:
                    heat_state = bool(d["heat"])
                    feature_queue.append(build_feature_packet(OP_HEATING, heat_state))

                if "light" in d and d["light"] != light_state:
                    light_state = bool(d["light"])
                    feature_queue.append(build_feature_packet(OP_LIGHTING, light_state))

                if "suck" in d and d["suck"] != suck_level:
                    suck_level = int(d["suck"])
                    feature_queue.append(build_feature_packet(OP_SUCKING, suck_level > 0, suck_level))

                if "squeeze" in d and d["squeeze"] != squeeze_level:
                    squeeze_level = int(d["squeeze"])
                    feature_queue.append(build_feature_packet(OP_SQUEEZING, squeeze_level > 0, squeeze_level))

                if "pump" in d and d["pump"] != pump_state:
                    pump_state = bool(d["pump"])
                    feature_queue.append(build_feature_packet(OP_SQUIRTING, pump_state))

            # Format 2: Direct Command strings
            elif text.startswith("VIBE:"):
                payload = text[5:].strip()
                if "," in payload:
                    parts = payload.split(",")
                    current_vibe_speeds = [int(int(p.strip()) * 255 / 100) for p in parts if p.strip().isdigit()]
                elif payload.isdigit():
                    val = int(int(payload) * 255 / 100)
                    current_vibe_speeds = [val, val, 0, 0]

            elif text.startswith("HEAT:"):
                on = text[5:].strip() in ["1", "true", "on", "ON"]
                if on != heat_state:
                    heat_state = on
                    feature_queue.append(build_feature_packet(OP_HEATING, heat_state))

            elif text.startswith("LIGHT:"):
                on = text[6:].strip() in ["1", "true", "on", "ON"]
                if on != light_state:
                    light_state = on
                    feature_queue.append(build_feature_packet(OP_LIGHTING, light_state))

            elif text.startswith("SUCK:"):
                lvl = int(text[5:].strip()) if text[5:].strip().isdigit() else 0
                if lvl != suck_level:
                    suck_level = lvl
                    feature_queue.append(build_feature_packet(OP_SUCKING, suck_level > 0, suck_level))

            elif text.startswith("SQUEEZE:"):
                lvl = int(text[8:].strip()) if text[8:].strip().isdigit() else 0
                if lvl != squeeze_level:
                    squeeze_level = lvl
                    feature_queue.append(build_feature_packet(OP_SQUEEZING, squeeze_level > 0, squeeze_level))

            elif text.startswith("PUMP:"):
                on = text[5:].strip() in ["1", "true", "on", "ON"]
                if on != pump_state:
                    pump_state = on
                    feature_queue.append(build_feature_packet(OP_SQUIRTING, pump_state))

            elif text.upper() == "STOP":
                current_vibe_speeds = [0, 0, 0, 0]
                feature_queue.append(build_feature_packet(OP_HEATING, False))
                feature_queue.append(build_feature_packet(OP_LIGHTING, False))
                feature_queue.append(build_feature_packet(OP_SUCKING, False))
                feature_queue.append(build_feature_packet(OP_SQUEEZING, False))
                feature_queue.append(build_feature_packet(OP_SQUIRTING, False))

        except Exception:
            pass

class BridgeDeviceManager:
    def __init__(self):
        self.client: BleakClient | None = None
        self.target_address: str | None = None
        self.target_name: str | None = None
        self.write_char: str | None = None
        self.should_run = True
        self.is_connected = False
        self.disconnect_event = asyncio.Event()

    def _on_disconnected(self, client):
        self.is_connected = False
        if self.should_run:
            self.disconnect_event.set()

    async def find_write_char(self):
        self.write_char = None
        if not self.client:
            return
        for service in self.client.services:
            for char in service.characteristics:
                props = char.properties
                if "write" in props or "write-without-response" in props:
                    if self.write_char is None:
                        self.write_char = char.uuid
                        break
        if not self.write_char:
            all_chars = [c.uuid for s in self.client.services for c in s.characteristics]
            self.write_char = all_chars[0] if all_chars else None

    async def connect_with_retries(self, max_attempts=None):
        attempt = 0
        while self.should_run and (max_attempts is None or attempt < max_attempts):
            attempt += 1
            sys.stdout.write(f"\r[Connecting] Trying {self.target_name or self.target_address} (Attempt {attempt})...        ")
            sys.stdout.flush()
            try:
                self.disconnect_event.clear()
                self.client = BleakClient(
                    self.target_address,
                    disconnected_callback=self._on_disconnected,
                    timeout=10.0
                )
                await self.client.connect()
                if self.client.is_connected:
                    self.is_connected = True
                    await self.find_write_char()
                    print(f"\n[✓] Connected to {self.target_name or self.target_address}!")
                    return True
            except Exception:
                pass
            await asyncio.sleep(3.0)
        return False

    async def auto_reconnect_loop(self):
        while self.should_run:
            await self.disconnect_event.wait()
            self.disconnect_event.clear()
            if not self.should_run:
                break
            print(f"\n[!] Connection lost to device. Auto-reconnecting...")
            await self.connect_with_retries()

async def main():
    print("=" * 65)
    print("  Virt-a-Mate (VaM) <---> Joyhub Full Feature BLE Bridge")
    print("=" * 65)

    mgr = BridgeDeviceManager()
    saved = load_saved_device()

    target_address = None
    target_name = None

    if saved and "address" in saved:
        print(f"\n[Saved Device]: {saved.get('name', 'Unknown')} ({saved['address']})")
        choice = await asyncio.to_thread(input, "Connect to saved device? [Y/n/scan]: ")
        choice = choice.strip().lower()

        if choice in ["", "y", "yes"]:
            target_address = saved["address"]
            target_name = saved.get("name", "Saved Device")

    if not target_address:
        print("\nScanning for Bluetooth devices (5 seconds)...")
        discovered = await BleakScanner.discover(timeout=5.0, return_adv=True)
        if not discovered:
            print("[!] No BLE devices found. Ensure device is ON and in range.")
            return

        device_list = list(discovered.values())
        print("\nFound Devices:")
        for idx, (dev, adv) in enumerate(device_list):
            name = dev.name or adv.local_name or "Unknown / Unnamed"
            rssi = adv.rssi if adv else "N/A"
            print(f"  [{idx + 1}] {name} (Address: {dev.address}, RSSI: {rssi} dBm)")

        selection = await asyncio.to_thread(input, "\nEnter device number to connect (or 'q' to quit): ")
        selection = selection.strip()
        if selection.lower() == 'q' or not selection.isdigit():
            return

        target_idx = int(selection) - 1
        if target_idx < 0 or target_idx >= len(device_list):
            print("Invalid selection.")
            return

        selected_device, _ = device_list[target_idx]
        target_address = selected_device.address
        target_name = selected_device.name or target_address

        save_device(target_address, target_name)
        print(f"[✓] Device memorized in {CONFIG_FILE.name}")

    mgr.target_address = target_address
    mgr.target_name = target_name

    connected = await mgr.connect_with_retries(max_attempts=3)
    if not connected:
        print("Could not connect to device. Exiting.")
        return

    reconnect_task = asyncio.create_task(mgr.auto_reconnect_loop())

    # Start UDP Server for VaM
    UDP_PORT = 8888
    loop = asyncio.get_running_loop()
    transport, _ = await loop.create_datagram_endpoint(
        lambda: VamUdpProtocol(),
        local_addr=("127.0.0.1", UDP_PORT)
    )
    print(f"[✓] UDP Server listening on 127.0.0.1:{UDP_PORT}")
    print("\nReady! In Virt-a-Mate:")
    print("  1. Add plugin: Custom/Scripts/Joyhub/JoyhubHaptics.cs")
    print("  2. Full 2-column dashboard: Vibe, Pulse, Heat, Light, Suck, Squeeze, Pump!\n")
    print("-" * 65)

    last_sent_speeds = [-1, -1, -1, -1]

    try:
        while True:
            # Drain feature commands (Heat, Light, Suck, Squeeze, Pump)
            while feature_queue:
                pkt = feature_queue.pop(0)
                if mgr.is_connected and mgr.client and mgr.write_char:
                    try:
                        await mgr.client.write_gatt_char(mgr.write_char, pkt, response=False)
                        await asyncio.sleep(0.02)
                    except Exception:
                        mgr.disconnect_event.set()

            # Safety timeout: If no packet from VaM for 1.5 seconds, shut off vibration
            if time.time() - last_packet_time > 1.5:
                target_speeds = [0, 0, 0, 0]
            else:
                target_speeds = current_vibe_speeds

            if mgr.is_connected and mgr.client and mgr.write_char:
                if target_speeds != last_sent_speeds:
                    pkt = build_vibe_packet(target_speeds)
                    try:
                        await mgr.client.write_gatt_char(mgr.write_char, pkt, response=False)
                        last_sent_speeds = list(target_speeds)
                    except Exception:
                        mgr.disconnect_event.set()

            # Draw console status bar
            max_pct = int(max(target_speeds) * 100 / 255) if target_speeds else 0
            bar_len = 16
            filled = int(bar_len * (max_pct / 100.0))
            bar = "█" * filled + "░" * (bar_len - filled)

            heat_sym = "🔥" if heat_state else "  "
            light_sym = "💡" if light_state else "  "
            suck_sym = f"🌀S{suck_level}" if suck_level > 0 else "    "

            status_conn = "Online" if mgr.is_connected else "Recon."
            status_line = f"\r[{status_conn}] Vibe:[{bar}] {max_pct:3d}% {heat_sym}{light_sym}{suck_sym} | Pkts: {packet_count} "
            sys.stdout.write(status_line)
            sys.stdout.flush()

            await asyncio.sleep(0.04)  # 25 Hz update loop

    except KeyboardInterrupt:
        print("\n[!] Stopping and disconnecting...")
        mgr.should_run = False
        reconnect_task.cancel()
        if mgr.client and mgr.client.is_connected and mgr.write_char:
            try:
                await mgr.client.write_gatt_char(mgr.write_char, build_vibe_packet([0, 0, 0, 0]), response=False)
                await mgr.client.disconnect()
            except Exception:
                pass
    finally:
        transport.close()

if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        print("\nExiting...")
