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
        public MissionType Type { get; set; }

        [JsonProperty("pool")]
        public string Pool { get; set; } = null!; // Daily, Weekly, Event

        [JsonProperty("status")]
        public MissionStatus Status { get; set; }

        [JsonIgnore]
        public bool Completed => Status == MissionStatus.completed;

        [JsonProperty("band")]
        public MissionBand Band { get; set; }

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

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            MissionPool = (MissionPool)Enum.Parse(typeof(MissionPool), Pool.Capitialize());
            Category = EnumUtils.ReloadedCategoryToEnum(CategoryId)!.Value;
        }
    }

    [UsedImplicitly]
    internal class AccSaberMissionResponse
    {

        [JsonProperty("userId")]
        public string UserId { get; set; } = null!;

        [JsonProperty("userName")]
        public string UserName { get; set; } = null!;

        [JsonProperty("userCountry")]
        public string UserCountry { get; set; } = null!;

        [JsonProperty("userAvatarUrl")]
        public string UserAvatarUrl { get; set; } = null!;

        [JsonProperty("completedAt")]
        public DateTime CompletedAt { get; set; }

        [JsonProperty("missionId")]
        public string MissionId { get; set; } = null!;

        [JsonProperty("templateId")]
        public string TemplateId { get; set; } = null!;

        [JsonProperty("templateCode")]
        public string TemplateCode { get; set; } = null!;

        [JsonProperty("templateDescription")]
        public string TemplateDescription { get; set; } = null!;

        [JsonProperty("type")]
        public string Type { get; set; } = null!;

        [JsonProperty("pool")]
        public string Pool { get; set; } = null!;

        [JsonProperty("band")]
        public string Band { get; set; } = null!;

        [JsonProperty("categoryId")]
        public string CategoryId { get; set; } = null!;

        [JsonProperty("categoryCode")]
        public string CategoryCode { get; set; } = null!;

        [JsonProperty("targetMapDifficultyId")]
        public string TargetMapDifficultyId { get; set; } = null!;

        [JsonProperty("xpAwarded")]
        public int XpAwarded { get; set; }

        [JsonProperty("itemAwardedId")]
        public string ItemAwardedId { get; set; } = null!;
    }

}
