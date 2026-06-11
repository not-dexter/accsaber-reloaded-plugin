using AccSaber.Models.Base;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;

namespace AccSaber.Models
{
    [UsedImplicitly]
    internal class AccSaberModifier : IModel
    {
        [JsonProperty("code")]
        public string Code { get; set; } = null!;

        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("multiplier")]
        public float Multiplier { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = null!;
    }
}
