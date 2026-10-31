using AccSaber.Models.Base;
using Newtonsoft.Json;
using JetBrains.Annotations;

namespace AccSaber.Models.ItemModels.Base
{
    [UsedImplicitly]
    internal abstract class ItemValueModel : IModel
    {
        [JsonIgnore]
        public System.Guid ItemId { get; set; }

        public virtual void Propagate() { }
    }
}
