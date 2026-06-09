using AccSaber.Models.Base;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AccSaber.Models.PlayerModels
{
    [UsedImplicitly]
    internal class AccSaberPlayerLevelData : IModel
    {
        [JsonProperty("level")]
        public string PlayerLevel { get; set; } = null!;

        [JsonProperty("title")]
        public string PlayerTitle { get; set; } = null!;

        [JsonProperty("xpForCurrentLevel")]
        public float XPForCurrentLevel { get; set; }

        [JsonProperty("xpForNextLevel")]
        public float XPForNextLevel { get; set; }

        [JsonProperty("progressPercent")]
        public float ProgressPercent { get; set; }

    }
}
