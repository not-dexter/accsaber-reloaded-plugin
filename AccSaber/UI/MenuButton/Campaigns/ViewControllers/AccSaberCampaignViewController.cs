using AccSaber.Managers;
using AccSaber.Models;
using AccSaber.Utils;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using HMUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Zenject;
using AccSaber.Consts;



#if NEW_VERSION
using BeatSaberMarkupLanguage;
#endif

namespace AccSaber.UI.MenuButton.Campaigns.ViewControllers
{
    [ViewDefinition("AccSaber.UI.MenuButton.Campaigns.Views.AccSaberCampaignView.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\AccSaberCampaignView.bsml")]
    internal class AccSaberCampaignViewController : Utils.Safety.BSMLSafeAutomaticViewController
    {
#pragma warning disable CS0414 // Field assigned to but never read.
        private bool _parsed = false;
        private CategoryTab _currentTab;
        private bool _isLoading;
        private bool _inCampaign;
        private bool _inMap;
        private string _campaignTitle = null!;
        private string _campaignDescription = null!;
        private string _campaignCreator = null!;
        private string _missionSongName = null!;
        private string _missionSongAuthor = null!;
        private string _missionObjective = null!;
        private AccSaberCampaign _currentCampaign = null!;
        private List<AccSaberCampaign> _activeCampaigns = null!;
        public BeatmapKey _curBeatMapKey { get; set; }

        public BeatmapLevel _curBeatMapLevel = null!;

        [UIObject("CampaignMapContainer")]
        private readonly GameObject _campaignMapContainer = null!;

        [UIComponent("CampaignImage")]
        private readonly ImageView _campaignImage = null!;

        [UIComponent("MissionImage")]
        private readonly ImageView _missionImage = null!;

        [UIComponent("campaign-list")]
        private readonly CustomCellListTableData _campaignList = null!;

        [UIValue("campaign-cells")]
        private readonly List<object> _campaignCells = [];

        [UIComponent("diff-list")]
        private readonly CustomCellListTableData _diffList = null!;

        [UIValue("diff-cells")]
        private readonly List<object> _diffCells = [];

        private enum CategoryTab
        {
            Active,
            Curated,
            All
        }

        [Inject] private readonly AccSaberStore _accSaberStore = null!;
        [Inject] private readonly AccSaberCampaignFlow _campaignFlow = null!;
        [Inject] private readonly AccSaberCampaignMapViewController _campaignMapViewController = null!;
        [Inject] private readonly MenuTransitionsHelper _menuTransitionsHelper = null!;
        [Inject] private readonly PlayerDataModel _playerDataModel = null!;
        [Inject] private readonly EnvironmentsListModel _environmentsListModel = null!;
        private CategoryTab CurrentTab
        {
            get => _currentTab;
            set
            {
                _currentTab = value;
                _ = UpdateTabs();
            }
        }
        [UIValue("is-loading")]
        private bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                NotifyPropertyChanged(nameof(IsLoading));
                NotifyPropertyChanged(nameof(IsNotLoading));
            }
        }
        [UIValue("InCampaign")]
        private bool InCampaign
        {
            get => _inCampaign;
            set
            {
                _inCampaign = value;
                NotifyPropertyChanged(nameof(InCampaign));
                NotifyPropertyChanged(nameof(NotInCampaign));
            }
        }
        [UIValue("InMap")]
        public bool InMap
        {
            get => _inMap;
            set
            {
                _inMap = value;
                NotifyPropertyChanged(nameof(InMap));
            }
        }
        [UIValue("CampaignTitle")]
        private string CampaignTitle
        {
            get => _campaignTitle;
            set
            {
                _campaignTitle = value;
                NotifyPropertyChanged(nameof(CampaignTitle));
            }
        }

        [UIValue("MissionMapName")]
        private string MissionMapName
        {
            get => _missionSongName;
            set
            {
                _missionSongName = value;
                NotifyPropertyChanged(nameof(MissionMapName));
            }
        }
        [UIValue("MissionMapArtist")]
        private string MissionMapArtist
        {
            get => _missionSongAuthor;
            set
            {
                _missionSongAuthor = value;
                NotifyPropertyChanged(nameof(MissionMapArtist));
            }
        }
        [UIValue("MissionObjective")]
        private string MissionObjective
        {
            get => _missionObjective;
            set
            {
                _missionObjective = value;
                NotifyPropertyChanged(nameof(MissionObjective));
            }
        }

        [UIValue("CampaignDescription")]
        private string CampaignDescription
        {
            get => _campaignDescription;
            set
            {
                _campaignDescription = value;
                NotifyPropertyChanged(nameof(CampaignDescription));
            }
        }
        [UIValue("CampaignCreator")]
        private string CampaignCreator
        {
            get => _campaignCreator;
            set
            {
                _campaignCreator = value;
                NotifyPropertyChanged(nameof(CampaignCreator));
            }
        }
        [UIValue("NotInCampaign")]
        private bool NotInCampaign => !_inCampaign;

        [UIValue("is-not-loading")]
        private bool IsNotLoading => !_isLoading;

        [UIAction("#post-parse")]
        private async void Parsed()
        {
            if(!_parsed)
            {
                _parsed = true;
            }

            VersionUtils.Parse(ResourcePaths.ACC_SABER_CAMPAIGN_MAP_VIEW, _campaignMapContainer, _campaignMapViewController);

            _activeCampaigns = await _accSaberStore.GetActiveCampaigns();
            
            CurrentTab = 0;
            InCampaign = false;
            InMap = false;

            _ = UpdateTabs();
        }

        [UIAction("campaign-selected")]
        private void CampaignSelected(TableView table, CampaignCell cellObj)
        {
            if (cellObj != null)
                _currentCampaign = cellObj.Data;

            table.ClearSelection();

            _ = UpdateCampaign(_currentCampaign);
        }

        [UIAction("BackPressed")]
        private void BackPressed()
        {
            _campaignFlow.HideLeaderboard();
            InCampaign = false;
            InMap = false;
            _ = UpdateTabs();
            _diffCells.Clear();
            _diffList.Data().Clear();
            _diffList.TableView().ReloadData();
        }

        [UIAction("PlayCampaign")]
        private async void PlayCampaign()
        {
            InCampaign = true;
            if (_currentCampaign is not null)
            {
                if (!_activeCampaigns.Contains(_currentCampaign) && _currentCampaign.ProgressStatus != "IN_PROGRESS")
                {
                    if (false && await _accSaberStore.StartCampaign(_currentCampaign.Id) == false)
                        Plugin.Log.Error("Failed to start campaign!");
                    else
                        _activeCampaigns.Add(await _accSaberStore.GetCampaign(_currentCampaign.Id));
                }

                _currentCampaign = await _accSaberStore.GetCampaign(_currentCampaign.Id);

                _campaignMapViewController.SetCampaign(_currentCampaign);

                _ = SetMaps(_currentCampaign);
            }
        }

        [UIAction("PlayMission")]
        private void PlayMission()
        {
            _menuTransitionsHelper.StartStandardLevel(
                gameMode: "Solo",
                beatmapKey: _curBeatMapKey,
                beatmapLevel: _curBeatMapLevel,
                overrideEnvironmentSettings: _playerDataModel.playerData.overrideEnvironmentSettings,
                overrideColorScheme: _playerDataModel.playerData.colorSchemesSettings.GetOverrideColorScheme(),
                beatmapOverrideColorScheme: _curBeatMapLevel.GetColorScheme(_curBeatMapKey.beatmapCharacteristic, _curBeatMapKey.difficulty),
                gameplayModifiers: _playerDataModel.playerData.gameplayModifiers,
                playerSpecificSettings: _playerDataModel.playerData.playerSpecificSettings,
                practiceSettings: null,
                environmentsListModel: _environmentsListModel,
                backButtonText: "buh",
                useTestNoteCutSoundEffects: false,
                startPaused: false,
                beforeSceneSwitchToGameplayCallback: null,
                afterSceneSwitchToGameplayCallback: null,
                levelFinishedCallback: LevelFinished,
                levelRestartedCallback: null
            );
        }

        private void LevelFinished(StandardLevelScenesTransitionSetupDataSO transition, LevelCompletionResults results)
        {
        }
#pragma warning disable IDE0060
        [UIAction("tab-selected")]
        private void CategoryTabSelected(SegmentedControl segmentedControl, int index)
        {
            CurrentTab = (CategoryTab)index;
        }
#pragma warning restore IDE0060
        public async Task UpdateTabs()
        {
            _campaignCells.Clear();
            _campaignList.Data().Clear();
            _campaignList.TableView().ReloadData();

            List<AccSaberCampaign> tabCampaigns = CurrentTab switch
            {
                CategoryTab.Active => _activeCampaigns,
                CategoryTab.Curated => await _accSaberStore.GetCampaigns("CURATED"),
                CategoryTab.All => await _accSaberStore.GetCampaigns("PUBLISHED"),
                _ => throw new NotImplementedException(),
            };

            foreach (var campaign in tabCampaigns)
            {
                if (CurrentTab == CategoryTab.Active && campaign.ProgressStatus != "IN_PROGRESS")
                    continue;

                _campaignCells.Add(new CampaignCell(campaign));
            }

            IEnumerator WaitThenUpdate()
            {
                yield return new WaitForEndOfFrame();

                _campaignList.TableView().ReloadData();
                IsLoading = false;
            }
            StartCoroutine(WaitThenUpdate());
        }

        public async Task UpdateCampaign(AccSaberCampaign campaign)
        {
            CampaignTitle = campaign.Name;
            CampaignCreator = campaign.CreatorName;
            CampaignDescription = campaign.Description;

            if ((campaign.IconUrl is not null &&campaign.IconUrl.Contains(".webp")) || campaign.IconUrl is null)
                await _campaignImage.SetImageAsync("AccSaber.Resources.AccSaber.png", false);
            else
                await _campaignImage.SetImageAsync(campaign.IconUrl, false);

        }

        public void SetMission(AccSaberCampaignMap map, BeatmapKey beatmapkey, BeatmapLevel beatmapLevel)
        {
            _curBeatMapKey = beatmapkey;
            _curBeatMapLevel = beatmapLevel;
            MissionMapName = map.SongName;
            MissionMapArtist = $"{map.SongAuthor} [<color=#c0548f>{map.MapAuthor}</color>]";

            string objective = map.RequirementType switch
            {
                "ACC" => $"Set at least <color={ColorUtils.OVERALL}>{map.RequirementValue * 100:N2}%</color> accuracy",
                "AP" => $"Set a score worth <color={ColorUtils.OVERALL}>{map.RequirementValue:N0} AP</color> play",
                "RANK" => $"Get rank <color={ColorUtils.OVERALL}>#{map.RequirementValue:N0}</color> or better on the map",
                "STREAK_115" => $"Get <color={ColorUtils.OVERALL}>{map.RequirementValue:N0}</color> 115s in a row",
                "SCORE" => $"Set a score of <color={ColorUtils.OVERALL}>{map.RequirementValue:N0}</color> points or higher",
                "FC" => $"Set a Full Combo",
                _ => $"Get something with a requirement value of {map.RequirementValue:N0}"
            };

            MissionObjective = objective;
            _ = _missionImage.SetImageAsync(map.CoverUrl);
            InMap = true;
        }

        public async Task SetMaps(AccSaberCampaign campaign)
        {
            _diffCells.Clear();
            _diffList.Data().Clear(); 

            foreach (var diff in campaign.Difficulties!)
            {
                _diffCells.Add(new CampaignMap(diff));
            }
            IEnumerator WaitThenUpdate()
            {
                yield return new WaitForEndOfFrame();

                _diffList.TableView().ReloadData();
                IsLoading = false;
            }
            StartCoroutine(WaitThenUpdate());
        }

        internal class CampaignCell(AccSaberCampaign campaign) : Utils.Safety.SafeNotifyPropertyChanged
        {
            public readonly AccSaberCampaign Data = campaign;

            [UIValue(nameof(Name))] private string Name => Data.Name;
            [UIValue(nameof(Author))] private string Author => Data.CreatorName;
            [UIValue(nameof(MapCount))] private int MapCount => Data.DifficultyCount!.Value;

        }

        internal class CampaignMap(AccSaberCampaignMap map) : Utils.Safety.SafeNotifyPropertyChanged
        {
            [UIValue(nameof(Name))] private string Name => map.SongName;
            [UIValue(nameof(Author))] private string Author => map.SongAuthor;
            [UIValue(nameof(MapCount))] private string MapCount => map.Difficulty;

        }


    }
}
