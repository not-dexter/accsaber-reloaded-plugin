using AccSaber.Models.Base;
using AccSaber.Utils;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System.Runtime.Serialization;

namespace AccSaber.Models.PlayerModels
{
    [UsedImplicitly]
    internal class AccSaberPlayerStats : Model
    {
        [JsonProperty("ap")]
        public float AP { get; set; }

        [JsonProperty("averageAcc")]
        public float AverageAcc { get; set; } = 0f;

        [JsonProperty("averageAp")]
        public float AverageAp { get; set; }

        [JsonProperty("categoryId")]
        public string CategoryId { get; set; } = null!;

        [JsonIgnore]
        public APCategory? Category { get; set; }

        [JsonProperty("countryRanking")]
        public int CountryRank { get; set; }

        //createdAt

        [JsonProperty("id")]
        public string StatId { get; set; } = null!;

        [JsonProperty("rankedPlays")]
        public int Plays { get; set; }

        [JsonProperty("ranking")]
        public int Rank { get; set; }

        [JsonProperty("scoreXp")]
        public float ScoreXp { get; set; }

        //topPlayId, updatedAt

        [JsonProperty("userId")]
        public string UserId { get; set; } = null!;

        [JsonIgnore]
        public AccSaberPlayerStatsDiff? StatsDiff { get; set; }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Category = EnumUtils.ReloadedCategoryToEnum(CategoryId);
        }
    }
}
