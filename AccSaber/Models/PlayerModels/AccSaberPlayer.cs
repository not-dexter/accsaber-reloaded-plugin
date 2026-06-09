using AccSaber.API;
using AccSaber.Models.Base;
using AccSaber.Models.ItemModels;
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
    internal sealed class AccSaberPlayer : IModel
    {
        [JsonIgnore]
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

        [JsonIgnore]
        private Task? loadStatDiffs = null;

        [JsonIgnore]
        public Task LoadItems
        {
            get
            {
                if (loadItems is null)
                {
                    loadItems = LoadPlayerItems();
                    return loadItems;
                }
                return loadItems;
            }
        }

        [JsonIgnore]
        private Task? loadItems = null;

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

        [JsonIgnore]
        public AccSaberEquippedItems? Items;

        public AccSaberPlayerStats? GetStat(APCategory category) => Statistics?.FirstOrDefault(stat => stat.Category == category);

        private async Task LoadStatDiffsFunc()
        {
            if (Statistics is null)
                return;

            foreach (AccSaberPlayerStats stat in Statistics)
            {
                if (stat.Category is null)
                    continue;

                string category_id = stat.Category.ToString().ToLower();

                if (stat.Category != APCategory.Overall)
                    category_id += "_acc";

                AccSaberPlayerStatsDiff? diff = 
                    await APIHandler.CallAPI_Json<AccSaberPlayerStatsDiff>(string.Format(HelpfulPaths.APAPI_PLAYER_STATDIFF, PlayerId, category_id), AccsaberAPI.Throttler);

                if (diff is not null)
                    stat.StatDiffs = diff;
            }
        }
        private async Task LoadPlayerItems()
        {
            Items = await APIHandler.CallAPI_Json<AccSaberEquippedItems>(string.Format(HelpfulPaths.APAPI_PLAYERID_ITEMS_EQUIPPED, PlayerId), AccsaberAPI.Throttler);
        }
    }
}
