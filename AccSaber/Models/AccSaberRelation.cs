using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using AccSaber.Managers;
using AccSaber.Models.Base;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AccSaber.Models
{
    [UsedImplicitly]
    internal sealed class AccSaberRelation : Model
    {
        [JsonProperty("userId")]
        public string UserId { get; set; } = null!;

        [JsonProperty("id")]
        public string ID { get; set; } = null!;

        [JsonProperty("type")]
        public string Type { get; set; } = null!;

        [JsonProperty("targetAvatarUrl")]
        public string TargetAvatarUrl { get; set; } = null!;

        [JsonProperty("targetCountry")]
        public string TargetCountry { get; set; } = null!;

        [JsonProperty("targetName")]
        public string TargetName { get; set; } = null!;

        [JsonProperty("TargetUserId")]
        public string TargetUserId { get; set; } = null!;

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }
    }
}