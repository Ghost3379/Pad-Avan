# PadAwan-Force Arduino Version

Arduino implementation for the FeatherS3 ESP32-S3 Macro Pad.

## Advantages over CircuitPython

- **Faster Boot**: Starts in ~100-500ms instead of 3-5 seconds
- **Better Performance**: Lower latency for button/encoder inputs
- **Less Memory Usage**: Compiled code instead of interpreted

## Required Libraries

Install the following libraries via Arduino Library Manager:

1. **Adafruit SSD1306** - For OLED display
2. **Adafruit GFX** - Graphics library (automatically installed with SSD1306)
3. **Adafruit BusIO** - I2C/SPI support (automatically installed)
4. **SD** - SD SPI Storage Module support (should already be included in ESP32)
5. **ArduinoJson** - JSON parsing (Version 6.x recommended)
6. **RotaryEncoder** - Rotary encoder support (by Matthias Hertel)
7. **UMS3** - Unexpected Maker FeatherS3 board support (via ESP32 board package)

## ESP32-S3 USB HID

The USB HID libraries are part of the ESP32-S3 core. The following headers should be available:

- `<USB.h>` - USB initialization
- `<USBHIDKeyboard.h>` - Keyboard HID
- `<USBHIDMouse.h>` - Mouse HID
- `<USBHIDConsumerControl.h>` - Consumer Control (Volume/Media)

**Important**: If these headers are not found:
- Make sure you have the latest ESP32 board version installed
- Select the board: **Tools > Board > ESP32 Arduino > UM FeatherS3** or **UM FeatherS3D**
- Enable Native USB: **Tools > USB Mode > Native USB**

## Installation

1. **Arduino IDE Setup**:
   - Install Arduino IDE 2.0.3 or higher
   - Add ESP32 board support:
     - File > Preferences > Additional Board Manager URLs
     - Add: `https://raw.githubusercontent.com/espressif/arduino-esp32/gh-pages/package_esp32_index.json`
   - Tools > Board > Boards Manager
   - Search for "esp32" and install "esp32 by Espressif Systems"

2. **Select Board**:
   - Tools > Board > ESP32 Arduino > **UM FeatherS3** or **UM FeatherS3D**
   - Tools > USB Mode > **Native USB**
   - Tools > Port > Select the COM port of your FeatherS3

3. **Install Libraries**:
   - Tools > Manage Libraries
   - Install the libraries mentioned above

4. **Upload Code**:
   - Open `padavan_fs3d.ino` in Arduino IDE
   - Click Upload
   - After upload: **Press the Reset button on FeatherS3** (important!)

## Pin Assignment

- **Buttons**: IO14, IO18, IO5, IO17, IO6, IO12
- **Rotary A**: A=IO10, B=IO11, Press=IO7
- **Rotary B**: A=IO1, B=IO3, Press=IO33
- **SD SPI Storage Module** (2GB): CS=IO38, LDO_2=IO39 (power control) - Integrated module, not a physical SD card
- **Display**: I2C, Address 0x3C (128x32 OLED)

## Serial Communication & Debugging

### Serial Monitor (Arduino IDE)

**Yes, you can read the console!** 🎉

Unlike CircuitPython, where the USB CDC console had to be disabled, in Arduino the **Serial Monitor works in parallel with app communication**.

**How to use:**
1. Open **Tools > Serial Monitor** in Arduino IDE
2. Set baud rate to **115200**
3. You'll see all debug output in real-time!

**Enable/Disable Debug Output:**
- In `padavan_fs3d.ino` you'll find: `#define DEBUG_SERIAL 1`
- Set to `1` for debug output (default)
- Set to `0` to disable debug output (app communication only)

### App Communication

The desktop app communicates over the same `Serial` port. App responses (like "PONG", "UPLOAD_OK", etc.) are always output, even if `DEBUG_SERIAL = 0`.

**Baud Rate**: 115200 (can be adjusted in `padavan_fs3d.ino`)

**Important**: You can use both the Serial Monitor and the app simultaneously. The app simply ignores debug lines.

## Current Status

### ✅ Implemented Features

- ✅ SD SPI Storage Module (2GB) initialization and configuration storage
- ✅ Multi-file JSON config structure (general.json + layerX.json)
- ✅ Display integration (OLED 128x32) with multiple modes
- ✅ Button and rotary encoder pin definitions
- ✅ Serial communication protocol (all commands)
- ✅ Battery status reading
- ✅ Display modes (layer, battery, time, off)
- ✅ Configuration upload/download
- ✅ Watchdog handling and stability improvements

### 🚧 In Progress

- ⏳ Button and rotary encoder action handlers (pin definitions ready, handlers to be implemented)
- ⏳ Keyboard HID key execution
- ⏳ Mouse HID support
- ⏳ Consumer control (volume/media)

### 📋 Known Issues / Adjustments

1. **USB HID Libraries**: If `USBHIDKeyboard.h` is not found, it might be that the ESP32 version doesn't fully support it yet. In this case, you might need to use an alternative library or update the ESP32 version.

2. **Keyboard Layout**: Swiss keyboard layout (QWERTZ) is implemented. For full support of all Swiss characters, `KeyboardLayoutWinCH.h` can be extended.

3. **SD SPI Storage Module**: The 2GB SPI storage module is integrated on the board (not a physical SD card). It must be operated in SPI mode. Make sure the CS line is correctly connected (IO38). The LDO_2 pin (IO39) controls power to the storage module.

4. **Display**: The display is 128x32 pixels (not 128x64). Make sure you're using the correct display size.

## Differences from CircuitPython Version

- **Boot Time**: Significantly faster (~100-500ms vs. 3-5s)
- **Serial**: Uses `Serial` instead of `usb_cdc.data`
- **JSON**: Uses ArduinoJson instead of Python's json
- **Rotary Encoder**: Uses RotaryEncoder library instead of rotaryio
- **Config Storage**: Multi-file JSON structure to prevent watchdog resets

## Compatibility with C# App

**Yes, the Arduino version works directly with the C# app!** ✅

All important commands are implemented:
- ✅ `PING` → `PONG`
- ✅ `UPLOAD_LAYER_CONFIG` → `READY_FOR_LAYER_CONFIG`
- ✅ `BEGIN_JSON` / `END_JSON` → `UPLOAD_OK`
- ✅ `GET_CURRENT_CONFIG` → `CURRENT_CONFIG:...`
- ✅ `DOWNLOAD_CONFIG` → `CONFIG:...`
- ✅ `SET_DISPLAY_MODE:...` → `DISPLAY_MODE_SET`
- ✅ `SET_TIME:...` → `TIME_SET`
- ✅ `BATTERY_STATUS` → `BATTERY:percentage,voltage,status`
- ✅ `GET_VERSION` → `VERSION:1.0.3`

**Important**: 
- Baud Rate: **115200** (already correct)
- Serial Port: Automatically detected
- Debug Output: Ignored by the app (can run in parallel)

## Next Steps

- [x] All app commands implemented
- [x] SD SPI Storage Module (2GB) initialization
- [x] Display integration
- [x] Multi-file config structure
- [ ] Implement button and rotary encoder action handlers
- [ ] Test all functions with the app
- [ ] Keyboard layout fully implemented
- [ ] Performance optimizations

## File Structure

```
padavan_fs3d/
├── padavan_fs3d.ino          # Main firmware file
├── DisplayHandler.h           # OLED display wrapper
└── KeyboardLayoutWinCH.h     # Swiss keyboard layout support
```

## Version

Current firmware version: **1.0.3**
