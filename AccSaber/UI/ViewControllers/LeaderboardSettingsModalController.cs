using AccSaber.Configuration;
using AccSaber.Consts;
using AccSaber.Utils;
using AccSaber.Utils.Misc;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Parser;
using HMUI;
using System;
using System.ComponentModel;
using System.Reflection;
using UnityEngine;
using Zenject;

namespace AccSaber.UI.ViewControllers
{
    internal sealed class LeaderboardSettingsModalController : IInitializable, IDisposable
    {
        private bool _parsed;
        
        [UIComponent("modal")]
        private ModalView _modalView = null!;

        [UIParams]
        private readonly BSMLParserParams _parserParams = null!;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action? OnCombineRelations;

        [Inject] private readonly PluginConfig PC = null!;
        private void Parse(Transform parentTransform)
        {
            if (!_parsed)
            {
                VersionUtils.Parse(ResourcePaths.LEADERBOARD_SETTINGS_MODAL, parentTransform.gameObject, this);
                _modalView.name = "LeaderboardSettingsModalController";
                
                _parsed = true;
            }
			
            _modalView.transform.SetParent(parentTransform.transform);
            Accessors.ViewValidAccessor(ref _modalView) = false;
        }

        [UIValue("ShowCombo")]
        public bool ShowCombo
        {
            get => PC.ShowCombo; set => PC.ShowCombo = value;
        }
        [UIValue("ShowStreak")]
        public bool ShowStreak
        {
            get => PC.ShowStreak; set => PC.ShowStreak = value;
        }

        [UIValue("CombineRelations")]
        public bool CombineRelations
        {
            get => PC.CombineRelations;
            set
            {
                PC.CombineRelations = value;
                OnCombineRelations?.Invoke();
            }
        }

        [UIValue("AccDecimals")]
        public int AccDecimals
        {
            get => PC.AccDecimals; set => PC.AccDecimals = value;
        }
        [UIValue("TimePlaces")]
        public int TimePlaces
        {
            get => PC.TimePlaces; set => PC.TimePlaces = value;
        }


        [UIValue("DisablePopups")]
        public bool DisablePopups
        {
            get => PC.DisablePopups; set => PC.DisablePopups = value;
        }

        [UIValue("DisableIncompleteSubmissions")]
        public bool DisableIncompleteSubmissions
        {
            get => !PC.SubmitOnIncompletePlay; set => PC.SubmitOnIncompletePlay = !value;
        }
        public void ShowModal(Transform parentTransform)
        {
            Parse(parentTransform);
            
            _parserParams.EmitEvent("close-modal");
            _parserParams.EmitEvent("open-modal");

            PropertyChanged?.Invoke(this, new(nameof(DisablePopups)));
        }

        public void HideModal()
        {
            if (!_parsed)
            {
                return;
            }
			
            _parserParams.EmitEvent("close-modal");
        }

        public void Initialize()
        {
            PC.PropertyChanged += PluginConfigUpdated;
        }
        public void Dispose()
        {
            PC.PropertyChanged -= PluginConfigUpdated;
        }

        private void PluginConfigUpdated(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName is nameof(PluginConfig.DisablePopups)){
                PropertyChanged?.Invoke(this, new(nameof(DisablePopups)));
                Plugin.Log.Info("this works");
            }
        }
    }
}