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
    internal class AccSaberCampaignCounterSettingsModalController
    {
        [Inject] private readonly PluginConfig config = null!;

        private bool parsed = false;

        [UIComponent("modal")]
        private ModalView modal = null!;

        [UIValue("FontSize")]
        private float FontSize
        {
            get => config.CampaignCounterFontSize; 
            set => config.CampaignCounterFontSize = value;
        }

        private void Parse(Transform parent)
        {
            if (!parsed)
            {
                VersionUtils.Parse(ResourcePaths.ACC_SABER_CAMPAIGN_COUNTER_SETTINGS_MODAL, parent, this);

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
