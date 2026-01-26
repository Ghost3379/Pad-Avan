using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PadAwan_Force.Models
{
    public class Layer
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("buttons")]
        public Dictionary<string, ButtonConfig> Buttons { get; set; } = new();

        [JsonPropertyName("knobs")]
        public Dictionary<string, KnobConfig> Knobs { get; set; } = new();
    }

    public class ButtonConfig
    {
        [JsonPropertyName("action")]
        public string Action { get; set; } = "None";

        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("holdToRepeat")]
        public bool HoldToRepeat { get; set; } = false;
    }

    public class KnobConfig
    {
        [JsonPropertyName("ccwAction")]
        public string CcwAction { get; set; } = "None";

        [JsonPropertyName("cwAction")]
        public string CwAction { get; set; } = "None";

        [JsonPropertyName("pressAction")]
        public string PressAction { get; set; } = "None";

        [JsonPropertyName("ccwKey")]
        public string? CcwKey { get; set; }

        [JsonPropertyName("cwKey")]
        public string? CwKey { get; set; }

        [JsonPropertyName("pressKey")]
        public string? PressKey { get; set; }

        [JsonPropertyName("pressHoldToRepeat")]
        public bool PressHoldToRepeat { get; set; } = false;
    }
}
