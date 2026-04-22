using System;
using AccSaber.Models.Base;
using JetBrains.Annotations;
using Newtonsoft.Json;

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
	internal class AccSaberUser : Model
	{
		[JsonProperty("ranking")]
		public int Rank { get; set; }

		[JsonProperty("countryRanking")]
		public int CountryRank { get; set; }

		[JsonProperty("playerId")]
		public string PlayerId { get; set; } = null!;

		[JsonProperty("playerName")]
		public string PlayerName { get; set; } = null!;

		[JsonProperty("levelData")]
		public LevelData LevelData { get; set; } = null!;

		[JsonProperty("avatarUrl")]
		public string AvatarUrl { get; set; } = null!;

		[JsonProperty("country")]
		public string Country { get; set; } = null!;

		[JsonProperty("hmd")]
		public string? Hmd { get; set; }

		[JsonProperty("averageAcc")]
		public float AverageAcc { get; set; }

		[JsonProperty("ap")]
		public float AP { get; set; }

		[JsonProperty("averageAp")]
		public float AverageApPerMap { get; set; }

		[JsonProperty("rankedPlays")]
		public int RankedPlays { get; set; }

		[JsonProperty("accChamp")]
		public bool AccChamp { get; set; }
	}
}