"""CircuitPython Essentials HID Keyboard example"""
import time
import json
import board
import busio
import sdcardio
import storage
import digitalio
import usb_hid
import usb_cdc
import displayio
import terminalio
from adafruit_display_text import label
import adafruit_displayio_ssd1306
from adafruit_hid.keyboard import Keyboard
from adafruit_hid.keyboard_layout_win_de import KeyboardLayout as KeyboardLayoutWinDE
from adafruit_hid.keycode_win_de import Keycode as KeycodeDE
import feathers3  # Import our helper functions

print("CODE.PY: Starting...")

# Check if USB is available (don't try to enable it)
usb = usb_cdc.data
print(f"CODE.PY: USB object: {usb}")

if usb:
    print("CODE.PY: USB is available")
else:
    print("CODE.PY: USB is NOT available - check boot.py!")
    print("CODE.PY: This means the app cannot communicate with the Feather S3")

# A simple neat keyboard demo in CircuitPython
def read_json_file(file_path, current_layer):
    try:
        with open(file_path, "r") as f:
            json_object = json.load(f)
            # Look for the layer by ID or name
            layers = json_object.get("layers", [])
            for layer in layers:
                if layer.get("id") == current_layer or layer.get("name") == f"Layer {current_layer}":
                    buttons = layer.get("buttons", {})
                    # Convert button dictionary to array in pin order
                    keys = []
                    for i in range(1, 7):  # Buttons 1-6
                        button_id = str(i)
                        if button_id in buttons:
                            button_config = buttons[button_id]
                            if button_config.get("enabled", True) and button_config.get("action") != "None":
                                keys.append(button_config.get("key", ""))
                            else:
                                keys.append("")  # Empty for disabled buttons
                        else:
                            keys.append("")  # Empty for missing buttons
                    return keys
            return []
    except Exception as e:
        print(f"Error reading JSON file: {e}")
        return []

# SD card setup with error handling
sd_available = False
file_path = "/sd/key-strokes.json"

try:
    spi = busio.SPI(board.SCK, MOSI=board.MOSI, MISO=board.MISO)
    cs = board.IO38
    sd = sdcardio.SDCard(spi, cs)
    vfs = storage.VfsFat(sd)
    storage.mount(vfs, "/sd")
    sd_available = True
    print("SD card mounted successfully")
except Exception as e:
    print(f"SD card error: {e}")
    sd_available = False
    # Use default configuration if SD card fails
    file_path = "/key-strokes.json"

# The pins we'll use, each will have an internal pullup
keypress_pins = [board.A6, board.A7, board.A0, board.A5, board.A4, board.A8, board.A9, board.A10, board.A3, board.A2]
# Our array of key objects 
key_pin_array = []

# Mapping von Strings zu HID-Keycodes
KEY_STRING_TO_CODE = {
    "A": KeycodeDE.A,
    "B": KeycodeDE.B,
    "C": KeycodeDE.C,
    "D": KeycodeDE.D,
    "E": KeycodeDE.E,
    "F": KeycodeDE.F,
    "G": KeycodeDE.G,
    "H": KeycodeDE.H,
    "I": KeycodeDE.I,
    "J": KeycodeDE.J,
    "K": KeycodeDE.K,
    "L": KeycodeDE.L,
    "M": KeycodeDE.M,
    "N": KeycodeDE.N,
    "O": KeycodeDE.O,
    "P": KeycodeDE.P,
    "Q": KeycodeDE.Q,
    "R": KeycodeDE.R,
    "S": KeycodeDE.S,
    "T": KeycodeDE.T,
    "U": KeycodeDE.U,
    "V": KeycodeDE.V,
    "W": KeycodeDE.W,
    "X": KeycodeDE.X,
    "Y": KeycodeDE.Y,
    "Z": KeycodeDE.Z,
    "0": KeycodeDE.ZERO,
    "1": KeycodeDE.ONE,
    "2": KeycodeDE.TWO,
    "3": KeycodeDE.THREE,
    "4": KeycodeDE.FOUR,
    "5": KeycodeDE.FIVE,
    "6": KeycodeDE.SIX,
    "7": KeycodeDE.SEVEN,
    "8": KeycodeDE.EIGHT,
    "9": KeycodeDE.NINE,
    "F1": KeycodeDE.F1,
    "F2": KeycodeDE.F2,
    "F3": KeycodeDE.F3,
    "F4": KeycodeDE.F4,
    "F5": KeycodeDE.F5,
    "F6": KeycodeDE.F6,
    "F7": KeycodeDE.F7,
    "F8": KeycodeDE.F8,
    "F9": KeycodeDE.F9,
    "F10": KeycodeDE.F10,
    "F11": KeycodeDE.F11,
    "F12": KeycodeDE.F12,
    "SPACE": KeycodeDE.SPACEBAR,
    "SHIFT": KeycodeDE.SHIFT,
    "CTRL": KeycodeDE.CONTROL,
    "ALT": KeycodeDE.ALT,
    "WIN": KeycodeDE.WINDOWS,
    "TAB": KeycodeDE.TAB,
    "ENTER": KeycodeDE.ENTER,
}

# === Display Setup ===
displayio.release_displays()
i2c = board.I2C()
display_bus = displayio.I2CDisplay(i2c, device_address=0x3C)
WIDTH = 128
HEIGHT = 32
display = adafruit_displayio_ssd1306.SSD1306(display_bus, width=WIDTH, height=HEIGHT)
splash = displayio.Group()
display.root_group =splash
layer_text = label.Label(terminalio.FONT, text="Layer: 1", color=0xFFFFFF, x=10, y=20, scale=2)
splash.append(layer_text)

# The keyboard object!
time.sleep(1)  # Sleep for a bit to avoid a race condition on some systems
keyboard = Keyboard(usb_hid.devices)
keyboard_layout = KeyboardLayoutWinDE(keyboard)  # We're in DE :)

# Make all pin objects inputs with pullups
for pin in keypress_pins:
    key_pin = digitalio.DigitalInOut(pin)
    key_pin.direction = digitalio.Direction.INPUT
    key_pin.pull = digitalio.Pull.UP
    key_pin_array.append(key_pin)

# Define current_layer before using it
current_layer = 1  # The current layer we're working with

# Initialize keys with default values if SD card is not available
if sd_available:
    keys_pressed = read_json_file(file_path, current_layer)
else:
    # Default configuration if SD card fails - only 6 buttons for our pad
    keys_pressed = ["A", "B", "C", "D", "E", "F"]

control_key = KeycodeDE.SHIFT

def save_json_string_to_file(json_string, file_path):
    try:
        json_object = json.loads(json_string)
        with open(file_path, "w") as f:
            json.dump(json_object, f)
        print("JSON erfolgreich gespeichert")
        return True
    except Exception as e:
        usb.write(f"JSON PARSE ERROR: {e}\n".encode())
        return False

def update_display_layer(layer):
    layer_text.text = f"Layer: {layer}"

def handle_command(command):
    """Handle commands from the GUI"""
    command = command.strip()
    print(f"Processing command: {command}")  # Debug: Zeige verarbeitete Befehle
    
    if command == "PING":
        print("Sending PONG response")  # Debug: Zeige PONG-Antwort
        usb.write(b"PONG\n")
        return True
    elif command == "DOWNLOAD_CONFIG":
        print("Processing DOWNLOAD_CONFIG")  # Debug: Zeige Download-Verarbeitung
        if sd_available:
            try:
                with open(file_path, "r") as f:
                    config_data = f.read()
                usb.write(f"CONFIG:{config_data}\n".encode())
                return True
            except Exception as e:
                usb.write(f"DOWNLOAD_ERROR: {e}\n".encode())
                return False
        else:
            usb.write(b"DOWNLOAD_ERROR: SD card not available\n")
            return False
    elif command == "BATTERY_STATUS":
        print("Processing BATTERY_STATUS")  # Debug: Zeige Batterie-Status
        try:
            battery_info = feathers3.get_battery_status()
            battery_response = f"BATTERY:{battery_info['percentage']},{battery_info['voltage']},{battery_info['status']}\n"
            usb.write(battery_response.encode())
            print(f"Battery response: {battery_response.strip()}")
            return True
        except Exception as e:
            print(f"Battery status error: {e}")
            usb.write(f"BATTERY_ERROR: {e}\n".encode())
            return False

    else:
        print(f"Unknown command: {command}")  # Debug: Zeige unbekannte Befehle
        usb.write(f"UNKNOWN_COMMAND: {command}\n".encode())
        return False

uploading = False
json_lines = []

while True:
    # USB-Daten vom Host-PC empfangen
    if usb and usb.in_waiting > 0:
        line = usb.readline().decode("utf-8").strip()
        print(f"USB received: {line}")  # Debug: Zeige alle empfangenen Befehle
        
        # Handle commands first
        if line in ["PING", "DOWNLOAD_CONFIG", "BATTERY_STATUS"]:
            print(f"Handling command: {line}")  # Debug: Zeige behandelte Befehle
            handle_command(line)
            continue

        if line == "BEGIN_JSON":
            uploading = True
            json_lines = []
            print("JSON upload started")  # Debug: Zeige JSON-Upload-Start
            # usb.write(b"BEGIN_OK\n")

        elif line == "END_JSON":
            uploading = False
            json_string = "\n".join(json_lines)
            print("JSON upload ended")  # Debug: Zeige JSON-Upload-Ende

            if sd_available:
                # DEBUG: Schreibe Rohdaten zur Kontrolle auf SD-Karte
                try:
                    with open("/sd/debug_raw.json", "w") as dbg:
                        dbg.write(json_string)
                except Exception as e:
                    usb.write(f"DEBUG SAVE ERROR: {e}\n".encode())

                try:
                    json_object = json.loads(json_string)
                    with open(file_path, "w") as f:
                        json.dump(json_object, f)
                    keys_pressed = read_json_file(file_path, current_layer)
                    usb.write(b"UPLOAD_OK\n")
                except Exception as e:
                    usb.write(f"UPLOAD_FAIL: {repr(e)}\n".encode())
            else:
                usb.write(b"UPLOAD_FAIL: SD card not available\n")

        elif uploading:
            json_lines.append(line)

    
    for key_pin in key_pin_array:
        if not key_pin.value:  # Is it grounded?
            i = key_pin_array.index(key_pin)

            if i == 9:  # Layer-Wechsler (last pin)
                while not key_pin.value:
                    pass
                current_layer += 1
                if current_layer > 3:
                    current_layer = 1
                update_display_layer(current_layer)
                keys_pressed = read_json_file(file_path, current_layer)

            elif i < 6:  # Only handle first 6 pins for our 6 buttons
                while not key_pin.value:
                    pass
                
                if i < len(keys_pressed):
                    key = keys_pressed[i]
                    
                    # Skip empty or disabled keys
                    if not key or key == "":
                        continue
                        
                    try:
                        if isinstance(key, str):
                            keyboard_layout.write(key)

                        elif isinstance(key, list):  # Kombination
                            keycodes = [KEY_STRING_TO_CODE[k.upper()] for k in key if k.upper() in KEY_STRING_TO_CODE]
                            keyboard.press(*keycodes)
                            keyboard.release_all()

                        elif isinstance(key, int):  # Einzelner HID-Keycode (z. B. bei SD-Export)
                            keyboard.press(control_key, key)
                            keyboard.release_all()

                    except Exception as e:
                        print(f"Fehler beim Senden der Taste {key}: {e}")

    time.sleep(0.01)