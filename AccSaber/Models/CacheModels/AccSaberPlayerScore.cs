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
        public string? CoverUrl { get; set; }

        [JsonProperty("songName")]
        public string SongName { get; set; } = null!;

        [JsonProperty("songAuthor")]
        public string? SongAuthor { get; set; }

        [JsonProperty("difficultyId")]
        public Guid DifficultyId { get; set; }

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
            Category = EnumUtils.ReloadedCategoryIdToCategory(score.CategoryId);
        }
        internal AccSaberPlayerScore(AccSaberBasicPlayerScore score, AccSaberBasicDifficulty diff)
        { // TODO: Actually implement this once I have the info needed to do so.
            Rank = score.Rank;
            CoverUrl = null;
            SongName = diff.ParentInfo?.SongName ?? "Not Found.";
            SongAuthor = null;
            DifficultyId = score.DifficultyId;
            Accuracy = score.Accuracy;
            AP = score.AP;
            //WeightedAp = score.WeightedAp;
            TimeSet = score.TimeSet;
            Difficulty = diff.Difficulty;
            Category = diff.Category ?? APCategory.Overall;
        }
    }

    [UsedImplicitly]
    public class AccSaberBasicPlayerScore : IModel
    {
        [JsonProperty("mapDifficultyId")]
        public Guid DifficultyId { get; set; }

        [JsonProperty("songHash")]
        public string Hash { get; set; } = null!;

        [JsonProperty("ssLeaderboardId")]
        public string SsLeaderboardId { get; set; } = null!;

        [JsonProperty("blLeaderboardId")]
        public string BlLeaderboardId { get; set; } = null!;

        [JsonProperty("ap")]
        public float AP { get; set; }

        [JsonProperty("accuracy")]
        public float Accuracy { get; set; }

        [JsonProperty("score")]
        public int Score { get; set; }

        [JsonProperty("maxScore")]
        public int MaxScore { get; set; }

        [JsonProperty("rank")]
        public int Rank { get; set; }

        [JsonProperty("blScoreId")]
        public long BlScoreId { get; set; }

        [JsonProperty("ssScoreId")]
        public long SsScoreId { get; set; }

        [JsonProperty("timeSet")]
        public DateTime TimeSet { get; set; }
    }
}
