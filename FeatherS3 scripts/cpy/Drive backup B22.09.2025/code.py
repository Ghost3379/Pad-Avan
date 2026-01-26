import feathers3
import time
import json
import board
import busio
import sdcardio
import storage
import digitalio
import rotaryio
import usb_hid
import usb_cdc
import displayio
import terminalio
from adafruit_display_text import label
import adafruit_displayio_ssd1306
from adafruit_hid.keyboard import Keyboard
from adafruit_hid.keyboard_layout_win_de import KeyboardLayout as KeyboardLayoutWinDE
from adafruit_hid.keycode_win_de import Keycode as KeycodeDE


print("CODE.PY: Starting...")

# Display mode settings (will be updated from desktop app)
display_mode = "off"  # "off", "layer", "battery", "time"
display_enabled = True

# Dynamic configuration limits (will be updated from config)
max_layers = 1  # Default, will be updated from config
max_buttons = 6  # Default, will be updated from config

# Rotary encoder state tracking (using CircuitPython rotaryio)
# State variables are now handled by the rotaryio.IncrementalEncoder objects

# Check if USB is available (don't try to enable it)
usb = usb_cdc.data
print(f"CODE.PY: USB object: {usb}")

if usb:
    print("CODE.PY: USB is available")
    print("CODE.PY: Ready to receive commands from desktop app")
    print("CODE.PY: Send 'PING' to test communication")
    print("CODE.PY: Waiting for commands...")
else:
    print("CODE.PY: USB is NOT available - check boot.py!")
    print("CODE.PY: This means the app cannot communicate with the Feather S3")

# A simple neat keyboard demo in CircuitPython
def read_json_file(file_path, current_layer):
    try:
        with open(file_path, "r") as f:
            json_object = json.load(f)
            print(f"Reading config for layer {current_layer}")

            # Handle new comprehensive format with layers array
            if "layers" in json_object and isinstance(json_object["layers"], list):
                layers = json_object["layers"]
                # Find the layer by index (current_layer is 1-based, array is 0-based)
                layer_index = current_layer - 1
                if 0 <= layer_index < len(layers):
                    layer = layers[layer_index]
                    buttons = layer.get("buttons", {})
                    layer_name = layer.get('name', f'Layer {current_layer}')
                    print(f"Found layer: {layer_name}")

                    # Convert button dictionary to array in pin order
                    keys = []
                    for i in range(1, max_buttons + 1):  # Dynamic button count
                        button_id = str(i)
                        if button_id in buttons:
                            button_config = buttons[button_id]
                            if button_config.get("enabled", True) and button_config.get("action") != "None":
                                action = button_config.get("action", "")
                                if action == "Layer Switch":
                                    keys.append("LAYER_SWITCH")
                                else:
                                    keys.append(button_config.get("key", ""))
                            else:
                                keys.append("")  # Empty for disabled buttons
                        else:
                            keys.append("")  # Empty for missing buttons
                    print(f"Loaded keys: {keys}")
                    return keys

            # Handle old format with layers object (backward compatibility)
            elif "layers" in json_object and isinstance(json_object["layers"], dict):
                layers = json_object["layers"]
                layer_key = f"layer{current_layer - 1}"
                if layer_key in layers:
                    layer = layers[layer_key]
                    keys = layer.get("keys", [])
                    # Pad to max_buttons if needed
                    while len(keys) < max_buttons:
                        keys.append("")
                    return keys[:max_buttons]  # Only return first max_buttons buttons

            print(f"No layer found for {current_layer}")
            return []
    except Exception as e:
        print(f"Error reading JSON file: {e}")
        return []

# SD card setup with error handling
sd_available = False
file_path = "/sd/macropad_config.json"

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
    file_path = "/macropad_config.json"

# Individual button pins - each with its own DigitalInOut object
# Buttons: 1->IO14, 2->IO18, 3->IO5, 4->IO17, 5->IO6, 6->IO12
button_1 = digitalio.DigitalInOut(board.IO14)
button_1.direction = digitalio.Direction.INPUT
button_1.pull = digitalio.Pull.UP

button_2 = digitalio.DigitalInOut(board.IO18)
button_2.direction = digitalio.Direction.INPUT
button_2.pull = digitalio.Pull.UP

button_3 = digitalio.DigitalInOut(board.IO5)
button_3.direction = digitalio.Direction.INPUT
button_3.pull = digitalio.Pull.UP

button_4 = digitalio.DigitalInOut(board.IO17)
button_4.direction = digitalio.Direction.INPUT
button_4.pull = digitalio.Pull.UP

button_5 = digitalio.DigitalInOut(board.IO6)
button_5.direction = digitalio.Direction.INPUT
button_5.pull = digitalio.Pull.UP

button_6 = digitalio.DigitalInOut(board.IO12)
button_6.direction = digitalio.Direction.INPUT
button_6.pull = digitalio.Pull.UP

# Rotary encoder pin definitions
# Rotary A: A->IO10, B->IO11, Press->IO7
# Rotary B: A->IO1, B->IO3, Press->IO33
rotary_a_pins = (board.IO10, board.IO11)  # Rotary A: A and B pins
rotary_b_pins = (board.IO1, board.IO3)    # Rotary B: A and B pins

# Rotary encoder button pins
rotary_a_button = digitalio.DigitalInOut(board.IO7)
rotary_a_button.direction = digitalio.Direction.INPUT
rotary_a_button.pull = digitalio.Pull.UP

rotary_b_button = digitalio.DigitalInOut(board.IO33)
rotary_b_button.direction = digitalio.Direction.INPUT
rotary_b_button.pull = digitalio.Pull.UP

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
display = None
layer_text = None
splash = None

try:
    print("CODE.PY: Initializing display...")
    displayio.release_displays()
    i2c = board.I2C()
    display_bus = displayio.I2CDisplay(i2c, device_address=0x3C)
    WIDTH = 128
    HEIGHT = 32
    display = adafruit_displayio_ssd1306.SSD1306(display_bus, width=WIDTH, height=HEIGHT)
    splash = displayio.Group()
    display.root_group = splash
    layer_text = label.Label(terminalio.FONT, text="Layer: 1", color=0xFFFFFF, x=10, y=20, scale=2)
    splash.append(layer_text)
    print("CODE.PY: Display initialized successfully")
except Exception as e:
    print(f"CODE.PY: ERROR - Display initialization failed: {e}")
    print("CODE.PY: Continuing without display...")
    display = None
    layer_text = None
    splash = None

# The keyboard object!
time.sleep(1)  # Sleep for a bit to avoid a race condition on some systems
keyboard = Keyboard(usb_hid.devices)
keyboard_layout = KeyboardLayoutWinDE(keyboard)  # We're in DE :)

# All button pins are already initialized above with individual DigitalInOut objects

# Initialize rotary encoders
# Rotary A encoder: A->IO10, B->IO11
# Using divisor=4 for encoders with 1 detent per cycle (most common)
# divisor options: 1=no detents or 4 detents/cycle, 2=2 detents/cycle, 4=1 detent/cycle
try:
    rotary_a = rotaryio.IncrementalEncoder(rotary_a_pins[0], rotary_a_pins[1], divisor=4)
    rotary_a_last_position = 0
    print("CODE.PY: Rotary A encoder initialized successfully")
except Exception as e:
    print(f"CODE.PY: Failed to initialize Rotary A encoder: {e}")
    rotary_a = None
    rotary_a_last_position = 0

# Rotary B encoder: A->IO1, B->IO3
try:
    rotary_b = rotaryio.IncrementalEncoder(rotary_b_pins[0], rotary_b_pins[1], divisor=4)
    rotary_b_last_position = 0
    print("CODE.PY: Rotary B encoder initialized successfully")
except Exception as e:
    print(f"CODE.PY: Failed to initialize Rotary B encoder: {e}")
    rotary_b = None
    rotary_b_last_position = 0

# Rotary encoder button pins are already initialized above

# Define current_layer before using it
current_layer = 1  # The current layer we're working with

def update_config_limits(config_data):
    """Update dynamic limits and settings based on configuration"""
    global max_layers, max_buttons, display_mode, display_enabled, current_layer

    try:
        # Update display settings
        if "display" in config_data:
            display_config = config_data["display"]
            if "mode" in display_config:
                display_mode = display_config["mode"]
                print(f"Loaded display mode from config: {display_mode}")
            if "enabled" in display_config:
                display_enabled = display_config["enabled"]
                print(f"Loaded display enabled from config: {display_enabled}")
            print(f"Updated display settings - Mode: {display_mode}, Enabled: {display_enabled}")
        else:
            print("No display settings found in config, using defaults")

        # Update current layer
        if "currentLayer" in config_data:
            current_layer = config_data["currentLayer"]
            print(f"Updated current layer to: {current_layer}")

        # Update layer limits from actual layers
        if "layers" in config_data:
            if isinstance(config_data["layers"], list):
                max_layers = len(config_data["layers"])  # Dynamic based on actual layers
                # Find max buttons from all layers
                max_buttons = 0
                for layer in config_data["layers"]:
                    if "buttons" in layer:
                        max_buttons = max(max_buttons, len(layer["buttons"]))
                    elif "keys" in layer:
                        max_buttons = max(max_buttons, len(layer["keys"]))
            elif isinstance(config_data["layers"], dict):
                max_layers = len(config_data["layers"])  # Dynamic based on actual layers
                # Find max buttons from all layers
                max_buttons = 0
                for layer_key, layer in config_data["layers"].items():
                    if "keys" in layer:
                        max_buttons = max(max_buttons, len(layer["keys"]))

        # Update limits from config if available
        if "limits" in config_data:
            limits = config_data["limits"]
            if "maxLayers" in limits:
                max_layers = limits["maxLayers"]
            if "maxButtons" in limits:
                max_buttons = limits["maxButtons"]
            # Remove layerSwitchPin if it exists (deprecated)
            if "layerSwitchPin" in limits:
                print("Warning: layerSwitchPin found in config - this is deprecated and will be ignored")

        print(f"Updated config limits - Layers: {max_layers} (dynamic), Buttons: {max_buttons}")
        print(f"Display settings - Mode: {display_mode}, Enabled: {display_enabled}")
        print(f"Current layer: {current_layer}")

        # Process knob configurations for current layer
        if "layers" in config_data and isinstance(config_data["layers"], list):
            layer_index = current_layer - 1
            if 0 <= layer_index < len(config_data["layers"]):
                layer = config_data["layers"][layer_index]
                if "knobs" in layer:
                    print(f"Processing knob configurations for layer {current_layer}:")
                    for knob_letter, knob_config in layer["knobs"].items():
                        print(f"  Knob {knob_letter}: CCW='{knob_config.get('ccwAction', 'None')}', CW='{knob_config.get('cwAction', 'None')}', Press='{knob_config.get('pressAction', 'None')}'")
                else:
                    print("No knob configurations found in current layer")
            else:
                print(f"Invalid layer index: {layer_index}")

        # Update display after loading config
        print("Applying display settings from config...")  # Debug: Show display update
        update_display_mode()
        print("Display settings applied successfully")  # Debug: Confirm display update

    except Exception as e:
        print(f"Error updating config limits: {e}")
        # Keep defaults if there's an error

# Initialize keys with default values if SD card is not available
if sd_available:
    keys_pressed = read_json_file(file_path, current_layer)

    # Try to load and update config limits from existing file
    try:
        print(f"Loading config from: {file_path}")
        with open(file_path, "r") as f:
            config_data = json.load(f)
            print(f"Config loaded successfully: {config_data}")
            update_config_limits(config_data)
    except Exception as e:
        print(f"Could not load config limits: {e}")
        print("Using default settings")
else:
    # Default configuration if SD card fails - only 6 buttons for our pad
    keys_pressed = ["A", "B", "C", "D", "E", "F"]

# Initialize display after loading config
print("Initializing display with loaded settings...")
try:
    update_display_mode()
    print("Display initialized successfully")
except Exception as e:
    print(f"Error initializing display: {e}")
    # Fallback to basic layer display
    try:
        layer_text.text = f"Layer: {current_layer}"
        print("Fallback display set")
    except Exception as e2:
        print(f"Fallback display error: {e2}")

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
    try:
        if layer_text is not None:
            layer_text.text = f"Layer: {layer}"
            print(f"Display layer: {layer}")
        else:
            print(f"Display not available - would show layer: {layer}")
    except Exception as e:
        print(f"Error updating display layer: {e}")

def update_display_message(message):
    """Update display with a temporary message"""
    try:
        if layer_text is not None:
            layer_text.text = message
            print(f"Display message: {message}")
        else:
            print(f"Display not available - would show: {message}")
    except Exception as e:
        print(f"Error updating display message: {e}")


def show_receiving_feedback():
    """Show receiving configuration feedback"""
    update_display_message("Receiving...")

def show_done_feedback(config_info=""):
    """Show done feedback - only 'Done!' then return to normal display"""
    print(f"show_done_feedback called with config_info: '{config_info}'")
    update_display_message("Done!")
    time.sleep(1)
    # Always return to normal display mode, ignore config_info
    print("Returning to normal display mode...")
    update_display_mode()

def update_display_mode():
    """Update display based on current display mode"""
    global display_enabled

    try:
        print(f"update_display_mode called - mode: {display_mode}, enabled: {display_enabled}")

        if not display_enabled or display_mode == "off":
            # Turn off display
            if layer_text is not None:
                layer_text.text = ""
            print("Display turned off")
            return

        if display_mode == "layer":
            print(f"Setting display to layer mode - current_layer: {current_layer}")
            update_display_layer(current_layer)
        elif display_mode == "battery":
            print("Setting display to battery mode")
            try:
                # Check if feathers3 module is available
                if 'feathers3' in globals():
                    battery_info = feathers3.get_battery_status()
                    battery_percent = battery_info['percentage']
                    print(f"Battery status: {battery_percent}%")
                    update_display_message(f"{battery_percent}%")
                else:
                    print("feathers3 module not available")
                    update_display_message("No Bat")
            except Exception as e:
                print(f"Battery error: {e}")
                update_display_message("?%")
        elif display_mode == "time":
            print("Setting display to time mode")
            try:
                current_time = time.localtime()
                time_str = f"{current_time.tm_hour:02d}:{current_time.tm_min:02d}"
                print(f"Time: {time_str}")
                update_display_message(time_str)
            except Exception as e:
                print(f"Time error: {e}")
                update_display_message("Time: ?")
        else:
            print(f"Unknown display mode: {display_mode}, defaulting to layer")
            update_display_layer(current_layer)

    except Exception as e:
        print(f"Error in update_display_mode: {e}")
        # Fallback to layer display
        try:
            update_display_layer(current_layer)
        except Exception as e2:
            print(f"Fallback error: {e2}")
            if layer_text is not None:
                layer_text.text = "Error"

def set_display_mode(mode, enabled=True):
    """Set display mode from desktop app"""
    global display_mode, display_enabled
    display_mode = mode
    display_enabled = enabled
    print(f"Display mode set to: {mode}, enabled: {enabled}")
    update_display_mode()

def handle_rotary_press(knob_letter):
    """Handle rotary encoder press action"""
    try:
        # Get current layer configuration
        current_config = read_json_file(file_path, current_layer)
        if not current_config:
            print(f"No config available for knob {knob_letter} press")
            return

        # Find knob configuration in current layer
        if "layers" in current_config and isinstance(current_config["layers"], list):
            layer_index = current_layer - 1
            if 0 <= layer_index < len(current_config["layers"]):
                layer = current_config["layers"][layer_index]
                if "knobs" in layer and knob_letter in layer["knobs"]:
                    knob_config = layer["knobs"][knob_letter]
                    press_action = knob_config.get("pressAction", "None")
                    print(f"Knob {knob_letter} press action: {press_action}")
                    execute_knob_action(press_action)
                else:
                    print(f"No knob {knob_letter} configuration found")
            else:
                print(f"Invalid layer index: {layer_index}")
        else:
            print("No layers configuration found")
    except Exception as e:
        print(f"Error handling rotary press: {e}")

def handle_rotary_rotation(knob_letter, direction):
    """Handle rotary encoder rotation (clockwise/counter-clockwise)"""
    try:
        # Get current layer configuration
        current_config = read_json_file(file_path, current_layer)
        if not current_config:
            print(f"No config available for knob {knob_letter} rotation")
            return

        # Find knob configuration in current layer
        if "layers" in current_config and isinstance(current_config["layers"], list):
            layer_index = current_layer - 1
            if 0 <= layer_index < len(current_config["layers"]):
                layer = current_config["layers"][layer_index]
                if "knobs" in layer and knob_letter in layer["knobs"]:
                    knob_config = layer["knobs"][knob_letter]
                    action = knob_config.get("cwAction" if direction == "cw" else "ccwAction", "None")
                    print(f"Knob {knob_letter} {direction} action: {action}")
                    execute_knob_action(action)
                else:
                    print(f"No knob {knob_letter} configuration found")
            else:
                print(f"Invalid layer index: {layer_index}")
        else:
            print("No layers configuration found")
    except Exception as e:
        print(f"Error handling rotary rotation: {e}")

def execute_knob_action(action):
    """Execute a knob action"""
    try:
        if action == "None" or not action:
            return

        if action == "Increase Volume":
            keyboard.press(KeycodeDE.VOLUME_UP)
            keyboard.release_all()
        elif action == "Decrease Volume":
            keyboard.press(KeycodeDE.VOLUME_DOWN)
            keyboard.release_all()
        elif action == "Scroll Up":
            keyboard.press(KeycodeDE.UP_ARROW)
            keyboard.release_all()
        elif action == "Scroll Down":
            keyboard.press(KeycodeDE.DOWN_ARROW)
            keyboard.release_all()
        elif action == "Switch Layer":
            if max_layers > 1:
                global current_layer
                current_layer += 1
                if current_layer > max_layers:
                    current_layer = 1
                print(f"Switched to layer {current_layer}")
                update_display_mode()
                # Reload button configuration for new layer
                keys_pressed = read_json_file(file_path, current_layer)
        elif action == "Key Press":
            # For now, just send Enter
            keyboard.press(KeycodeDE.ENTER)
            keyboard.release_all()
        elif action == "Key combo":
            # For now, just send Ctrl+C
            keyboard.press(KeycodeDE.CONTROL, KeycodeDE.C)
            keyboard.release_all()
        else:
            print(f"Unknown knob action: {action}")
    except Exception as e:
        print(f"Error executing knob action '{action}': {e}")


def handle_command(command):
    """Handle commands from the GUI"""
    command = command.strip()
    print(f"Processing command: {command}")  # Debug: Zeige verarbeitete Befehle

    if command == "PING":
        print("CODE.PY: Received PING command - sending PONG response")  # Debug: Zeige PONG-Antwort
        usb.write(b"PONG\n")
        print("CODE.PY: PONG sent successfully")
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
    elif command == "UPLOAD_LAYER_CONFIG":
        print("Processing UPLOAD_LAYER_CONFIG")  # Debug: Zeige Layer-Config-Upload
        usb.write(b"READY_FOR_LAYER_CONFIG\n")
        return True
    elif command == "GET_CURRENT_CONFIG":
        print("Processing GET_CURRENT_CONFIG")  # Debug: Zeige Current-Config-Abfrage
        if sd_available:
            try:
                with open(file_path, "r") as f:
                    config_data = f.read()
                usb.write(f"CURRENT_CONFIG:{config_data}\n".encode())
                return True
            except Exception as e:
                usb.write(f"CONFIG_ERROR: {e}\n".encode())
                return False
        else:
            usb.write(b"CONFIG_ERROR: SD card not available\n")
            return False
    elif command.startswith("SET_DISPLAY_MODE:"):
        # Format: SET_DISPLAY_MODE:mode,enabled
        try:
            parts = command.split(":", 1)[1].split(",")
            mode = parts[0]
            enabled = parts[1].lower() == "true" if len(parts) > 1 else True
            set_display_mode(mode, enabled)
            usb.write(b"DISPLAY_MODE_SET\n")
            return True
        except Exception as e:
            usb.write(f"DISPLAY_MODE_ERROR: {e}\n".encode())
            return False
    elif command.startswith("SET_TIME:"):
        # Format: SET_TIME:HH:MM
        try:
            time_str = command.split(":", 1)[1]
            if display_mode == "time":
                update_display_message(time_str)
            usb.write(b"TIME_SET\n")
            return True
        except Exception as e:
            usb.write(f"TIME_ERROR: {e}\n".encode())
            return False

    else:
        print(f"Unknown command: {command}")  # Debug: Zeige unbekannte Befehle
        usb.write(f"UNKNOWN_COMMAND: {command}\n".encode())
        return False

uploading = False
json_lines = []

# Display already initialized after config loading

# Display update counter for time mode
display_update_counter = 0

# Heartbeat counter to show FeatherS3 is alive
heartbeat_counter = 0

print("CODE.PY: Entering main loop...")

while True:
    # Heartbeat every 10 seconds to show we're alive
    heartbeat_counter += 1
    if heartbeat_counter >= 10000:  # 10 seconds at 1ms sleep
        heartbeat_counter = 0
        print("CODE.PY: Heartbeat - FeatherS3 is running")

    # Update display periodically (every 100 loops for time mode)
    display_update_counter += 1
    if display_update_counter >= 100:
        display_update_counter = 0
        if display_mode == "time":
            update_display_mode()


    # USB-Daten vom Host-PC empfangen
    if usb and usb.in_waiting > 0:
        line = usb.readline().decode("utf-8").strip()
        print(f"USB received: {line}")  # Debug: Zeige alle empfangenen Befehle

        # Handle commands first
        if line in ["PING", "DOWNLOAD_CONFIG", "BATTERY_STATUS", "UPLOAD_LAYER_CONFIG", "GET_CURRENT_CONFIG"] or line.startswith("SET_DISPLAY_MODE:") or line.startswith("SET_TIME:"):
            print(f"Handling command: {line}")  # Debug: Zeige behandelte Befehle
            handle_command(line)
            continue

        if line == "BEGIN_JSON":
            uploading = True
            json_lines = []
            print("JSON upload started")  # Debug: Zeige JSON-Upload-Start
            print(f"Will save to: {file_path}")  # Debug: Show target file
            show_receiving_feedback()  # Show "Receiving..." on display
            # usb.write(b"BEGIN_OK\n")

        elif line == "END_JSON":
            uploading = False
            json_string = "\n".join(json_lines)
            print("JSON upload ended")  # Debug: Zeige JSON-Upload-Ende
            print(f"JSON length: {len(json_string)} characters")  # Debug: Show JSON length
            print(f"Will save to: {file_path}")  # Debug: Show target file

            if sd_available:
                # DEBUG: Schreibe Rohdaten zur Kontrolle auf SD-Karte
                try:
                    with open("/sd/debug_raw.json", "w") as dbg:
                        dbg.write(json_string)
                except Exception as e:
                    usb.write(f"DEBUG SAVE ERROR: {e}\n".encode())

                try:
                    json_object = json.loads(json_string)

                    # Update dynamic limits based on new configuration
                    update_config_limits(json_object)

                    # Save to the main config file
                    print(f"Saving configuration to {file_path}")  # Debug: Show save action
                    with open(file_path, "w") as f:
                        json.dump(json_object, f)
                    print(f"Main config file saved successfully")  # Debug: Confirm save

                    # Also save a backup with timestamp
                    import time
                    backup_path = f"/sd/key-strokes_backup_{int(time.time())}.json"
                    with open(backup_path, "w") as f:
                        json.dump(json_object, f)
                    print(f"Backup saved to {backup_path}")  # Debug: Show backup save

                    # Reload the current layer configuration
                    keys_pressed = read_json_file(file_path, current_layer)
                    usb.write(b"UPLOAD_OK\n")
                    print(f"CODE.PY: Configuration saved successfully to {file_path}")

                    # Show done feedback with layer info
                    layer_count = len(json_object.get("layers", []))
                    config_info = f"Saved {layer_count} layers"
                    print(f"CODE.PY: Reloaded configuration with {layer_count} layers")
                    show_done_feedback(config_info)
                except Exception as e:
                    usb.write(f"UPLOAD_FAIL: {repr(e)}\n".encode())
                    show_done_feedback("Save failed!")
            else:
                usb.write(b"UPLOAD_FAIL: SD card not available\n")

        elif uploading:
            json_lines.append(line)


    # Check individual buttons
    buttons = [button_1, button_2, button_3, button_4, button_5, button_6]
    for i, button_pin in enumerate(buttons):
        if not button_pin.value:  # Is it grounded?
            while not button_pin.value:
                pass

            if i < len(keys_pressed):
                key = keys_pressed[i]

                # Skip empty or disabled keys
                if not key or key == "":
                    continue

                # Check for special layer switching action
                if key == "LAYER_SWITCH" and max_layers > 1:
                    print(f"Layer switch button pressed - current layer: {current_layer}")
                    current_layer += 1
                    if current_layer > max_layers:
                        current_layer = 1
                    print(f"Switched to layer {current_layer} (max: {max_layers})")
                    update_display_mode()
                    keys_pressed = read_json_file(file_path, current_layer)
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

    # Check rotary encoder buttons
    if not rotary_a_button.value:  # Rotary A button pressed
        while not rotary_a_button.value:
            pass
        print("Rotary A - Press")
        handle_rotary_press("A")

    if not rotary_b_button.value:  # Rotary B button pressed
        while not rotary_b_button.value:
            pass
        print("Rotary B - Press")
        handle_rotary_press("B")

    # Handle rotary encoder rotation detection using CircuitPython rotaryio
    # Rotary A encoder
    if rotary_a is not None:
        rotary_a_position = rotary_a.position
        if rotary_a_position != rotary_a_last_position:
            if rotary_a_position > rotary_a_last_position:
                print("Rotary A - Clockwise")
                handle_rotary_rotation("A", "cw")
            else:
                print("Rotary A - Counter-clockwise")
                handle_rotary_rotation("A", "ccw")
            rotary_a_last_position = rotary_a_position

    # Rotary B encoder
    if rotary_b is not None:
        rotary_b_position = rotary_b.position
        if rotary_b_position != rotary_b_last_position:
            if rotary_b_position > rotary_b_last_position:
                print("Rotary B - Clockwise")
                handle_rotary_rotation("B", "cw")
            else:
                print("Rotary B - Counter-clockwise")
                handle_rotary_rotation("B", "ccw")
            rotary_b_last_position = rotary_b_position
