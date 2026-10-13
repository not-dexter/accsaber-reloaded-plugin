using AccSaber.Models.Base;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;

namespace AccSaber.Models
{
    [UsedImplicitly]
    internal class AccSaberClan : IModel
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("slug")]
        public string Slug { get; set; } = null!;

        [JsonProperty("name")]
        public string Name { get; set; } = null!;

        [JsonProperty("tag")]
        public string Tag { get; set; } = null!;

        [JsonProperty("tagColor")]
        public string TagColor { get; set; } = null!;

        [JsonProperty("primaryColor")]
        public string PrimaryColor { get; set; } = null!;

        [JsonProperty("secondaryColor")]
        public string SecondaryColor { get; set; } = null!;

    }
}
