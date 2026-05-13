using System;
using System.Runtime.Serialization;
using AccSaber.Models.Base;
using AccSaber.Utils;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AccSaber.Models
{
    [UsedImplicitly]
    internal sealed class AccSaberDifficulty : AccSaberBasicDifficulty
    {
		[JsonProperty("beatsaverCode")]
		public string BeatsaverCode { get; set; } = null!;

        [JsonProperty("blLeaderboardId")]
        public string BlLeaderboardId { get; set; } = null!;

        [JsonProperty("categoryId")]
        public string CategoryId { get; set; } = null!;

        [JsonProperty("characteristic")]
        public string Characteristic { get; set; } = null!;

        //coverUrl, createdAt

        [JsonProperty("id")]
        public string DifficultyId { get; set; } = null!;

        //mapAuthor, mapId, maxScore, rankedAt, songAuthor, songName, ssLeaderboardId, statistics

        [JsonProperty("status")]
		public string RankedStatus { get; set; } = null!;

        [JsonProperty("criteriaStatus")]
        public string CriteriaStatus { get; set; } = null!;

        [JsonIgnore]
        public MapStatus? Status => EnumUtils.RankedStatusToEnum(RankedStatus);

        [JsonIgnore]
        public new string? Hash { get; set; }

        [JsonIgnore]
        public AccSaberRankedMap? ParentInfo { get; set; }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Category ??= EnumUtils.ReloadedCategoryToEnum(CategoryId);

            //Difficulty = HelpfulPaths.ToDiff(HelpfulPaths.ReloadedDiffToDiffNum(DifficultyString));
        }
    }

    [UsedImplicitly]
    internal class AccSaberBasicDifficulty : Model
    {
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

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Difficulty = EnumUtils.ReloadedDiffToDiff(DifficultyString);

            if (CategoryCode is not null)
            {
                string temp = CategoryCode.Split('_')[0];
                Category = (APCategory?)Enum.Parse(typeof(APCategory), char.ToUpper(temp[0]) + temp.Substring(1));
            }
        }
    }
}