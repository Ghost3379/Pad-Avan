# PowerShell script to build and create installer
# Run this script to publish the app and create an installer

Write-Host "Building Pad-Avan Force..." -ForegroundColor Green

# Step 1: Clean previous publish
if (Test-Path "publish") {
    Remove-Item -Recurse -Force "publish"
    Write-Host "Cleaned previous publish directory" -ForegroundColor Yellow
}

# Step 2: Publish as self-contained (includes .NET runtime)
Write-Host "Publishing application..." -ForegroundColor Green
dotnet publish "Pad-Avan Force\Pad-Avan Force.csproj" `
    --configuration Release `
    --output "publish" `
    --self-contained true `
    --runtime win-x64 `
    -p:PublishSingleFile=false `
    -p:IncludeNativeLibrariesForSelfExtract=true

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Publish successful!" -ForegroundColor Green

# Step 3: Check if Inno Setup is installed
$innoSetupPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $innoSetupPath)) {
    $innoSetupPath = "C:\Program Files\Inno Setup 6\ISCC.exe"
}

if (-not (Test-Path $innoSetupPath)) {
    Write-Host "Inno Setup not found!" -ForegroundColor Red
    Write-Host "Please install Inno Setup from: https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
    Write-Host "Or manually compile installer.iss using Inno Setup Compiler" -ForegroundColor Yellow
    exit 1
}

# Step 4: Create installer directory
if (-not (Test-Path "installer")) {
    New-Item -ItemType Directory -Path "installer" | Out-Null
}

# Step 4.5: Delete any existing installer to avoid conflicts
if (Test-Path "installer\PadAvanForce-Setup.exe") {
    Write-Host "Removing existing installer..." -ForegroundColor Yellow
    Remove-Item "installer\PadAvanForce-Setup.exe" -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
}

# Step 5: Compile installer
Write-Host "Creating installer..." -ForegroundColor Green
& $innoSetupPath "installer.iss"

if ($LASTEXITCODE -eq 0) {
    Write-Host "Installer created successfully!" -ForegroundColor Green
    Write-Host "Installer location: installer\PadAvanForce-Setup.exe" -ForegroundColor Cyan
} else {
    Write-Host "Installer creation failed!" -ForegroundColor Red
    exit 1
}

