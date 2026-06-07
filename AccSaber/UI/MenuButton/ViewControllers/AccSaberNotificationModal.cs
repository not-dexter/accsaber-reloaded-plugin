using AccSaber.Consts;
using AccSaber.Utils;
using AccsaberLeaderboard.UI.BSML_Addons.Components;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Parser;
using HMUI;
using System.ComponentModel;
using System.Reflection;
using UnityEngine;
using static AccSaber.UI.MenuButton.ViewControllers.AccSaberMissionScreen;

namespace AccSaber.UI.MenuButton.ViewControllers
{
    internal sealed class AccSaberNotificationModal : INotifyPropertyChanged
    {
        private bool _parsed;
        private AccSaberMissionScreen _missionScreen = null!;
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
            

        public void ShowModal(Transform parentTransform, AccSaberMissionScreen instance, ICellDataSource data, AccSaberMainFlowCoordinator parentFlowCoordinator)
        {
            Parse(parentTransform);

            _missionScreen = instance;
            _data = data;
            _parentFlowCoordinator = parentFlowCoordinator;

            if (_data is not MissionCell cell)
                return;

            switch (cell.Data.Type)
            {
                case >= MissionType.ACC_ON_MAP and <= MissionType.STREAK_ON_MAP or MissionType.COMEBACK_PB:
                    TargetPrompt = "Would you like to go to this map?";
                    break;
                case MissionType.PLAY_N_MAPS or MissionType.SCORES_N or MissionType.STREAK_N_IN_CATEGORY or MissionType.PB_ABOVE_THRESHOLD or MissionType.XP_IN_WINDOW:
                    TargetPrompt = "Would you like to go to this Playlist?";
                    break;
                default: return;
            }

            _parserParams.EmitEvent("close-modal");
            _parserParams.EmitEvent("open-modal");
        }

        [UIAction("ClickedYes")]
        public async void ClickedYes()
        {
            if (_missionScreen == null || _data == null || _parentFlowCoordinator == null)
                return;

            if (_data is not MissionCell cell)
                return;

            _modalView.Hide(false);

            _missionScreen.GoToMission(cell);
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
    }
}