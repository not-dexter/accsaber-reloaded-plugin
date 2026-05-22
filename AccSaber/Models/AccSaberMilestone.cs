using AccSaber.Models.Base;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using UnityEngine;

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

        [JsonIgnore]
        public float CalculatedProgress => CalcProgress(TargetValue, Progress, Progress > TargetValue);

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
        
        public static float CalcProgress(float target, float progress, bool swap)
        {
            const float shiftAmount = 0.97f; // Shift both values down to make it more meaningful to go from 97 to 98

            bool isPercent = target < 1f;
            bool needsShifting = target >= (shiftAmount + 0.01f);

            if (swap)
                (progress, target) = (target, progress);
            if (isPercent)
            {
                const float baseNum = 2f;
                const float expMult = 3f;
                const float expMultSquared = expMult * expMult;

                float progPercent = needsShifting ? (progress - shiftAmount) / (target - shiftAmount) : progress / target;

                float exp = expMultSquared * progPercent - expMultSquared;

                return Mathf.Pow(baseNum, exp);
            }
            else return progress / target;
        }
    }
}