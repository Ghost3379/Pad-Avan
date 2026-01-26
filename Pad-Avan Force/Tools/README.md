# Tools Directory

This directory contains external tools required by PadAwan-Force.

## esptool.exe

`esptool.exe` is used for flashing firmware to ESP32-S3 devices.

### How to add esptool.exe:

1. **Option 1: Download from GitHub**
   - Go to: https://github.com/espressif/esptool/releases
   - Download the latest `esptool.exe` for Windows
   - Place it in this `Tools` directory

2. **Option 2: Install via Python (if you have Python installed)**
   ```powershell
   pip install esptool
   ```
   Then copy `esptool.exe` from Python's Scripts folder to this directory.

3. **Option 3: Use Arduino IDE installation**
   - If you have Arduino IDE with ESP32 board package installed
   - Find esptool.exe in: `%LOCALAPPDATA%\Arduino15\packages\esp32\tools\esptool_py\*\esptool.exe`
   - Copy it to this directory

### Note:
The application will automatically use `esptool.exe` from this directory if it exists.
If not found, it will search in PATH and other common locations.

