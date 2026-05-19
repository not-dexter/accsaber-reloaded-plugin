using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using AccSaber.Managers;
using AccSaber.Models.Base;
using AccSaber.Models.CacheModels;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AccSaber.Models
{
    [UsedImplicitly]
    internal sealed class AccSaberRankedMap : AccSaberBasicMap
    {
        [JsonProperty("beatsaverCode")]
        public string BeatSaverKey { get; set; } = null!;

        [JsonProperty("coverUrl")]
        public string CoverUrl { get; set; } = null!;

        //createdAt

        [JsonProperty("difficulties")]
        public new List<AccSaberDifficulty>? Difficulties { get; set; }

        [JsonProperty("id")]
        public string MapId { get; set; } = null!;

        [JsonProperty("mapAuthor")]
        public string LevelAuthorName { get; set; } = null!;

        [JsonProperty("songAuthor")]
        public string SongAuthorName { get; set; } = null!;

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (Difficulties is null && base.Difficulties.Count > 0)
            {
                Difficulties = [];
                Difficulties.AddRange(base.Difficulties.Where(diff => diff is AccSaberDifficulty).Cast<AccSaberDifficulty>());
            }

            if (Difficulties is not null)
            {
                if (base.Difficulties.Count < Difficulties.Count)
                {
                    base.Difficulties.Clear();
                    base.Difficulties.AddRange(Difficulties);
                }

                foreach (AccSaberDifficulty difficulty in Difficulties)
                {
                    difficulty.ParentInfo = this;
                }
            }
        }
    }
}