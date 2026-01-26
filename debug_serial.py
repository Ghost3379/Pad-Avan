#!/usr/bin/env python3
"""
FeatherS3 / CircuitPython serial comms tester (handles CDC DATA vs CONSOLE)
"""

import serial, serial.tools.list_ports as lp
import time

BAUD = 115200          # USB CDC ignoriert's meist, aber ist "üblich"
READ_INIT_SEC = 2.0
READ_RESP_SEC = 3.0
PING_BYTES = b"PING\n"

def classify_port(p):
    """Try to guess DATA vs CONSOLE from pyserial's port info"""
    desc = (p.description or "").lower()
    name = (p.name or "").lower()
    hwid = (p.hwid or "").lower()
    manu = (getattr(p, "manufacturer", "") or "").lower()
    iface = (getattr(p, "interface", "") or "").lower()

    # Heuristik: manche Stacks labeln "data" im interface/desc
    hints = "data" in desc or "data" in iface
    replish = "repl" in desc or "console" in desc

    # Adafruit VID häufig 0x239a; Espressif/UM können variieren
    vid = getattr(p, "vid", None)
    pid = getattr(p, "pid", None)

    guess = "unknown"
    if hints and not replish:
        guess = "DATA?"
    elif replish:
        guess = "CONSOLE?"
    return guess, {"vid": vid, "pid": pid, "desc": p.description, "iface": iface, "manu": manu, "hwid": p.hwid}

def list_ports_verbose():
    ports = list(lp.comports())
    if not ports:
        print("❌ No COM ports found.")
        return []
    print(f"✅ Found {len(ports)} port(s):")
    for i, p in enumerate(ports, 1):
        guess, meta = classify_port(p)
        print(f"  {i}. {p.device}  [{guess}]")
        print(f"     desc={meta['desc']}  manu={meta['manu']}  iface={meta['iface']}")
        if meta["vid"] is not None:
            print(f"     VID:PID={meta['vid']:04x}:{meta['pid']:04x}")
        print(f"     hwid={meta['hwid']}")
    return ports

def try_ping(port_name):
    print(f"\n🔌 Testing {port_name} ...")
    try:
        with serial.Serial(port_name, BAUD, timeout=0.2, write_timeout=0.5) as ser:
            # Stabilisieren
            ser.reset_input_buffer()
            ser.reset_output_buffer()

            # DTR/RTS an manchen Hosts wichtig (zeigt "connected" an)
            ser.dtr = True
            ser.rts = False

            # Initialoutput einsammeln (REPL, boot, prints)
            print("📖 Reading initial output ...")
            t0 = time.time()
            initial = []
            while time.time() - t0 < READ_INIT_SEC:
                try:
                    line = ser.readline()
                    if not line:
                        continue
                    s = line.decode("utf-8", "ignore").strip()
                    if s:
                        initial.append(s)
                        print("  >", s)
                except Exception as e:
                    print("  read err:", e)
                    break

            # PING senden (mit LF)
            print("📤 Sending PING ...")
            ser.write(PING_BYTES)
            ser.flush()

            # Antwort lesen
            print("📥 Waiting for PONG ...")
            t0 = time.time()
            while time.time() - t0 < READ_RESP_SEC:
                line = ser.readline()
                if not line:
                    continue
                s = line.decode("utf-8", "ignore").strip()
                if not s:
                    continue
                print("  <", s)
                if "PONG" in s:
                    print(f"✅ SUCCESS on {port_name}")
                    return True
            print("❌ No PONG on this port.")
            return False
    except Exception as e:
        print(f"❌ Open/IO error on {port_name}: {e}")
        return False

def main():
    print("🚀 FeatherS3 Serial Ping")
    print("="*50)
    ports = list_ports_verbose()
    if not ports:
        return False

    # Bevorzugt zuerst die Ports testen, die wie DATA aussehen
    ports_sorted = sorted(
        ports,
        key=lambda p: 0 if classify_port(p)[0].startswith("DATA") else 1
    )

    ok_ports = []
    for p in ports_sorted:
        if try_ping(p.device):
            ok_ports.append(p.device)

    print("\n" + "="*50)
    if ok_ports:
        print("🎉 PONG from:", ", ".join(ok_ports))
        return True
    else:
        print("💥 No PONG on any port.")
        print("\nTips:")
        print("• Du musst den **CDC-Data-Port** erwischen (dein code.py nutzt usb_cdc.data).")
        print("• In settings.toml/boot.py: CIRCUITPY_USB_CDC_DATA=1 (Data-Port aktiv).")
        print("• Achte auf Zeilenende '\\n' und dass dein code.py tatsächlich läuft.")
        print("• Wenn nur REPL chatty ist (und kein PONG): Das ist der Console-Port – anderen testen.")
        return False

if __name__ == "__main__":
    ok = main()
    exit(0 if ok else 1)
