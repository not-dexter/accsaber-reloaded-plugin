using AccSaber.Configuration;
using AccSaber.Consts;
using AccSaber.Utils;
using AccSaber.Utils.Misc;
using BeatSaberMarkupLanguage.Attributes;
using UnityEngine;
using Zenject;

namespace AccSaber.UI.MenuButton.Campaigns.ViewControllers
{
    internal class AccSaberCampaignZoomModalController : BasicModalController
    {
        [Inject] private readonly PluginConfig config = null!;

        [UIValue("DefaultZoom")]
        private float DefaultZoom
        {
            get => config.CampaignDefaultZoomValue;
            set => config.CampaignDefaultZoomValue = value;
        }

        [UIValue("ZoomIncrement")]
        private float ZoomIncrement
        {
            get => config.CampaignZoomIncrementValue;
            set => config.CampaignZoomIncrementValue = value;
        }

        [UIValue("MinZoom")]
        private float MinZoom
        {
            get => config.CampaignMinZoomValue;
            set
            {
                config.CampaignMinZoomValue = value;

                if (value > config.CampaignMaxZoomValue)
                    config.CampaignMaxZoomValue = value;
            }
        }

        [UIValue("MaxZoom")]
        private float MaxZoom
        {
            get => config.CampaignMaxZoomValue;
            set
            {
                config.CampaignMaxZoomValue = value;

                if (value < config.CampaignMinZoomValue)
                    config.CampaignMinZoomValue = value;
            }
        }

        protected override void FirstParse(Transform parent) => 
            VersionUtils.Parse(ResourcePaths.ACC_SABER_CAMPAIGN_ZOOM_MODAL, parent, this);
    }
}
