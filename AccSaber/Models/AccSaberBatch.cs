using AccSaber.Models.Base;
using AccSaber.Utils;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace AccSaber.Models
{
    [UsedImplicitly]
    internal class AccSaberBatch : IModel
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = null!;

        [JsonProperty("description")]
        public string Description { get; set; } = null!;

        [JsonProperty("status")]
        public BatchStatus Status { get; set; }

        [JsonProperty("difficulties")]
        public List<AccSaberDifficulty> Difficulties { get; set; } = null!;

        [JsonProperty("releasedAt")]
        public DateTime ReleasedAt { get; set; }

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("updatedAt")]
        public DateTime UpdatedAt { get; set; }
    }
}
