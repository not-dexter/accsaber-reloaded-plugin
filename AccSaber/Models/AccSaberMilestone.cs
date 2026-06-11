using AccSaber.Models.Base;
using AccSaber.Utils;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Runtime.Serialization;
using UnityEngine;

namespace AccSaber.Models
{
	[UsedImplicitly]

	internal class AccSaberMilestone : IModel
	{
        [JsonProperty("milestoneId")]
		public Guid MilestoneId { get; set; }

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

        [JsonProperty("progress")]
        public float Progress { get; set; }

        [JsonProperty("normalizedProgress")]
        public float NormalizedProgress { get; set; }

        [JsonIgnore]
        public float CalculatedProgress => CalcProgress(TargetValue, Progress, Progress > TargetValue);

        [JsonProperty("completed")]
        public bool Completed { get; set; }

        [JsonProperty("completedAt")]
        public DateTime CompletedAt { get; set; }

        [JsonProperty("completionPercentage")]
        public float CompletionPercentage { get; set; }

        [JsonProperty("setId")]
        public Guid SetId { get; set; }

        [JsonProperty("categoryId")]
        public Guid? CategoryId { get; set; }

        [JsonIgnore]
        public APCategory Category { get; set; }


        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Category = EnumUtils.ReloadedCategoryIdToCategory(CategoryId);
        }

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

    [UsedImplicitly]
    internal class AccSaberFullMilestone : AccSaberMilestone
    {
        [JsonProperty("blExclusive")]
        public bool BlExclusive { get; set; }

        [JsonProperty("comparison")]
        public ComparisonType Comparison { get; set; }

        [JsonProperty("id")]
        public Guid Id { get => MilestoneId; set => MilestoneId = value; }

        [JsonProperty("querySpec")]
        public AccSaberQuery QuerySpec { get; set; } = null!;
    }
}