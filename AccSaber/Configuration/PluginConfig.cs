using System.Runtime.CompilerServices;
using IPA.Config.Stores;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]

namespace AccSaber.Configuration
{
	internal class PluginConfig
	{
        public virtual bool CombineRelations { get; set; } = false;
        public virtual bool RainbowHeader { get; set; } = false;
        public virtual int AccDecimals { get; set; } = 4;
    }
}