# Padawan / PadAwan-Force Project Description

## Overview
**Padawan** is a customizable macro pad (hardware + firmware) with 6 programmable buttons and 2 rotary encoders, controlled by a desktop configuration application called **PadAwan-Force**. The project consists of two main components:

1. **Hardware/Firmware (Padawan)**: Arduino-based firmware for ESP32-S3 microcontrollers
2. **Desktop Software (PadAwan-Force)**: Cross-platform configuration application built with Avalonia UI (C#/.NET 8.0)

## Hardware Components

### Supported Boards
- **Unexpected Maker Feather S3** (older version, uses voltage divider for battery)
- **Unexpected Maker Feather S3D** (newer version, uses MAX chip for battery measurement)

### Peripherals
- **6 Tactile Buttons**: Programmable with various actions (key presses, text typing, key combos, layer switching)
- **2 Rotary Encoders with Push Buttons**: Can be configured for volume control, scrolling, or custom actions
- **SSD1306 OLED Display** (128x64): Shows layer info, battery status, or time
- **SD SPI Storage Module** (2GB): Stores configuration files (not a physical SD card, but an integrated storage module)
- **USB HID**: Acts as a keyboard and consumer control device (media keys)

## Software Architecture

### Firmware (Arduino/ESP32-S3)
**Location**: `FeatherS3 scripts/Arduino version/`
- **padavan_fs3.ino**: Firmware for Feather S3 (voltage divider battery monitoring)
- **padavan_fs3d.ino**: Firmware for Feather S3D (MAX chip battery monitoring)

**Key Libraries**:
- **TinyUSB**: USB HID support (keyboard, consumer control for media keys)
- **ArduinoJson**: JSON parsing for configuration files
- **Adafruit SSD1306**: OLED display control
- **SD/SPI**: SD card storage
- **UMS3 Library**: Unexpected Maker S3 helper library (battery monitoring, board peripherals)

**Key Features**:
- USB CDC (Serial) communication at 115200 baud
- USB HID keyboard and consumer control
- SD card configuration storage
- I2C display communication
- Battery monitoring (different methods for FS3 vs FS3D)
- Layer-based configuration system
- Rotary encoder debouncing
- Button debouncing

**Serial Communication Protocol**:
- `PING` → `PONG` (connection test)
- `GET_VERSION` → `VERSION:1.0.0`
- `DOWNLOAD_CONFIG` → `CONFIG:{json}`
- `GET_CURRENT_CONFIG` → `CURRENT_CONFIG:{json}`
- `UPLOAD_LAYER_CONFIG` → `READY_FOR_LAYER_CONFIG`
- `BEGIN_JSON` ... `END_JSON` → `UPLOAD_OK`
- `BATTERY_STATUS` → `BATTERY:percentage,voltage,charging`
- `SET_DISPLAY_MODE:mode,enabled` → `DISPLAY_MODE_SET`
- `SET_TIME:HH:MM:SS` → `TIME_SET`

**Configuration File Format** (JSON stored on SD card):
```json
{
  "version": "1.0",
  "currentLayer": 1,
  "layers": [
    {
      "id": 1,
      "name": "Layer 1",
      "buttons": {
        "1": {"action": "Type Text", "key": "Hello"},
        "2": {"action": "Special Key", "key": "ENTER"},
        "3": {"action": "Key Combo", "key": "CTRL+C"}
      },
      "knobs": {
        "A": {
          "ccwAction": "Decrease Volume",
          "cwAction": "Increase Volume",
          "pressAction": "Special Key",
          "pressKey": "ENTER"
        }
      }
    }
  ]
}
```

**Button Actions**:
- `None`: No action
- `Type Text`: Type a custom string
- `Special Key`: Send special keys (Enter, Tab, Arrow keys, Esc, media controls)
- `Key Combo`: Multi-key combinations (Ctrl+C, Alt+Tab, etc.)
- `Layer Switch`: Switch to another layer
- `Windows Key`: Windows shortcuts

**Knob Actions**:
- `Increase/Decrease Volume`: System volume control
- `Scroll Up/Down`: Mouse wheel scrolling
- Same actions as buttons (Type Text, Special Key, Key Combo, etc.)

### Desktop Application (Avalonia UI)
**Location**: `Pad-Avan Force/`

**Technology Stack**:
- **Avalonia UI 11.0**: Cross-platform UI framework (Windows, macOS, Linux)
- **.NET 8.0**: Runtime and SDK
- **MVVM Pattern**: Model-View-ViewModel architecture
- **System.IO.Ports**: Serial communication with device

**Project Structure**:
```
Pad-Avan Force/
├── Models/
│   ├── ConfigManager.cs          # Configuration file management (JSON)
│   ├── FeatherConnection.cs      # Serial communication handler
│   └── Layer.cs                  # Layer data model
├── ViewModels/
│   ├── MainWindowViewModel.cs    # Main window logic
│   ├── UpdateWindowViewModel.cs  # Update system logic
│   └── ViewModelBase.cs          # Base class for view models
├── Views/
│   ├── MainWindow.axaml          # Main UI (button/knob grid, layer management)
│   ├── ButtonConfigWindow.axaml  # Button configuration dialog
│   ├── KnobConfigWindow.axaml    # Knob configuration dialog
│   └── UpdateWindow.axaml        # Update check dialog
└── Assets/
    └── Pad-Avan Force logo.ico   # Application icon
```

**Key Features**:
- Serial port detection and connection
- Real-time configuration upload/download
- Layer management (create, switch, configure)
- Visual button and knob configuration
- Configuration import/export (JSON files)
- Battery status display
- Display mode configuration (layer, battery, time, off)
- Firmware version checking
- Automatic reconnection handling
- Grace period for connection stability

**Configuration Storage**:
- Desktop app stores configs in: `%AppData%\Pad-Avan Force\`
- Device stores configs on SD card: `/macropad_config.json`

## Technical Details

### USB Initialization (Critical)
The ESP32-S3 uses native USB with CDC (Serial) enabled in the bootloader. The firmware must:
- **NOT** call `USB.begin()` in `setup()` (bootloader already initializes USB)
- Only call `Serial.begin(115200)` and wait for Serial to be available
- Then initialize HID: `Keyboard.begin()` and `ConsumerControl.begin()`
- This ensures both Serial (CDC) and HID work simultaneously

### SD Card Initialization
- Non-blocking initialization: quick attempt in `setup()`, then retries in `loop()`
- SD card powered by LDO_2 (separate power rail)
- Delays added for power stabilization
- Retry interval: 5 seconds

### Display Handling
- Uses `DisplayHandler.h` library (header-only) for abstraction
- I2C communication with timeout handling
- Prevents unnecessary updates (checks if content changed)
- Can be disabled via configuration

### Memory Management
- Pre-allocated buffers for JSON reading (prevents fragmentation)
- Uses `std::unique_ptr` for dynamic allocation
- `yield()` calls to prevent watchdog resets

### Serial Communication Stability
- `Serial.flush()` after all writes
- Timeout-based reading (100ms max per read)
- Grace period after connection (10 seconds)
- Two consecutive ping failures required before disconnecting

## Build & Deployment

### Firmware
1. Open `.ino` file in Arduino IDE
2. Select board: "Unexpected Maker FeatherS3" or "Unexpected Maker FeatherS3D"
3. Install required libraries (see README.md)
4. Upload sketch
5. Export compiled binary for releases (`.bin` file)

### Desktop Application
1. `dotnet restore`
2. `dotnet build`
3. `dotnet publish -c Release -r win-x64 --self-contained`
4. Installer created with Inno Setup (`installer.iss`)

### Installer
- Created with **Inno Setup**
- Script: `installer.iss`
- Build script: `build-installer.ps1`
- Output: `installer/PadAvanForce_x64_v1.0.X-Setup.exe`
- Installs to: `Program Files\Pad-Avan Force\`
- Config stored in: `%AppData%\Pad-Avan Force\` (writable location)

## Recent Fixes & Improvements

1. **USB CDC Initialization**: Removed `USB.begin()` to fix COM port not appearing
2. **SD Card Non-Blocking**: Moved SD init to loop with retries to prevent hangs
3. **Display Flickering**: Added content change detection to prevent unnecessary updates
4. **Reconnection Bug**: Added grace period and improved connection stability logic
5. **Media Keys**: Added Play/Pause, Next/Previous Track, Stop, Volume Up/Down, Mute
6. **Config File Location**: Changed from Program Files to AppData for write access
7. **Display Handler**: Created abstraction library for better compilation speed
8. **Memory Management**: Improved JSON reading with pre-allocated buffers
9. **Serial Stability**: Added timeouts and flush calls for reliable communication

## File Naming Conventions
- **Padawan** = Hardware/Firmware (Arduino)
- **PadAwan-Force** = Desktop Software (Avalonia UI)
- **padavan_fs3.ino** = Feather S3 firmware
- **padavan_fs3d.ino** = Feather S3D firmware

## GitHub Integration
- Firmware `.bin` files stored in `releases/firmware/`
- Releases created on GitHub with firmware binaries
- Update system checks GitHub Releases for new versions
- Version format: `v1.0.X` or `firmware-v1.0.X`

## Current Status
- Firmware: Stable, both FS3 and FS3D versions working
- Desktop App: Stable, Windows installer available
- Features: Core functionality complete (buttons, knobs, layers, display, battery)
- Known Issues: None currently blocking

## Development Environment
- **Arduino IDE 2.0+**: For firmware development
- **Visual Studio 2022 / JetBrains Rider**: For desktop app development
- **.NET 8.0 SDK**: Required for building desktop app
- **Inno Setup**: For creating Windows installer
- **Git**: Version control

