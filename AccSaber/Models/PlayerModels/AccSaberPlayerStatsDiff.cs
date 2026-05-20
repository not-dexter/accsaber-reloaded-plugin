using AccSaber.Models.Base;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AccSaber.Models.PlayerModels
{
    [UsedImplicitly]
    internal class AccSaberPlayerStatsDiff : Model
    {
        [JsonProperty("apDiff")]
        public float ApDiff { get; set; }

        [JsonProperty("averageAccDiff")]
        public float AverageAccDiff { get; set; }

        [JsonProperty("averageApDiff")]
        public float AverageApDiff { get; set; }

        [JsonProperty("rankingDiff")]
        public int RankingDiff { get; set; }

        [JsonProperty("countryRankingDiff")]
        public int CountryDiff { get; set; }

        [JsonProperty("milestoneSetBonusXpDiff")]
        public float MilestoneSetBonusXpDiff { get; set; }

        [JsonProperty("milestoneXpDiff")]
        public float MilestoneXpDiff { get; set; }
        [JsonProperty("rankedPlaysDiff")]
        public int RankedPlaysDiff { get; set; }

        [JsonProperty("scoreXpDiff")]
        public float ScoreXpDiff { get; set; }
    }
}
