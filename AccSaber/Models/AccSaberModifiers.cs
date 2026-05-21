using AccSaber.Models.Base;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AccSaber.Models
{
    [UsedImplicitly]
    internal class AccSaberModifiers : Model
    {
        [JsonProperty("code")]
        public string Code { get; set; } = null!;

        [JsonProperty("id")]
        public string Id { get; set; } = null!;

        [JsonProperty("multiplier")]
        public float Multiplier { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = null!;
    }
}
