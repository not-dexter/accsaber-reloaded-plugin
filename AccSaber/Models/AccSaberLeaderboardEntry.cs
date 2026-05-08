using System;
using System.Collections.Generic;
using AccSaber.Models.Base;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AccSaber.Models
{
	[UsedImplicitly]
	internal sealed class AccSaberLeaderboardEntry : Model
	{
		[JsonProperty("rank")]
		public int Rank { get; set; }

		[JsonProperty("userId")]
		public string PlayerId { get; set; } = null!;

		[JsonProperty("songName")]
		public string SongName { get; set; } = null!;

		[JsonProperty("songAuthor")]
		public string SongAuthor { get; set; } = null!;

		[JsonProperty("coverUrl")]
		public string CoverUrl { get; set; } = null!;

		[JsonProperty("difficulty")]
		public string Difficulty { get; set; } = null!;

		[JsonProperty("categoryId")]
		public string CategoryId { get; set; } = null!;

		[JsonProperty("avatarUrl")]
		public string AvatarURL { get; set; } = null!;

		[JsonProperty("userName")]
		public string PlayerName { get; set; } = null!;

		[JsonProperty("accuracy")]
		public float Accuracy { get; set; }

		[JsonProperty("score")]
		public int Score { get; set; }

		[JsonProperty("ap")]
		public float AP { get; set; }

		[JsonProperty("accChamp")]
		public bool AccChamp { get; set; }

		[JsonProperty("modifierIds")]
		public List<string> Modifiers { get; set; } = null!;

		[JsonProperty("badCuts")]
		public int BadCuts { get; set; }

		[JsonProperty("wallHits")]
		public int WallHits { get; set; }

		[JsonProperty("bombHits")]
		public int BombHits { get; set; }

		[JsonProperty("misses")]
		public int Misses { get; set; }

		public bool FC => (Misses + BombHits + BadCuts + WallHits == 0);

		[JsonProperty("timeSet")]
		public DateTime TimeSet { get; set; }
	}
}