using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace AccSaber.Models.ItemModels
{
    [UsedImplicitly]
    internal class AccSaberItemFill : IEquatable<AccSaberItemFill>
    {
        [JsonProperty("type")]
        public FillType Type { get; set; }

        [JsonProperty("hex")]
        public string? Color { get; set; }

        [JsonProperty("base")]
        public string? Base { get; set; }

        [JsonProperty("shadow")]
        public string? Shadow { get; set; }

        [JsonProperty("highlight")]
        public string? Highlight { get; set; }

        [JsonProperty("stops")]
        public List<AccSaberItemGradientStop>? Stops { get; set; }

        [JsonProperty("angleDeg")]
        public int? AngleDegree { get; set; }

        public bool Equals(AccSaberItemFill other)
        {
            if (Color is not null)
                return other.Color is not null && other.Color.Equals(Color);

            if (Stops is not null)
            {

                if (!Type.Equals(other.Type) || other.Stops is null || Stops.Count != other.Stops.Count)
                    return false;

                for (int i = 0, len = Stops.Count; i < len; ++i)
                    if (!Stops[i].Equals(other.Stops[i]))
                        return false;

                return true;
            }

            return false;
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context) 
        {
            if (Base is not null && Color is null)
                Color = Base;
        }
    }

    internal enum FillType
    {
        solid, linear, pixel_metal
    }
}
