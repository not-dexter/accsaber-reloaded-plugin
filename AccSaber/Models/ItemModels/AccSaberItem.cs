using AccSaber.Models.Base;
using AccSaber.Models.ItemModels.Base;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Runtime.Serialization;

namespace AccSaber.Models.ItemModels
{
    [UsedImplicitly]
    internal class AccSaberItem<T> : Model where T : ItemValueModel
    {
        [JsonProperty("id")]
        public string Id { get; set; } = null!;

        [JsonProperty("typeId")]
        public string TypeId { get; set; } = null!;

        [JsonProperty("typeKey")]
        public string TypeKey { get; set; } = null!;

        [JsonProperty("name")]
        public string Name { get; set; } = null!;

        [JsonProperty("description")]
        public string Description { get; set; } = null!;

        [JsonProperty("iconUrl")]
        public string IconUrl { get; set; } = null!;

        [JsonProperty("value")]
        public T Value { get; set; } = default!;

        [JsonProperty("rarity")]
        public string Rarity { get; set; } = null!;

        [JsonProperty("tradeable")]
        public bool Tradeable { get; set; }

        [JsonProperty("visible")]
        public bool Visible { get; set; }

        [JsonProperty("active")]
        public bool Active { get; set; }

        [JsonProperty("deprecated")]
        public bool Deprecated { get; set; }

        [JsonProperty("stackable")]
        public bool Stackable { get; set; }

        [JsonProperty("welcomeGrant")]
        public bool WelcomeGrant { get; set; }

        [JsonProperty("worth")]
        public int Worth { get; set; }

        [JsonProperty("requirement")]
        public string Requirement { get; set; } = null!;

        [JsonProperty("unlockLevel")]
        public int UnlockLevel { get; set; }

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Value.ItemId = Id;
            Value.Propagate();
        }

    }
}
