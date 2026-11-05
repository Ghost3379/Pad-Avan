using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PadAwan_Force.Models
{
    public class ConfigManager
    {
        private const string ConfigFileName = "macropad_config.json";
        private readonly string _configPath;

        public ConfigManager()
        {
            // Use AppData folder instead of installation directory (which is read-only when installed)
            // This allows the app to save config files even when installed to Program Files
            var appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Pad-Avan Force"
            );
            
            // Ensure the directory exists
            if (!Directory.Exists(appDataFolder))
            {
                Directory.CreateDirectory(appDataFolder);
            }
            
            _configPath = Path.Combine(appDataFolder, ConfigFileName);
        }

        public async Task<List<Layer>> LoadLayersAsync()
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    return CreateDefaultLayers();
                }

                var json = await File.ReadAllTextAsync(_configPath);
                var config = ParseConfigJson(json);
                return config?.Layers ?? CreateDefaultLayers();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading config: {ex.Message}");
                return CreateDefaultLayers();
            }
        }

        public async Task<ConfigData?> LoadConfigAsync()
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    return null;
                }

                var json = await File.ReadAllTextAsync(_configPath);
                return ParseConfigJson(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading config: {ex.Message}");
                return null;
            }
        }

        public static ConfigData? ParseConfigJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var config = new ConfigData();

                // 1. General settings (check under "general" or fallback to root)
                bool isNestedGeneral = false;
                JsonElement generalElem = root;
                if (root.TryGetProperty("general", out var gElem) && gElem.ValueKind == JsonValueKind.Object)
                {
                    generalElem = gElem;
                    isNestedGeneral = true;
                }

                if (generalElem.TryGetProperty("version", out var versionElem))
                    config.Version = versionElem.GetString() ?? "1.0";

                if (generalElem.TryGetProperty("created", out var createdElem))
                    config.Created = createdElem.GetString() ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                if (generalElem.TryGetProperty("device", out var deviceElem))
                    config.Device = deviceElem.GetString() ?? "FeatherS3";

                if (generalElem.TryGetProperty("currentLayer", out var currentLayerElem))
                    config.CurrentLayer = currentLayerElem.GetInt32();

                if (generalElem.TryGetProperty("wrapAround", out var wrapAroundElem))
                    config.WrapAround = wrapAroundElem.GetBoolean();

                if (generalElem.TryGetProperty("display", out var displayElem))
                {
                    config.Display = new DisplaySettings();
                    if (displayElem.TryGetProperty("mode", out var modeElem))
                        config.Display.Mode = modeElem.GetString() ?? "layer";
                    if (displayElem.TryGetProperty("enabled", out var enabledElem))
                        config.Display.Enabled = enabledElem.GetBoolean();
                }

                if (generalElem.TryGetProperty("layerOrder", out var layerOrderElem) && layerOrderElem.ValueKind == JsonValueKind.Array)
                {
                    config.LayerOrder = new List<string>();
                    foreach (var item in layerOrderElem.EnumerateArray())
                    {
                        config.LayerOrder.Add(item.GetString() ?? "");
                    }
                }

                // Check root fallbacks if general settings were nested
                if (isNestedGeneral)
                {
                    if (config.CurrentLayer == 1 && root.TryGetProperty("currentLayer", out var rootCurrLayer))
                        config.CurrentLayer = rootCurrLayer.GetInt32();
                    if (root.TryGetProperty("wrapAround", out var rootWrap))
                        config.WrapAround = rootWrap.GetBoolean();
                }

                // 2. Layers collection
                if (root.TryGetProperty("layers", out var layersElem) && layersElem.ValueKind == JsonValueKind.Array)
                {
                    config.Layers = new List<Layer>();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    foreach (var layerElem in layersElem.EnumerateArray())
                    {
                        var layer = JsonSerializer.Deserialize<Layer>(layerElem.GetRawText(), options);
                        if (layer != null)
                        {
                            config.Layers.Add(layer);
                        }
                    }
                }

                return config;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing config JSON: {ex.Message}");
                return null;
            }
        }

        public async Task SaveLayersAsync(List<Layer> layers, string displayMode = "layer", bool displayEnabled = true, int currentLayer = 1, bool wrapAround = true)
        {
            const int maxRetries = 3;
            const int retryDelay = 100; // milliseconds
            
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    // Create layer order array (list of layer names in order)
                    var layerOrder = layers.Select(l => l.Name).ToList();
                    
                    var config = new ConfigData
                    {
                        Version = "1.0",
                        Created = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        Device = "FeatherS3",
                        Display = new DisplaySettings
                        {
                            Mode = displayMode,
                            Enabled = displayEnabled
                        },
                        CurrentLayer = currentLayer,
                        LayerOrder = layerOrder,
                        WrapAround = wrapAround,
                        Layers = layers,
                        Limits = new ConfigLimits
                        {
                            MaxLayers = layers.Count, // Dynamic based on actual layers
                            MaxButtons = 6,
                            MaxKnobs = 2
                        },
                        LastModified = DateTime.Now
                    };

                    var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                    // Use a temporary file to avoid file locking issues
                    var tempPath = _configPath + ".tmp";
                    await File.WriteAllTextAsync(tempPath, json);
                    
                    // Atomic move to replace the original file
                    File.Move(tempPath, _configPath, true);
                    
                    return; // Success, exit the retry loop
                }
                catch (IOException) when (attempt < maxRetries - 1)
                {
                    // File is locked, wait and retry
                    await Task.Delay(retryDelay * (attempt + 1));
                    continue;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving config (attempt {attempt + 1}): {ex.Message}");
                    if (attempt == maxRetries - 1)
                    {
                        throw; // Re-throw on final attempt
                    }
                    await Task.Delay(retryDelay * (attempt + 1));
                }
            }
        }

        private List<Layer> CreateDefaultLayers()
        {
            return new List<Layer>
            {
                new Layer
                {
                    Id = 1,
                    Name = "Layer 1",
                    Buttons = new Dictionary<string, ButtonConfig>
                    {
                        ["1"] = new ButtonConfig { Action = "None" },
                        ["2"] = new ButtonConfig { Action = "None" },
                        ["3"] = new ButtonConfig { Action = "None" },
                        ["4"] = new ButtonConfig { Action = "None" },
                        ["5"] = new ButtonConfig { Action = "None" },
                        ["6"] = new ButtonConfig { Action = "None" }
                    },
                    Knobs = new Dictionary<string, KnobConfig>
                    {
                        ["A"] = new KnobConfig { CcwAction = "None", CwAction = "None", PressAction = "None" },
                        ["B"] = new KnobConfig { CcwAction = "None", CwAction = "None", PressAction = "None" }
                    }
                }
            };
        }
    }

    public class ConfigData
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        [JsonPropertyName("created")]
        public string Created { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        [JsonPropertyName("device")]
        public string Device { get; set; } = "FeatherS3";

        [JsonPropertyName("display")]
        public DisplaySettings Display { get; set; } = new();

        [JsonPropertyName("currentLayer")]
        public int CurrentLayer { get; set; } = 1;

        [JsonPropertyName("layerOrder")]
        public List<string> LayerOrder { get; set; } = new();

        [JsonPropertyName("wrapAround")]
        public bool WrapAround { get; set; } = true;

        [JsonPropertyName("layers")]
        public List<Layer> Layers { get; set; } = new();

        [JsonPropertyName("limits")]
        public ConfigLimits Limits { get; set; } = new();

        [JsonPropertyName("lastModified")]
        public DateTime LastModified { get; set; }
    }

    public class DisplaySettings
    {
        [JsonPropertyName("mode")]
        public string Mode { get; set; } = "layer";

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;
    }

    public class ConfigLimits
    {
        [JsonPropertyName("maxLayers")]
        public int MaxLayers { get; set; } = 3;

        [JsonPropertyName("maxButtons")]
        public int MaxButtons { get; set; } = 6;

        [JsonPropertyName("maxKnobs")]
        public int MaxKnobs { get; set; } = 2;
    }
}
