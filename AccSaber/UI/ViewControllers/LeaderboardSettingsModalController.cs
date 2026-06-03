using AccSaber.Configuration;
using AccSaber.Consts;
using AccSaber.Utils;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.Parser;
using HMUI;
using System;
using System.Reflection;
using UnityEngine;
using Zenject;

namespace AccSaber.UI.ViewControllers
{
    internal sealed class LeaderboardSettingsModalController
    {
        private bool _parsed;
        
        [UIComponent("modal")]
        private ModalView _modalView = null!;

        [UIParams]
        private readonly BSMLParserParams _parserParams = null!;

        public event Action? OnCombineRelations;
        public event Action? OnSettingUpdated;

        [Inject] private readonly PluginConfig PC = null!;
        private void Parse(Transform parentTransform)
        {
            if (!_parsed)
            {
                VersionUtils.BSMLParser_Instance.Parse(Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), ResourcePaths.LEADERBOARD_SETTINGS_MODAL), parentTransform.gameObject, this);
                _modalView.name = "LeaderboardSettingsModalController";
                
                _parsed = true;
            }
			
            _modalView.transform.SetParent(parentTransform.transform);
            Accessors.ViewValidAccessor(ref _modalView) = false;
        }

        [UIValue("ShowCombo")]
        public bool ShowCombo
        {
            get => PC.ShowCombo;
            set
            {
                PC.ShowCombo = value;
                OnSettingUpdated!.Invoke();
            }
        }

        [UIValue("CombineRelations")]
        public bool CombineRelations
        {
            get => PC.CombineRelations;
            set
            {
                OnCombineRelations!.Invoke();
            }
        }

        [UIValue("AccDecimals")]
        public int AccDecimals
        {
            get => PC.AccDecimals;
            set
            {
                PC.AccDecimals = value;
                OnSettingUpdated!.Invoke();
            }
        }
        [UIValue("TimePlaces")]
        public int TimePlaces
        {
            get => PC.TimePlaces;
            set
            {
                PC.TimePlaces = value;
                OnSettingUpdated!.Invoke();
            }
        }
        public void ShowModal(Transform parentTransform)
        {
            Parse(parentTransform);
            
            _parserParams.EmitEvent("close-modal");
            _parserParams.EmitEvent("open-modal");
        }

        public void HideModal()
        {
            if (!_parsed)
            {
                return;
            }
			
            _parserParams.EmitEvent("close-modal");
        }
    }
}