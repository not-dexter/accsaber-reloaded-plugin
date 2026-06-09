using AccSaber.Models.Base;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;

namespace AccSaber.Models.CacheModels
{
    [UsedImplicitly]
    internal class AccSaberSerializedCache : IModel
    {
        [JsonProperty("lastUpdated")]
        public DateTime LastUpdated { get; set; }

        [JsonProperty("maxLength")]
        public int MaxLength { get; set; } = -1;

        [JsonProperty("extraData")]
        public List<object>? ExtraData = null;

        [JsonProperty("content")]
        public IEnumerable Content { get; set; } = null!;

        [JsonIgnore]
        public string Name { get; set; } = null!;
    }

    [UsedImplicitly]
    internal class AccSaberSerializedCache<T> : AccSaberSerializedCache where T : IModel
    {
        [JsonIgnore]
        public new List<T> Content
        {
            get => (List<T>)base.Content;
            set => base.Content = value;
        }

        public AccSaberSerializedCache() => base.Content = new List<T>();
    }

    [UsedImplicitly]
    internal class AccSaberSerializedOrderedCache<T> : AccSaberSerializedCache where T : OrderedModel<T>
    {
        [JsonIgnore]
        public new SortedSet<T> Content
        {
            get => (SortedSet<T>)base.Content;
            set => base.Content = value;
        }

        public AccSaberSerializedOrderedCache() => base.Content = new SortedSet<T>();
    }
}
