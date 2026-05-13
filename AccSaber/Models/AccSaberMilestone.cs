using System;
using AccSaber.Models.Base;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AccSaber.Models
{
	[UsedImplicitly]

	internal class AccSaberMilestone : Model
	{
        [JsonProperty("completed")]
        public bool Completed { get; set; }

        [JsonProperty("completedAt")]
        public DateTime CompletedAt { get; set; }

        [JsonProperty("completionPercentage")]
        public float CompletionPercentage { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; } = null!;

        [JsonProperty("milestoneId")]
		public string MilestoneId { get; set; } = null!;

        [JsonProperty("normalizedProgress")]
        public float NormalizedProgress { get; set; }

        [JsonProperty("progress")]
        public float Progress { get; set; }

        [JsonProperty("setId")]
        public string SetId { get; set; } = null!;

        [JsonProperty("targetValue")]
        public float TargetValue { get; set; }

        [JsonProperty("tier")]
        public string Tier { get; set; } = null!;

        [JsonProperty("title")]
        public string Title { get; set; } = null!;


        [JsonProperty("type")]
        public string Type { get; set; } = null!;


        [JsonProperty("xp")]
        public float XP { get; set; }
	}
}