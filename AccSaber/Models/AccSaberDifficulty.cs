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
        public Guid CategoryId { get; set; }

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
            Category ??= EnumUtils.ReloadedCategoryIdToCategory(CategoryId);
            Status = EnumUtils.RankedStatusToEnum(RankedStatus);
        }
    }

    [UsedImplicitly]
    internal class AccSaberBasicDifficulty : IModel
    {
        [JsonProperty("id")]
        public Guid DifficultyId { get; set; }

        [JsonProperty("songHash")]
        public string Hash { get; set; } = null!;

        [JsonProperty("difficulty")]
        public ReloadedDifficulty ReloadedDifficulty { get; set; }

        [JsonIgnore]
        public BeatmapDifficulty Difficulty;

        [JsonProperty("complexity")]
        public float Complexity { get; set; }

        [JsonProperty("categoryCode")]
        public ReloadedAPCategory? CategoryCode { get; set; }

        [JsonIgnore]
        public APCategory? Category { get; set; }

        //[JsonProperty("ssLeaderboardId")]
        //public string? SsLeaderboardId { get; set; }

        //[JsonProperty("blLeaderboardId")]
        //public string? BlLeaderboardId { get; set; }

        [JsonIgnore]
        public AccSaberBasicMap? ParentInfo { get; set; }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Difficulty = EnumUtils.ReloadedDiffToDiff(ReloadedDifficulty);

            if (CategoryCode is not null)
                Category = EnumUtils.ReloadedCategoryToCategory(CategoryCode.Value);
        }

        [OnSerializing]
        private void OnSerializing(StreamingContext context)
        {
            Hash = null!;
        }
    }
}