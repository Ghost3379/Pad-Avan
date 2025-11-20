using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Media;
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
        private string latestFirmwareVersion = "";

        [ObservableProperty]
        private string latestSoftwareVersion = "";

        [ObservableProperty]
        private bool hasUpdateInfo = false;

        [ObservableProperty]
        private bool canUpdateFirmware = false;

        [ObservableProperty]
        private bool canUpdateSoftware = false;

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

        // GitHub repository info
        private const string GITHUB_REPO_OWNER = "Ghost3379";
        private const string GITHUB_REPO_NAME = "PadAwan";
        private const string GITHUB_API_BASE = "https://api.github.com";

        public UpdateWindowViewModel(FeatherConnection? featherConnection = null)
        {
            _featherConnection = featherConnection;
            
            // Get software version from assembly
            var assembly = Assembly.GetExecutingAssembly();
            var versionAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (versionAttribute != null)
            {
                SoftwareVersion = $"v{versionAttribute.InformationalVersion}";
            }
            else
            {
                var fileVersion = FileVersionInfo.GetVersionInfo(assembly.Location);
                SoftwareVersion = $"v{fileVersion.FileVersion ?? "1.0.0"}";
            }

            // Try to get firmware version from device
            _ = LoadDeviceFirmwareVersionAsync();
        }

        private async Task LoadDeviceFirmwareVersionAsync()
        {
            if (_featherConnection != null && _featherConnection.IsConnected)
            {
                var version = await _featherConnection.GetFirmwareVersionAsync();
                if (version != null)
                {
                    DeviceFirmwareVersion = $"v{version}";
                }
                else
                {
                    DeviceFirmwareVersion = "Not connected";
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
            CanUpdateSoftware = false;

            try
            {
                var latestRelease = await GetLatestReleaseAsync();
                if (latestRelease != null)
                {
                    // Extract versions from release
                    LatestFirmwareVersion = latestRelease.FirmwareVersion ?? "Unknown";
                    LatestSoftwareVersion = latestRelease.SoftwareVersion ?? "Unknown";
                    
                    HasUpdateInfo = true;
                    
                    // Compare versions (remove 'v' prefix for comparison)
                    string deviceVersion = DeviceFirmwareVersion.Replace("v", "").Replace("V", "").Trim();
                    string softwareVersion = SoftwareVersion.Replace("v", "").Replace("V", "").Trim();
                    string latestFirmware = LatestFirmwareVersion.Replace("v", "").Replace("V", "").Trim();
                    string latestSoftware = LatestSoftwareVersion.Replace("v", "").Replace("V", "").Trim();
                    
                    CanUpdateFirmware = !string.IsNullOrEmpty(deviceVersion) && 
                                       deviceVersion != "Not connected" && 
                                       deviceVersion != "Unknown" &&
                                       CompareVersions(deviceVersion, latestFirmware) < 0;
                    
                    CanUpdateSoftware = CompareVersions(softwareVersion, latestSoftware) < 0;
                    
                    if (!CanUpdateFirmware && !CanUpdateSoftware)
                    {
                        StatusMessage = "You are running the latest versions!";
                        StatusMessageColor = Brushes.LimeGreen;
                    }
                    else
                    {
                        StatusMessage = "Updates available!";
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
            // Try to extract from tag name (e.g., "v1.0.0" or "firmware-v1.0.0")
            if (release.TagName != null)
            {
                string tag = release.TagName.ToLower();
                if (type == "firmware" && tag.Contains("firmware"))
                {
                    // Extract version from tag like "firmware-v1.0.0"
                    var match = System.Text.RegularExpressions.Regex.Match(tag, @"v?(\d+\.\d+\.\d+)");
                    if (match.Success) return $"v{match.Groups[1].Value}";
                }
                else if (type == "software" && tag.Contains("software"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(tag, @"v?(\d+\.\d+\.\d+)");
                    if (match.Success) return $"v{match.Groups[1].Value}";
                }
                else if (!tag.Contains("firmware") && !tag.Contains("software"))
                {
                    // Assume tag is general version (e.g., "v1.0.0")
                    var match = System.Text.RegularExpressions.Regex.Match(tag, @"v?(\d+\.\d+\.\d+)");
                    if (match.Success) return $"v{match.Groups[1].Value}";
                }
            }
            
            // Try to extract from body/description
            if (release.Body != null)
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    release.Body, 
                    $@"{type}[:\s]+v?(\d+\.\d+\.\d+)", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
                if (match.Success) return $"v{match.Groups[1].Value}";
            }
            
            return release.TagName; // Fallback to tag name
        }

        [RelayCommand]
        private async Task UpdateFirmwareAsync()
        {
            if (!CanUpdateFirmware || _featherConnection == null || !_featherConnection.IsConnected)
            {
                StatusMessage = "Cannot update firmware: Device not connected";
                StatusMessageColor = Brushes.Red;
                return;
            }

            IsUpdating = true;
            UpdateStatus = "Updating firmware...";
            UpdateProgress = 0;
            StatusMessage = "";

            try
            {
                // Step 1: Get latest release info
                UpdateStatus = "Checking for firmware...";
                UpdateProgress = 10;
                var release = await GetLatestReleaseAsync();
                
                if (release == null || string.IsNullOrEmpty(release.DownloadUrl))
                {
                    StatusMessage = "No firmware file found in latest release.";
                    StatusMessageColor = Brushes.Red;
                    return;
                }

                // Step 2: Download .bin file
                UpdateStatus = "Downloading firmware...";
                UpdateProgress = 20;
                string binPath = await DownloadFirmwareAsync(release.DownloadUrl);
                
                if (string.IsNullOrEmpty(binPath) || !File.Exists(binPath))
                {
                    StatusMessage = "Failed to download firmware file.";
                    StatusMessageColor = Brushes.Red;
                    return;
                }

                // Step 3: Flash firmware using esptool
                UpdateStatus = "Flashing firmware...";
                UpdateProgress = 60;
                bool success = await FlashFirmwareAsync(binPath, _featherConnection.ComPort);
                
                if (success)
                {
                    UpdateProgress = 100;
                    UpdateStatus = "Firmware updated successfully!";
                    StatusMessage = "✅ Firmware update completed! Please reconnect your device.";
                    StatusMessageColor = Brushes.LimeGreen;
                    
                    // Clean up downloaded file
                    try { File.Delete(binPath); } catch { }
                }
                else
                {
                    StatusMessage = "❌ Firmware flash failed. Check console for details.";
                    StatusMessageColor = Brushes.Red;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error updating firmware: {ex.Message}";
                StatusMessageColor = Brushes.Red;
                System.Diagnostics.Debug.WriteLine($"Firmware update error: {ex}");
            }
            finally
            {
                IsUpdating = false;
                if (UpdateProgress < 100) UpdateProgress = 0;
            }
        }

        private async Task<string> DownloadFirmwareAsync(string downloadUrl)
        {
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "PadAwan-Force");
                Directory.CreateDirectory(tempDir);
                
                string fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
                if (string.IsNullOrEmpty(fileName)) fileName = "firmware.bin";
                
                string filePath = Path.Combine(tempDir, fileName);
                
                UpdateProgressText = "Downloading...";
                
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
                                double progress = 20 + (downloadedBytes / (double)totalBytes.Value * 40); // 20-60%
                                UpdateProgress = progress;
                                UpdateProgressText = $"Downloading: {downloadedBytes / 1024} KB / {totalBytes.Value / 1024} KB";
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

        private async Task<bool> FlashFirmwareAsync(string binPath, string comPort)
        {
            try
            {
                // Find esptool.py or esptool.exe
                string esptoolPath = FindEsptool();
                
                if (string.IsNullOrEmpty(esptoolPath))
                {
                    StatusMessage = "esptool not found. Please install esptool.py or esptool.exe";
                    StatusMessageColor = Brushes.Red;
                    return false;
                }

                UpdateProgressText = "Erasing flash...";
                UpdateProgress = 60;
                
                // Step 1: Erase flash
                bool eraseSuccess = await RunEsptoolAsync(esptoolPath, comPort, new[] { "erase_flash" });
                if (!eraseSuccess)
                {
                    StatusMessage = "Failed to erase flash";
                    return false;
                }

                UpdateProgressText = "Writing firmware...";
                UpdateProgress = 70;
                
                // Step 2: Write firmware
                bool writeSuccess = await RunEsptoolAsync(esptoolPath, comPort, new[] 
                { 
                    "write_flash", 
                    "0x0", 
                    binPath 
                });
                
                if (!writeSuccess)
                {
                    StatusMessage = "Failed to write firmware";
                    return false;
                }

                UpdateProgressText = "Verifying...";
                UpdateProgress = 90;
                
                // Step 3: Verify
                bool verifySuccess = await RunEsptoolAsync(esptoolPath, comPort, new[] 
                { 
                    "verify_flash", 
                    "0x0", 
                    binPath 
                });
                
                UpdateProgress = 95;
                return verifySuccess;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Flash error: {ex.Message}");
                return false;
            }
        }

        private string? FindEsptool()
        {
            // Try esptool.exe first (Windows)
            string[] possiblePaths = new[]
            {
                "esptool.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Arduino15", "packages", "esp32", "tools", "esptool_py", "*", "esptool.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".platformio", "penv", "Scripts", "esptool.exe"),
                "python", // Fallback to python + esptool.py
                "python3"
            };

            foreach (var path in possiblePaths)
            {
                if (path.Contains("*"))
                {
                    // Search in directory
                    var dir = Path.GetDirectoryName(path);
                    var pattern = Path.GetFileName(path);
                    if (Directory.Exists(dir))
                    {
                        var files = Directory.GetFiles(dir, pattern, SearchOption.AllDirectories);
                        if (files.Length > 0) return files[0];
                    }
                }
                else
                {
                    // Check if command exists
                    try
                    {
                        var process = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = path,
                                Arguments = "--version",
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = true
                            }
                        };
                        process.Start();
                        process.WaitForExit(2000);
                        if (process.ExitCode == 0 || path == "python" || path == "python3")
                        {
                            // For python, check if esptool module is available
                            if (path == "python" || path == "python3")
                            {
                                var checkProcess = new Process
                                {
                                    StartInfo = new ProcessStartInfo
                                    {
                                        FileName = path,
                                        Arguments = "-m esptool version",
                                        UseShellExecute = false,
                                        RedirectStandardOutput = true,
                                        RedirectStandardError = true,
                                        CreateNoWindow = true
                                    }
                                };
                                checkProcess.Start();
                                checkProcess.WaitForExit(2000);
                                if (checkProcess.ExitCode == 0)
                                {
                                    return path; // Return python, we'll use "python -m esptool"
                                }
                            }
                            else
                            {
                                return path;
                            }
                        }
                    }
                    catch { }
                }
            }

            return null;
        }

        private async Task<bool> RunEsptoolAsync(string esptoolPath, string comPort, string[] arguments)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = esptoolPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                // Build arguments
                var argsList = new List<string> { "--port", comPort, "--baud", "921600", "--chip", "esp32s3" };
                
                // If esptoolPath is python/python3, use "python -m esptool"
                if (esptoolPath == "python" || esptoolPath == "python3")
                {
                    processInfo.FileName = esptoolPath;
                    argsList.Insert(0, "-m");
                    argsList.Insert(1, "esptool");
                }
                
                argsList.AddRange(arguments);
                processInfo.Arguments = string.Join(" ", argsList);

                System.Diagnostics.Debug.WriteLine($"Running: {processInfo.FileName} {processInfo.Arguments}");

                var process = new Process { StartInfo = processInfo };
                
                var output = new System.Text.StringBuilder();
                var error = new System.Text.StringBuilder();
                
                process.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) error.AppendLine(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                
                await Task.Run(() => process.WaitForExit(60000)); // 60 second timeout
                
                System.Diagnostics.Debug.WriteLine($"esptool output: {output}");
                if (error.Length > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"esptool error: {error}");
                }

                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"esptool execution error: {ex.Message}");
                return false;
            }
        }

        [RelayCommand]
        private async Task UpdateSoftwareAsync()
        {
            StatusMessage = "Software update not yet implemented.";
            StatusMessageColor = Brushes.Yellow;
            
            // Software updates would require:
            // 1. Download new installer/executable
            // 2. Close current application
            // 3. Run installer
            // This is more complex and might be better handled by an installer system
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

