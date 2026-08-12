using System;
using AccSaber.Models.Base;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AccSaber.Models
{
    [UsedImplicitly]
    internal sealed class AccSaberLeaderboardPlayer : IModel
    {
        [JsonProperty("ranking")]
        public int Ranking { get; set; }

        [JsonProperty("countryRanking")]
        public int CountryRanking { get; set; }

        [JsonProperty("userId")]
        public string UserId { get; set; } = null!;

        [JsonProperty("userName")]
        public string UserName { get; set; } = null!;

        [JsonProperty("country")]
        public string Country { get; set; } = null!;

        [JsonProperty("avatarUrl")]
        public string AvatarUrl { get; set; } = null!;

        [JsonProperty("cdnAvatarUrl")]
        public string CdnAvatarUrl { get; set; } = null!;

        [JsonProperty("ap")]
        public float AP { get; set; }

        [JsonProperty("averageAcc")]
        public float AverageAcc { get; set; }

        [JsonProperty("rankedPlays")]
        public int RankedPlays { get; set; }

        [JsonProperty("topPlayId")]
        public Guid TopPlayId { get; set; }

        [JsonProperty("playerInactive")]
        public bool PlayerInactive { get; set; }

        [JsonProperty("rankingLastWeek")]
        public int RankingLastWeek { get; set; }

        [JsonProperty("supporterTier")]
        public string SupporterTier { get; set; } = null!;

        [JsonIgnore]
        public int MaxPage;
    }
}