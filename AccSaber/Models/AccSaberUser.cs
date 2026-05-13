using AccSaber.API;
using AccSaber.Models.Base;
using AccSaber.Utils;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace AccSaber.Models
{
	[UsedImplicitly]

	internal class LevelData : Model
	{
		[JsonProperty("level")]
		public string PlayerLevel { get; set; } = null!;

		[JsonProperty("title")]
		public string PlayerTitle { get; set; } = null!;

		[JsonProperty("xpForCurrentLevel")]
		public float XPForCurrentLevel { get; set; }

		[JsonProperty("xpForNextLevel")]
		public float XPForNextLevel { get; set; }

		[JsonProperty("progressPercent")]
		public float ProgressPercent { get; set; }

	}

	internal class StatsDiff : Model
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

	[UsedImplicitly]
	internal class PlayerStats : Model
	{
		[JsonProperty("ap")]
		public float AP { get; set; }

		[JsonProperty("averageAcc")]
		public float AverageAcc { get; set; }

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
		public StatsDiff? StatsDiff { get; set; }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Category = EnumUtils.ReloadedCategoryToEnum(CategoryId);
        }
    }

    [UsedImplicitly]
    internal sealed class AccSaberUser : Model
	{
		public Task LoadStatDiffs { get; private set; } = Task.CompletedTask;

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
		public LevelData LevelData { get; set; } = null!;

		[JsonProperty("name")]
		public string PlayerName { get; set; } = null!;

		[JsonProperty("playerInactive")]
		public bool PlayerInactive { get; set; }

		//relations

		[JsonProperty("statistics")]
		public List<PlayerStats>? Statistics { get; set; }

		//xpCountryRanking, xpRanking

		public PlayerStats? GetStat(APCategory category) => Statistics?.FirstOrDefault(stat => stat.Category == category);

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
			if (Statistics is not null)
			{
				LoadStatDiffs = Task.Run(async () =>
				{
                    foreach (PlayerStats stat in Statistics)
                    {
						string category_id = stat.Category.ToString();
						category_id = char.ToLower(category_id[0]) + category_id.Substring(1);

						if (stat.Category != APCategory.Overall)
							category_id += "_acc";

						string? dataStr = await APIHandler.CallAPI_String(string.Format(HelpfulPaths.APAPI_PLAYER_STATDIFF, PlayerId, category_id));

						if (dataStr is not null)
							stat.StatsDiff = JsonConvert.DeserializeObject<StatsDiff>(dataStr);
					}
                });
			}
		}
    }
}