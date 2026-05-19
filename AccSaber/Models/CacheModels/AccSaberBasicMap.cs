using AccSaber.Models.Base;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace AccSaber.Models.CacheModels
{
    [UsedImplicitly]
    internal class AccSaberBasicMap : Model
    {
        [JsonProperty("songName")]
        public string? SongName { get; set; }

        [JsonProperty("songHash")]
        public string Hash { get; set; } = null!;

        [JsonProperty("difficulties")]
        public virtual List<AccSaberBasicDifficulty> Difficulties { get; set; } = [];


        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (Difficulties is not null)
                foreach (AccSaberBasicDifficulty difficulty in Difficulties)
                {
                    difficulty.Hash = Hash;
                    difficulty.ParentInfo = this;
                }
        }

    }
}
