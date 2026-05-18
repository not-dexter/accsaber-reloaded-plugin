using AccSaber.Models.Base;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace AccSaber.Models.CacheModels
{
    [UsedImplicitly]
    internal class AccSaberSerializedCache<T> where T : Model
    {
        [JsonProperty("lastUpdated")]
        public DateTime LastUpdated { get; set; }

        [JsonProperty("maxLength")]
        public int MaxLength { get; set; } = -1;

        [JsonProperty("content")]
        public List<T> Content { get; set; } = [];
    }
}
