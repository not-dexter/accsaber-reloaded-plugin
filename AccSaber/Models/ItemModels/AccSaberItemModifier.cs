using AccSaber.Models.Base;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AccSaber.Models.ItemModels
{
    [UsedImplicitly]
    internal class AccSaberItemModifier : Model
    {
        [JsonProperty("id")]
        public string Id { get; set; } = null!;

        [JsonProperty("key")]
        public string Key { get; set; } = null!;

        [JsonProperty("name")]
        public string Name { get; set; } = null!;

        [JsonProperty("colorHex")]
        public string ColorHex { get; set; } = null!;

        [JsonProperty("effectSpec")]
        public JObject EffectSpec { get; set; } = null!;
    }
}
