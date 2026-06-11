using AccSaber.Models.Base;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace AccSaber.Models
{
    internal class AccSaberCampaign : IModel
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("creatorId")]
        public string CreatorId { get; set; } = null!;

        [JsonProperty("creatorName")]
        public string CreatorName { get; set; } = null!;

        [JsonProperty("creatorAlias")]
        public string CreatorAlias { get; set; } = null!;

        [JsonProperty("name")]
        public string Name { get; set; } = null!;

        [JsonProperty("summary")]
        public string Summary { get; set; } = null!;

        [JsonProperty("status")]
        public string Status { get; set; } = null!;

        [JsonProperty("seekingCuration")]
        public bool SeekingCuration { get; set; }

        [JsonProperty("progressionAgnostic")]
        public bool ProgressionAgnostic { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; } = null!;

        [JsonProperty("legacy")]
        public bool Legacy { get; set; }

        [JsonProperty("verified")]
        public bool Verified { get; set; }

        [JsonProperty("difficultyCount")]
        public int? DifficultyCount { get; set; }

        [JsonProperty("completionMode")]
        public string CompletionMode { get; set; } = null!;

        [JsonProperty("completionXp")]
        public int CompletionXp { get; set; }

        [JsonProperty("curatorNotes")]
        public string CuratorNotes { get; set; } = null!;

        [JsonProperty("backgroundUrl")]
        public string BackgroundUrl { get; set; } = null!;

        [JsonProperty("submittedAt")]
        public DateTime SubmittedAt { get; set; }

        [JsonProperty("playlistExportEnabled")]
        public bool PlaylistExportEnabled { get; set; }

        [JsonProperty("curatedAt")]
        public DateTime CuratedAt { get; set; }

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("tags")]
        public List<CampaignTags>? Tags { get; set; }

        [JsonProperty("difficulties")]
        public List<AccSaberCampaignMap>? Difficulties { get; set; }
    }

    internal class CampaignTags
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("kind")]
        public string Kind { get; set; } = null!;

        [JsonProperty("categoryId")]
        public Guid CategoryId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = null!;

        [JsonProperty("system")]
        public bool System { get; set; }
    }

    internal class AccSaberCampaignMap
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("mapDifficultyId")]
        public Guid MapDifficultyId { get; set; }

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

        [JsonProperty("requirementType")]
        public string RequirementType { get; set; } = null!;

        [JsonProperty("requirementValue")]
        public float RequirementValue { get; set; }

        [JsonProperty("prerequisiteMode")]
        public string PrerequisiteMode { get; set; } = null!;

        [JsonProperty("description")]
        public string Description { get; set; } = null!;


        [JsonProperty("xp")]
        public int XP { get; set; }

        [JsonProperty("prerequisiteMapIds")]
        public virtual List<Guid> PrerequisiteMapIds { get; set; } = [];
    }
}
