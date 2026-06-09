using AccSaber.Models.Base;
using AccSaber.Utils;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace AccSaber.Models
{
    [UsedImplicitly]
    internal class AccSaberScore : IModel
    {
        [JsonProperty("nonce", Required = Required.DisallowNull)]
        internal string Nonce { get; set; } = MiscUtils.GenerateNonce(64);

        [JsonProperty("mapDifficultyId", Required = Required.DisallowNull)]
        public string MapDifficultyId { get; set; } = null!;

        [JsonProperty("score", Required = Required.DisallowNull)]
        public uint Score { get; set; } = 0;

        [JsonProperty("scoreNoMods", Required = Required.DisallowNull)]
        public uint ScoreNoMods { get; set; } = 0;

        [JsonProperty("maxCombo")]
        public int MaxCombo { get; set; } = 0;

        [JsonProperty("badCuts")]
        public int BadCuts { get; set; } = 0;

        [JsonProperty("misses")]
        public int Misses { get; set; } = 0;

        [JsonProperty("wallHits")]
        public int WallHits { get; set; } = 0;

        [JsonProperty("bombHits")]
        public int BombHits { get; set; } = 0;

        [JsonProperty("pauses")]
        public int Pauses { get; set; } = 0;

        [JsonProperty("streak115")]
        public int Streak115 { get; set; } = 0;

        [JsonProperty("hmd")]
        public string Headset { get; set; } = null!;

        [JsonProperty("timeSet")]
        public DateTime TimeSet { get; set; }

        [JsonProperty("modifierCodes")]
        public List<string> ModifierCodes { get; set; } = [];

        [JsonProperty("partial")]
        public bool? UncompletedMap { get; set; } = null;
    }
}
