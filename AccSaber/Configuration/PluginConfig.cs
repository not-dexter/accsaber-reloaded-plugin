using System.Runtime.CompilerServices;
using IPA.Config.Stores;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]

namespace AccSaber.Configuration
{
	internal class PluginConfig
	{
        public virtual bool CombineRelations { get; set; } = false;
        public virtual bool RainbowHeader { get; set; } = false;
        public virtual bool ShowCombo { get; set; } = true;
        public virtual int AccDecimals { get; set; } = 2;
        public virtual int TimePlaces { get; set; } = 2;
        public virtual bool DisablePopups { get; set; } = false;
        public virtual bool GoToPlaylist { get; set; } = true;
    }
}