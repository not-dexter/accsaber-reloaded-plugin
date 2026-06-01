using AccSaber.Models.Base;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AccSaber.Models.ItemModels
{
    [UsedImplicitly]
    internal class AccSaberItemGlisten : Model
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; }

        [JsonProperty("highlight")]
        public string Color { get; set; } = null!;

        [JsonProperty("durationMs")]
        public int DurationMs { get; set; }

        [JsonProperty("intervalMs")]
        public int IntervalMs { get; set; }
    }
}
