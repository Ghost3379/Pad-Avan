# Release v1.0.2

## 🎉 What's New

### Major Features
- ✅ **Windows Installer** - Professional installer for easy installation and updates
- ✅ **Media Controls** - Full media control support (Play/Pause, Next/Previous Track, Volume, Mute)
- ✅ **Improved Reconnection** - Better stability when reconnecting devices

### Bug Fixes
- 🔧 **USB CDC/Serial Port Fix** - FS3D now appears as COM port after flashing (no bootloader mode needed)
- 🔧 **Read Config UI Update** - Configuration now properly updates the UI when reading from device
- 🔧 **Config Save Crash Fix** - App no longer crashes when saving config (now uses AppData folder)
- 🔧 **Reconnection Race Conditions** - Fixed connection lock to prevent status overwrites

### Improvements
- 🎨 **New Application Logo** - Updated branding
- 📁 **File Save Dialog** - Choose download location for "Download Config"
- 🔄 **UI Improvements** - Renamed "Escape" to "Esc" for consistency
- 🔒 **Connection Stability** - Added connection locks and grace periods

## 📦 Downloads

### Desktop Application
- **Windows Installer**: `PadAvanForce_x64_v1.0.5-Setup.exe` (or latest installer in `installer/` folder)
  - Self-contained installer with .NET runtime
  - Automatic updates supported

### Firmware Binaries
- **FeatherS3 (v1.0.0)**: `padavan_fs3-v1.0.0.bin`
  - For Unexpected Maker FeatherS3 boards
  - Uses voltage divider for battery measurement
  
- **FeatherS3D (v1.0.2)**: `padavan_fs3d-v1.0.2.bin`
  - For Unexpected Maker FeatherS3D boards
  - Uses MAX chip for battery measurement

## 🚀 Installation

### Desktop Application
1. Download the Windows installer
2. Run `PadAvanForce_x64_v1.0.5-Setup.exe`
3. Follow the installation wizard
4. Launch "Pad-Avan Force" from Start Menu

### Firmware Update
**Option 1: Via Desktop App (Recommended)**
1. Connect your macro pad
2. Open Pad-Avan Force
3. Go to **Update** → **Check for Updates**
4. Click **Update Firmware** if available

**Option 2: Manual Flash**
1. Download the appropriate `.bin` file for your board
2. Use Arduino IDE or esptool to flash:
   ```bash
   esptool.py --chip esp32s3 --port COMx write_flash 0x0 padavan_fs3d-v1.0.2.bin
   ```

## 📋 Firmware Versions
- **padavan_fs3**: v1.0.0
- **padavan_fs3d**: v1.0.2

## 🔄 Updating from Previous Versions

If you have a previous version installed:
- **Installer**: Just run the new installer - it will automatically update your installation
- **Firmware**: Use the desktop app's update feature or manually flash the new `.bin` file

## 🐛 Known Issues
- None at this time

## 🙏 Thanks
Thank you for using Pad-Avan Force! If you encounter any issues, please report them on GitHub.

---

**Full Changelog**: See commit history for detailed changes

