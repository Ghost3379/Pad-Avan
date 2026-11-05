# Release Notes: Pad-Avan Force Software v2.5.0 & Firmware v2.3

**Release Tag:** `FS-v2.3-v2.5`  
**Target Hardware:** Unexpected Maker FeatherS3 / ESP32-S3  
**Web Flasher:** https://pad-avan.base44.app/flasher  

---

## Highlights

- **Hardware Firmware v2.3 (Swiss German Layout & Rotary Encoder Fixes):**
  - Standardized rotary encoders to register on every physical detent click (using `LatchMode::TWO03` for 24-step mechanical EC11 encoders) rather than requiring two detent movements per action.
  - Fixed shortcut execution on Swiss German (QWERTZ) systems: `getComboKeycode()` and `pressKey()` now correctly swap `Z` and `Y` so that shortcuts like `Ctrl+Z` (Undo) and `Ctrl+Y` (Redo) execute accurately on Windows with a Swiss German or German keyboard layout.
- **Update Manager Redesign & Silent Updates:** Complete visual and backend overhaul of the desktop Update Window. Supports one-click silent desktop application updates and automated bootloader mode handoff to the web flasher with pre-filled parameters.
- **Multi-Version GitHub Tag Format:** Introduced support for composite release tags (`FS-v<firmware>-v<software>`), enabling simultaneous yet independent firmware and desktop software updates.
- **Flash Pad Button Polish:** Eliminated Avalonia FluentTheme gray button disabling during flashing; enhanced flashing feedback with a responsive pulsing green glow animation.
- **Interactive Multi-Key Shortcut Recorder:** Integrated a dedicated key recorder control supporting up to 6 simultaneous modifier and key combinations with clear UI chips.
- **Layer Management & Reordering:** Full visual overhaul of Layer Settings, including drag-and-drop layer reordering, inline name editing, layer 1 protection, and wrap-around toggling.
- **Documentation & Project Rebranding:** Standardized naming across the entire project to **Pad-Avan** and **Pad-Avan Force**, fixing legacy typos, merge conflict remnants, and broken CI build workflows.
- **Streamlined Release Packaging:** Reorganized release directories into `Firmware releases/` and `Installer releases/`, shipping with an Inno Setup standalone installer.

---

## Detailed Changes

### 1. Hardware Firmware (v2.3)
- **Swiss German QWERTZ Key Shortcut Fix:** Corrected USB HID scancode translation in `getComboKeycode()` (`padavan_fs3d.ino`) and `pressKey()` (`KeyboardLayoutWinCH.h`). Shortcuts containing `Z` or `Y` now swap scancodes appropriately so Windows QWERTZ accurately registers `Ctrl+Z` as `Ctrl+Z` instead of `Ctrl+Y`.
- **Rotary Encoder Precision:** Changed latch mode to `RotaryEncoder::LatchMode::TWO03` in `FeatherS3 scripts/Arduino version/padavan_fs3d/padavan_fs3d.ino`. EC11 encoders with 24 physical detents now register immediately on each individual click rather than every second click.
- **Firmware Lineage Cleanup:** Consolidated active firmware development exclusively on the `padavan_fs3d` codebase. Legacy CircuitPython (`cpy/`) and original `padavan_fs3` sketches remain archived for hardware history.
- **Version Query Reporting:** Firmware reports version `2.3` via serial query (`VERSION` command).
- **Web Flasher Merged Binary:** Documented and verified that flashing via the web flasher requires the Arduino IDE merged binary (`padavan_fs3d.ino.merged.bin`), which includes the bootloader, partition table, and application image flashed starting at offset `0x0`.

### 2. Update Manager Overhaul
- **Modern Dual-Card UI:** Rebuilt `UpdateWindow.axaml` into an expanded 720x580 dual-card interface displaying side-by-side status for both Firmware and Software.
- **Live Status Badges:** Dynamic status badges indicate "Checking...", "Update Available", or "Up to Date" with color coding (LimeGreen for current, Cyan for pending update).
- **Silent Background App Update:** The "Update Application" button downloads the setup executable, generates a background script (`silent_update.bat`), executes Inno Setup with `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART`, closes the current app, and automatically restarts Pad-Avan Force.
- **Automated Web Flasher Handoff:** The "Update Firmware" button executes the serial `BOOTLOADER` command, waits for USB CDC to disconnect, suppresses reconnection loops, and opens `https://pad-avan.base44.app/flasher` in the default browser with parameters:
  `?firmwareUrl=<url>&version=<ver>&autoFlash=true&alreadyInBootloader=true`
- **Flexible Tag Parser:** `UpdateWindowViewModel.cs` parses composite tags like `FS-v2.3-v2.5` (firmware `v2.3` and software `v2.5`), single tags `FS-v2.0`, firmware-only `F-v...`, and software-only `S-v...`.
- **Target Repository Alignment:** Configured API endpoints to point to `Ghost3379/Pad-Avan`.

### 3. Desktop Application & UI Polish (v2.5.0)
- **Flash Pad Button Styling:** Overrode default Avalonia FluentTheme control templates (`PART_ContentPresenter`) to ensure the button maintains its custom border and white text across pointer-over, pressed, and disabled states. Enabled `AllowConcurrentExecutions = true` on the async relay command to eliminate button flicker.
- **Flashing Glow Animation:** Updated pulsing keyframe animation from cyan to vibrant green (`#00FF00` border, `#9900FF00` box shadow, `#003808` base border) to match flashing status aesthetics.
- **Multi-Key Combination Recorder:** Replaced rigid dropdown selectors with `KeyRecorderControl.axaml`, enabling recording of up to 6 simultaneous modifier and key combinations with visual tag removal.
- **Layer Management & Reordering:**
  - Modernized `LayerSettingsWindow.axaml` with visual drag-and-drop handles.
  - Inline layer name renaming with focus loss commitment.
  - Layer 1 protection badge preventing accidental deletion or reordering of default layers.
  - Wrap-around layer navigation toggle.
  - Robust JSON configuration parser (`ConfigManager.ParseConfigJson`) supporting flat and nested general settings formats.
- **Unsaved Changes Tracking:** Visual asterisk indicators alert users when unsaved layer modifications are present.

### 4. Build, Packaging & Repository Cleanup
- **Inno Setup Installer:** Generated standalone Windows x64 setup executable `Installer releases/PadAvanForce_x64_v2.5-Setup.exe` (34.3 MB).
- **Directory Structure:** Standardized output locations:
  - `Firmware releases/` for compiled `.bin` firmware artifacts.
  - `Installer releases/` for Windows `.exe` setup installers.
- **Git Tracking Rules:** Updated `.gitignore` to track binaries within the official release directories while continuing to ignore intermediate build artifacts.
- **CI / GitHub Actions:** Corrected project and sketch paths in `.github/workflows/build.yml` for automated CI builds.
- **Global Rebranding:** Updated all occurrences of legacy naming to **Pad-Avan** and **Pad-Avan Force** across `README.md`, `PROJECT_DESCRIPTION.md`, `CONTRIBUTING.md`, tools documentation, and license files.

---

## Upgrade Instructions

### Desktop Software Upgrade
1. **Existing Installations:**
   - Open **Update Manager** from the app menu and click **Check for Updates**.
   - Click **Update Application** to download and silently install the update with automatic restart.
   - Alternatively, download and run `PadAvanForce_x64_v2.5-Setup.exe` directly from the release page (no prior uninstallation needed; settings and configurations are preserved).

### Firmware Upgrade
1. Connect the Pad-Avan device via USB.
2. In Pad-Avan Force, open **Update Manager** and click **Update Firmware**, or navigate directly to https://pad-avan.base44.app/flasher in Chrome/Edge.
3. Select your ESP32-S3 / FeatherS3 port when prompted and click **Program**.

