using AccSaber.Configuration;
using AccSaber.Consts;
using AccSaber.Utils;
using BeatSaberMarkupLanguage.Attributes;
using UnityEngine;
using Zenject;

namespace AccSaber.UI.MenuButton.Campaigns.ViewControllers
{
    internal class AccSaberCampaignSettingsModalController : Utils.Misc.BasicModalController
    {
        [Inject] private readonly PluginConfig config = null!;

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

        [UIValue(nameof(ShowTerminalIndicator))]
        private bool ShowTerminalIndicator
        {
            get => config.ShowTerminalIndicator;
            set => config.ShowTerminalIndicator = value;
        }

        [UIValue(nameof(ShowPrereqIndicator))]
        private bool ShowPrereqIndicator
        {
            get => config.ShowPrereqIndicator;
            set => config.ShowPrereqIndicator = value;
        }

        [UIValue(nameof(CampaignBackButton))]
        private bool CampaignBackButton
        {
            get => config.CampaignBackButton;
            set => config.CampaignBackButton = value;
        }

        [UIValue(nameof(ImgBGBrightness))]
        private float ImgBGBrightness
        {
            get => config.CampaignImageBackgroundBrightness;
            set => config.CampaignImageBackgroundBrightness = value;
        }

        [UIValue(nameof(ImgBGAlpha))]
        private float ImgBGAlpha
        {
            get => config.CampaignImageBackgroundAlpha;
            set => config.CampaignImageBackgroundAlpha = value;
        }

        [UIValue(nameof(ColorBGBrightness))]
        private float ColorBGBrightness
        {
            get => config.CampaignColorBackgroundBrightness;
            set => config.CampaignColorBackgroundBrightness = value;
        }

        [UIValue(nameof(ColorBGAlpha))]
        private float ColorBGAlpha
        {
            get => config.CampaignColorBackgroundAlpha;
            set => config.CampaignColorBackgroundAlpha = value;
        }

        [UIValue(nameof(PixelsPerFrame))]
        private int PixelsPerFrame
        {
            get => config.CampaignMaxCoverageLoadsPerFrame;
            set => config.CampaignMaxCoverageLoadsPerFrame = value;
        }

        [UIValue(nameof(ObjectsPerFrame))]
        private int ObjectsPerFrame
        {
            get => config.CampaignMaxObjectLoadsPerFrame;
            set => config.CampaignMaxObjectLoadsPerFrame = value;
        }

        protected override void FirstParse(Transform parent) => 
            VersionUtils.Parse(ResourcePaths.ACC_SABER_CAMPAIGN_SETTINGS_MODAL, parent, this);
    }
}
