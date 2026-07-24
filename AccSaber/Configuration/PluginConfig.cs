using System.ComponentModel;
using System.Runtime.CompilerServices;
using IPA.Config.Stores;
using UnityEngine;

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
        public virtual bool ShowPrereqIndicator { get; set; } = true;

        public virtual float CampaignImageBackgroundBrightness { get; set; } = 0.5f;
        public virtual float CampaignImageBackgroundAlpha { get; set; } = 0.8f;
        public virtual float CampaignColorBackgroundBrightness { get; set; } = 0f;
        public virtual float CampaignColorBackgroundAlpha { get; set; } = 0.5f;
        public virtual int CampaignMaxCoverageLoadsPerFrame { get; set; } = 10000;
        public virtual int CampaignMaxObjectLoadsPerFrame { get; set; } = 10;

        public virtual float CampaignDefaultZoomValue { get; set; } = 0.2f;
        public virtual float CampaignZoomIncrementValue { get; set; } = 0.025f;
        public virtual float CampaignMinZoomValue { get; set; } = 0.025f;
        public virtual float CampaignMaxZoomValue { get; set; } = 0.75f;

        public virtual float CampaignCounterFontSize { get; set; } = 2f;
        public virtual bool CampaignCounterGoalColors { get; set; } = true;
        public virtual float CampaignCounterCheckmarkScale { get; set; } = 6f;

        public virtual Color CampaignCounterNeutralColor { get; set; } = Color.white;
        public virtual Color CampaignCounterGoodColor { get; set; } = new(0f, 1f, 0f);
        public virtual Color CampaignCounterBadColor { get; set; } = Color.red;
        public virtual Color CampaignCounterCheckmarkGoodColor { get; set; } = Color.white;
        public virtual Color CampaignCounterCheckmarkBadColor { get; set; } = new(0.5f, 0.5f, 0.5f, 0.5f);
    }
}