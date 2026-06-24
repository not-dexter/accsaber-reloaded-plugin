using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AccSaber.Models
{
    [UsedImplicitly]
    internal class AccSaberCurve
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = null!;
        
        [JsonProperty("points")]
        public List<Vector2>? Points { get; set; }

        [JsonProperty("scale")]
        public float? Scale { get; set; }

        [JsonProperty("shift")]
        public float? Shift { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; } = null!;

        [JsonProperty("xParameterName")]
        public string? XName { get; set; }

        [JsonProperty("xParameterValue")]
        public float? XVal { get; set; }

        [JsonProperty("yParameterName")]
        public string? YName { get; set; }

        [JsonProperty("yParameterValue")]
        public float? YVal { get; set; }

        [JsonProperty("zParameterName")]
        public string? ZName { get; set; }

        [JsonProperty("zParameterValue")]
        public float? ZVal { get; set; }
        
    }
}
