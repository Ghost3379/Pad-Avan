# Creating an Installer for Pad-Avan Force

## Quick Start (Easiest Method)

### Option 1: Self-Contained Publish (No Installer Needed)

Just publish the app as a single folder that users can run directly:

```powershell
dotnet publish "Pad-Avan Force\Pad-Avan Force.csproj" --configuration Release --output "publish" --self-contained true --runtime win-x64
```

This creates a `publish` folder with everything needed. Users can just copy this folder anywhere and run `Pad-Avan Force.exe`.

### Option 2: Inno Setup Installer (Recommended)

1. **Install Inno Setup** (free):
   - Download from: https://jrsoftware.org/isdl.php
   - Install it (default location is fine)

2. **Build the installer**:
   ```powershell
   .\build-installer.ps1
   ```
   
   Or manually:
   - Open `installer.iss` in Inno Setup Compiler
   - Click "Build" → "Compile"
   - The installer will be created in the `installer` folder

3. **Customize** (optional):
   - Edit `installer.iss` to change app name, version, publisher info
   - Add license file, readme, etc.
   - Customize icons and shortcuts

## What the Installer Does

- Installs the application to `C:\Program Files\Pad-Avan Force`
- Creates Start Menu shortcuts
- Optionally creates desktop shortcut
- Adds uninstaller to Windows Add/Remove Programs
- Includes all dependencies (.NET runtime if self-contained)

## Alternative: WiX Toolset (MSI Installer)

For a more professional MSI installer:

1. Install WiX Toolset: https://wixtoolset.org/
2. Create a `.wxs` file (WiX XML)
3. Compile with `candle` and `light` tools

More complex but gives you a standard MSI installer.

## Publishing Options

### Self-Contained (Recommended)
- Includes .NET runtime (~100MB)
- No need for users to install .NET separately
- Larger download but works everywhere

### Framework-Dependent
- Smaller size (~10-20MB)
- Requires users to have .NET 8.0 Runtime installed
- Better for updates (can update just the app)

## Notes

- The `installer.iss` script is configured for self-contained publish
- Adjust paths in `installer.iss` if your publish output is different
- Test the installer on a clean Windows machine before distributing

