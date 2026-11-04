using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Media;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using PadAwan_Force.Models;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.IO.Ports;
using System.Linq;

namespace PadAwan_Force.ViewModels
{
    public partial class UpdateWindowViewModel : ViewModelBase
    {
        private readonly FeatherConnection? _featherConnection;
        private readonly HttpClient _httpClient = new HttpClient();

        [ObservableProperty]
        private string deviceFirmwareVersion = "Unknown";

        [ObservableProperty]
        private string softwareVersion = "1.0.0";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LatestFirmwareDisplay))]
        private string latestFirmwareVersion = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LatestSoftwareDisplay))]
        private string latestSoftwareVersion = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FirmwareStatusBadge))]
        [NotifyPropertyChangedFor(nameof(FirmwareStatusColor))]
        [NotifyPropertyChangedFor(nameof(SoftwareStatusBadge))]
        [NotifyPropertyChangedFor(nameof(SoftwareStatusColor))]
        private bool hasUpdateInfo = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SoftwareStatusBadge))]
        [NotifyPropertyChangedFor(nameof(SoftwareStatusColor))]
        private bool canUpdateSoftware = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FirmwareStatusBadge))]
        [NotifyPropertyChangedFor(nameof(FirmwareStatusColor))]
        private bool canUpdateFirmware = false;

        [ObservableProperty]
        private bool isUpdating = false;

        [ObservableProperty]
        private double updateProgress = 0;

        [ObservableProperty]
        private string updateStatus = "";

        [ObservableProperty]
        private string updateProgressText = "";

        [ObservableProperty]
        private string statusMessage = "";

        [ObservableProperty]
        private IBrush statusMessageColor = Brushes.White;

        // Computed display properties
        public string LatestFirmwareDisplay => string.IsNullOrEmpty(LatestFirmwareVersion) ? "—" : LatestFirmwareVersion;
        public string LatestSoftwareDisplay => string.IsNullOrEmpty(LatestSoftwareVersion) ? "—" : LatestSoftwareVersion;

        public string FirmwareStatusBadge
        {
            get
            {
                if (!HasUpdateInfo) return "Checking...";
                if (CanUpdateFirmware) return "Update Available";
                return "Up to Date";
            }
        }

        public IBrush FirmwareStatusColor
        {
            get
            {
                if (!HasUpdateInfo) return Brushes.Gray;
                if (CanUpdateFirmware) return new SolidColorBrush(Color.Parse("#00FFFF"));
                return Brushes.LimeGreen;
            }
        }

        public string SoftwareStatusBadge
        {
            get
            {
                if (!HasUpdateInfo) return "Checking...";
                if (CanUpdateSoftware) return "Update Available";
                return "Up to Date";
            }
        }

        public IBrush SoftwareStatusColor
        {
            get
            {
                if (!HasUpdateInfo) return Brushes.Gray;
                if (CanUpdateSoftware) return new SolidColorBrush(Color.Parse("#00FFFF"));
                return Brushes.LimeGreen;
            }
        }

        // GitHub repository info
        private const string GITHUB_REPO_OWNER = "Ghost3379";
        private const string GITHUB_REPO_NAME = "Pad-Avan";
        private const string GITHUB_API_BASE = "https://api.github.com";
        
        // TODO: Replace with your actual Base44 website URL
        private const string FIRMWARE_FLASHER_URL = "https://pad-avan.base44.app/Flasher";

        public UpdateWindowViewModel(FeatherConnection? featherConnection = null)
        {
            _featherConnection = featherConnection;
            
            // Get software version from assembly
            var assembly = Assembly.GetExecutingAssembly();
            var versionAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (versionAttribute != null)
            {
                string version = versionAttribute.InformationalVersion;
                // Remove commit hash if present (format: "1.0.0+abc123")
                int plusIndex = version.IndexOf('+');
                if (plusIndex > 0)
                {
                    version = version.Substring(0, plusIndex);
                }
                if (version.EndsWith(".0") && version.Count(c => c == '.') > 1)
                {
                    version = version.Substring(0, version.Length - 2);
                }
                SoftwareVersion = $"v{version}";
            }
            else
            {
                var fileVersion = FileVersionInfo.GetVersionInfo(assembly.Location);
                string version = fileVersion.FileVersion ?? "1.0.0";
                // Remove commit hash if present
                int plusIndex = version.IndexOf('+');
                if (plusIndex > 0)
                {
                    version = version.Substring(0, plusIndex);
                }
                if (version.EndsWith(".0") && version.Count(c => c == '.') > 1)
                {
                    version = version.Substring(0, version.Length - 2);
                }
                SoftwareVersion = $"v{version}";
            }

            // Try to get firmware version from device
            _ = LoadDeviceFirmwareVersionAsync();
        }

        public async Task LoadDeviceFirmwareVersionAsync()
        {
            if (_featherConnection != null && _featherConnection.IsConnected)
            {
                DeviceFirmwareVersion = "Loading...";
                var version = await _featherConnection.GetFirmwareVersionAsync();
                if (version != null && !string.IsNullOrEmpty(version))
                {
                    DeviceFirmwareVersion = $"v{version.TrimStart('v', 'V')}";
                }
                else if (!string.IsNullOrEmpty(_featherConnection.FirmwareVersion) && _featherConnection.FirmwareVersion != "v1.2.3")
                {
                    // Fallback to last known confirmed version
                    string cached = _featherConnection.FirmwareVersion.TrimStart('v', 'V');
                    DeviceFirmwareVersion = $"v{cached}";
                }
                else
                {
                    DeviceFirmwareVersion = "Unknown";
                }
            }
            else
            {
                DeviceFirmwareVersion = "Not connected";
            }
        }

        [RelayCommand]
        private async Task CheckForUpdatesAsync()
        {
            StatusMessage = "Checking for updates...";
            StatusMessageColor = Brushes.White;
            HasUpdateInfo = false;
            CanUpdateFirmware = false;

            try
            {
                // Refresh device firmware version first
                await LoadDeviceFirmwareVersionAsync();
                
                var latestRelease = await GetLatestReleaseAsync();
                if (latestRelease != null)
                {
                    // Extract versions from release
                    LatestFirmwareVersion = latestRelease.FirmwareVersion ?? "Unknown";
                    LatestSoftwareVersion = latestRelease.SoftwareVersion ?? "Unknown";
                    
                    HasUpdateInfo = true;
                    
                    // Compare firmware versions (remove 'v' prefix for comparison)
                    string deviceVersion = DeviceFirmwareVersion.Replace("v", "").Replace("V", "").Trim();
                    string latestFirmware = LatestFirmwareVersion.Replace("v", "").Replace("V", "").Trim();
                    
                    // Check if newer firmware is available
                    bool hasNewerVersion = !string.IsNullOrEmpty(latestFirmware) && 
                                          !string.IsNullOrEmpty(deviceVersion) &&
                                          deviceVersion != "Not connected" && 
                                          deviceVersion != "Unknown" &&
                                          deviceVersion != "Loading..." &&
                                          CompareVersions(deviceVersion, latestFirmware) < 0;
                    
                    // Enable update button if version is unknown (can't compare) OR if newer version available
                    CanUpdateFirmware = deviceVersion == "Unknown" || 
                                       deviceVersion == "Not connected" ||
                                       hasNewerVersion;
                    
                    // Compare software versions
                    string currentSoftware = SoftwareVersion.Replace("v", "").Replace("V", "").Trim();
                    string latestSoftware = LatestSoftwareVersion.Replace("v", "").Replace("V", "").Trim();
                    
                    bool hasNewerSoftware = !string.IsNullOrEmpty(latestSoftware) && 
                                           !string.IsNullOrEmpty(currentSoftware) &&
                                           CompareVersions(currentSoftware, latestSoftware) < 0;
                    
                    CanUpdateSoftware = hasNewerSoftware;
                    
                    if (hasNewerSoftware && hasNewerVersion)
                    {
                        StatusMessage = "Updates available for both firmware and software!";
                        StatusMessageColor = Brushes.LimeGreen;
                    }
                    else if (hasNewerSoftware)
                    {
                        StatusMessage = "Software update available!";
                        StatusMessageColor = Brushes.LimeGreen;
                    }
                    else if (hasNewerVersion)
                    {
                        StatusMessage = "Firmware update available!";
                        StatusMessageColor = Brushes.LimeGreen;
                    }
                    else
                    {
                        StatusMessage = "You are running the latest versions!";
                        StatusMessageColor = Brushes.LimeGreen;
                    }
                }
                else
                {
                    StatusMessage = "No releases found on GitHub.";
                    StatusMessageColor = Brushes.Yellow;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error checking for updates: {ex.Message}";
                StatusMessageColor = Brushes.Red;
                System.Diagnostics.Debug.WriteLine($"Update check error: {ex}");
            }
        }

        private async Task<GitHubRelease?> GetLatestReleaseAsync()
        {
            try
            {
                string url = $"{GITHUB_API_BASE}/repos/{GITHUB_REPO_OWNER}/{GITHUB_REPO_NAME}/releases/latest";
                
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "PadAwan-Force-Updater");
                
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    var release = JsonSerializer.Deserialize<GitHubRelease>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    if (release != null)
                    {
                        // Extract firmware and software versions from release
                        // Format: "v1.0.0" or "Firmware: v1.0.0, Software: v1.0.0"
                        release.FirmwareVersion = ExtractVersionFromRelease(release, "firmware");
                        release.SoftwareVersion = ExtractVersionFromRelease(release, "software");
                        release.DownloadUrl = release.Assets?.FirstOrDefault(a => a.Name?.EndsWith(".bin") == true)?.BrowserDownloadUrl;
                    }
                    
                    return release;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"GitHub API error: {response.StatusCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching release: {ex.Message}");
                return null;
            }
        }

        private string? ExtractVersionFromRelease(GitHubRelease release, string type)
        {
            // Try to extract from tag name using new naming convention:
            // FS-v2.0 = Both Firmware and Software at v2.0
            // F-v2.x = Firmware-only at v2.x
            // S-v2.x = Software-only at v2.x
            if (release.TagName != null)
            {
                string tag = release.TagName.ToUpper();
                
                // Check for new naming convention: FS-, F-, or S- prefix
                if (tag.StartsWith("FS-"))
                {
                    // Combined update: FS-v2.0 or FS-v2.0-v2.1 format
                    // Pattern: FS-v2.0-v2.1 (first is firmware, second is software)
                    var matchTwoVersions = System.Text.RegularExpressions.Regex.Match(tag, @"FS-V?(\d+\.\d+(?:\.\d+)?)-V?(\d+\.\d+(?:\.\d+)?)");
                    if (matchTwoVersions.Success)
                    {
                        // Two versions found: firmware first, software second
                        if (type == "firmware")
                        {
                            return $"v{matchTwoVersions.Groups[1].Value}";
                        }
                        else if (type == "software")
                        {
                            return $"v{matchTwoVersions.Groups[2].Value}";
                        }
                    }
                    else
                    {
                        // Single version: both firmware and software use the same version
                        var match = System.Text.RegularExpressions.Regex.Match(tag, @"FS-V?(\d+\.\d+(?:\.\d+)?)");
                        if (match.Success) return $"v{match.Groups[1].Value}";
                    }
                }
                else if (tag.StartsWith("F-"))
                {
                    // Firmware-only update
                    if (type == "firmware")
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(tag, @"F-V?(\d+\.\d+(?:\.\d+)?)");
                        if (match.Success) return $"v{match.Groups[1].Value}";
                    }
                }
                else if (tag.StartsWith("S-"))
                {
                    // Software-only update
                    if (type == "software")
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(tag, @"S-V?(\d+\.\d+(?:\.\d+)?)");
                        if (match.Success) return $"v{match.Groups[1].Value}";
                    }
                }
                
                // Fallback: Try old naming convention (backward compatibility)
                string tagLower = tag.ToLower();
                if (type == "firmware" && tagLower.Contains("firmware"))
                {
                    // Extract version from tag like "firmware-v1.0.0"
                    var match = System.Text.RegularExpressions.Regex.Match(tagLower, @"v?(\d+\.\d+(?:\.\d+)?)");
                    if (match.Success) return $"v{match.Groups[1].Value}";
                }
                else if (type == "software" && tagLower.Contains("software"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(tagLower, @"v?(\d+\.\d+(?:\.\d+)?)");
                    if (match.Success) return $"v{match.Groups[1].Value}";
                }
                else if (!tagLower.Contains("firmware") && !tagLower.Contains("software") && 
                         !tagLower.StartsWith("f-") && !tagLower.StartsWith("s-") && !tagLower.StartsWith("fs-"))
                {
                    // Assume tag is general version (e.g., "v1.0.0")
                    var match = System.Text.RegularExpressions.Regex.Match(tagLower, @"v?(\d+\.\d+(?:\.\d+)?)");
                    if (match.Success) return $"v{match.Groups[1].Value}";
                }
            }
            
            // Try to extract from body/description
            if (release.Body != null)
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    release.Body, 
                    $@"{type}[:\s]+v?(\d+\.\d+(?:\.\d+)?)", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
                if (match.Success) return $"v{match.Groups[1].Value}";
            }
            
            return null; // Return null if no version found (instead of tag name)
        }

        [RelayCommand]
        private async Task UpdateFirmwareAsync()
        {
            if (_featherConnection == null)
            {
                StatusMessage = "Cannot update firmware: Device connection required";
                StatusMessageColor = Brushes.Red;
                return;
            }
            
            if (!_featherConnection.IsConnected)
            {
                StatusMessage = "Cannot update firmware: Device not connected";
                StatusMessageColor = Brushes.Red;
                return;
            }

            // Set flag to prevent reconnection during update
                _featherConnection.IsUpdatingFirmware = true;

            IsUpdating = true;
            UpdateStatus = "Preparing for update...";
            UpdateProgress = 0;
            StatusMessage = "";

            try
            {
                // Step 1: Get latest release info (for firmware URL to pass to website)
                UpdateStatus = "Preparing for update...";
                UpdateProgress = 20;
                var release = await GetLatestReleaseAsync();
                
                string firmwareUrl = release?.DownloadUrl ?? "";
                string firmwareVersion = release?.FirmwareVersion ?? "latest";

                // Step 2: Enter bootloader mode
                UpdateStatus = "Preparing for update...";
                UpdateProgress = 40;
                StatusMessage = "Putting device into bootloader mode...";
                
                bool bootloaderSuccess = await _featherConnection.EnterBootloaderModeAsync();
                if (!bootloaderSuccess)
                {
                    StatusMessage = "Failed to enter bootloader mode. Please try again.";
                    StatusMessageColor = Brushes.Red;
                    return;
                }

                // Step 3: Disconnect and wait for device to switch modes
                UpdateStatus = "Preparing for update...";
                UpdateProgress = 60;
                StatusMessage = "Device is entering bootloader mode. Please wait...";
                
                _featherConnection.Disconnect();
                await Task.Delay(3000); // Wait for device to switch from TinyUSB CDC to USB JTAG

                // Step 4: Open website
                UpdateStatus = "Preparing for update...";
                UpdateProgress = 80;
                StatusMessage = "Opening firmware flasher...";
                        
                // Open the base44 website with firmware URL and parameters
                // alreadyInBootloader=true tells the website to skip the first connection step
                string websiteUrl = $"{FIRMWARE_FLASHER_URL}?firmwareUrl={Uri.EscapeDataString(firmwareUrl)}&version={Uri.EscapeDataString(firmwareVersion)}&autoFlash=true&alreadyInBootloader=true";
                
                Process.Start(new ProcessStartInfo
                {
                    FileName = websiteUrl,
                    UseShellExecute = true
                });
                
                UpdateProgress = 100;
                UpdateStatus = "Ready";
                StatusMessage = "Device is in bootloader mode. The firmware flasher website has been opened. Please select 'USB JTAG/serial debug unit' when prompted.";
                StatusMessageColor = Brushes.LimeGreen;
                    }
                    catch (Exception ex)
                    {
                StatusMessage = $"Error preparing for update: {ex.Message}";
                StatusMessageColor = Brushes.Red;
                System.Diagnostics.Debug.WriteLine($"Firmware update error: {ex}");
            }
            finally
            {
                // Reset progress bar visibility, keeping _featherConnection.IsUpdatingFirmware = true to prevent reconnects
                IsUpdating = false;
            }
        }


        [RelayCommand]
        private async Task UpdateSoftwareAsync()
        {
            if (!CanUpdateSoftware)
            {
                StatusMessage = "Cannot update software: No update available";
                StatusMessageColor = Brushes.Red;
                return;
            }

            IsUpdating = true;
            UpdateStatus = "Updating software...";
            UpdateProgress = 5;
            StatusMessage = "Checking latest release...";
            StatusMessageColor = Brushes.White;

            try
            {
                // Step 1: Get latest release info
                var release = await GetLatestReleaseAsync();
                
                if (release == null)
                {
                    StatusMessage = "No release found on GitHub.";
                    StatusMessageColor = Brushes.Red;
                    return;
                }
                
                // Step 2: Find installer in release assets
                string? installerUrl = null;
                if (release.Assets != null)
                {
                    foreach (var asset in release.Assets)
                    {
                        if (asset.Name != null &&
                            (asset.Name.EndsWith("-Setup.exe", StringComparison.OrdinalIgnoreCase) ||
                             asset.Name.EndsWith("Setup.exe", StringComparison.OrdinalIgnoreCase) ||
                             (asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && asset.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase))))
                        {
                            installerUrl = asset.BrowserDownloadUrl;
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(installerUrl))
                {
                    StatusMessage = "No installer found in latest release assets.";
                    StatusMessageColor = Brushes.Red;
                    return;
                }

                // Step 3: Download installer
                UpdateStatus = "Downloading update...";
                UpdateProgress = 15;
                string installerPath = await DownloadInstallerAsync(installerUrl);
                
                if (string.IsNullOrEmpty(installerPath) || !File.Exists(installerPath))
                {
                    StatusMessage = "Failed to download installer.";
                    StatusMessageColor = Brushes.Red;
                    return;
                }

                // Step 4: Run silent installer and automatically restart
                UpdateStatus = "Preparing update...";
                UpdateProgress = 95;
                
                string localAppExe = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs", "Pad-Avan Force", "Pad-Avan Force.exe");

                string targetExe = Environment.ProcessPath ?? localAppExe;

                // If running from dev bin directory, prefer installed copy if it exists
                if (targetExe.Contains(@"\bin\", StringComparison.OrdinalIgnoreCase) && File.Exists(localAppExe))
                {
                    targetExe = localAppExe;
                }

                bool isAllUsers = targetExe.Contains("Program Files", StringComparison.OrdinalIgnoreCase);

                string tempDir = Path.Combine(Path.GetTempPath(), "PadAwan-Force");
                Directory.CreateDirectory(tempDir);
                string batchFile = Path.Combine(tempDir, "silent_update.bat");

                string allUsersParam = isAllUsers ? " /ALLUSERS" : "";
                string batchScript = $@"@echo off
rem Wait for Pad-Avan Force to fully exit and release file locks
timeout /t 2 /nobreak >nul

rem Run Inno Setup installer silently with zero prompts
start /wait """" ""{installerPath}"" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART{allUsersParam}

rem Relaunch Pad-Avan Force
start """" ""{targetExe}""

rem Clean up batch file
(goto) 2>nul & del ""%~f0""
";
                await File.WriteAllTextAsync(batchFile, batchScript);

                UpdateProgress = 100;
                UpdateStatus = "Updating and restarting...";
                StatusMessage = "Update ready. Installing in the background and restarting Pad-Avan Force...";
                StatusMessageColor = Brushes.LimeGreen;

                await Task.Delay(1200);

                var processInfo = new ProcessStartInfo
                {
                    FileName = batchFile,
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    Verb = isAllUsers ? "runas" : ""
                };

                Process.Start(processInfo);

                // Exit this application
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error updating software: {ex.Message}";
                StatusMessageColor = Brushes.Red;
                System.Diagnostics.Debug.WriteLine($"Software update error: {ex}");
            }
            finally
            {
                IsUpdating = false;
                if (UpdateProgress < 100) UpdateProgress = 0;
            }
        }

        private async Task<string> DownloadInstallerAsync(string downloadUrl)
        {
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "PadAwan-Force");
                Directory.CreateDirectory(tempDir);
                
                string fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
                if (string.IsNullOrEmpty(fileName)) fileName = "PadAvanForce-Setup.exe";
                
                string filePath = Path.Combine(tempDir, fileName);
                
                UpdateProgressText = "Downloading installer...";
                
                using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    
                    long? totalBytes = response.Content.Headers.ContentLength;
                    long downloadedBytes = 0;
                    
                    using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    {
                        var buffer = new byte[8192];
                        int bytesRead;
                        
                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            downloadedBytes += bytesRead;
                            
                            if (totalBytes.HasValue)
                            {
                                double progress = 20 + (downloadedBytes / (double)totalBytes.Value * 70); // 20-90%
                                UpdateProgress = progress;
                                UpdateProgressText = $"Downloading: {downloadedBytes / 1024 / 1024} MB / {totalBytes.Value / 1024 / 1024} MB";
                            }
                        }
                    }
                }
                
                UpdateProgressText = "Download complete";
                return filePath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Download error: {ex.Message}");
                return string.Empty;
            }
        }

        // Helper method to compare version strings (e.g., "v1.0.0" vs "v1.0.1")
        private int CompareVersions(string version1, string version2)
        {
            // Remove 'v' prefix if present
            version1 = version1.TrimStart('v', 'V').Trim();
            version2 = version2.TrimStart('v', 'V').Trim();

            var v1Parts = version1.Split('.');
            var v2Parts = version2.Split('.');

            int maxLength = Math.Max(v1Parts.Length, v2Parts.Length);
            for (int i = 0; i < maxLength; i++)
            {
                int v1Part = i < v1Parts.Length && int.TryParse(v1Parts[i], out int v1) ? v1 : 0;
                int v2Part = i < v2Parts.Length && int.TryParse(v2Parts[i], out int v2) ? v2 : 0;

                if (v1Part < v2Part) return -1;
                if (v1Part > v2Part) return 1;
            }

            return 0;
        }
    }

    // GitHub API Models
    public class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }
        
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        
        [JsonPropertyName("body")]
        public string? Body { get; set; }
        
        [JsonPropertyName("assets")]
        public List<GitHubAsset>? Assets { get; set; }
        
        [JsonPropertyName("published_at")]
        public DateTime? PublishedAt { get; set; }
        
        // Extracted versions (not from API)
        public string? FirmwareVersion { get; set; }
        public string? SoftwareVersion { get; set; }
        public string? DownloadUrl { get; set; }
    }

    public class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        
        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
        
        [JsonPropertyName("size")]
        public long Size { get; set; }
    }
}

