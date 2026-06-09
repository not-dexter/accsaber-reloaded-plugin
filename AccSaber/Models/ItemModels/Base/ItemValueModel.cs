using AccSaber.Models.Base;
using Newtonsoft.Json;

namespace AccSaber.Models.ItemModels.Base
{
    internal abstract class ItemValueModel : IModel
    {
        [JsonIgnore]
        public string ItemId { get; set; } = null!;

        public virtual void Propagate() { }
    }
}
