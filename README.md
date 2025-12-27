# Pad-Avan

A powerful, customizable macro pad with 6 buttons and 2 rotary encoders, featuring a desktop configuration application built with Avalonia UI.

**Pad-Avan** = Hardware/Firmware (Arduino ESP32-S3)  
**Pad-Avan-Force** = Desktop Configuration Software (Avalonia UI)

🌐 **Website**: [https://pad-avan.base44.app](https://pad-avan.base44.app)


=======
![Pad-Avan](https://img.shields.io/badge/Version-1.3.0-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![Avalonia](https://img.shields.io/badge/Avalonia-11.0-green)
![Arduino](https://img.shields.io/badge/Arduino-ESP32--S3-orange)

## 🎯 Features

### Hardware
- **6 Programmable Buttons** - Assign any action, key combo, or special key
- **2 Rotary Encoders** - Volume control, scrolling, or custom actions
- **OLED Display** (128x32) - Shows layer, battery status, or time
- **SD SPI Storage Module** (2GB) - Integrated storage module (not a physical SD card) for storing configurations
- **USB HID** - Works as a standard keyboard/consumer control device

### Desktop Application (Pad- Avan-Force)
- **Cross-Platform UI** - Built with Avalonia UI (Windows, macOS, Linux)
- **Layer Management** - Create and manage multiple configuration layers
- **Visual Configuration** - Easy-to-use interface for buttons and knobs
- **Real-time Updates** - Configure your device without reflashing
- **Configuration Import/Export** - Save and load configurations from files
- **Update System** - Check for firmware and software updates via GitHub Releases

## 📋 Requirements

### Hardware
- **Unexpected Maker (UM) Feather S3** or **Feather S3D** (ESP32-S3)
- 6x Tactile buttons
- 2x Rotary encoders with push buttons
- SSD1306 OLED display (128x32)
- **SD SPI Storage Module** (2GB) - Integrated storage module (not a physical microSD card)
- USB-C cable

**Note**: The Feather S3 uses a voltage divider for battery measurement, while the Feather S3D uses a MAX chip.

### Software
- **Arduino IDE** (2.0+) with ESP32 board support
  - Install ESP32 board package: **Tools > Board > Boards Manager > Search "esp32"**
  - Select board: **Unexpected Maker FeatherS3** or **Unexpected Maker FeatherS3D**
- **.NET 8.0 SDK** (for building the desktop app)
- **Visual Studio 2022** or **JetBrains Rider** (recommended)

## 🚀 Getting Started

### 1. Hardware Setup

1. Solder components according to the wiring diagram (TODO: Add diagram)
2. The **SD SPI Storage Module (2GB)** is already integrated on the board - no physical SD card needed
3. Connect via USB-C

### 2. Firmware Installation

**Choose the correct firmware version:**
<<<<<<< HEAD
- **Feather S3D** (recommended): Uses MAX chip for battery measurement
  - Use: `FeatherS3 scripts/Arduino version/padavan_fs3d/padavan_fs3d.ino`
- **Feather S3** (legacy): Uses voltage divider for battery measurement
  - Use: `FeatherS3 scripts/Arduino version/padavan_fs3/padavan_fs3.ino` (if available)
=======
- **Feather S3** (older): Uses voltage divider for battery measurement
  - Use: `FeatherS3 scripts/Arduino version/Pad-Avan fs3/Pad-Avan_fs3.ino`
- **Feather S3D** (newer): Uses MAX chip for battery measurement
  - Use: `FeatherS3 scripts/Arduino version/Pad-Avan fs3d/Pad-Avan_fs3d.ino`
>>>>>>> 1907bc308604ee760159c1007ff690f4b946aa28

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
<<<<<<< HEAD
Padawan/
├── Pad-Avan Force/              # Desktop configuration software (Avalonia UI)
=======
Pad-Avan/
├── Pad-Avan-Force/              # Desktop configuration software (Avalonia UI)
>>>>>>> 1907bc308604ee760159c1007ff690f4b946aa28
│   ├── Models/                 # Data models
│   ├── ViewModels/             # MVVM view models
│   ├── Views/                  # UI views (XAML)
│   └── Assets/                 # Icons and resources
│
└── FeatherS3 scripts/           # Pad-Avan firmware
    ├── Arduino version/        # Current firmware (Arduino)
<<<<<<< HEAD
    │   ├── padavan_fs3d/       # Feather S3D firmware (MAX chip) - RECOMMENDED
    │   │   ├── padavan_fs3d.ino
    │   │   ├── DisplayHandler.h
    │   │   └── KeyboardLayoutWinCH.h
    │   └── padavan_fs3/        # Feather S3 firmware (voltage divider) - Legacy
    │       └── padavan_fs3.ino
=======
    │   ├── Pad-Avan fs3/        # Feather S3 firmware (voltage divider)
    │   │   └── Pad-Avan_fs3.ino
    │   └── Pad-Avan fs3d/       # Feather S3D firmware (MAX chip)
    │       ├── Pad-Avan_fs3d.ino
    │       └── KeyboardLayoutWinCH.h
>>>>>>> 1907bc308604ee760159c1007ff690f4b946aa28
    └── cpy/                    # Legacy CircuitPython version (deprecated)
```

**Naming Convention:**
- **Pad-Avan** = The macro pad hardware & firmware (Arduino)
- **Pad-Avan-Force** = The desktop configuration software (Avalonia UI)

**Firmware Versions:**
- **padavan_fs3d**: For Unexpected Maker Feather S3D (uses MAX chip for battery measurement) - **RECOMMENDED**
- **padavan_fs3**: For Unexpected Maker Feather S3 (uses voltage divider for battery measurement) - Legacy
- **CircuitPython (cpy)**: Legacy version, no longer supported (switched to Arduino for better performance and reliability)

## 📦 Creating Firmware Releases

When creating a new **Pad-Avan** firmware release on GitHub:

1. **Build the firmware** in Arduino IDE:
   - Open the appropriate firmware file:
     - `FeatherS3 scripts/Arduino version/padavan_fs3d/padavan_fs3d.ino` (for Feather S3D - RECOMMENDED)
     - `FeatherS3 scripts/Arduino version/padavan_fs3/padavan_fs3.ino` (for Feather S3 - Legacy)
   - Sketch → Export compiled Binary
   - The `.bin` file will be created in the same folder as the `.ino` file
   - File name will be something like: `Pad-Avan_fs3d.ino.bin` or `Pad-Avan_fs3d.bin`

2. **Create a GitHub Release**:
   - Go to [Releases](https://github.com/Ghost3379/Pad-Avan/releases)
   - Click "Draft a new release"
   - **Tag version**: `v1.0.0` (or `firmware-v1.0.0` for firmware-only releases)
   - **Release title**: `Pad-Avan Firmware v1.0.0`
   - **Description**: Include version info, e.g.:
     ```
     ## Pad-Avan Firmware v1.0.0
     
     Firmware: v1.0.0
     Software: v1.0.0
     
     ### Changes
     - Bug fixes
     - New features
     ```
   - **Attach the `.bin` file** as an asset:
     - Drag & drop the `.bin` file into the "Attach binaries" section
     - Or click "Choose your files" and select the `.bin` file
   - Click "Publish release"

3. **Users can update** via the **Pad-Avan-Force** desktop app:
   - The app automatically detects the `.bin` file in the release assets
   - One-click firmware update without manual flashing!
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

### Configuration File Format

Configurations are stored as JSON:

```json
{
  "version": "1.0",
  "currentLayer": 1,
  "layers": [
    {
      "id": 1,
      "name": "Layer 1",
      "buttons": {
        "1": {
          "action": "Type Text",
          "key": "Hello World"
        }
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

## 🔮 Roadmap

- [x] Basic button and knob configuration
- [x] Layer management
- [x] Configuration import/export
- [x] Update system UI
- [x] Display integration (OLED 128x32)
- [x] Multi-file JSON config structure
- [x] Improved connection stability
- [ ] Button and knob action handlers (in progress)
- [ ] GitHub API integration for updates
- [ ] esptool integration for firmware flashing
- [ ] Custom key combo builder improvements
- [ ] Macro recording/playback
- [ ] RGB LED support (if hardware added)
- [ ] Wireless mode (Bluetooth HID)

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

## 📝 Naming Convention

- **Pad-Avan** = The macro pad hardware & firmware (Arduino ESP32-S3)
- **Pad-Avan-Force** = The desktop configuration software (Avalonia UI)

The "Force" in Pad-Avan-Force refers to the software being the "force" that configures the Pad-Avan macro pad.

## 🌐 Website

Visit our website for more information, documentation, and updates:
- **Website**: [https://Pad-Avan-force.base44.app](https://Pad-Avan-force.base44.app)

## 📧 Contact

For questions, issues, or feature requests, please open an issue on GitHub.

---

**Made with ❤️ for the macro pad community**

