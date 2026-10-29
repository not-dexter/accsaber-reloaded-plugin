using JetBrains.Annotations;
using Newtonsoft.Json;
using System;

namespace AccSaber.Models.WarModels
{
    [UsedImplicitly]
    internal class AccSaberClanWarPool : IWarModel
    {
        [JsonProperty("difficulty")]
        public AccSaberDifficulty Difficulty { get; set; } = null!;

        // PickedBy, Source are skipped.

        [JsonProperty("viewerScore")]
        public AccSaberClanWarScore ViewerScore { get; set; } = null!;

        [JsonProperty("targetScore")]
        public AccSaberClanWarScore? TargetScore { get; set; }

        [JsonProperty("targetPicked")]
        public bool TargetPicked { get; set; }

        [JsonProperty("targetRepeatMap")]
        public bool TargetRepeatMap { get; set; }
    }

    [UsedImplicitly]
    internal class AccSaberClanWarScore : IWarModel
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("score")]
        public int Score { get; set; }

        [JsonProperty("accuracy")]
        public float Accuracy { get; set; }

        [JsonProperty("ap")]
        public float Ap { get; set; }

        [JsonProperty("rank")]
        public int Rank { get; set; }
    }
}
