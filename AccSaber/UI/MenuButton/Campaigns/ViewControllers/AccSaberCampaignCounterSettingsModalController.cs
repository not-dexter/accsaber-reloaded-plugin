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
    internal class AccSaberCampaignCounterSettingsModalController : BasicModalController
    {
        [Inject] private readonly PluginConfig config = null!;

        [UIComponent("colorModal")]
        private readonly ModalView ColorModal = null!;


        [UIValue("FontSize")]
        private float FontSize
        {
            get => config.CampaignCounterFontSize; 
            set => config.CampaignCounterFontSize = value;
        }

        [UIValue("GoalColors")]
        private bool GoalColors
        {
            get => config.CampaignCounterGoalColors; 
            set => config.CampaignCounterGoalColors = value;
        }

        [UIValue("CheckmarkScale")]
        private float CheckmarkScale
        {
            get => config.CampaignCounterCheckmarkScale;
            set => config.CampaignCounterCheckmarkScale = value;
        }

        [UIValue("NeutralColor")]
        private Color NeutralColor
        {
            get => config.CampaignCounterNeutralColor; 
            set => config.CampaignCounterNeutralColor = value;
        }

        [UIValue("NeutralAlpha")]
        private int NeutralAlpha
        {
            get => Mathf.RoundToInt(NeutralColor.a * 255f);
            set => NeutralColor = NeutralColor.ColorWithAlpha(value / 255f);
        }

        [UIValue("GoodColor")]
        private Color GoodColor
        {
            get => config.CampaignCounterGoodColor; 
            set => config.CampaignCounterGoodColor = value;
        }

        [UIValue("GoodAlpha")]
        private int GoodAlpha
        {
            get => Mathf.RoundToInt(GoodColor.a * 255f);
            set => GoodColor = GoodColor.ColorWithAlpha(value / 255f);
        }

        [UIValue("BadColor")]
        private Color BadColor
        {
            get => config.CampaignCounterBadColor; 
            set => config.CampaignCounterBadColor = value;
        }

        [UIValue("BadAlpha")]
        private int BadAlpha
        {
            get => Mathf.RoundToInt(BadColor.a * 255f);
            set => BadColor = BadColor.ColorWithAlpha(value / 255f);
        }

        [UIValue("CheckmarkGoodColor")]
        private Color CheckmarkGoodColor
        {
            get => config.CampaignCounterCheckmarkGoodColor; 
            set => config.CampaignCounterCheckmarkGoodColor = value;
        }

        [UIValue("CheckmarkGoodAlpha")]
        private int CheckmarkGoodAlpha
        {
            get => Mathf.RoundToInt(CheckmarkGoodColor.a * 255f);
            set => CheckmarkGoodColor = CheckmarkGoodColor.ColorWithAlpha(value / 255f);
        }

        [UIValue("CheckmarkBadColor")]
        private Color CheckmarkBadColor
        {
            get => config.CampaignCounterCheckmarkBadColor; 
            set => config.CampaignCounterCheckmarkBadColor = value;
        }

        [UIValue("CheckmarkBadAlpha")]
        private int CheckmarkBadAlpha
        {
            get => Mathf.RoundToInt(CheckmarkBadColor.a * 255f);
            set => CheckmarkBadColor = CheckmarkBadColor.ColorWithAlpha(value / 255f);
        }

        protected override void FirstParse(Transform parent) => 
            VersionUtils.Parse(ResourcePaths.ACC_SABER_CAMPAIGN_COUNTER_SETTINGS_MODAL, parent, this);

        protected override void Parse(Transform parent)
        {
            base.Parse(parent);

            ColorModal.transform.SetParent(Modal.transform);
            ColorModal.AttachTo(Modal);
        }
    }
}
