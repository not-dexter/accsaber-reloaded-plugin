using AccSaber.Models.Base;
using AccSaber.Models.ItemModels;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace AccSaber.Models
{
    [UsedImplicitly]
    internal class AccSaberEvent : IModel, IComparable<AccSaberEvent>
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; } = null!;

        [JsonProperty("description")]
        public string Description { get; set; } = null!;

        [JsonProperty("backgroundUrl")]
        public string BackgroundUrl { get; set; } = null!;

        [JsonProperty("iconUrl")]
        public string IconUrl { get; set; } = null!;

        [JsonProperty("startsAt")]
        public DateTime StartsAt { get; set; }

        [JsonProperty("endsAt")]
        public DateTime EndsAt { get; set; }

        [JsonProperty("bonusXp")]
        public float BonusXp { get; set; }

        [JsonProperty("bonusItems")]
        public List<AccSaberItemReference> BonusItems { get; set; } = [];

        [JsonProperty("active")]
        public bool Active { get; set; }

        [JsonProperty("live")]
        public bool Live { get; set; }

        [JsonProperty("currentWeek")]
        public int CurrentWeek { get; set; }

        [JsonProperty("totalWeeks")]
        public int TotalWeeks { get; set; }


        public int CompareTo(AccSaberEvent other) => StartsAt.CompareTo(other.StartsAt);
    }

    [UsedImplicitly]
    internal class AccSaberEventMission : AccSaberMission
    {
        [JsonProperty("unlocksAt")]
        public DateTime UnlocksAt { get; set; }

        [JsonProperty("completableUntil")]
        public DateTime CompletableUntil { get; set; }

        [JsonProperty("week")]
        public int Week { get; set; }

        [JsonProperty("unlocked")]
        public bool Unlocked { get; set; }

        [JsonProperty("open")]
        public bool Open { get; set; }

        [JsonProperty("repeatable")]
        public bool Repeatable { get; set; }

        [JsonProperty("maxCompletions")]
        public int MaxCompletions { get; set; }

    }

    [UsedImplicitly]
    internal class EventMissionTargets
    {
        [JsonProperty("categoryId")]
        public Guid CategoryId { get; set; }

        [JsonProperty("mapDifficultyId")]
        public Guid? MapDifficultyId { get; set; }

        [JsonProperty("playerID")]
        public string? PlayerID { get; set; } = null!;

        [JsonProperty("acc")]
        public float? Acc {  get; set; }

        [JsonProperty("ap")]
        public float? AP { get; set; }

        [JsonProperty("score")]
        public int? Score { get; set; }

        [JsonProperty("count")]
        public int? Count { get; set; }

        [JsonProperty("xp")]
        public int? XP { get; set; }

        [JsonProperty("thresholdAP")]
        public float? ThresholdAP { get; set; }

        [JsonProperty("streak")]
        public int? Streak { get; set; }
    }


    [UsedImplicitly]
    internal class AccSaberEventResponse
    {
        [JsonProperty("event")]
        public AccSaberEvent Event { get; set; } = null!;

        [JsonProperty("missions")]
        public List<AccSaberEventMission> Missions { get; set; } = null!;
    }

    [UsedImplicitly]
    internal class AccSaberEventMe : IModel
    {
        [JsonProperty("mission")]
        public AccSaberEventMission Mission { get; set; } = null!;

        [JsonProperty("current")]
        public AccSaberEventMission? Current { get; set; } = null;

        [JsonProperty("completions")]
        public int Completions {  get; set; }

        [JsonProperty("completed")]
        public bool Completed { get; set; }

        [JsonProperty("weekLocked")]
        public bool WeekLocked { get; set; }
    }

    [UsedImplicitly]
    internal class AccSaberEventBegun
    {
        [JsonProperty("begun")]
        public bool Begun { get; set; }
    }
}
