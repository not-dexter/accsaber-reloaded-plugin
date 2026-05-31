using AccSaber.Models.Base;
using AccSaber.Models.ItemModels.Base;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace AccSaber.Models.ItemModels
{
    [UsedImplicitly]
    internal class AccSaberItemType<T> : Model where T : ItemValueModel
    {
        [JsonProperty("linkId")]
        public string LinkId { get; set; } = null!;

        [JsonProperty("item")]
        public AccSaberItem<T> Item { get; set; } = null!;

        [JsonProperty("modifiers")]
        public List<AccSaberItemModifier> Modifiers { get; set; } = [];

        [JsonProperty("serialNumber")]
        public int SerialNumber { get; set; }

        [JsonProperty("quantity")]
        public int Quantity { get; set; }

        [JsonProperty("source")]
        public string Source { get; set; } = null!;

        [JsonProperty("sourceId")]
        public string SourceId { get; set; } = null!;

        [JsonProperty("awardedByStaffId")]
        public string? AwardedByStaffId { get; set; }

        [JsonProperty("reason")]
        public string? Reason { get; set; }

        [JsonProperty("awardedAt")]
        public DateTime AwardedAt { get; set; }
    }
}
