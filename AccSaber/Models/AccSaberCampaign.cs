using AccSaber.Models.Base;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccSaber.Models
{
    internal class AccSaberCampaign : Model
    {
        [JsonProperty("id")]
        public string Id { get; set; } = null!;

        [JsonProperty("creatorId")]
        public string CreatorId { get; set; } = null!;

        [JsonProperty("creatorName")]
        public string CreatorName { get; set; } = null!;

        [JsonProperty("description")]
        public string Description { get; set; } = null!;

        [JsonProperty("verified")]
        public bool Verified { get; set; }

        [JsonProperty("mapCount")]
        public int MapCount { get; set; }

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("maps")]
        public List<AccSaberCampaignMap>? Maps { get; set; }
    }

    internal class AccSaberCampaignMap
    {
        [JsonProperty("id")]
        public string Id { get; set; } = null!;

        [JsonProperty("mapDifficultyId")]
        public string MapDifficultyId { get; set; } = null!;

        [JsonProperty("songName")]
        public string SongName { get; set; } = null!;

        [JsonProperty("songAuthor")]
        public string SongAuthor { get; set; } = null!;

        [JsonProperty("coverUrl")]
        public string CoverUrl { get; set; } = null!;

        [JsonProperty("difficulty")]
        public string Difficulty { get; set; } = null!;

        [JsonProperty("characteristic")]
        public string Characteristic { get; set; } = null!;

        [JsonProperty("accuracyRequirement")]
        public float AccuracyRequirement { get; set; }

        [JsonProperty("xp")]
        public int XP { get; set; }

        [JsonProperty("prerequisiteMapIds")]
        public virtual List<string> PrerequisiteMapIds { get; set; } = [];
    }
}
