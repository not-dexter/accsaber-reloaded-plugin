using AccSaber.Models.Base;
using AccSaber.Utils;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;


namespace AccSaber.Models
{
    [UsedImplicitly]
    internal class AccSaberLeaderboardEntry : IModel, IEquatable<AccSaberLeaderboardEntry>
    {
        [JsonProperty("accuracy")]
        public float Accuracy { get; set; }

        [JsonProperty("active")]
        public bool Active { get; set; }

        [JsonProperty("ap")]
        public float AP { get; set; }

        [JsonProperty("avatarUrl")]
        public string AvatarURL { get; set; } = null!;

        [JsonProperty("clan")]
        public AccSaberClan Clan { get; set; } = null!;

        [JsonProperty("badCuts")]
        public int BadCuts { get; set; }

        [JsonProperty("baseXp")]
        public float BaseXp { get; set; }

        [JsonProperty("blScoreId")]
        public string? BlScoreId { get; set; }

        [JsonProperty("bombHits")]
        public int? BombHits { get; set; }

        [JsonProperty("bonusXp")]
        public float BonusXp { get; set; }

        [JsonProperty("categoryId")]
        public Guid CategoryId { get; set; }

        [JsonProperty("country")]
        public string Country { get; set; } = null!;

        [JsonProperty("coverUrl")]
        public string CoverUrl { get; set; } = null!;

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("difficulty")]
        public string Difficulty { get; set; } = null!;

        [JsonProperty("hmd")]
        public string Hmd { get; set; } = null!;

        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("mapAuthor")]
        public string MapAuthor { get; set; } = null!;

        [JsonProperty("mapDifficultyId")]
        public Guid DifficultyId { get; set; }

        [JsonProperty("mapId")]
        public Guid MapId { get; set; }

        [JsonProperty("misses")]
        public int Misses { get; set; }

        [JsonProperty("modifierIds")]
        public List<object> ModifierIds { get; set; } = null!;

        [JsonProperty("pauses")]
        public int? Pauses { get; set; }

        [JsonProperty("rank")]
        public int Rank { get; set; }

        [JsonProperty("rankWhenSet")]
        public int RankWhenSet { get; set; }

        [JsonProperty("reweightDerivative")]
        public bool ReweightDerivative { get; set; }

        [JsonProperty("score")]
        public int Score { get; set; }

        [JsonProperty("scoreNoMods")]
        public int ScoreNoMods { get; set; }

        [JsonProperty("songAuthor")]
        public string SongAuthor { get; set; } = null!;

        [JsonProperty("songHash")]
        public string SongHash { get; set; } = null!;

        [JsonProperty("songName")]
        public string SongName { get; set; } = null!;

        [JsonProperty("streak115")]
        public int? Streak115 { get; set; }

        [JsonProperty("timeSet")]
        public DateTime TimeSet { get; set; }

        [JsonProperty("userId")]
        public string PlayerId { get; set; } = null!;

        [JsonProperty("userName")]
        public string PlayerName { get; set; } = null!;

        [JsonProperty("wallHits")]
        public int? WallHits { get; set; }

        [JsonProperty("weightedAp")]
        public float WeightedAp { get; set; }

        [JsonProperty("xpGained")]
        public float XpGained { get; set; }

        [JsonIgnore]
        public bool FC => Misses + (BombHits ?? 0) + BadCuts + (WallHits ?? 0) == 0;

        public bool Equals(AccSaberLeaderboardEntry other) =>
            PlayerId.Equals(other.PlayerId) && AP == other.AP && TimeSet == other.TimeSet;

        public override bool Equals(object obj) => obj is AccSaberLeaderboardEntry entry && Equals(entry);
        public override int GetHashCode() => MiscUtils.GetHashCode(PlayerId, TimeSet);
    }
}