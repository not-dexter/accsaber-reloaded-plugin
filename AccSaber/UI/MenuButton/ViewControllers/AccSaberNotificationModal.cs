using AccSaber.Consts;
using AccSaber.Utils;
using AccsaberLeaderboard.UI.BSML_Addons.Components;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Parser;
using HMUI;
using System;
using System.ComponentModel;
using System.Reflection;
using UnityEngine;
using Zenject;
using static AccSaber.UI.MenuButton.ViewControllers.AccSaberMissionScreen;
using static AccSaber.UI.MenuButton.ViewControllers.AccSaberMenuViewController;

namespace AccSaber.UI.MenuButton.ViewControllers
{
    internal sealed class AccSaberNotificationModal : INotifyPropertyChanged, IDisposable, IInitializable
    {
        private bool _parsed;
        private object _parentObject = null!;
        private ICellDataSource _data = null!;
        private AccSaberMainFlowCoordinator _parentFlowCoordinator = null!;
        private string _targetPrompt = null!;
        public event PropertyChangedEventHandler? PropertyChanged;

        [UIComponent("modal")]
        private ModalView _modalView = null!;

        [UIParams]
        private readonly BSMLParserParams _parserParams = null!;

        [UIValue("TargetPrompt")]
        private string TargetPrompt
        {
            get => _targetPrompt;
            set
            {
                _targetPrompt = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TargetPrompt)));
            }
        }

        private void Parse(Transform parentTransform)
        {
            if (!_parsed)
            {
                VersionUtils.BSMLParser_Instance.Parse(Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), ResourcePaths.ACC_SABER_NOTIF_MODAL), parentTransform.gameObject, this);
                _modalView.name = "AccSaberNotificationModal";

                _parsed = true;
            }

            _modalView.transform.SetParent(parentTransform.transform);
            Accessors.ViewValidAccessor(ref _modalView) = false;
        }
            
        public void Initialize()
        {
            _modalView.blockerClickedEvent += HideModal;
        }

        public void ShowModal(Transform parentTransform, object instance, ICellDataSource data, AccSaberMainFlowCoordinator parentFlowCoordinator)
        {
            Parse(parentTransform);

            _parentObject = instance;
            _data = data;
            _parentFlowCoordinator = parentFlowCoordinator;

            if (_data is MissionCell mission && _parentObject is AccSaberMissionScreen)
            {
                switch (mission.Data.Type)
                {
                    case >= MissionType.ACC_ON_MAP and <= MissionType.STREAK_ON_MAP or MissionType.COMEBACK_PB:
                        TargetPrompt = "Would you like to go to this map?";
                        break;
                    case MissionType.PLAY_N_MAPS or MissionType.SCORES_N or MissionType.STREAK_N_IN_CATEGORY or MissionType.PB_ABOVE_THRESHOLD or MissionType.XP_IN_WINDOW:
                        TargetPrompt = "Would you like to go to this Playlist?";
                        break;
                    default: return;
                }
            }
            else if(_data is ScoreCell && _parentObject is AccSaberMenuViewController)
                TargetPrompt = "Would you like to go to this map?";

            _parserParams.EmitEvent("close-modal");
            _parserParams.EmitEvent("open-modal");
        }

        [UIAction("ClickedYes")]
        public void ClickedYes()
        {
            if (_parentObject == null || _data == null || _parentFlowCoordinator == null)
                return;

            _modalView.Hide(false);
            if (_parentObject is AccSaberMissionScreen _missionScreen)
            {
                if (_data is not MissionCell mission)
                    return;

                _missionScreen.GoToMission(mission);
            }
            else if(_parentObject is AccSaberMenuViewController _menuViewController)
            {
                if (_data is not ScoreCell cell)
                    return;

                _ = _menuViewController.levelUtils.GoToSong(cell.Data.DifficultyId, null, () => _parentFlowCoordinator.CloseToMainMenu(), cell.UpdateStatus);
            }
        }

        [UIAction("ClickedNo")]
        public void ClickedNo()
        {
            HideModal();
        }


        public void HideModal()
        {
            if (!_parsed)
            {
                return;
            }
            
            _parserParams.EmitEvent("close-modal");
        }
        public void Dispose()
        {
            _modalView.blockerClickedEvent -= HideModal;
        }
    }
}