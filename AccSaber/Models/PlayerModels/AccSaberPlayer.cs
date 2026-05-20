using AccSaber.API;
using AccSaber.Models.Base;
using AccSaber.Utils;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AccSaber.Models.PlayerModels
{
    [UsedImplicitly]
    internal sealed class AccSaberPlayer : Model
    {
        public Task LoadStatDiffs
        {
            get
            {
                if (loadStatDiffs is null)
                {
                    loadStatDiffs = LoadStatDiffsFunc();
                    return loadStatDiffs;
                }
                return loadStatDiffs;
            }
        }
        private Task? loadStatDiffs = null;

        [JsonProperty("avatarUrl")]
        public string? AvatarUrl { get; set; }

        [JsonProperty("banned")]
        public bool Banned { get; set; }

        [JsonProperty("country")]
        public string Country { get; set; } = null!;

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("hmd")]
        public string Headset { get; set; } = null!;

        [JsonProperty("id")]
        public string PlayerId { get; set; } = null!;

        [JsonProperty("lastActiveTime")]
        public DateTime LastActiveTime { get; set; }

        [JsonProperty("levelData")]
        public AccSaberPlayerLevelData LevelData { get; set; } = null!;

        [JsonProperty("name")]
        public string PlayerName { get; set; } = null!;

        [JsonProperty("playerInactive")]
        public bool PlayerInactive { get; set; }

        //relations

        [JsonProperty("statistics")]
        public List<AccSaberPlayerStats>? Statistics { get; set; }

        //xpCountryRanking, xpRanking

        public AccSaberPlayerStats? GetStat(APCategory category) => Statistics?.FirstOrDefault(stat => stat.Category == category);

        private async Task LoadStatDiffsFunc()
        {
            foreach (AccSaberPlayerStats stat in Statistics!)
            {
                if (stat.Category is null)
                    continue;

                string category_id = stat.Category.ToString();
                category_id = char.ToLower(category_id[0]) + category_id.Substring(1);

                if (stat.Category != APCategory.Overall)
                    category_id += "_acc";

                string? dataStr = await APIHandler.CallAPI_String(string.Format(HelpfulPaths.APAPI_PLAYER_STATDIFF, PlayerId, category_id));

                if (dataStr is not null)
                    stat.StatsDiff = JsonConvert.DeserializeObject<AccSaberPlayerStatsDiff>(dataStr);
            }
        }
    }
}
