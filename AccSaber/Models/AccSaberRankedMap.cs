using AccSaber.Models.CacheModels;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Runtime.Serialization;

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

        [JsonProperty("id")]
        public Guid MapId { get; set; }

        [JsonProperty("mapAuthor")]
        public string LevelAuthorName { get; set; } = null!;

        [JsonProperty("songAuthor")]
        public string SongAuthorName { get; set; } = null!;

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (Difficulties is not null)
                foreach (AccSaberDifficulty difficulty in Difficulties.Where(diff => diff is AccSaberDifficulty).Cast<AccSaberDifficulty>())
                    difficulty.ParentInfo = this;
        }
    }
}