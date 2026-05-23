using AccSaber.API;
using AccSaber.Models.Base;
using AccSaber.Utils;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System.Runtime.Serialization;
using System.Threading.Tasks;

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
        public string PlayerId { get; set; } = null!;

        [JsonIgnore]
        public AccSaberPlayerStatsDiff? StatDiffs { get; set; }

        public async Task<bool> LoadStatDiff()
        {
            if (Category is null || StatDiffs is not null)
                return false;

            string category_id = Category.ToString().ToLower();

            if (Category != APCategory.Overall)
                category_id += "_acc";

            StatDiffs = await APIHandler.CallAPI_Json<AccSaberPlayerStatsDiff>(string.Format(HelpfulPaths.APAPI_PLAYER_STATDIFF, PlayerId, category_id), AccsaberAPI.throttler);

            return StatDiffs is not null;
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Category = EnumUtils.ReloadedCategoryToEnum(CategoryId);
        }
    }
}
