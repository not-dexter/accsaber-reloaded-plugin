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
        public string NewsId { get; set; } = null!;

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
        public string BatchId { get; set; } = null!;

        [JsonProperty("campaignId")]
        public string CampaignId { get; set; } = null!;

        [JsonProperty("milestoneSetId")]
        public string MilestoneSetId { get; set; } = null!;

        [JsonProperty("curveId")]
        public string CurveId { get; set; } = null!;

        [JsonProperty("publishedAt")]
        public DateTime PublishedAt { get; set; }
    }
}