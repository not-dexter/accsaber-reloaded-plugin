using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using AccSaber.Managers;
using AccSaber.Models.Base;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AccSaber.Models
{
    [UsedImplicitly]
    internal sealed class AccSaberScore : Model
    {
        [JsonProperty("userId")]
        public string UserId { get; set; } = null!;

    }
}