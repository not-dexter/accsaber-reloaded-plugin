using System;
using AccSaber.Models.Base;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AccSaber.Models
{
	[UsedImplicitly]

	internal class AccSaberMilestone : Model
	{
		[JsonProperty("milestoneId")]
		public string MilestoneId { get; set; } = null!;

		[JsonProperty("userCompleted")]
		public bool Completed { get; set; }

		[JsonProperty("userCompletedAt")]
		public DateTime CompletedAt { get; set; }

		[JsonProperty("title")]
		public string Title { get; set; } = null!;

		[JsonProperty("description")]
		public string Description { get; set; } = null!;

		[JsonProperty("type")]
		public string Type { get; set; } = null!;

		[JsonProperty("tier")]
		public string Tier { get; set; } = null!;

		[JsonProperty("xp")]
		public float XP { get; set; }

		[JsonProperty("targetValue")]
		public float TargetValue { get; set; }

		[JsonProperty("userNormalizedProgress")]
		public float NormalizedProgress { get; set; }

		[JsonProperty("userProgress")]
		public float Progress { get; set; }

		[JsonProperty("setId")]
		public string SetId { get; set; } = null!;

		[JsonProperty("completionPercentage")]
		public float CompletionPercentage { get; set; }

		[JsonProperty("categoryId")]
		public string CategoryId { get; set; } = null!;

	}
}