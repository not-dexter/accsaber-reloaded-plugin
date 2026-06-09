using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AccSaber.Models
{
    [UsedImplicitly]
    internal class AccSaberQuery
    {
        [JsonProperty("select")]
        public AccSaberQuerySelect Select { get; set; } = null!;

        [JsonProperty("from")]
        public string From { get; set; } = null!;
    }

    [UsedImplicitly]
    internal class AccSaberQuerySelect
    {
        [JsonProperty("function")]
        public FunctionType Function { get; set; }

        [JsonProperty("column")]
        public string Column { get; set; } = null!;

        public enum FunctionType
        {
            MIN, MAX
        }
    }


}
