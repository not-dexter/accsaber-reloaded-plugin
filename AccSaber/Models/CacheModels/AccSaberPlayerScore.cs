using AccSaber.Counter;
using AccSaber.Models.Base;
using AccSaber.Utils;
using AccSaber.Utils.Misc;
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

        [JsonProperty("personalRank")]
        public int PersonalRank { get; set; } = -1;

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
        public float WeightedAp { get; set; } = -1f;

        [JsonProperty("timeSet")]
        public DateTime TimeSet { get; set; }

        [JsonProperty("difficulty")]
        public BeatmapDifficulty? Difficulty { get; set; } = null;

        [JsonProperty("category")]
        public APCategory? Category { get; set; } = null;

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
        internal AccSaberPlayerScore(AccSaberBasicPlayerScore score)
        {
            Rank = score.Rank;
            CoverUrl = score.CoverUrl;
            SongName = score.SongName;
            SongAuthor = score.SongAuthor;
            DifficultyId = score.DifficultyId;
            Accuracy = score.Accuracy;
            AP = score.AP;
            TimeSet = score.TimeSet;
        }

        internal void SetWeight(APCalc calc)
        {
            WeightedAp = AP * calc.GetWeight(PersonalRank);
        }
        internal void SetValues(AccSaberBasicDifficulty diff, APCalc calc)
        {
            if (PersonalRank < 0)
                throw new Exception("Cannot set values without the personal rank being set first.");

            Difficulty = diff.Difficulty;
            Category = diff.Category ?? APCategory.Overall;

            SetWeight(calc);
        }
        internal void SetValues(SerializationHandler handler, APCalc calc) => 
            SetValues(handler.CachedDifficulties[DifficultyId], calc);
        
        internal void SetValues()
        {
            SerializationHandler? handler = Plugin.Container.TryResolve<SerializationHandler>();
            APCalc? calc = Plugin.Container.TryResolve<APCalc>();

            if (handler is null || calc is null)
                throw new Exception("Cannot resolve util classes.");

            SetValues(handler, calc);
        }
    }

    [UsedImplicitly]
    public class AccSaberBasicPlayerScore : IModel
    {
        [JsonProperty("mapDifficultyId")]
        public Guid DifficultyId { get; set; }

        [JsonProperty("songHash")]
        public string Hash { get; set; } = null!;

        [JsonProperty("songName")]
        public string SongName { get; set; } = null!;

        [JsonProperty("songAuthor")]
        public string SongAuthor { get; set; } = null!;

        [JsonProperty("coverUrl")]
        public string CoverUrl { get; set; } = null!;

        // cdnCoverUrl

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
