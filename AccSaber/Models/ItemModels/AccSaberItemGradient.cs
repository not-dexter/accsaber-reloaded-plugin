using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace AccSaber.Models.ItemModels
{
    [UsedImplicitly]
    internal class AccSaberItemGradient : IEquatable<AccSaberItemGradient>
    {
        [JsonProperty("type")]
        public string Type { get; set; } = null!;

        [JsonProperty("stops")]
        public List<AccSaberItemGradientStop> Stops { get; set; } = [];

        [JsonProperty("angleDeg")]
        public int AngleDegree { get; set; }

        public bool Equals(AccSaberItemGradient other)
        {
            if (!Type.Equals(other.Type) || Stops.Count != other.Stops.Count)
                return false;

            for (int i = 0, len = Stops.Count; i < len; ++i)
                if (!Stops[i].Equals(other.Stops[i]))
                    return false;

            return true;
        }
    }

    [UsedImplicitly]
    internal class AccSaberItemGradientStop : IEquatable<AccSaberItemGradientStop>
    {
        [JsonProperty("hex")]
        public string Color { get; set; } = null!;

        [JsonProperty("atPct")]
        public int AtPercent { get; set; }

        public bool Equals(AccSaberItemGradientStop other)
        {
            return Color.Equals(other.Color) && AtPercent.Equals(other.AtPercent);
        }
    }
}
