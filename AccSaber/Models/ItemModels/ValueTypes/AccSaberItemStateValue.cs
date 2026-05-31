using AccSaber.Models.ItemModels.Base;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace AccSaber.Models.ItemModels.ValueTypes
{
    [UsedImplicitly]
    internal class AccSaberItemStateValue<T> : ItemValueModel where T : ItemStateModel<T>
    {
        [JsonProperty("states")]
        public List<T> States { get; set; } = [];

        [JsonProperty("durationMs")]
        public int DurationMs { get; set; } = 1000;

        public override void Propagate()
        {
            foreach (T state in States)
            {
                state.ItemId = ItemId;
                state.Propagate();
            }
        }
    }
}
