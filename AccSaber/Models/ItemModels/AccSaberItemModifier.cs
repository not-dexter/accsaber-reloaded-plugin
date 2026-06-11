using AccSaber.Models.Base;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace AccSaber.Models.ItemModels
{
    [UsedImplicitly]
    internal class AccSaberItemModifier : IModel
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

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
