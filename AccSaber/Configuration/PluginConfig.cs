using System.ComponentModel;
using System.Runtime.CompilerServices;
using IPA.Config.Stores;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]

namespace AccSaber.Configuration
{
	internal class PluginConfig : INotifyPropertyChanged
	{
        public event PropertyChangedEventHandler? PropertyChanged;

        public virtual bool CombineRelations { get; set { field = value; PropertyChanged?.Invoke(this, new(nameof(CombineRelations))); } } = false;
        public virtual bool RainbowHeader { get; set { field = value; PropertyChanged?.Invoke(this, new(nameof(RainbowHeader))); } } = false;
        public virtual bool ShowCombo { get; set { field = value; PropertyChanged?.Invoke(this, new(nameof(ShowCombo))); } } = true;
        public virtual bool ShowStreak { get; set { field = value; PropertyChanged?.Invoke(this, new(nameof(ShowStreak))); } } = true;
        public virtual int AccDecimals { get; set { field = value; PropertyChanged?.Invoke(this, new(nameof(AccDecimals))); } } = 2;
        public virtual int TimePlaces { get; set { field = value; PropertyChanged?.Invoke(this, new(nameof(TimePlaces))); } } = 2;
        public virtual bool DisablePopups { get; set { field = value; PropertyChanged?.Invoke(this, new(nameof(DisablePopups))); } } = false;
        public virtual bool GoToPlaylist { get; set { field = value; PropertyChanged?.Invoke(this, new(nameof(GoToPlaylist))); } } = true;
        public virtual bool SubmitOnIncompletePlay { get; set { field = value; PropertyChanged?.Invoke(this, new(nameof(SubmitOnIncompletePlay))); } } = true;
        public virtual bool AllowMultipleCustomPlaylists { get; set { field = value; PropertyChanged?.Invoke(this, new(nameof(AllowMultipleCustomPlaylists))); } } = true;
        public virtual string CustomPlaylistPath { get; set { field = value; PropertyChanged?.Invoke(this, new(nameof(CustomPlaylistPath))); } } = "";
    }
}