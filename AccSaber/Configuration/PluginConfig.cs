using System.ComponentModel;
using System.Runtime.CompilerServices;
using IPA.Config.Stores;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]

namespace AccSaber.Configuration
{
	internal class PluginConfig : INotifyPropertyChanged
	{
        public event PropertyChangedEventHandler? PropertyChanged;

        public void RaisePropertyChanged(string memberName)
        {
            PropertyChanged?.Invoke(this, new(memberName));
        }

        public virtual bool CombineRelations { get; set; } = false;
        public virtual bool RainbowHeader { get; set; } = false;
        public virtual bool ShowCombo { get; set; } = true;
        public virtual bool ShowStreak { get; set; } = true;
        public virtual int AccDecimals { get; set; } = 2;
        public virtual int TimePlaces { get; set; } = 2;
        public virtual bool DisablePopups { get; set; } = false;
        public virtual bool GoToPlaylist { get; set; } = true;
        public virtual bool SubmitOnIncompletePlay { get; set; } = true;
        public virtual bool AllowMultipleCustomPlaylists { get; set; } = true;
        public virtual string CustomPlaylistPath { get; set; } = "";
        public virtual bool StickScrolling { get; set; } = true;
        public virtual float ScrollSpeed { get; set; } = 2.0f;
    }
}