using System;
using System.Runtime.Serialization;
using AccSaber.Managers;
using AccSaber.Models.Base;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AccSaber.Models
{
    [UsedImplicitly]
    internal sealed class AccSaberDifficulty : Model
    {
		[JsonProperty("active")]
		public bool Active { get; set; }

		[JsonProperty("status")]
		public string RankedStatus { get; set; } = null!;

		[JsonProperty("criteriaStatus")]
		public string CriteriaStatus { get; set; } = null!;

		[JsonProperty("autoCriteriaStatus")]
		public string AutoCriteriaStatus { get; set; } = null!;

		[JsonProperty("blLeaderboardId")]
		public string BlLeaderboardId { get; set; } = null!;

		[JsonProperty("categoryId")]
		public string CategoryId { get; set; } = null!;

		[JsonProperty("characteristic")]
		public string Characteristic { get; set; } = null!;

		[JsonProperty("difficulty")]
		public string Difficulty { get; set; } = null!;

		[JsonProperty("complexity")]
		public float Complexity { get; set; }

	}
}