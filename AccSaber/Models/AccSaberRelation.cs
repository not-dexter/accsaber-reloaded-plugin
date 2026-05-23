using System;
using System.Runtime.Serialization;
using AccSaber.Models.Base;
using AccSaber.Utils;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AccSaber.Models
{
    [UsedImplicitly]
    internal sealed class AccSaberRelation : Model
    {
        [JsonProperty("id")]
        public string Id { get; set; } = null!;

        [JsonProperty("userId")]
        public string PlayerId { get; set; } = null!;

        [JsonProperty("TargetUserId")]
        public string TargetPlayerId { get; set; } = null!;

        [JsonProperty("targetName")]
        public string TargetName { get; set; } = null!;

        [JsonProperty("targetAvatarUrl")]
        public string TargetAvatarUrl { get; set; } = null!;

        [JsonProperty("targetCountry")]
        public string TargetCountry { get; set; } = null!;

        [JsonProperty("type")]
        public string Type { get; set; } = null!;

        [JsonIgnore]
        public RelationType Relation { get; set; }

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Relation = (RelationType)Enum.Parse(typeof(RelationType), Type);
        }
    }
}