using AccSaber.Utils;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Runtime.Serialization;

namespace AccSaber.Models
{
    [UsedImplicitly]
    internal class AccSaberMission
    {
        [JsonProperty("id")]
        public string Id { get; set; } = null!;

        [JsonProperty("name")]
        public string Name { get; set; } = null!;

        [JsonProperty("description")]
        public string Description { get; set; } = null!;

        [JsonProperty("type")]
        public string Type { get; set; } = null!;

        [JsonProperty("pool")]
        public string Pool { get; set; } = null!; // Daily, Weekly, Event

        [JsonProperty("status")]
        public string Status { get; set; } = null!;

        [JsonIgnore]
        public bool Completed => Status.Equals("completed");

        [JsonProperty("band")]
        public string Band { get; set; } = null!; // Mission difficulty

        [JsonProperty("categoryId")]
        public string CategoryId { get; set; } = EnumUtils.OverallReloadedCategory;

        [JsonIgnore]
        public APCategory Category { get; set; }

        [JsonProperty("categoryCode")]
        public string? CategoryCode { get; set; }

        [JsonProperty("targetMapDifficultyId")]
        public string? TargetMapDifficultyId { get; set; }

        [JsonProperty("targetMapSongName")]
        public string? TargetMapSongName { get; set; } 

        [JsonProperty("targetPlayerId")]
        public string? TargetPlayerId { get; set; } 

        [JsonProperty("targetPlayerName")]
        public string? TargetPlayerName { get; set; }

        [JsonProperty("targetAcc")]
        public float? TargetAcc { get; set; }

        [JsonProperty("targetAp")]
        public float? TargetAp { get; set; }

        [JsonProperty("targetScore")]
        public int? TargetScore { get; set; }

        [JsonProperty("targetCount")]
        public int? TargetCount { get; set; }

        [JsonProperty("targetXp")]
        public int? TargetXp { get; set; }

        [JsonProperty("targetThresholdAp")]
        public float? TargetThresholdAp { get; set; }

        [JsonProperty("targetStreak")]
        public int? TargetStreak { get; set; }

        [JsonProperty("progressCount")]
        public int ProgressCount { get; set; }

        [JsonProperty("xpReward")]
        public int? XpReward { get; set; }

        [JsonProperty("itemRewardId")]
        public string? ItemRewardId { get; set; }

        [JsonProperty("itemRewardName")]
        public string? ItemRewardName { get; set; }

        [JsonProperty("assignedAt")]
        public DateTime AssignedAt { get; set; }

        [JsonProperty("expiresAt")]
        public DateTime ExpiresAt { get; set; }

        [JsonProperty("completedAt")]
        public DateTime CompletedAt { get; set; }

        [JsonIgnore]
        public MissionPool MissionPool { get; set; }

        [JsonIgnore]
        public MissionBand MissionBand { get; set; }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            MissionPool = (MissionPool)Enum.Parse(typeof(MissionPool), Pool.Capitialize());
            MissionBand = (MissionBand)Enum.Parse(typeof(MissionBand), Band.ToLower());
            Category = EnumUtils.ReloadedCategoryToEnum(CategoryId)!.Value;
        }
    }

}
