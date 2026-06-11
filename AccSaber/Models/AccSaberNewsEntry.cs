using System;
using AccSaber.Models.Base;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AccSaber.Models
{
    [UsedImplicitly]
    internal sealed class AccSaberNewsEntry : IModel
    {
        [JsonProperty("id")]
        public Guid NewsId { get; set; }

        [JsonProperty("authorName")]
        public string AuthorName { get; set; } = null!;

        [JsonProperty("title")]
        public string Title { get; set; } = null!;

        [JsonProperty("slug")]
        public string Slug { get; set; } = null!;

        [JsonProperty("description")]
        public string Description { get; set; } = null!;

        [JsonProperty("content")]
        public string Content { get; set; } = null!;

        [JsonProperty("imageUrl")]
        public string ImageUrl { get; set; } = null!;

        [JsonProperty("type")]
        public string Type { get; set; } = null!;

        [JsonProperty("pinned")]
        public bool Pinned { get; set; }

        [JsonProperty("batchId")]
        public Guid BatchId { get; set; }

        [JsonProperty("campaignId")]
        public Guid CampaignId { get; set; }

        [JsonProperty("milestoneSetId")]
        public Guid MilestoneSetId { get; set; }

        [JsonProperty("curveId")]
        public Guid CurveId { get; set; }

        [JsonProperty("publishedAt")]
        public DateTime PublishedAt { get; set; }
    }
}