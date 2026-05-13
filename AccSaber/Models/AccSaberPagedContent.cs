using AccSaber.Models.Base;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace AccSaber.Models
{
    [UsedImplicitly]
    internal sealed class AccSaberPagedContent<T> : AccSaberPagedContent where T : Model
    {
        [JsonProperty("content")]
        public List<T>? Content { get; set; }
    }
    [UsedImplicitly]
    internal class AccSaberPagedContent : Model
    {
        [JsonProperty("empty")]
        public bool Empty { get; set; }

        [JsonProperty("first")]
        public bool FirstPage { get; set; }

        [JsonProperty("last")]
        public bool LastPage { get; set; }

        [JsonProperty("number")]
        public int Number { get; set; }

        [JsonProperty("numberOfElements")]
        public int NumberOfElements { get; set; }

        [JsonProperty("pageable")]
        public Pageable Pageable { get; set; } = null!;

        [JsonProperty("sort")]
        public Sort Sort { get; set; } = null!;

        [JsonProperty("totalElements")]
        public int TotalElements { get; set; }

        [JsonProperty("totalPages")]
        public int TotalPages { get; set; }

        [JsonProperty("size")]
        public int Size { get; set; }
    }

    [UsedImplicitly]
    internal sealed class Pageable : Model
    {
        [JsonProperty("offset")]
        public int Offset { get; set; }

        [JsonProperty("pageNumber")]
        public int PageNumber { get; set; }

        [JsonProperty("pageSize")]
        public int PageSize { get; set; }

        [JsonProperty("paged")]
        public bool Paged { get; set; }

        [JsonProperty("sort")]
        public Sort Sort { get; set; } = null!;

        [JsonProperty("unpaged")]
        public bool Unpaged { get; set; }
    }

    [UsedImplicitly]
    internal sealed class Sort : Model
    {
        [JsonProperty("empty")]
        public bool Empty { get; set; }

        [JsonProperty("sorted")]
        public bool Sorted { get; set; }

        [JsonProperty("unsorted")]
        public bool Unsorted { get; set; }
    }
}
