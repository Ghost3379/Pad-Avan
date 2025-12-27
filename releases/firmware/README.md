# Firmware Binaries

This folder contains compiled firmware binaries (.bin files) for distribution.

## How to Export Binaries from Arduino IDE

1. Open the firmware file in Arduino IDE:
   - For FeatherS3D (RECOMMENDED): `FeatherS3 scripts/Arduino version/padavan_fs3d/padavan_fs3d.ino`
   - For FeatherS3 (Legacy): `FeatherS3 scripts/Arduino version/padavan_fs3/padavan_fs3.ino` (if available)

2. Select the correct board:
   - **Unexpected Maker FeatherS3D** (for padavan_fs3d - RECOMMENDED)
   - **Unexpected Maker FeatherS3** (for padavan_fs3 - Legacy)

3. Verify/Compile the sketch (Ctrl+R)

4. Export the compiled binary:
   - Go to: **Sketch → Export compiled Binary**
   - The `.bin` file will be created in the same folder as the `.ino` file

5. Copy the `.bin` file to this folder:
   - Rename it appropriately (e.g., `padawan_fs3-v1.0.0.bin`, `padavan_fs3d-v1.0.2.bin`)
   - Place it in this `releases/firmware/` directory

## File Naming Convention

- `padavan_fs3d-v{VERSION}.bin` - For FeatherS3D boards (RECOMMENDED)
- `padavan_fs3-v{VERSION}.bin` - For FeatherS3 boards (Legacy)

Example:
- `padavan_fs3d-v1.0.3.bin` (current version)
- `padavan_fs3-v1.0.0.bin` (legacy)

## Including in GitHub Releases

When creating a GitHub release:
1. Attach the `.bin` files as release assets
2. Users can download and flash them manually, or
3. The Pad-Avan Force app can automatically detect and download them for firmware updates

