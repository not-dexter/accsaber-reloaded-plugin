using AccSaber.Configuration;
using AccSaber.Consts;
using AccSaber.Utils;
using AccSaber.Utils.Misc;
using BeatSaberMarkupLanguage.Attributes;
using HMUI;
using UnityEngine;
using Zenject;

namespace AccSaber.UI.MenuButton.Campaigns.ViewControllers
{
    internal class AccSaberCampaignSettingsModalController
    {
        private bool parsed = false;

        [Inject] private readonly PluginConfig config = null!;


        [UIComponent("modal")]
        private ModalView modal = null!;


        [UIValue(nameof(StickScrolling))]
        private bool StickScrolling
        {
            get => config.StickScrolling; 
            set => config.StickScrolling = value;
        }

        [UIValue(nameof(ScrollSpeed))]
        private float ScrollSpeed
        {
            get => config.ScrollSpeed;
            set => config.ScrollSpeed = value;
        }

        [UIValue(nameof(ShowPrereqIndicator))]
        private bool ShowPrereqIndicator
        {
            get => config.ShowPrereqIndicator;
            set => config.ShowPrereqIndicator = value;
        }

        private void Parse(Transform parent)
        {
            if (!parsed)
            {
                VersionUtils.Parse(ResourcePaths.ACC_SABER_CAMPAIGN_SETTINGS_MODAL, parent, this);

                parsed = true;
            }

            modal.transform.SetParent(parent.transform);
            Accessors.ViewValidAccessor(ref modal) = false;
        }
        public void ShowModal(Transform parent)
        {
            Parse(parent);

            modal.Show(true, true);
        }
    }
}
