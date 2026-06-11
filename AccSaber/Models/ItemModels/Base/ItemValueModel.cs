using AccSaber.Models.Base;
using Newtonsoft.Json;

namespace AccSaber.Models.ItemModels.Base
{
    internal abstract class ItemValueModel : IModel
    {
        [JsonIgnore]
        public System.Guid ItemId { get; set; }

        public virtual void Propagate() { }
    }
}
