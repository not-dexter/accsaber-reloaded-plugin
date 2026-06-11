using AccSaber.Models.Base;
using AccSaber.Utils;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;

namespace AccSaber.Models.CacheModels
{
    [UsedImplicitly]
    public class AccSaberPlayerScore : IModel
    {
        [JsonProperty("rank")]
        public int Rank { get; set; }

        [JsonProperty("coverUrl")]
        public string CoverUrl { get; set; } = null!;

        [JsonProperty("songName")]
        public string SongName { get; set; } = null!;

        [JsonProperty("songAuthor")]
        public string SongAuthor { get; set; } = null!;

        [JsonProperty("difficultyId")]
        public string DifficultyId { get; set; } = null!;

        [JsonProperty("accuracy")]
        public float Accuracy { get; set; }

        [JsonProperty("ap")]
        public float AP { get; set; }

        [JsonProperty("weightedAp")]
        public float WeightedAp { get; set; }

        [JsonProperty("timeSet")]
        public DateTime TimeSet { get; set; }

        [JsonProperty("difficulty")]
        public BeatmapDifficulty Difficulty { get; set; }

        [JsonProperty("category")]
        public APCategory Category { get; set; }

        public AccSaberPlayerScore() { }
        internal AccSaberPlayerScore(AccSaberLeaderboardEntry score)
        {
            Rank = score.Rank;
            CoverUrl = score.CoverUrl;
            SongName = score.SongName;
            SongAuthor = score.SongAuthor;
            DifficultyId = score.DifficultyId;
            Accuracy = score.Accuracy;
            AP = score.AP;
            WeightedAp = score.WeightedAp;
            TimeSet = score.TimeSet;
            Difficulty = EnumUtils.ReloadedDiffToDiff(MiscUtils.ParseEnum<ReloadedDifficulty>(score.Difficulty));
            Category = EnumUtils.ReloadedCategoryIdToEnum(score.CategoryId);
        }
    }
}
