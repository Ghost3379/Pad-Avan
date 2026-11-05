# Pad-Avan Firmware (Arduino ESP32-S3)

Native C++ Arduino firmware for the Pad-Avan Macro Pad running on Unexpected Maker FeatherS3 and FeatherS3D.

## Advantages over CircuitPython

- **Faster Boot**: Boots in ~100-300ms instead of 3-5 seconds
- **Superior Responsiveness**: Sub-millisecond latency for button presses and rotary encoder ticks
- **Zero Overhead & Cool Operation**: Bare-metal compiled code without garbage collection pauses or excessive CPU heating
- **Full HID Stack**: Simultaneous USB CDC Serial, Keyboard HID, Mouse HID, and Consumer Control media keys

## Universal Firmware (`padavan_fs3d.ino`)

The firmware in `padavan_fs3d/` is the **active, universal firmware** compatible with both:
- **Feather S3D** (uses onboard MAX17048G fuel gauge chip for precision battery monitoring)
- **Feather S3** (pin-compatible legacy board)

## Required Libraries

Install via Arduino Library Manager:

1. **Adafruit SSD1306** - For 128x32 OLED display
2. **Adafruit GFX** - Graphics primitives (installed automatically with SSD1306)
3. **Adafruit BusIO** - I2C/SPI bus handling (installed automatically)
4. **SD** - Built into ESP32 core
5. **ArduinoJson** - JSON parsing (v6.x)
6. **RotaryEncoder** - By Matthias Hertel (configured with `LatchMode::TWO03` for immediate single-detent response)
7. **UMS3** - Unexpected Maker S3 helper library (via board package)

## ESP32-S3 Native USB HID

The USB HID stack runs directly via ESP32-S3 TinyUSB:
- `<USB.h>` - USB initialization
- `<USBHIDKeyboard.h>` - Keyboard HID
- `<USBHIDMouse.h>` - Mouse HID
- `<USBHIDConsumerControl.h>` - Volume, mute, and media controls

**Arduino IDE Settings:**
- Board: **Unexpected Maker FeatherS3** or **UM FeatherS3D**
- USB CDC On Boot: **Enabled**
- PSRAM: **Enabled**
- Upload Mode: **USB-OTG CDC (TinyUSB)**
- USB Mode: **USB-OTG (TinyUSB)**
- Upload Speed: **921600**

## Pin Assignment

- **Buttons (1-6)**: IO14, IO18, IO5, IO17, IO6, IO12
- **Rotary A**: A=IO10, B=IO11, Press=IO7
- **Rotary B**: A=IO1, B=IO3, Press=IO33
- **SD SPI Storage Module (2GB)**: CS=IO38, LDO_2=IO39
- **Display**: I2C (SDA/SCL), Address 0x3C (128x32 OLED)

## Serial Communication & Protocol

Communicates with Pad-Avan Force at 115200 baud:
- `PING` → `PONG`
- `GET_VERSION` → `VERSION:2.3`
- `GET_CURRENT_CONFIG` → `CURRENT_CONFIG:...`
- `DOWNLOAD_CONFIG` → `CONFIG:...`
- `UPLOAD_LAYER_CONFIG` → `READY_FOR_LAYER_CONFIG`
- `BEGIN_JSON` / `END_JSON` → `UPLOAD_OK`
- `SET_DISPLAY_MODE:...` → `DISPLAY_MODE_SET`
- `SET_TIME:...` → `TIME_SET`
- `BATTERY_STATUS` → `BATTERY:percentage,voltage,status`

## Implemented Features

- [x] Multi-file JSON SD storage (`general.json` + `layerX.json`)
- [x] Full Keyboard HID & Swiss layout (QWERTZ) support
- [x] Mouse click execution (Left, Right, Middle)
- [x] Consumer Control media keys (Volume Up/Down/Mute, Play/Pause, Next/Prev)
- [x] Multi-key combos up to 6 simultaneous keys
- [x] Instant single-detent rotary encoder tracking (`LatchMode::TWO03`)
- [x] Bidirectional layer switching with wrap-around
- [x] OLED display modes (Layer, Battery, Time, Off)
- [x] Automatic bootloader mode switching for web flashing

## Version

Current firmware version: **v2.3**
