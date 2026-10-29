using JetBrains.Annotations;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace AccSaber.Models.WarModels
{
    [UsedImplicitly]
    internal class AccSaberClanWarDetails : IWarModel
    {
        [JsonProperty("war")]
        public AccSaberClanWar War { get; set; } = null!;

        [JsonProperty("pool")]
        public List<AccSaberClanWarPool> Pool { get; set; } = null!;
    }
}
