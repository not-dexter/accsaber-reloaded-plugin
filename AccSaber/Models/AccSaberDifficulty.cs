using System;
using System.Runtime.Serialization;
using AccSaber.Models.Base;
using AccSaber.Models.CacheModels;
using AccSaber.Utils;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AccSaber.Models
{
    [UsedImplicitly]
    internal sealed class AccSaberDifficulty : AccSaberBasicDifficulty
    {
		[JsonProperty("beatsaverCode")]
		public string? BeatsaverCode { get; set; }

        [JsonProperty("blLeaderboardId")]
        public string? BlLeaderboardId { get; set; }

        [JsonProperty("categoryId")]
        public string CategoryId { get; set; } = null!;

        [JsonProperty("characteristic")]
        public string Characteristic { get; set; } = null!;

        //coverUrl, createdAt, mapAuthor, mapId, maxScore, rankedAt, songAuthor, songName, ssLeaderboardId, statistics

        [JsonProperty("status")]
		public string RankedStatus { get; set; } = null!;

        [JsonProperty("criteriaStatus")]
        public string CriteriaStatus { get; set; } = null!;

        [JsonIgnore]
        public MapStatus? Status { get; set; } = null;

        [JsonIgnore]
        public new AccSaberRankedMap? ParentInfo { get; set; }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Category ??= EnumUtils.ReloadedCategoryToEnum(CategoryId);
            Status = EnumUtils.RankedStatusToEnum(RankedStatus);
        }
    }

    [UsedImplicitly]
    internal class AccSaberBasicDifficulty : Model
    {
        [JsonProperty("id")]
        public string DifficultyId { get; set; } = null!;

        [JsonProperty("songHash")]
        public string Hash { get; set; } = null!;

        [JsonProperty("difficulty")]
        public string DifficultyString { get; set; } = null!;

        [JsonIgnore]
        public BeatmapDifficulty Difficulty;

        [JsonProperty("complexity")]
        public float Complexity { get; set; }

        [JsonProperty("categoryCode")]
        public string? CategoryCode { get; set; }

        [JsonIgnore]
        public APCategory? Category { get; set; }

        [JsonIgnore]
        public AccSaberBasicMap? ParentInfo { get; set; }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Difficulty = EnumUtils.ReloadedDiffToDiff(DifficultyString);

            if (CategoryCode is not null)
            {
                string temp = CategoryCode.Split('_')[0];
                Category = (APCategory?)Enum.Parse(typeof(APCategory), temp.Capitialize());
            }
        }
    }
}