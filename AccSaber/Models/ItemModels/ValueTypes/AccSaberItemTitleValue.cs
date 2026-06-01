using AccSaber.Models.ItemModels.ValueTypes.States;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AccSaber.Models.ItemModels.ValueTypes
{
    [UsedImplicitly]
    internal class AccSaberItemTitleValue : AccSaberItemStateValue<AccSaberColorState>
    {
        [JsonProperty("font")]
        public string? Font { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; } = null!;

    }
}
