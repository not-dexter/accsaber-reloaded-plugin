using AccSaber.Configuration;
using AccSaber.Utils;
using BeatSaberMarkupLanguage.Attributes;
using HMUI;
using System;
using Zenject;

namespace AccSaber.UI.MenuButton.ViewControllers
{
    internal class AccSaberPlaylistSettingsModalController : Utils.Safety.SafeNotifyPropertyChanged, IInitializable, IDisposable
    {
        [Inject] private readonly PluginConfig config = null!;
        [Inject] private readonly PlaylistUtils playlistUtils = null!;
        [Inject] private readonly AccSaberMainFlowCoordinator mainFlowCoordinator = null!;

        private bool parsed = false;

        [UIComponent(nameof(modal))] 
        private readonly ModalView modal = null!;


        [UIValue(nameof(OverridePlaylists))] 
        private bool OverridePlaylists
        {
            get => !config.AllowMultipleCustomPlaylists;
            set
            {
                if (config.AllowMultipleCustomPlaylists != value) 
                    return;

                config.AllowMultipleCustomPlaylists = !value;
                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(AllowPopups))]
        private bool AllowPopups
        {
            get => !config.DisablePopups;
            set
            {
                if (config.DisablePopups != value)
                    return;

                config.DisablePopups = !value;
                NotifyPropertyChanged();
            }
        }


        [UIAction("#post-parse")]
        private void PostParse()
        {
            if (parsed)
                return;

            parsed = true;
        }

        [UIAction(nameof(ClearCustomPlaylists))]
        private void ClearCustomPlaylists()
        {
            playlistUtils.DeleteCustomPlaylists();
        }

        public void Show()
        {
            if (!parsed)
                return;

            modal.Show(true, true);
        }
        public void Hide()
        {
            modal?.Hide(false);
        }

        public void Initialize()
        {
            mainFlowCoordinator.OnHubDeactivated += Hide;
        }
        public void Dispose()
        {
            mainFlowCoordinator.OnHubDeactivated -= Hide;
        }
    }
}
