using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
#if WINDOWS
using System.Management; // für optionale VID/PID-Filterung
#endif

//hi, test from cursor, read in VS
//hi, test from VS, I read your message from cursor, means i can have both open and code in both

namespace PadAwan_Force.Models
{
    public class FeatherConnection
    {
        // Constants for ESP32-S3/CircuitPython compatibility
        private const int Baud = 115200; // statt 9600
        private const int ReadToMs = 300;  // Reduziert von 1000ms - PONG sollte sofort kommen
        private const int WriteToMs = 2000;
        private const string Eol = "\n"; // falls Firmware "\r\n" nutzt: entsprechend ändern

        public bool IsConnected { get; private set; }
        public string ComPort { get; private set; } = "None";
        public string Status { get; private set; } = "n/c";

        public int Battery { get; private set; } = 0;

        public int Color { get; private set; } = 0xFF0000;
        public SerialPort? SerialPort { get; private set; }
        
        // Flag to prevent reconnection during firmware update
        public bool IsUpdatingFirmware { get; set; } = false;

        // Track when connection was established to allow grace period for Windows port registration
        private DateTime? _connectionEstablishedTime = null;
        private const int ConnectionGracePeriodSeconds = 2; // Reduziert von 10s - schnellere Erkennung
        
        // Lock to prevent multiple simultaneous connection attempts
        private readonly object _connectionLock = new object();
        private bool _isConnecting = false;

        // Device Information Properties
        public string DeviceName { get; private set; } = "PadAwan Force";
        public string FirmwareVersion { get; private set; } = "v1.2.3";
        public string HardwareRevision { get; private set; } = "Rev A";
        public string SerialNumber { get; private set; } = "PAF-2024-001";
        public string Uptime { get; private set; } = "0h 0m";
        public string LastConnected { get; private set; } = "Never";
        public string DataTransferred { get; private set; } = "0 MB";

        public event EventHandler? ConnectionStatusChanged;
        public event EventHandler? DeviceInfoChanged;

        private static SerialPort CreatePort(string portName)
        {
            return new SerialPort(portName, Baud, Parity.None, 8, StopBits.One)
            {
                Handshake = Handshake.None,
                // IMPORTANT:
                // Many USB-CDC devices (including ESP32-S3 USB CDC) only start sending/receiving
                // once the host asserts DTR/RTS. Arduino Serial Monitor typically does this.
                // If we keep these disabled, we can open the port but never receive bytes (BytesToRead stays 0).
                DtrEnable = true,
                RtsEnable = true,
                ReadTimeout = ReadToMs,
                WriteTimeout = WriteToMs,
                NewLine = Eol,
                Encoding = Encoding.ASCII
            };
        }

        private static List<string> GetCandidatePorts()
        {
            // Schnell alle Ports holen
            var ports = SerialPort.GetPortNames().ToList();
            System.Diagnostics.Debug.WriteLine($"GetCandidatePorts: Found ports: {(ports.Count == 0 ? "<none>" : string.Join(", ", ports))}");
            
#if WINDOWS
            // Optional: Filter for ESP32/Adafruit VID:PID (303A:80D7)
            // ABER: WMI ist sehr langsam - nur verwenden wenn es nicht zu viele Ports gibt
            // Oder: Erst alle Ports testen, dann filtern nur wenn nötig
            if (ports.Count <= 10) // Nur filtern wenn nicht zu viele Ports
            {
            try
            {
                var filteredPorts = new List<string>();
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_SerialPort"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var deviceId = obj["DeviceID"]?.ToString();
                        var pnpDeviceId = obj["PNPDeviceID"]?.ToString();
                        
                        if (deviceId != null && pnpDeviceId != null && 
                            pnpDeviceId.Contains("VID_303A") && pnpDeviceId.Contains("PID_80D7"))
                        {
                            filteredPorts.Add(deviceId);
                        }
                    }
                }
                
                if (filteredPorts.Any())
                {
                        System.Diagnostics.Debug.WriteLine($"GetCandidatePorts: VID/PID filter matched: {string.Join(", ", filteredPorts)}");
                        // ESP32-Ports zuerst testen (höhere Priorität)
                        return filteredPorts.Concat(ports.Except(filteredPorts)).ToList();
                }
                
                System.Diagnostics.Debug.WriteLine("GetCandidatePorts: VID/PID filter matched no ports; falling back to all ports");
            }
                catch (Exception ex)
            {
                    // WMI kann langsam/fehlerhaft sein -> fallback auf rohe Portliste
                    System.Diagnostics.Debug.WriteLine($"GetCandidatePorts: WMI query failed, falling back to all ports. Error: {ex.Message}");
                }
            }
#endif
            
            return ports;
        }

        public async Task<bool> TryConnectAsync()
        {
            // Don't try to connect if firmware update is in progress
            if (IsUpdatingFirmware)
            {
                System.Diagnostics.Debug.WriteLine("Skipping connection attempt - firmware update in progress");
                return false;
            }
            
            // Prevent multiple simultaneous connection attempts
            lock (_connectionLock)
            {
                if (_isConnecting)
                {
                    System.Diagnostics.Debug.WriteLine("TryConnectAsync: Already connecting, skipping duplicate attempt");
                    return IsConnected; // Return current connection state
                }
                _isConnecting = true;
            }
            
            try
            {
                // 1) Bereits bestehende Verbindung prüfen
                if (IsConnected && SerialPort != null && SerialPort.IsOpen)
                {
                    if (await PingAsync())
                    {
                        _isConnecting = false;
                        return true;
                    }
                    Disconnect();
                    await Task.Delay(100);  // Reduziert von 300ms
                }

                // 2) Ports sammeln (optional erst nach VID/PID filtern)
                var ports = GetCandidatePorts();
                // Debug-Output nur wenn viele Ports (Performance)
                if (ports.Count > 3)
                {
                    System.Diagnostics.Debug.WriteLine($"Found {ports.Count} candidate ports");
                }
                
                foreach (var port in ports)
                {
                    if (await TestPortAsync(port))
                    {
                        // aktive Verbindung sauber neu aufbauen
                        try
                        {
                            SerialPort?.Close();
                            SerialPort?.Dispose();
                        } 
                        catch { /* ignore */ }

                        SerialPort = CreatePort(port);
                        try
                        {
                            SerialPort.Open();
                            // nach Open kurz stabilisieren lassen
                            await Task.Delay(50);  // Reduziert von 100ms - sehr schnelle Verbindung
                            SerialPort.DiscardInBuffer();
                            SerialPort.DiscardOutBuffer();

                            if (await PingAsync())
                            {
                                IsConnected = true;
                                ComPort = port;
                                Status = "Connected";
                                Color = 0x00FF00;
                                _connectionEstablishedTime = DateTime.Now; // Track when connection was established
                                OnConnectionStatusChanged();
                                System.Diagnostics.Debug.WriteLine($"Successfully connected to {port}");
                                _isConnecting = false;
                                return true;
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Open on {port} failed: {ex.Message}");
                        }

                        // fallback: schließen/entsorgen
                        try { SerialPort?.Close(); SerialPort?.Dispose(); } catch { }
                        SerialPort = null;
                    }
                }

                // nichts gefunden - but don't change status if we're already connected
                // This prevents overwriting a successful connection with "n/c" due to race conditions
                if (!IsConnected)
                {
                IsConnected = false;
                ComPort = "None";
                Status = "n/c";
                Color = 0xFF0000;
                Battery = 0;
                OnConnectionStatusChanged();
                System.Diagnostics.Debug.WriteLine("No FeatherS3 device found on any COM port");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("TryConnectAsync: No new device found, but already connected - keeping existing connection");
                }
                _isConnecting = false;
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Connection error: {ex.Message}");
                _isConnecting = false;
                // Don't change status if we're already connected - this prevents overwriting a good connection
                if (!IsConnected)
                {
                IsConnected = false;
                ComPort = "None";
                Status = $"Error: {ex.Message}";
                Color = 0xFF0000;
                Battery = 0;
                OnConnectionStatusChanged();
                }
                return false;
            }
        }

        private async Task<bool> TestPortAsync(string portName)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"TestPortAsync({portName}): Opening port...");
                using var test = CreatePort(portName);
                test.Open();
                System.Diagnostics.Debug.WriteLine($"TestPortAsync({portName}): Port opened, waiting 100ms...");
                await Task.Delay(250);             // Mehr Zeit für USB-CDC + DTR/RTS LineState
                test.DiscardInBuffer();
                test.DiscardOutBuffer();
                System.Diagnostics.Debug.WriteLine($"TestPortAsync({portName}): Buffers cleared");

                // PING schicken
                System.Diagnostics.Debug.WriteLine($"TestPortAsync({portName}): Sending PING...");
                test.Write("PING\n");  // Explizit \n statt WriteLine für bessere Kontrolle
                test.BaseStream.Flush();  // Explizit flushen
                System.Diagnostics.Debug.WriteLine($"TestPortAsync({portName}): PING sent, waiting 100ms for response...");
                await Task.Delay(150);  // Mehr Zeit für Antwort
                
                var started = DateTime.UtcNow;
                // Timeout erhöht - PONG sollte innerhalb von 1000ms kommen
                int timeoutMs = 1000;
                bool pongReceived = false;
                int readAttempts = 0;
                int pingsResent = 0;

                System.Diagnostics.Debug.WriteLine($"TestPortAsync({portName}): Starting to read responses (timeout: {timeoutMs}ms)...");
                
                while ((DateTime.UtcNow - started).TotalMilliseconds < timeoutMs && !pongReceived)
                {
                    readAttempts++;
                    int bytesAvailable = test.BytesToRead;
                    
                    // Prüfe ob Daten verfügbar sind, bevor ReadLine() aufgerufen wird
                    if (bytesAvailable > 0)
                {
                        System.Diagnostics.Debug.WriteLine($"TestPortAsync({portName}): Bytes available: {bytesAvailable} (attempt {readAttempts})");
                    try
                    {
                        var line = test.ReadLine()?.Trim();
                        if (!string.IsNullOrEmpty(line))
                        {
                            System.Diagnostics.Debug.WriteLine($"TestPortAsync({portName}): Received line: '{line}'");
                            
                            // Check for PONG (main response)
                            if (line.Equals("PONG", StringComparison.OrdinalIgnoreCase))
                                {
                                    System.Diagnostics.Debug.WriteLine($"TestPortAsync({portName}): ✓ PONG received - port is valid!");
                                pongReceived = true;
                                return true;
                                }
                            // Ignore READY (sent after first PONG handshake)
                            if (line.Equals("READY", StringComparison.OrdinalIgnoreCase))
                            {
                                System.Diagnostics.Debug.WriteLine($"TestPortAsync({portName}): READY received (ignoring, waiting for PONG)");
                                // Continue reading to find PONG
                                continue;
                            }
                            // optional: Bootmeldungen überspringen und weiter lesen
                            System.Diagnostics.Debug.WriteLine($"TestPortAsync({portName}): Unknown response: '{line}'");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"TestPortAsync({portName}): Empty line received");
                        }
                    }
                    catch (TimeoutException ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"TestPortAsync({portName}): ReadLine timeout: {ex.Message}");
                        // weiter warten bis Gesamttimeout
                    }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"TestPortAsync({portName}): Read error: {ex.Message}");
                        }
                    }
                    else if (readAttempts % 10 == 0)
                    {
                        // Log every 10th attempt if no data available
                        System.Diagnostics.Debug.WriteLine($"TestPortAsync({portName}): No data available (attempt {readAttempts}, elapsed: {(DateTime.UtcNow - started).TotalMilliseconds:F0}ms)");
                        // Some devices only respond after a second PING once line state settles
                        if (pingsResent < 2)
                        {
                            pingsResent++;
                            System.Diagnostics.Debug.WriteLine($"TestPortAsync({portName}): Re-sending PING (attempt {pingsResent})...");
                            try
                            {
                                test.Write("PING\n");
                                test.BaseStream.Flush();
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"TestPortAsync({portName}): Re-send PING failed: {ex.Message}");
                            }
                        }
                    }
                    await Task.Delay(20);  // Etwas länger warten zwischen Versuchen
                }
                
                if (!pongReceived)
                {
                    System.Diagnostics.Debug.WriteLine($"TestPortAsync({portName}): ✗ Timeout after {readAttempts} attempts - PONG not received (BytesToRead: {test.BytesToRead})");
                }

                // Kein Debug-Output bei Timeout für Performance
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                // Port von anderem Prozess belegt (z.B. Arduino Serial Monitor) - nicht still schlucken, sonst wirkt es wie "kein Gerät vorhanden"
                System.Diagnostics.Debug.WriteLine($"TestPortAsync({portName}): ✗ Port is busy/in use (UnauthorizedAccessException). Close Arduino Serial Monitor / PuTTY / andere Apps, die diesen COM Port geöffnet haben.");
                return false;
            }
            catch
            {
                // Stille Fehlerbehandlung - kein Debug-Output für Performance
                return false;
            }
        }


        public void Disconnect()
        {
            try
            {
                if (SerialPort?.IsOpen == true)
                {
                    SerialPort.Close();
                    SerialPort.Dispose();
                    SerialPort = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during disconnect: {ex.Message}");
            }

            IsConnected = false;
            ComPort = "None";
            Status = "n/c";
            Color = 0xFF0000;
            Battery = 0;
            _connectionEstablishedTime = null; // Reset connection time
            
            System.Diagnostics.Debug.WriteLine($"Disconnect called - IsConnected: {IsConnected}, Status: {Status}, ComPort: {ComPort}");
            OnConnectionStatusChanged();
        }

        protected virtual void OnConnectionStatusChanged()
        {
            System.Diagnostics.Debug.WriteLine($"OnConnectionStatusChanged called - IsConnected: {IsConnected}, Status: {Status}, ComPort: {ComPort}, Battery: {Battery}");
            ConnectionStatusChanged?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnDeviceInfoChanged()
        {
            System.Diagnostics.Debug.WriteLine($"OnDeviceInfoChanged called - Battery: {Battery}, IsConnected: {IsConnected}");
            DeviceInfoChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task RefreshDeviceInfoAsync()
        {
            System.Diagnostics.Debug.WriteLine($"RefreshDeviceInfoAsync called - IsConnected: {IsConnected}");
            
            if (!IsConnected)
            {
                // Reset to default values when not connected
                DeviceName = "PadAwan Force";
                FirmwareVersion = "v1.2.3";
                HardwareRevision = "Rev A";
                SerialNumber = "PAF-2024-001";
                Uptime = "0h 0m";
                LastConnected = "Never";
                DataTransferred = "0 MB";
                Battery = 0;
                System.Diagnostics.Debug.WriteLine("Not connected - resetting device info to defaults");
                OnDeviceInfoChanged();
                return;
            }
            
            // Don't refresh device info too soon after connection - give it time to stabilize
            if (_connectionEstablishedTime.HasValue)
            {
                var timeSinceConnection = DateTime.Now - _connectionEstablishedTime.Value;
                if (timeSinceConnection.TotalSeconds < 3) // Wait 3 seconds before refreshing device info
                {
                    System.Diagnostics.Debug.WriteLine($"RefreshDeviceInfoAsync: Too soon after connection ({timeSinceConnection.TotalSeconds:F1}s), skipping");
                    return;
                }
            }

            try
            {
                // Simulate device communication delay
                await Task.Delay(200);

                // In a real implementation, these would be actual device queries
                // For now, we'll simulate some realistic values
                var random = new Random();
                
                // Simulate battery reading (20-100%)
                Battery = random.Next(20, 101);
                
                // Simulate uptime (1-24 hours)
                var hours = random.Next(1, 25);
                var minutes = random.Next(0, 60);
                Uptime = $"{hours}h {minutes}m";
                
                // Update last connected time
                LastConnected = DateTime.Now.ToString("MMM dd HH:mm");
                
                // Simulate data transfer (0.1-5.0 MB)
                var dataAmount = random.Next(1, 51) / 10.0;
                DataTransferred = $"{dataAmount:F1} MB";

                System.Diagnostics.Debug.WriteLine($"Refreshed device info - Battery: {Battery}, IsConnected: {IsConnected}");
                OnDeviceInfoChanged();
            }
            catch (Exception ex)
            {
                // Handle communication errors
                System.Diagnostics.Debug.WriteLine($"Error refreshing device info: {ex.Message}");
            }
        }

        public void UpdateConnectionInfo(string comPort, bool connected, string status)
        {
            ComPort = comPort;
            IsConnected = connected;
            Status = status;
            Color = connected ? 0x00FF00 : 0xFF0000;
            OnConnectionStatusChanged();
        }

        public async Task<bool> SendLayersAsync(List<Layer> layers)
        {
            if (!IsConnected || SerialPort == null || !SerialPort.IsOpen)
            {
                System.Diagnostics.Debug.WriteLine("Cannot send layers - not connected to FeatherS3");
                return false;
            }

            try
            {
                // Convert layers to JSON
                var json = System.Text.Json.JsonSerializer.Serialize(new { layers }, new JsonSerializerOptions 
                { 
                    WriteIndented = false 
                });
                
                System.Diagnostics.Debug.WriteLine($"Sending layers to FeatherS3: {json}");
                
                // Send JSON data to FeatherS3
                SerialPort.WriteLine("BEGIN_JSON");
                
                // Split JSON into lines and send each line
                var lines = json.Split('\n');
                foreach (string line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        SerialPort.WriteLine(line);
                        await Task.Delay(10); // Small delay between lines
                    }
                }
                
                SerialPort.WriteLine("END_JSON");
                
                // Wait for response
                await Task.Delay(1000);
                
                // Read response
                if (SerialPort.BytesToRead > 0)
                {
                    string response = SerialPort.ReadLine();
                    System.Diagnostics.Debug.WriteLine($"FeatherS3 response: {response}");
                    
                    if (response.Contains("UPLOAD_OK"))
                    {
                        System.Diagnostics.Debug.WriteLine("Layers uploaded successfully to FeatherS3");
                        return true;
                    }
                    else if (response.Contains("UPLOAD_FAIL"))
                    {
                        System.Diagnostics.Debug.WriteLine($"Upload failed: {response}");
                        return false;
                    }
                }
                
                System.Diagnostics.Debug.WriteLine("No response from FeatherS3");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sending layers to FeatherS3: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> PingAsync()
        {
            var sp = SerialPort;
            if (sp == null || !sp.IsOpen)
            {
                System.Diagnostics.Debug.WriteLine("PingAsync: port not open");
                return false;
            }

            try
            {
                sp.DiscardInBuffer();
                sp.Write("PING\n");  // Explizit \n statt WriteLine für bessere Kontrolle
                sp.BaseStream.Flush();  // Explizit flushen
                await Task.Delay(50);  // Mehr Zeit für Antwort

                var deadline = DateTime.UtcNow.AddMilliseconds(500);  // Timeout erhöht
                bool pongReceived = false;
                
                while (DateTime.UtcNow < deadline && !pongReceived)
                {
                    // Prüfe ob Daten verfügbar sind, bevor ReadLine() aufgerufen wird
                    if (sp.BytesToRead > 0)
                {
                    try
                    {
                        var line = sp.ReadLine()?.Trim();
                        if (!string.IsNullOrEmpty(line))
                        {
                            System.Diagnostics.Debug.WriteLine($"PingAsync: Received '{line}'");
                            
                            // Check for PONG (main response)
                            if (line.Equals("PONG", StringComparison.OrdinalIgnoreCase))
                            {
                                System.Diagnostics.Debug.WriteLine($"Ping response: PONG received");
                                pongReceived = true;
                                return true;
                            }
                            // Ignore READY (sent after first PONG handshake)
                            if (line.Equals("READY", StringComparison.OrdinalIgnoreCase))
                            {
                                System.Diagnostics.Debug.WriteLine($"Ping response: READY received (ignoring, waiting for PONG)");
                                // Continue reading to find PONG
                                continue;
                            }
                            System.Diagnostics.Debug.WriteLine($"Ping response: '{line}' (not PONG or READY)");
                        }
                    }
                        catch (TimeoutException) 
                        { 
                            // weiter warten bis deadline
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"PingAsync read error: {ex.Message}");
                        }
                    }
                    await Task.Delay(10);  // Etwas länger warten
                }
                
                if (!pongReceived)
                {
                    System.Diagnostics.Debug.WriteLine("PingAsync: Timeout - PONG not received");
                }
                System.Diagnostics.Debug.WriteLine("Ping: timeout");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ping error: {ex.Message}");
                return false; // hier nicht Disconnect(), sonst killst du Connect-Flows
            }
        }
        public async Task<bool> RequestBatteryStatusAsync()
        {
            if (!IsConnected || SerialPort == null || !SerialPort.IsOpen)
            {
                System.Diagnostics.Debug.WriteLine("RequestBatteryStatusAsync: Not connected or port not open");
                return false;
            }

            try
            {
                SerialPort.WriteLine("BATTERY_STATUS");
                await Task.Delay(200);
                
                if (SerialPort.BytesToRead > 0)
                {
                    string response = SerialPort.ReadLine();
                    System.Diagnostics.Debug.WriteLine($"Battery response: {response}");
                    
                    if (response.StartsWith("BATTERY:"))
                    {
                        var parts = response.Substring(8).Split(',');
                        if (parts.Length >= 3)
                        {
                            if (int.TryParse(parts[0], out int percentage))
                            {
                                Battery = percentage;
                                System.Diagnostics.Debug.WriteLine($"Battery updated to {percentage}% - connection is working!");
                                OnDeviceInfoChanged();
                                return true;
                            }
                        }
                    }
                }
                
                System.Diagnostics.Debug.WriteLine("No battery response received");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Battery status error: {ex.Message}");
                // Don't call CheckConnectionHealthAsync here as it might cause disconnection
                return false;
            }
        }

        public async Task<string?> GetFirmwareVersionAsync()
        {
            if (!IsConnected || SerialPort == null || !SerialPort.IsOpen)
            {
                System.Diagnostics.Debug.WriteLine("GetFirmwareVersionAsync: Not connected or port not open");
                return null;
            }

            try
            {
                SerialPort.DiscardInBuffer();
                SerialPort.WriteLine("GET_VERSION");
                await Task.Delay(500); // Give device more time to respond
                
                // Wait for response with timeout - read multiple lines if needed
                int attempts = 0;
                string fullResponse = "";
                while (attempts < 20) // Wait up to 1 second
                {
                    await Task.Delay(50);
                    attempts++;
                    
                    if (SerialPort.BytesToRead > 0)
                    {
                        // Read all available data
                        while (SerialPort.BytesToRead > 0)
                        {
                            string line = SerialPort.ReadLine();
                            fullResponse += line + "\n";
                            System.Diagnostics.Debug.WriteLine($"Version response line: {line}");
                        }
                        
                        // Check if we got the version
                        if (fullResponse.Contains("VERSION:"))
                        {
                            int versionIndex = fullResponse.IndexOf("VERSION:");
                            string versionLine = fullResponse.Substring(versionIndex);
                            string version = versionLine.Substring(8).Split('\n', '\r')[0].Trim();
                            System.Diagnostics.Debug.WriteLine($"Firmware version extracted: {version}");
                            return version;
                        }
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"No version response received. Full response: {fullResponse}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Version request error: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> UploadConfigurationAsync(string jsonConfig)
        {
            // Reuse the existing SendLayerConfigurationAsync method
            return await SendLayerConfigurationAsync(jsonConfig);
        }

        public async Task<bool> CheckConnectionHealthAsync()
        {
            if (!IsConnected || SerialPort == null || !SerialPort.IsOpen)
            {
                System.Diagnostics.Debug.WriteLine("CheckConnectionHealthAsync: Not connected or port not open");
                return false;
            }

            try
            {
                // Try to ping the device
                bool pingResult = await PingAsync();
                if (!pingResult)
                {
                    System.Diagnostics.Debug.WriteLine("Connection health check failed - device not responding");
                    Disconnect();
                    return false;
                }
                System.Diagnostics.Debug.WriteLine("Connection health check passed");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Connection health check error: {ex.Message}");
                Disconnect();
                return false;
            }
        }

        public bool IsDeviceStillConnected()
        {
            try
            {
                if (SerialPort == null || !SerialPort.IsOpen)
                {
                    System.Diagnostics.Debug.WriteLine("IsDeviceStillConnected: SerialPort is null or not open");
                    return false;
                }
                
                // Grace period: Don't check connection health immediately after connection
                // Windows needs time to fully register the port in GetPortNames()
                if (_connectionEstablishedTime.HasValue)
                {
                    var timeSinceConnection = DateTime.Now - _connectionEstablishedTime.Value;
                    if (timeSinceConnection.TotalSeconds < ConnectionGracePeriodSeconds)
                    {
                        System.Diagnostics.Debug.WriteLine($"IsDeviceStillConnected: In grace period ({timeSinceConnection.TotalSeconds:F1}s), assuming connected");
                        return true; // Assume connected during grace period
                    }
                }

                // Check if the port is still available
                // Note: GetPortNames() can be slow and might temporarily not include a port
                // during device reconnection, so we should be lenient
                try
                {
                    var portNames = SerialPort.GetPortNames();
                    if (!portNames.Any(p => p == SerialPort.PortName))
                    {
                        // Port not in list - but this could be a temporary Windows issue
                        // Only disconnect if we also can't access the port
                        try
                        {
                            // Try to access a property to see if port is actually accessible
                            var test = SerialPort.PortName;
                            // If we get here, port is still accessible even if not in list
                            System.Diagnostics.Debug.WriteLine($"IsDeviceStillConnected: Port {SerialPort.PortName} not in list but still accessible");
                            return true; // Be lenient - port might be temporarily missing from list
                        }
                        catch
                        {
                            System.Diagnostics.Debug.WriteLine($"IsDeviceStillConnected: Port {SerialPort.PortName} no longer accessible");
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // If GetPortNames() fails, assume connection is still good if port is open
                    System.Diagnostics.Debug.WriteLine($"IsDeviceStillConnected: GetPortNames() failed: {ex.Message}, assuming connected");
                    return SerialPort.IsOpen;
                }

                // Port is in list and open - connection is good
                System.Diagnostics.Debug.WriteLine("IsDeviceStillConnected: Port is available and open");
                return true;
            }
            catch (Exception ex)
            {
                // On any exception, check if port is still open as fallback
                System.Diagnostics.Debug.WriteLine($"IsDeviceStillConnected error: {ex.Message}, checking if port is open");
                try
                {
                    return SerialPort != null && SerialPort.IsOpen;
                }
                catch
                {
                    return false;
                }
            }
        }

        public async Task<bool> SendLayerConfigurationAsync(string jsonConfig)
        {
            Console.WriteLine("=== SendLayerConfigurationAsync called ===");
            Console.WriteLine($"IsConnected: {IsConnected}");
            Console.WriteLine($"SerialPort: {SerialPort?.PortName}");
            Console.WriteLine($"SerialPort.IsOpen: {SerialPort?.IsOpen}");
            
            if (!IsConnected || SerialPort == null || !SerialPort.IsOpen) 
            {
                Console.WriteLine("Cannot send - not connected or port not open");
                return false;
            }

            try
            {
                Console.WriteLine("Sending layer configuration to FeatherS3...");
                System.Diagnostics.Debug.WriteLine("Sending layer configuration to FeatherS3...");
                
                // Send upload command
                Console.WriteLine("Sending UPLOAD_LAYER_CONFIG command...");
                SerialPort.WriteLine("UPLOAD_LAYER_CONFIG");
                await Task.Delay(100);

                // Wait for ready response
                Console.WriteLine("Waiting for READY_FOR_LAYER_CONFIG response...");
                string response = await ReadResponseAsync(2000);
                Console.WriteLine($"Received response: '{response}'");
                
                if (!response.Contains("READY_FOR_LAYER_CONFIG"))
                {
                    Console.WriteLine($"Unexpected response: {response}");
                    System.Diagnostics.Debug.WriteLine($"Unexpected response: {response}");
                    return false;
                }
                
                Console.WriteLine("Received READY_FOR_LAYER_CONFIG, proceeding with JSON upload...");

                // Send JSON data
                Console.WriteLine("Sending BEGIN_JSON...");
                SerialPort.WriteLine("BEGIN_JSON");
                await Task.Delay(50);

                // Split JSON into lines and send
                Console.WriteLine($"Sending JSON data ({jsonConfig.Length} characters)...");
                string[] lines = jsonConfig.Split('\n');
                Console.WriteLine($"Sending {lines.Length} lines...");
                foreach (string line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        SerialPort.WriteLine(line);
                        await Task.Delay(10); // Small delay between lines
                    }
                }

                Console.WriteLine("Sending END_JSON...");
                SerialPort.WriteLine("END_JSON");
                await Task.Delay(100);

                // Wait for upload confirmation
                response = await ReadResponseAsync(5000);
                if (response.Contains("UPLOAD_OK"))
                {
                    System.Diagnostics.Debug.WriteLine("Configuration uploaded successfully!");
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Upload failed: {response}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sending configuration: {ex.Message}");
                return false;
            }
        }

        public async Task<string?> GetCurrentConfigurationAsync()
        {
            if (!IsConnected || SerialPort == null || !SerialPort.IsOpen) return null;

            try
            {
                System.Diagnostics.Debug.WriteLine("Requesting current configuration from FeatherS3...");
                
                SerialPort.DiscardInBuffer();
                SerialPort.WriteLine("GET_CURRENT_CONFIG");
                await Task.Delay(300);

                // Lese die gesamte Antwort - JSON kann mehrzeilig sein
                var startTime = DateTime.Now;
                var response = new System.Text.StringBuilder();
                bool foundPrefix = false;
                int emptyLineCount = 0;

                while ((DateTime.Now - startTime).TotalMilliseconds < 5000)
                {
                    if (SerialPort.BytesToRead > 0)
                    {
                        string line = SerialPort.ReadLine();
                        
                        if (line.StartsWith("CURRENT_CONFIG:"))
                        {
                            foundPrefix = true;
                            // Entferne den Prefix und füge den Rest hinzu
                            string jsonPart = line.Substring(15);
                            if (!string.IsNullOrWhiteSpace(jsonPart))
                            {
                                response.Append(jsonPart);
                                emptyLineCount = 0;
                            }
                        }
                        else if (foundPrefix)
                        {
                            // Wenn wir den Prefix gefunden haben, sammle alle weiteren Zeilen
                            if (string.IsNullOrWhiteSpace(line))
                            {
                                emptyLineCount++;
                                // Nach 2 leeren Zeilen annehmen, dass die JSON vollständig ist
                                if (emptyLineCount >= 2)
                                {
                                    break;
                                }
                            }
                            else
                            {
                                emptyLineCount = 0;
                                response.AppendLine(line);
                            }
                        }
                    }
                    await Task.Delay(10);
                }

                string configJson = response.ToString().Trim();
                
                if (foundPrefix && configJson.Length > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Configuration retrieved successfully! Length: {configJson.Length}");
                    return configJson;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to get configuration. Found prefix: {foundPrefix}, Length: {configJson.Length}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting configuration: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> SetDisplayModeAsync(string mode, bool enabled = true)
        {
            if (!IsConnected || SerialPort == null || !SerialPort.IsOpen) return false;

            try
            {
                System.Diagnostics.Debug.WriteLine($"Setting display mode to {mode}, enabled: {enabled}");
                SerialPort.WriteLine($"SET_DISPLAY_MODE:{mode},{enabled}");
                await Task.Delay(200);

                string response = await ReadResponseAsync(2000);
                if (response.Contains("DISPLAY_MODE_SET"))
                {
                    System.Diagnostics.Debug.WriteLine("Display mode set successfully!");
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to set display mode: {response}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting display mode: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SetTimeAsync(string timeString)
        {
            if (!IsConnected || SerialPort == null || !SerialPort.IsOpen) return false;

            try
            {
                System.Diagnostics.Debug.WriteLine($"Setting time to {timeString}");
                SerialPort.WriteLine($"SET_TIME:{timeString}");
                await Task.Delay(200);

                string response = await ReadResponseAsync(2000);
                if (response.Contains("TIME_SET"))
                {
                    System.Diagnostics.Debug.WriteLine("Time set successfully!");
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to set time: {response}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting time: {ex.Message}");
                return false;
            }
        }

        private async Task<string> ReadResponseAsync(int timeoutMs)
        {
            var startTime = DateTime.Now;
            var response = new System.Text.StringBuilder();

            while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
            {
                if (SerialPort != null && SerialPort.BytesToRead > 0)
                {
                    string line = SerialPort.ReadLine();
                    response.AppendLine(line);
                    
                    // Check if we have a complete response
                    if (line.Contains("UPLOAD_OK") || line.Contains("UPLOAD_FAIL") || 
                        line.Contains("CONFIG_ERROR") || line.Contains("CURRENT_CONFIG:"))
                    {
                        break;
                    }
                }
                await Task.Delay(10);
            }

            return response.ToString().Trim();
        }
    }
}


