# Pad-Avan

A powerful, customizable macro pad with 6 buttons and 2 rotary encoders, featuring a desktop configuration application built with Avalonia UI.

**Pad-Avan** = Hardware/Firmware (Arduino ESP32-S3)  
**Pad-Avan Force** = Desktop Configuration Software (Avalonia UI)

🌐 **Website**: [https://pad-avan.base44.app](https://pad-avan.base44.app)

![Pad-Avan](https://img.shields.io/badge/Software-v2.5.0-blue)
![Firmware](https://img.shields.io/badge/Firmware-v2.3.0-orange)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![Avalonia](https://img.shields.io/badge/Avalonia-11.0-green)
![Arduino](https://img.shields.io/badge/Arduino-ESP32--S3-orange)

## 🎯 Features

### Hardware
- **6 Programmable Buttons** - Assign any action, custom text, complex key combo, or special key
- **2 Rotary Encoders with Push Buttons** - High-responsiveness single-detent tracking for volume, scrolling, or custom shortcuts
- **OLED Display** (128x32) - Real-time feedback for active layer, battery status, or system time
- **SD SPI Storage Module** (2GB) - Integrated on-board storage module for persistent layer configurations
- **USB HID** - Native keyboard, mouse, and consumer media control

### Desktop Application (Pad-Avan Force)
- **Cross-Platform UI** - Built with Avalonia UI (Windows, macOS, Linux)
- **Interactive Layer Settings** - Drag-and-drop layer reordering with live visual feedback
- **Layer Management** - Create, clone, rename, reorder, and delete configuration layers
- **Key Combo Builder** - Record complex shortcuts with modifiers and up to 6 simultaneous keys
- **Device Synchronization** - Read active layer and configuration directly from device RAM and storage
- **Visual Feedback** - Real-time unsaved change indicators, pulsating status glows, and custom button styling
- **Update System** - Automatic bootloader mode preparation and web-based firmware flashing via [pad-avan.base44.app/flasher](https://pad-avan.base44.app/flasher)

## 📋 Requirements

### Hardware
- **Unexpected Maker (UM) Feather S3D** (ESP32-S3) or Legacy version: Feather S3
- 6x Tactile buttons
- 2x Rotary encoders with push buttons
- SSD1306 OLED display (128x32)
- **SD SPI Storage Module** (2GB) - Integrated storage module (not a physical microSD card)
- USB-C cable

**Note**: The Feather S3 uses a voltage divider for battery measurement, while the Feather S3D uses a MAX17048G+T10 chip, which is far more efficient and precise.

### Software
- **Arduino IDE** (2.0+) with ESP32 board support
  - Install ESP32 board package: **Tools > Board > Boards Manager > Search "esp32"**
  - Select board: **Unexpected Maker FeatherS3** or **Unexpected Maker FeatherS3D**
- **.NET 8.0 SDK** (for building the desktop app)
- **Visual Studio 2022** or **JetBrains Rider** (recommended)

## 🚀 Getting Started

### 1. Hardware Setup

1. Solder components according to the wiring diagram
2. The **SD SPI Storage Module (2GB)** is already integrated on the board - no physical SD card needed
3. Connect via USB-C

### 2. Firmware Installation

**Choose the correct firmware version:**

- **Feather S3D** (recommended): Uses MAX chip for battery measurement
  - Use: `FeatherS3 scripts/Arduino version/padavan_fs3d/padavan_fs3d.ino`
- **Feather S3** (legacy): Uses voltage divider for battery measurement
  - Use: `FeatherS3 scripts/Arduino version/padavan_fs3/padavan_fs3.ino`


#### Option A: Build from Source
1. Open the appropriate `.ino` file for your board in Arduino IDE
2. Install required libraries (via **Tools > Manage Libraries**):
   - **ArduinoJson** (v6.x recommended)
   - **Adafruit SSD1306** (for OLED display)
   - **Adafruit GFX** (automatically installed with SSD1306)
   - **RotaryEncoder** (by Matthias Hertel)
   - **SD** (ESP32 built-in)
   - **SPI** (ESP32 built-in)
   - **Wire** (ESP32 built-in)
   - **USBHIDKeyboard**, **USBHIDMouse**, **USBHIDConsumerControl** (ESP32 built-in)
3. Select board: **Unexpected Maker FeatherS3** or **Unexpected Maker FeatherS3D**
4. Select **Tools > USB Mode > Native USB**
5. Upload the sketch

#### Option B: Flash Pre-built Firmware (via Update System)
1. Connect your Pad-Avan device
2. Open **Pad-Avan-Force** desktop application
3. Click **Update** → **Check for Updates**
4. If a new firmware version is available, click **Update Firmware**
5. The application will automatically download and flash the `.bin` file from GitHub Releases

**Note**: There was a CircuitPython version in the `cpy` folder, but it's no longer supported. We switched to Arduino for better performance and reliability.

### 3. Desktop Application

#### Building from Source

```bash
cd Pad-Avan-Force
dotnet restore
dotnet build
dotnet run
```

#### Running the Application

1. Connect your **Pad-Avan** device via USB
2. Launch the **Pad-Avan-Force** desktop application
3. Select the COM port from the dropdown
4. Click "Connect"
5. Start configuring your buttons and knobs!

## 📖 Usage

### Button Configuration

1. Click on any button (1-6) in the main window
2. Select an action:
   - **None** - No action
   - **Type Text** - Type a custom string
   - **Special Key** - Send a special key (Enter, Tab, Arrow keys, etc.)
   - **Key Combo** - Create multi-key combinations (Ctrl+C, Alt+Tab, etc.)
   - **Layer Switch** - Switch to another layer
   - **Windows Key** - Open Start menu / Windows shortcuts

### Knob Configuration

1. Click on knob A or B
2. Configure rotation actions:
   - **Volume Control** - Increase/decrease system volume
   - **Scroll** - Scroll up/down
   - **Custom Actions** - Assign any button action
3. Configure press action:
   - Same options as buttons
   - Supports key combos and special keys

### Layer Management

- Create multiple layers for different use cases (gaming, productivity, media, etc.)
- Switch layers using button actions
- Each layer has independent button and knob configurations

### Display Modes

Configure the OLED display to show:
- **Layer** - Current active layer
- **Battery** - Battery percentage
- **Time** - Current time (requires time sync from desktop app)
- **Off** - Display disabled

## 🏗️ Project Structure

```
Pad-Avan/
├── Pad-Avan Force/              # Desktop configuration software (Avalonia UI)
│   ├── Models/                 # Data models & connection handling
│   ├── ViewModels/             # MVVM view models
│   ├── Views/                  # UI views & custom controls (XAML)
│   └── Assets/                 # Icons and branding resources
│
└── FeatherS3 scripts/           # Pad-Avan firmware
    ├── Arduino version/        # Current C++ firmware (Arduino)
    │   ├── padavan_fs3d/       # Universal firmware (Feather S3D & S3) - ACTIVE
    │   │   ├── padavan_fs3d.ino
    │   │   ├── DisplayHandler.h
    │   │   └── KeyboardLayoutWinCH.h
    │   └── padavan_fs3/        # Legacy Feather S3 firmware (deprecated)
    │       └── padavan_fs3.ino
    └── cpy/                    # Legacy CircuitPython version (deprecated)
```

**Naming Convention:**
- **Pad-Avan** = The macro pad hardware & firmware (Arduino)
- **Pad-Avan Force** = The desktop configuration software (Avalonia UI)

**Firmware Versions:**
- **padavan_fs3d**: Universal firmware for Unexpected Maker Feather S3D and Feather S3 boards - **ACTIVE & MAINTAINED**
- **padavan_fs3**: Legacy Feather S3 sketch (deprecated - use `padavan_fs3d` instead)
- **CircuitPython (cpy)**: Legacy prototype, no longer supported (switched to Arduino for superior performance, responsiveness, and thermal efficiency)

## 📦 Creating Releases

When creating a new release on GitHub:

1. **Build the firmware** in Arduino IDE:
   - Open: `FeatherS3 scripts/Arduino version/padavan_fs3d/padavan_fs3d.ino`
   - Select board: **Unexpected Maker FeatherS3** or **FeatherS3D**
   - **Sketch → Export compiled Binary**
   - Rename the exported binary to `padavan_fs3d-v2.3.bin`

2. **Publish the Desktop Application**:
   - Build the Windows installer using Inno Setup (`installer.iss`)
   - Output: `installer/PadAvanForce_x64_v2.5-Setup.exe`

3. **Create a GitHub Release**:
   - Go to [GitHub Releases](https://github.com/Ghost3379/Pad-Avan/releases)
   - Click "Draft a new release"
   - **Tag version**: `FS-v2.3-v2.5` (format: `FS-v{Firmware}-v{Software}`)
   - **Release title**: `Pad-Avan v2.3 & Force v2.5 Release`
   - **Attach assets**:
     - `padavan_fs3d-v2.3.bin` (Firmware binary for web flasher & desktop updater)
     - `PadAvanForce_x64_v2.5-Setup.exe` (Windows desktop installer)
   - Click "Publish release"

4. **Updating**:
   - **Via Desktop App**: The app detects the update, puts the device into bootloader mode, and opens the [Web Flasher](https://pad-avan.base44.app/flasher).
   - **Via Web Flasher**: Visit [pad-avan.base44.app/flasher](https://pad-avan.base44.app/flasher) directly to flash any release directly in your browser.
   - No need to download Arduino IDE or esptool manually

## 🔧 Configuration

### Serial Communication Protocol

The device communicates via USB Serial at 115200 baud:

- `PING` → `PONG` (connection test)
- `GET_VERSION` → `VERSION:1.0.3` (firmware version)
- `DOWNLOAD_CONFIG` → `CONFIG:{json}` (download configuration)
- `GET_CURRENT_CONFIG` → `CURRENT_CONFIG:{json}` (get active config)
- `UPLOAD_LAYER_CONFIG` → `READY_FOR_LAYER_CONFIG` (prepare for upload)
- `BEGIN_JSON` ... `END_JSON` → `UPLOAD_OK` (upload configuration)
- `BATTERY_STATUS` → `BATTERY:percentage,voltage,charging` (battery info)

## 🐛 Troubleshooting

### Device Not Connecting

1. Check USB cable (data cable required, not charge-only)
2. Verify COM port in Device Manager
3. Ensure drivers are installed (CP210x or CH340)
4. Try different USB port

### Buttons Not Responding

1. Check button wiring
2. Verify configuration is uploaded to device
3. Check if device is in correct layer
4. Test with serial monitor for debug output

### Display Not Working

1. Verify I2C connections (SDA/SCL)
2. Check display address (usually 0x3C)
3. Ensure display is enabled in configuration

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- **Pad-Avan** firmware built with [TinyUSB](https://github.com/hathach/tinyusb) for USB HID support
- **Pad-Avan-Force** desktop app built with [Avalonia UI](https://avaloniaui.net/)
- Inspired by the macro pad community


## 📧 Contact

For questions, issues, or feature requests, please open an issue on GitHub.

---

**Made with ❤️ for the macro pad community**

