using AccSaber.Models.Base;
using AccSaber.Models.ItemModels;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace AccSaber.Models
{
    [UsedImplicitly]
    internal class AccSaberEvent : IModel
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; } = null!;

        [JsonProperty("description")]
        public string Description { get; set; } = null!;

        [JsonProperty("backgroundUrl")]
        public string BackgroundUrl { get; set; } = null!;

        [JsonProperty("iconUrl")]
        public string IconUrl { get; set; } = null!;

        [JsonProperty("startsAt")]
        public DateTime StartsAt { get; set; }

        [JsonProperty("endsAt")]
        public DateTime EndsAt { get; set; }

        [JsonProperty("bonusXp")]
        public float BonusXp { get; set; }

        [JsonProperty("bonusItems")]
        public List<AccSaberItemReference> BonusItems { get; set; } = [];

        [JsonProperty("active")]
        public bool Active { get; set; }

        [JsonProperty("live")]
        public bool Live { get; set; }

        [JsonProperty("currentWeek")]
        public int CurrentWeek { get; set; }

        [JsonProperty("totalWeeks")]
        public int TotalWeeks { get; set; }
    }
}
