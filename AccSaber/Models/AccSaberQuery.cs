using AccSaber.Models.Base;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AccSaber.Models
{
    [UsedImplicitly]
    internal class AccSaberQuery : IModel
    {
        [JsonProperty("select")]
        public AccSaberQuerySelect Select { get; set; } = null!;

        [JsonProperty("from")]
        public string From { get; set; } = null!;

        [JsonProperty("having")]
        public AccSaberQueryHaving? Having { get; set; }
    }

    [UsedImplicitly]
    internal class AccSaberQuerySelect : IModel
    {
        [JsonProperty("function")]
        public FunctionType Function { get; set; }

        [JsonProperty("column")]
        public string Column { get; set; } = null!;

        [JsonProperty("operator")]
        public string? Operator { get; set; }

        public enum FunctionType
        {
            MIN, MAX, COUNT, COUNT_DISTINCT
        }
    }

    [UsedImplicitly]
    internal class AccSaberQueryHaving : AccSaberQuerySelect
    {
        [JsonProperty("value_query")]
        public AccSaberQuery? ValueQuery { get; set; }
    }

}
