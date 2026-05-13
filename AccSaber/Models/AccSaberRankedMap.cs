using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using AccSaber.Managers;
using AccSaber.Models.Base;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AccSaber.Models
{
    [UsedImplicitly]
    internal sealed class AccSaberRankedMap : Model
    {
        [JsonProperty("beatsaverCode")]
        public string BeatSaverKey { get; set; } = null!;

        [JsonProperty("coverUrl")]
        public string CoverUrl { get; set; } = null!;

        //createdAt

        [JsonProperty("difficulties")]
        public List<AccSaberDifficulty>? Difficulties { get; set; }

        [JsonProperty("id")]
        public string MapId { get; set; } = null!;

        [JsonProperty("mapAuthor")]
        public string LevelAuthorName { get; set; } = null!;

        [JsonProperty("songAuthor")]
        public string SongAuthorName { get; set; } = null!;

        [JsonProperty("songHash")]
        public string Hash { get; set; } = null!;

        [JsonProperty("songName")]
        public string SongName { get; set; } = null!;

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (Difficulties is not null)
            {
                foreach (AccSaberDifficulty difficulty in Difficulties)
                {
                    difficulty.Hash = Hash;
                    difficulty.ParentInfo = this;
                }
            }
        }
    }
}