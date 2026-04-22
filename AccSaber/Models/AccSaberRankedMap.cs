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
        [JsonProperty("songName")]
        public string SongName { get; set; } = null!;

        [JsonProperty("songSubName")]
        public string SongSubName { get; set; } = null!;

        [JsonProperty("songAuthor")]
        public string SongAuthorName { get; set; } = null!;

        [JsonProperty("mapAuthor")]
        public string LevelAuthorName { get; set; } = null!;

        [JsonProperty("beatsaverCode")]
        public string BeatSaverKey { get; set; } = null!;

        [JsonProperty("songHash")]
        public string SongHash { get; set; } = null!;

        [JsonProperty("difficulties")]
        public List<AccSaberDifficulty> Difficulties { get; set; } = null!;

        [JsonProperty("complexity")]
        public float Complexity { get; set; }

        [JsonProperty("blLeaderboardId")]
        public string BlLeaderboardId { get; set; } = null!;

        [JsonProperty("leaderboardId")]
        public string LeaderboardId { get; set; } = null!;

        [JsonProperty("difficulty")]
        public string Difficulty { get; set; } = null!;

        [JsonProperty("categoryId")]
        public string CategoryId { get; set; } = null!;

        [JsonProperty("rankedAt")]
        public DateTime DateRanked { get; set; }


        [JsonIgnore]
        public AccSaberStore.AccSaberMapCategories Category; //TODO: switch to using categoryId for like everything

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            /*
            Category = CategoryId switch
            {
                "b0000000-0000-0000-0000-000000000001" => AccSaberStore.AccSaberMapCategories.True,
                "b0000000-0000-0000-0000-000000000002" => AccSaberStore.AccSaberMapCategories.Standard,
                "b0000000-0000-0000-0000-000000000003" => AccSaberStore.AccSaberMapCategories.Tech
             //   _ => throw new ArgumentOutOfRangeException()
            };*/
        }
    }
}