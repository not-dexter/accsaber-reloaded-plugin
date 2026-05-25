using System.Runtime.CompilerServices;
using System.Collections.Generic;
using IPA.Config.Stores.Converters;
using IPA.Config.Stores.Attributes;
using IPA.Config.Stores;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]

namespace AccSaber.Configuration
{
	internal class PluginConfig
	{
        public virtual bool CombineRelations { get; set; } = false;
        public virtual bool RainbowHeader { get; set; } = false;
        public virtual int AccDecimals { get; set; } = 4;

        [UseConverter(typeof(ListConverter<string>))]
        public List<string> Friends { get; set; } = new List<string>();

        public bool IsFriend(string userid)
        {
            return Friends.Contains(userid);
        }
    }
}