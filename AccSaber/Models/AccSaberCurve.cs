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
        public List<Vector2> Points { get; set; } = null!;

        [JsonProperty("scale")]
        public float Scale { get; set; }

        [JsonProperty("shift")]
        public float Shift { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; } = null!;
        
    }
}
