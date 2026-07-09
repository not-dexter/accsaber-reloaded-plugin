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
using AccSaber.Utils.Misc;
using static AccSaber.Managers.CampaignProgress;
using UnityEngine.UI;
using System.Reflection;
using IPA.Loader;








#if NEW_VERSION
using BeatSaberMarkupLanguage;
#endif

namespace AccSaber.UI.MenuButton.Campaigns.ViewControllers
{
    [ViewDefinition("AccSaber.UI.MenuButton.Campaigns.Views.AccSaberCampaignView.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\AccSaberCampaignView.bsml")]
    internal class AccSaberCampaignViewController : Utils.Safety.BSMLSafeAutomaticViewController, IInitializable
    {
        private static MethodInfo? RecordPlayMethod;

#pragma warning disable CS0414 // Field assigned to but never read.
        private bool _parsed = false;
        private CategoryTab _currentTab;
        private bool _isLoading;
        private bool _inCampaign;
        private bool _missionHasRewards;
        private bool _missionLocked;
        private bool _inMap;
        private bool _invalidateActive;
        private string _campaignTitle = null!; 
        private string _campaignCategory = null!;
        private string _campaignDescription = null!;
        private string _campaignCreator = null!;
        private string _missionSongName = null!;
        private string _missionSongAuthor = null!;
        private string _missionRewards = null!;
        private string _missionSongNoteCount = null!;
        private string _missionSongNPS = null!;
        private string _missionSongNJS = null!;
        private string _missionSongDuration = null!;
        private string _missionObjective = null!;
        private AccSaberCampaign? _currentCampaign;
        private List<AccSaberCampaign> _activeCampaigns = null!;
        private readonly List<CampaignMap> _diffCells = [];

        private CampaignProgressValue CampaignProgressVal
        {
            get;
            set
            {
                field = value;
                MissionComplete = value.Completion == CompletionStatus.Complete;
            }
        }

#if NEW_VERSION
        public BeatmapKey CurrentBeatMapKey { get; set; }
        public BeatmapLevel? CurrentBeatMapLevel { get; set; }
#else
        public IDifficultyBeatmap? CurrentBeatMapLevel { get; set; }
#endif
        public AccSaberCampaignMap? CurrentMap;
        public bool MapStarted { get; private set; } = false;

        [UIObject("CampaignMapContainer")]
        private readonly GameObject _campaignMapContainer = null!;


        [UIComponent("CampaignImage")]
        private readonly ImageView _campaignImage = null!;

        [UIComponent("MissionImage")]
        private readonly ImageView _missionImage = null!;

        [UIComponent("MissionButton")]
        private readonly Button _missionButton = null!;

        [UIComponent("campaign-list")]
        private readonly CustomCellListTableData _campaignList = null!;

        [UIValue("campaign-cells")]
        private readonly List<object> _campaignCells = [];

        private enum CategoryTab
        {
            Active,
            Curated,
            All
        }

        [Inject] private readonly AccSaberStore _accSaberStore = null!;
        [Inject] private readonly SerializationHandler _serialHandler = null!;
        [Inject] private readonly Utils.Safety.MainThreadDispatcher _threadDispatcher = null!;
        [Inject] private readonly AccSaberCampaignFlow _campaignFlow = null!;
        [Inject] private readonly AccSaberCampaignMapViewController _campaignMapViewController = null!;
        [Inject] private readonly MenuTransitionsHelper _menuTransitionsHelper = null!;
        [Inject] private readonly PlayerDataModel _playerDataModel = null!;
        [Inject] private readonly BeatmapLevelsModel _beatmapLevelsModel = null!;
#if NEW_VERSION
        [Inject] private readonly BeatmapDataLoader _beatmapDataLoader = null!;
        [Inject] private readonly EnvironmentsListModel _environmentsListModel = null!;
#endif
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
        [UIValue("campaign-selected")]
        private bool CampaignSelected
        {
            get;
            set
            {
                if (value == field)
                    return;
                field = value;
                NotifyPropertyChanged();
            }
        } = false;

        [UIValue("in-campaign")]
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
        [UIValue("in-map")]
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
        [UIValue("CampaignCategory")]
        private string CampaignCategory
        {
            get => _campaignCategory;
            set
            {
                _campaignCategory = value;
                NotifyPropertyChanged(nameof(CampaignCategory));
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
        [UIValue("MissionMapNPS")]
        private string MissionMapNPS
        {
            get => _missionSongNPS;
            set
            {
                _missionSongNPS = value;
                NotifyPropertyChanged(nameof(MissionMapNPS));
            }
        }
        [UIValue("MissionMapNoteCount")]
        private string MissionMapNoteCount
        {
            get => _missionSongNoteCount;
            set
            {
                _missionSongNoteCount = value;
                NotifyPropertyChanged(nameof(MissionMapNoteCount));
            }
        }
        [UIValue("MissionMapNJS")]
        private string MissionMapNJS
        {
            get => _missionSongNJS;
            set
            {
                _missionSongNJS = value;
                NotifyPropertyChanged(nameof(MissionMapNJS));
            }
        }
        [UIValue("MissionMapDuration")]
        private string MissionMapDuration
        {
            get => _missionSongDuration;
            set
            {
                _missionSongDuration = value;
                NotifyPropertyChanged(nameof(MissionMapDuration));
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

        [UIValue("MissionComplete")]
        private bool MissionComplete
        {
            get;
            set
            {
                if (value != field)
                {
                    field = value;
                    NotifyPropertyChanged();
                }
            }
        }
        [UIValue("MissionHasRewards")]
        public bool MissionHasRewards
        {
            get => _missionHasRewards;
            set
            {
                _missionHasRewards = value;
                NotifyPropertyChanged(nameof(MissionHasRewards));
            }
        }
        [UIValue("MissionLocked")]
        public bool MissionLocked
        {
            get => _missionLocked;
            set
            {
                _missionLocked = value;
                NotifyPropertyChanged(nameof(MissionLocked));
            }
        }
        [UIValue("MissionRewards")]
        private string MissionRewards
        {
            get => _missionRewards;
            set
            {
                _missionRewards = value;
                NotifyPropertyChanged(nameof(MissionRewards));
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
        [UIValue("not-in-campaign")]
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
        private void OnCampaignSelected(TableView table, CampaignCell cellObj)
        {
            if (cellObj is not null)
                _currentCampaign = cellObj.Data;
            else
                return;

            CampaignSelected = true;

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
        }

        [UIAction("PlayCampaign")]
        private async void PlayCampaign()
        {
            InCampaign = true;
            if (_currentCampaign is not null)
            {
                if (!_activeCampaigns.Contains(_currentCampaign) && _currentCampaign.ProgressStatus != "IN_PROGRESS")
                    _missionButton.SetButtonText("Start Campaign");
                else
                    _missionButton.SetButtonText("Play");


                _currentCampaign = await _accSaberStore.GetCampaign(_currentCampaign.Id);

                _campaignMapViewController.SetCampaign(_currentCampaign);

                SetMaps(_currentCampaign);
            }
        }

        [UIAction("PlayMission")]
        private async void PlayMission()
        {
            if (CurrentBeatMapLevel is null)
            {
                Plugin.Log.Error("The current beat map is null when the play button is shown!!!");
                return;
            }

            if (_currentCampaign is not null)
            {
                if (!_activeCampaigns.Contains(_currentCampaign) && _currentCampaign.ProgressStatus != "IN_PROGRESS")
                {
                    if (await _accSaberStore.StartCampaign(_currentCampaign.Id) == false)
                        Plugin.Log.Error("Failed to start campaign!");
                    else
                    {
                        _invalidateActive = true;
                        _activeCampaigns.Add(_currentCampaign);
                        _missionButton.SetButtonText("Play");
                    }
                }
            }

            MapStarted = true;

            RecordPlayMethod?.Invoke(null, null);

#if V40
            _menuTransitionsHelper.StartStandardLevel(
                gameMode: "Solo",
                beatmapKey: CurrentBeatMapKey,
                beatmapLevel: CurrentBeatMapLevel,
                overrideEnvironmentSettings: _playerDataModel.playerData.overrideEnvironmentSettings,
                playerOverrideColorScheme: _playerDataModel.playerData.colorSchemesSettings.GetOverrideColorScheme(),
                playerOverrideLightshowColors: _playerDataModel.playerData.colorSchemesSettings.ShouldOverrideLightshowColors(),
                beatmapOverrideColorScheme: CurrentBeatMapLevel.GetColorScheme(CurrentBeatMapKey.beatmapCharacteristic, CurrentBeatMapKey.difficulty),
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
#elif NEW_VERSION
            _menuTransitionsHelper.StartStandardLevel(
                gameMode: "Solo",
                beatmapKey: CurrentBeatMapKey,
                beatmapLevel: CurrentBeatMapLevel,
                overrideEnvironmentSettings: _playerDataModel.playerData.overrideEnvironmentSettings,
                overrideColorScheme: _playerDataModel.playerData.colorSchemesSettings.GetOverrideColorScheme(),
                beatmapOverrideColorScheme: CurrentBeatMapLevel.GetColorScheme(CurrentBeatMapKey.beatmapCharacteristic, CurrentBeatMapKey.difficulty),
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
#else
            _menuTransitionsHelper.StartStandardLevel(
                gameMode: "Solo",
                difficultyBeatmap: CurrentBeatMapLevel,
                previewBeatmapLevel: CurrentBeatMapLevel.level,
                overrideEnvironmentSettings: _playerDataModel.playerData.overrideEnvironmentSettings,
                overrideColorScheme: _playerDataModel.playerData.colorSchemesSettings.GetOverrideColorScheme(),
                gameplayModifiers: _playerDataModel.playerData.gameplayModifiers,
                playerSpecificSettings: _playerDataModel.playerData.playerSpecificSettings,
                practiceSettings: null,
                backButtonText: "buh",
                useTestNoteCutSoundEffects: false,
                startPaused: false,
                beforeSceneSwitchCallback: null,
                levelFinishedCallback: LevelFinished,
                levelRestartedCallback: null
            );
#endif
        }

        private void LevelFinished(StandardLevelScenesTransitionSetupDataSO transition, LevelCompletionResults results)
        {
            MapStarted = false;

            if (_currentCampaign is not null)
                _threadDispatcher.EnqueueAction(async () =>
                {
                    _currentCampaign = await _accSaberStore.GetCampaign(_currentCampaign.Id);

                    _campaignMapViewController.SetCampaign(_currentCampaign);

                    SetMaps(_currentCampaign);
                });
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

            if (_invalidateActive)
            {
                _activeCampaigns = await _accSaberStore.GetActiveCampaigns();
                _invalidateActive = false;
            }

            List<AccSaberCampaign> tabCampaigns = CurrentTab switch
            {
                CategoryTab.Active => _activeCampaigns,
                CategoryTab.Curated => await _accSaberStore.GetCampaigns("CURATED"),
                CategoryTab.All => await _accSaberStore.GetCampaigns(),
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
            CampaignCategory = "";
            CampaignTitle = campaign.Name;
            CampaignCreator = campaign.CreatorName;
            CampaignDescription = campaign.Description;

            if (campaign.Tags is not null)
            {
                foreach (var tag in campaign.Tags)
                {
                    if (tag.Kind != CampaignTags.CampaignTagKind.CATEGORY)
                        continue;

                    CampaignCategory = CampaignCategory == "" ? $"<color={ColorUtils.GetColor(EnumUtils.ReloadedCategoryIdToCategoryNullable(tag.CategoryId))}>{tag.Name}</color>" :
                        CampaignCategory + $" | <color={ColorUtils.GetColor(EnumUtils.ReloadedCategoryIdToCategoryNullable(tag.CategoryId))}>{tag.Name}</color>";
                }

            }

            if ((campaign.IconUrl is not null &&campaign.IconUrl.Contains(".webp")) || campaign.IconUrl is null)
                await _campaignImage.SetImageAsync("AccSaber.Resources.AccSaber.png", false);
            else
                await _campaignImage.LoadImage(campaign.IconUrl);


        }

#if NEW_VERSION
        public async void SetMission(AccSaberCampaignMap map, BeatmapKey beatmapKey, BeatmapLevel beatmapLevel, CampaignProgress.CampaignProgressValue completion)
        {
            CurrentBeatMapKey = beatmapKey;
            CurrentBeatMapLevel = beatmapLevel;
#else
        public async void SetMission(AccSaberCampaignMap map, IDifficultyBeatmap beatmapLevel, CampaignProgress.CampaignProgressValue completion)
        {
            CurrentBeatMapLevel = beatmapLevel;
#endif            
            int noteCount = 0;
            float nps = 0;
            float njs = 0;

            try
            {
#if NEW_VERSION
                LoadBeatmapLevelDataResult mapInfo = await _beatmapLevelsModel.LoadBeatmapLevelDataAsync(beatmapLevel.levelID, BeatmapLevelDataVersion.Original, System.Threading.CancellationToken.None);

                BeatmapDataBasicInfo? mapData = await _beatmapDataLoader.LoadBasicBeatmapDataAsync(mapInfo.beatmapLevelData!, beatmapKey);
#else

                IBeatmapDataBasicInfo mapData = await beatmapLevel.GetBeatmapDataBasicInfoAsync();
#endif

                if (mapData is not null)
                {
                    noteCount = mapData.cuttableNotesCount;
#if NEW_VERSION
                    nps = mapData.cuttableNotesCount / beatmapLevel.songDuration;
                    njs = beatmapLevel.GetDifficultyBeatmapData(beatmapKey.beatmapCharacteristic, beatmapKey.difficulty).noteJumpMovementSpeed;
#else
                    nps = noteCount / beatmapLevel.level.songDuration;
                    njs = beatmapLevel.noteJumpMovementSpeed;
#endif
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Error(e);
            }

            if (map.XP > 0)
            {
                MissionHasRewards = true;
                MissionRewards = $"<color={ColorUtils.OVERALL}>+{map.XP:N0}XP</color>";
            }
            else
                MissionHasRewards = false;


            _missionButton.gameObject.SetActive(completion.Completion != CompletionStatus.Incomplete);
            MissionLocked = completion.Completion == CompletionStatus.Incomplete;
          

            MissionMapName = map.SongName;
            MissionMapArtist = $"{map.SongAuthor} [<color=#c0548f>{map.MapAuthor}</color>]";
            MissionMapNPS = $"{nps:N2}";
            MissionMapNoteCount = noteCount.ToString();
            MissionMapNJS = $"{njs:N1}";
#if NEW_VERSION
            TimeSpan Duration = TimeSpan.FromSeconds(beatmapLevel.songDuration);
#else
            TimeSpan Duration = TimeSpan.FromSeconds(beatmapLevel.level.songDuration);
#endif


            MissionMapDuration = string.Format("{0:D1}:{1:D2}", Duration.Minutes, Duration.Seconds);


            string objective = map.RequirementType switch
            {
                AccSaberCampaignMap.CampaignRequirementType.ACC => $"Set a score with at least <color={ColorUtils.RANK}>{map.RequirementValue * 100:N2}%</color> accuracy",
                AccSaberCampaignMap.CampaignRequirementType.AP => $"Set a score worth <color={ColorUtils.RANK}>{map.RequirementValue:N0} AP</color>",
                AccSaberCampaignMap.CampaignRequirementType.RANK => $"Get rank <color={ColorUtils.RANK}>#{map.RequirementValue:N0}</color> or better on the map",
                AccSaberCampaignMap.CampaignRequirementType.STREAK_115 => $"Hit <color={ColorUtils.RANK}>{map.RequirementValue:N0}</color> 115s in a row",
                AccSaberCampaignMap.CampaignRequirementType.SCORE => $"Set a score of <color={ColorUtils.RANK}>{map.RequirementValue:N0}</color> points or higher",
                AccSaberCampaignMap.CampaignRequirementType.FC => $"Set a Full Combo",
                _ => $"Get something with a requirement value of {map.RequirementValue:N0}"
            };

            MissionObjective = objective;
            _ = _missionImage.LoadCoverImage(_serialHandler.CachedDifficulties[map.MapDifficultyId].Hash, map.CoverUrl);

            CurrentMap = map;

            CampaignProgressVal = completion;

            InMap = true;
        }

        public void SetMaps(AccSaberCampaign campaign)
        {
            _diffCells.Clear();

            foreach (AccSaberCampaignMap diff in campaign.Difficulties!)
            {
                _diffCells.Add(new CampaignMap(diff));
            }
        }

        public void Initialize()
        {
            if (RecordPlayMethod is not null)
                return;

            Assembly? beatleaderAssembly = PluginManager.GetPluginFromId("BeatLeader")?.Assembly;

            if (beatleaderAssembly is not null)
            {
                Type recorderUtils = beatleaderAssembly.GetType("BeatLeader.Utils.RecorderUtils");
                RecordPlayMethod = recorderUtils.GetMethod("OnActionButtonWasPressed", BindingFlags.Static | BindingFlags.NonPublic);

                Plugin.Log.Info("Beatleader submission patched.");
            }
            else
                Plugin.Log.Info("Beatleader assembly not found.");
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
