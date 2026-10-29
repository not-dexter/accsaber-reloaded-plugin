using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace AccSaber.Models.WarModels
{
    [UsedImplicitly]
    internal class AccSaberClanWar : IWarModel
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("attack")]
        public AccSaberClanWarData AttackingClanData { get; set; } = null!;

        [JsonProperty("defend")]
        public AccSaberClanWarData DefendingClanData { get; set; } = null!;


        public bool IsClanInWar(Guid clanId)
        {
            return AttackingClanData.ClanIds.Contains(clanId) || DefendingClanData.ClanIds.Contains(clanId);
        }
    }

    [UsedImplicitly]
    internal class AccSaberClanWarData : IWarModel
    {
        [JsonProperty("lead")]
        public AccSaberClan LeadClan { get; set; } = null!;

        [JsonProperty("stake")]
        public float Stake { get; set; }

        [JsonProperty("stakeRemaining")]
        public float StakeRemaining { get; set; }

        [JsonProperty("sides")]
        public List<AccSaberClanWarSides> Sides { get; set; } = [];

        [JsonIgnore]
        public List<Guid> ClanIds { get; set; } = [];


        [OnDeserialized]
        public void OnDeserialized(StreamingContext context)
        {
            foreach (AccSaberClanWarSides side in Sides)
                ClanIds.Add(side.Clan.Id);
        }
    }

    [UsedImplicitly]
    internal class AccSaberClanWarSides : IWarModel
    {
        [JsonProperty("clan")]
        public AccSaberClan Clan { get; set; } = null!;

        [JsonProperty("team")]
        public string? Team { get; set; }

        [JsonProperty("fallen")]
        public bool Fallen { get; set; }
    }
}
