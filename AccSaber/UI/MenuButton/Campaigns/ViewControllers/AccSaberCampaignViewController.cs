using AccSaber.Configuration;
using AccSaber.Consts;
using AccSaber.Counter;
using AccSaber.Managers;
using AccSaber.Models;
using AccSaber.Utils;
using AccSaber.Utils.Misc;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Tags;
using HMUI;
using IPA.Loader;
using IPA.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

using static AccSaber.Managers.CampaignProgress;
using static AccSaber.Models.CampaignModel;


namespace AccSaber.UI.MenuButton.Campaigns.ViewControllers
{
    [ViewDefinition("AccSaber.UI.MenuButton.Campaigns.Views.AccSaberCampaignView.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\AccSaberCampaignView.bsml")]
    internal class AccSaberCampaignViewController : Utils.Safety.BSMLSafeAutomaticViewController, IInitializable, IDisposable
    {
        private static MethodInfo? RecordPlayMethod;

        private bool _parsed = false, _updateOnFinish = false;
        private bool _invalidateActive = true;
        private DateTime _lastScoreSubmit = DateTime.MinValue, _lastUpdate = DateTime.MinValue, _lastServerUpdate = DateTime.MinValue;
        private AccSaberCampaign? _currentCampaign;
        private List<AccSaberCampaign> _activeCampaigns = null!;
        private readonly Queue<Guid> _nextGotoMapId = [];
        private readonly List<Guid> _includedTags = [];

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
        public int CurrentMaxNoteCount;
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

        [UIComponent("search-container")]
        private readonly VerticalLayoutGroup _campaignSearchContainer = null!;

        [UIComponent("campaign-description")]
        private readonly TextMeshProUGUI _campaignDescription = null!;

        internal InputFieldView? CampaignSearchInput { get; private set; }
        CurvedTextMeshPro? songSearchPlaceholder = null;

        private enum CategoryTab
        {
            Active,
            Official,
            Curated,
            All,
            Completed
        }

        [Inject] private readonly APCalc _calc = null!;
        [Inject] private readonly AccSaberStore _accSaberStore = null!;
        [Inject] private readonly SerializationHandler _serialHandler = null!;
        [Inject] private readonly PluginConfig _config = null!;
        [Inject] private readonly PlayerSocialLife _playerInfo = null!;
        [Inject] private readonly AccSaberCampaignFlow _campaignFlow = null!;
        [Inject] private readonly AccSaberCampaignMapViewController _campaignMapViewController = null!;
        [Inject] private readonly AccSaberCampaignSettingsModalController _campaignSettingsModalController = null!;
        [Inject] private readonly AccSaberCampaignCounterSettingsModalController _campaignCounterSettingsModalController = null!;
        [Inject] private readonly MenuTransitionsHelper _menuTransitionsHelper = null!;
        [Inject] private readonly PlayerDataModel _playerDataModel = null!;
        [Inject] private readonly SongPreviewPlayer _songPreviewPlayer = null!;
#if NEW_VERSION
        [Inject] private readonly SettingsManager _SettingsManager = null!;
        [Inject] private readonly BeatmapLevelsModel _beatmapLevelsModel = null!;
        [Inject] private readonly BeatmapDataLoader _beatmapDataLoader = null!;
        [Inject] private readonly EnvironmentsListModel _environmentsListModel = null!;
#endif

        [UIValue("count-img")]
        private const string CountImg = ResourcePaths.COUNT;


        private CategoryTab CurrentTab
        {
            get;
            set
            {
                field = value;
                _ = UpdateTabs();
            }
        }

        [UIValue("is-loading")]
        private bool IsLoading
        {
            get;
            set
            {
                field = value;
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
                field = value;
                NotifyPropertyChanged();
            }
        } = false;

        [UIValue("campaign-curated")]
        private bool CampaignCurated
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged();
            }
        } = false;
        [UIValue("in-campaign")]
        public bool InCampaign
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged(nameof(InCampaign));
                NotifyPropertyChanged(nameof(NotInCampaign));
            }
        }

        [UIValue("in-map")]
        public bool InMap
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged(nameof(InMap));
                NotifyPropertyChanged(nameof(InMapOrBarrier));
            }
        }

        [UIValue("in-barrier")]
        public bool InBarrier
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged(nameof(InBarrier));
                NotifyPropertyChanged(nameof(InMapOrBarrier));
            }
        }

        [UIValue("NoCampaignSelected")]
        private bool NoCampaignSelected
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged(nameof(NoCampaignSelected));
                NotifyPropertyChanged(nameof(CampaignIsSelected));
            }
        }

        [UIValue("CampaignIsSelected")]
        private bool CampaignIsSelected => !NoCampaignSelected;

        [UIValue("CampaignTitle")]
        private string CampaignTitle
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged();
            }
        } = null!;

        [UIValue("CampaignCategory")]
        private string CampaignCategory
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged();
            }
        } = null!;

        [UIValue("MissionMapName")]
        private string MissionMapName
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged();
            }
        } = null!;

        [UIValue("MissionMapArtist")]
        private string MissionMapArtist
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged();
            }
        } = null!;

        [UIValue("MissionMapNPS")]
        private string MissionMapNPS
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged();
            }
        } = null!;

        [UIValue("MissionMapNoteCount")]
        private string MissionMapNoteCount
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged();
            }
        } = null!;

        [UIValue("MissionMapNJS")]
        private string MissionMapNJS
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged();
            }
        } = null!;

        [UIValue("MissionMapDuration")]
        private string MissionMapDuration
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged();
            }
        } = null!;

        [UIValue("MissionObjective")]
        private string MissionObjective
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged();
            }
        } = null!;

        [UIValue("MissionComplete")]
        private bool MissionComplete
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue("MissionHasRewards")]
        public bool MissionHasRewards
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue("MissionLocked")]
        public bool MissionLocked
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue("MissionProgress")]
        private string MissionProgress
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged();
            }
        } = null!;

        [UIValue("MissionRewards")]
        private string MissionRewards
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged();
            }
        } = null!;

        [UIValue("CampaignRewards")]
        private string CampaignRewards
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged();
            }
        } = null!;

        [UIValue("CampaignDescription")]
        private string CampaignDescription
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged();
            }
        } = null!;

        [UIValue("CampaignCreator")]
        private string CampaignCreator
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged();
            }
        } = null!;

        [UIValue("not-in-campaign")]
        private bool NotInCampaign => !InCampaign;

        [UIValue("is-not-loading")]
        private bool IsNotLoading => !IsLoading;

        [UIValue("in-map-or-barrier")]
        private bool InMapOrBarrier => InMap || InBarrier;

        [UIComponent("category-filter")]
        private readonly GridLayoutGroup _categoryFilter = null!;

        [UIComponent("difficulty-filter")]
        private readonly GridLayoutGroup _difficultyFilter = null!;

        [UIComponent("theme-filter")]
        private readonly GridLayoutGroup _themeFilter = null!;

        //[UIComponent("genre-scrollable")]
        //private readonly ScrollView _genreContainer = null!;

        [UIComponent("genre-filter")]
        private readonly GridLayoutGroup _genreFilter = null!;

        private ClickableText _textTemplate = null!;
        private async Task GetFilters()
        {
            try
            {
                foreach (CampaignTag tag in await _accSaberStore.GetCampaignTags())
                {
                    GridLayoutGroup? tagGrid = tag.Kind switch
                    {
                        CampaignTagKind.CATEGORY => _categoryFilter,
                        CampaignTagKind.DIFFICULTY => _difficultyFilter,
                        CampaignTagKind.THEME => _themeFilter,
                        CampaignTagKind.GENRE => _genreFilter,
                        _ => throw new NotImplementedException()
                    };

                    if (tagGrid is not null)
                    { 
                        ClickableText newText = Instantiate(_textTemplate, tagGrid.transform, false); // need to do this since for some reason it didnt wanna be curved <3

                        newText.text = tag.Name;
                        newText.fontSize = 2.5f;
                        newText.alignment = TMPro.TextAlignmentOptions.Center;
                        newText.gameObject.SetActive(true);

                        newText.OnClickEvent += (pointerData) => OnTagClicked(tag, newText);
                    }
                }

                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(_genreFilter.rectTransform);
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex);
            }
        }

        private void OnTagClicked(CampaignTag tag, ClickableText text)
        {
            if (_includedTags.Contains(tag.Id))
            {
                _includedTags.Remove(tag.Id);
                text.color = ColorUtils.Color("#ffffff");
                text.DefaultColor = ColorUtils.Color("#ffffff");
            }
            else
            {
                _includedTags.Add(tag.Id);
                text.color = ColorUtils.Color(ColorUtils.TRUE);
                text.DefaultColor = ColorUtils.Color(ColorUtils.TRUE);
            }
            _ = UpdateTabs();
        }


        [UIAction("#post-parse")]
        private void Parsed()
        {
            if(!_parsed)
            {
                _parsed = true;

                _genreFilter.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                _genreFilter.constraintCount = 3;

                if (_textTemplate is null)
                {
                    // transform here doesn't matter at all.
                    _textTemplate = new ClickableTextTag().CreateObject(_categoryFilter.transform).GetComponent<ClickableText>();
                    _textTemplate.gameObject.SetActive(false);
                }

                _ = GetFilters();
            }

            VersionUtils.Parse(ResourcePaths.ACC_SABER_CAMPAIGN_MAP_VIEW, _campaignMapContainer, _campaignMapViewController);
            GameObject? searchBox = Resources.FindObjectsOfTypeAll<InputFieldView>().FirstOrDefault(x => x.gameObject.name == "SearchInputField")?.gameObject;

            if (searchBox is not null)
            {
                GameObject newSearchBox = Instantiate(searchBox, _campaignSearchContainer.transform, false);
                CampaignSearchInput = newSearchBox.GetComponent<InputFieldView>();
                CampaignSearchInput.SetText(string.Empty);
                songSearchPlaceholder = newSearchBox.transform.Find("PlaceholderText")?.GetComponent<CurvedTextMeshPro>();
                CampaignSearchInput.SetField("_keyboardPositionOffset", new Vector3(-45, -25));
                CampaignSearchInput.onValueChanged.AddListener(__ => _ = UpdateTabs());
                NoCampaignSelected = true;
            }
            _campaignDescription.enableAutoSizing = true;
            _campaignDescription.fontSizeMax = 3.5f;
            _campaignDescription.fontSizeMin = 1f;

            CurrentTab = 0;
            InCampaign = false;
            InMap = false;
        }

        [UIAction("campaign-selected")]
        private void OnCampaignSelected(TableView table, CampaignCell cellObj)
        {
            if (cellObj is not null)
                _currentCampaign = cellObj.Data;
            else
                return;
            NoCampaignSelected = false;
            CampaignSelected = true;

            table.ClearSelection();

            _ = UpdateCampaign(_currentCampaign);
        }

        [UIValue("ZoomIn")]
        private void ZoomIn()
        {
            if (_currentCampaign is not null && _campaignMapViewController.CurrentOffsetData?.ScaleFactor < 0.75f)
            {
                _campaignMapViewController.UpdateScalingDelta(0.025f);

                if (InMap && CurrentMap is not null)
                    _campaignMapViewController.ScrollToNode(CurrentMap.Id);
            }
        }

        [UIValue("ZoomOut")]
        private void ZoomOut()
        {
            if (_currentCampaign is not null && _campaignMapViewController.CurrentOffsetData?.ScaleFactor > 0.025f)
            {
                _campaignMapViewController.UpdateScalingDelta(-0.025f);

                if (InMap && CurrentMap is not null)
                    _campaignMapViewController.ScrollToNode(CurrentMap.Id);
            }
        }

        [UIAction("GotoMap")]
        private void GotoMap()
        {
            if (_currentCampaign is null || !InCampaign || _nextGotoMapId.Count == 0)
                return;

            Guid current = _nextGotoMapId.Dequeue();

            _campaignMapViewController.ClickNode(current);

            _nextGotoMapId.Enqueue(current);
        }

        [UIAction("ShowSettings")]
        private void ShowSettings()
        {
            if (!_parsed)
                return;

            _campaignSettingsModalController.ShowModal(_campaignMapContainer.transform);
        }

        [UIAction("ShowCounterSettings")]
        private void ShowCounterSettings()
        {
            if (!_parsed)
                return;

            _campaignCounterSettingsModalController.ShowModal(_campaignMapContainer.transform);
        }

        public void BackPressed()
        {
            _campaignFlow.HideLeaderboard();
            InCampaign = false;
            InBarrier = false;
            InMap = false;
            _songPreviewPlayer.CrossfadeToDefault();
            _ = UpdateTabs();
        }

        [UIAction("PlayCampaign")]
        private async void PlayCampaign()
        {
            InCampaign = true;
            if (_currentCampaign is not null)
            {                  
                if (_activeCampaigns.Find(x => x.Id == _currentCampaign.Id) is null && _currentCampaign.ProgressStatus != UserCampaignProgress.IN_PROGRESS)
                    _missionButton.SetButtonText("Start Campaign");
                else
                    _missionButton.SetButtonText("Play");

                _currentCampaign = await _accSaberStore.GetCampaign(_currentCampaign.Id, true);

                await _campaignMapViewController.SetCampaign(_currentCampaign);

                UpdateGoToMapButton();
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
                if (_activeCampaigns.Find(x => x.Id == _currentCampaign.Id) is null && _currentCampaign.ProgressStatus != UserCampaignProgress.IN_PROGRESS)
                {
                    if (await _accSaberStore.StartCampaign(_currentCampaign.Id) == false)
                        Plugin.Log.Error("Failed to start campaign!");
                    else
                    {
                        _invalidateActive = true;
                        _activeCampaigns.Add(await _accSaberStore.GetCampaign(_currentCampaign.Id));
                        _missionButton.SetButtonText("Play");
                    }
                }
            }
            var colorScheme = _playerDataModel.playerData.colorSchemesSettings.overrideDefaultColors
            ? _playerDataModel.playerData.colorSchemesSettings.GetSelectedColorScheme()
            : null;
            MapStarted = true;

            RecordPlayMethod?.Invoke(null, null);



#if V42
            _menuTransitionsHelper.StartStandardLevel(
                gameMode: "Solo",
                beatmapKey: CurrentBeatMapKey,
                beatmapLevel: CurrentBeatMapLevel,
                overrideEnvironmentSettings: _playerDataModel.playerData.overrideEnvironmentSettings,
                playerOverrideColorScheme: _playerDataModel.playerData.colorSchemesSettings.GetOverrideColorScheme(),
                playerOverrideLightshowColors: _playerDataModel.playerData.colorSchemesSettings.ShouldOverrideLightshowColors(),
                gameplayModifiers: _playerDataModel.playerData.gameplayModifiers,
                playerSpecificSettings: _playerDataModel.playerData.playerSpecificSettings,
                practiceSettings: null,
                environmentsListModel: _environmentsListModel,
                gameplayAdditionalInformation: new("buh"),
                beforeSceneSwitchToGameplayCallback: null,
                afterSceneSwitchToGameplayCallback: null,
                levelFinishedCallback: LevelFinished,
                levelRestartedCallback: null
            );
#elif V41
            _menuTransitionsHelper.StartStandardLevel(
                gameMode: "Solo",
                beatmapKey: CurrentBeatMapKey,
                beatmapLevel: CurrentBeatMapLevel,
                overrideEnvironmentSettings: _playerDataModel.playerData.overrideEnvironmentSettings,
                playerOverrideColorScheme: _playerDataModel.playerData.colorSchemesSettings.GetOverrideColorScheme(),
                playerOverrideLightshowColors: _playerDataModel.playerData.colorSchemesSettings.ShouldOverrideLightshowColors(),
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
#elif V40
            _menuTransitionsHelper.StartStandardLevel(
                gameMode: "Solo",
                beatmapKey: CurrentBeatMapKey,
                beatmapLevel: CurrentBeatMapLevel,
                overrideEnvironmentSettings: _playerDataModel.playerData.overrideEnvironmentSettings,
                playerOverrideColorScheme: colorScheme,
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
                overrideColorScheme: colorScheme,
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
                overrideColorScheme: colorScheme,
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
            {
                _ = UpdateTabs();

                if (_updateOnFinish)
                {
                    _updateOnFinish = false;
                    UpdateCampaign();
                }
            }
        }

#pragma warning disable IDE0060
        [UIAction("tab-selected")]
        private void CategoryTabSelected(SegmentedControl segmentedControl, int index)
        {
            CurrentTab = (CategoryTab)index;
        }
#pragma warning restore IDE0060

        private void UpdateGoToMapButton()
        {
            if (_currentCampaign is null)
                return;

            _nextGotoMapId.Clear();
            HashSet<Guid> barrierIds = [.. _currentCampaign.Barriers.Select(barrier => barrier.Id)];
            foreach (Guid unlockedIds in _campaignMapViewController.CampaignProgress.NodesSortedByProgression(CompletionStatus.Unlocked).Where(id => !barrierIds.Contains(id)))
                _nextGotoMapId.Enqueue(unlockedIds);
        }

        public async Task UpdateTabs()
        {
            await _playerInfo.LoadTask;

            _campaignCells.Clear();
            _campaignList.Data().Clear();
            _campaignList.TableView().ReloadData();

            try
            {
                if (_invalidateActive)
                {
                    _invalidateActive = false;
                    _activeCampaigns = await _accSaberStore.GetActiveCampaigns();
                }

                List<AccSaberCampaign> tabCampaigns = CurrentTab switch
                {
                    CategoryTab.Active => _activeCampaigns,
                    CategoryTab.Official => await _accSaberStore.GetCampaigns(CampaignStatus.CURATED),
                    CategoryTab.Curated => await _accSaberStore.GetCampaigns(CampaignStatus.CURATED),
                    CategoryTab.All => await _accSaberStore.GetCampaigns(),
                    CategoryTab.Completed => _activeCampaigns,
                    _ => throw new NotImplementedException(),
                };

                foreach (var campaign in tabCampaigns)
                {
                    if ((CurrentTab == CategoryTab.Active && campaign.ProgressStatus != UserCampaignProgress.IN_PROGRESS) ||
                        (CurrentTab == CategoryTab.Completed && campaign.ProgressStatus != UserCampaignProgress.COMPLETED) ||
                        (CurrentTab == CategoryTab.Official && !campaign.Official))
                        continue;

                    if (CampaignSearchInput!.text != "" && !campaign.Name.ToLower().Contains(CampaignSearchInput!.text.ToLower()))
                        continue;

                    bool match = false;

                    if (_includedTags.Count == 0)
                        match = true;                       
                    else if (campaign.Tags is not null)
                    {
                        foreach (var tagId in _includedTags)
                        {
                            if (campaign.Tags.Any(x => x.Id == tagId))
                            {
                                match = true;
                                break;
                            }
                        }
                    }

                    if (match == false)
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
            catch (Exception ex)
            {
                Plugin.Log.Error(ex);
            }
        }

        public async Task UpdateCampaign(AccSaberCampaign campaign)
        {
            CampaignCategory = "";
            CampaignTitle = campaign.Name;
            CampaignCreator = campaign.CreatorAlias ?? campaign.CreatorName;
            CampaignDescription = campaign.Description;
            CampaignRewards = "";
            CampaignCurated = false;

            if (campaign.Status == CampaignStatus.CURATED)
            {
                string items = string.Empty;

                if (campaign.Items is not null)
                {
                    foreach (var item in campaign.Items)
                    {

                        string quantity = item.Quantity == 1 ? "" : $"{item.Quantity}x ";

                        items += $"<color={ColorUtils.RANK}>{quantity}{item.ItemName}</color>\n";

                    }
                }
                
                if (campaign.CompletionXp != 0)
                    items += $"<color={ColorUtils.AP}>+{campaign.CompletionXp} XP</color>";

                CampaignRewards = items;

                if ((campaign.Items is not null && campaign.Items.Count > 0) || campaign.CompletionXp != 0)
                    CampaignCurated = true;
            }


            if (campaign.Tags is not null)
            {
                foreach (var tag in campaign.Tags)
                {
                    if (tag.Kind != CampaignTagKind.CATEGORY)
                        continue;

                    CampaignCategory = CampaignCategory == "" ? $"<color={ColorUtils.GetColor(EnumUtils.ReloadedCategoryIdToCategoryNullable(tag.CategoryId))}>{tag.Name}</color>" :
                        CampaignCategory + $" | <color={ColorUtils.GetColor(EnumUtils.ReloadedCategoryIdToCategoryNullable(tag.CategoryId))}>{tag.Name}</color>";
                }

            }

            if ((campaign.IconUrl is not null && campaign.IconUrl.Contains(".webp")) || campaign.IconUrl is null)
                await _campaignImage.SetImageAsync("AccSaber.Resources.AccSaber.png", false);
            else
                StartCoroutine(_campaignImage.LoadImageRoutine(campaign.IconUrl));


        }

#if NEW_VERSION
        public async void SetMission(AccSaberCampaignMap map, BeatmapKey beatmapKey, BeatmapLevel beatmapLevel, CampaignProgressValue completion, bool withSound = true)
        {
            CurrentBeatMapKey = beatmapKey;
            CurrentBeatMapLevel = beatmapLevel;
#else
        public async void SetMission(AccSaberCampaignMap map, IDifficultyBeatmap beatmapLevel, CampaignProgressValue completion, bool withSound = true)
        {
            CurrentBeatMapLevel = beatmapLevel;
#endif            
            float nps = 0;
            float njs = 0;

            try
            {
#if NEW_VERSION
                LoadBeatmapLevelDataResult mapInfo = await _beatmapLevelsModel.LoadBeatmapLevelDataAsync(beatmapLevel.levelID, BeatmapLevelDataVersion.Original, CancellationToken.None);

                BeatmapDataBasicInfo? mapData = await _beatmapDataLoader.LoadBasicBeatmapDataAsync(mapInfo.beatmapLevelData!, beatmapKey);
#else

                IBeatmapDataBasicInfo mapData = await beatmapLevel.GetBeatmapDataBasicInfoAsync();
#endif

                if (mapData is not null)
                {
                    CurrentMaxNoteCount = mapData.cuttableNotesCount;
#if NEW_VERSION
                    nps = mapData.cuttableNotesCount / beatmapLevel.songDuration;
                    njs = beatmapLevel.GetDifficultyBeatmapData(beatmapKey.beatmapCharacteristic, beatmapKey.difficulty)?.noteJumpMovementSpeed ?? 0f;
#else
                    nps = CurrentMaxNoteCount / beatmapLevel.level.songDuration;
                    njs = beatmapLevel.noteJumpMovementSpeed;
#endif
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Error(e);
            }

            MissionHasRewards = map.XP > 0 || map.Items.Count > 0;

            if (MissionHasRewards)
            {
                StringBuilder str = new();

                foreach (AccSaberCampaignItem item in map.Items)
                {
                    string quantity = item.Quantity == 1 ? "" : $"{item.Quantity}x ";
                    str.AppendLine($"<color={ColorUtils.RANK}>{quantity}{item.ItemName}</color>");
                }

                str.AppendLine($"<color={ColorUtils.OVERALL}>+{map.XP:N0}XP</color>");

                MissionRewards = str.ToString();
            }


            MissionLocked = completion.Completion == CompletionStatus.Incomplete;
            _missionButton.gameObject.SetActive(!MissionLocked);

            MissionProgress = map.RequirementType switch
            {
                CampaignRequirementType.ACC => $"<color={ColorUtils.ACC}>{completion.Progress * 100f:N2}%</color> / <color={ColorUtils.ACC}>{map.RequirementValue * 100f:N2}%</color>",
                CampaignRequirementType.AP => $"<color={ColorUtils.AP}>{completion.Progress:0.##}ap</color> / <color={ColorUtils.AP}>{map.RequirementValue:0.##}ap</color>",
                CampaignRequirementType.RANK => $"<color={ColorUtils.RANK}>#{completion.Progress:N0}</color> / <color={ColorUtils.RANK}>#{map.RequirementValue:N0}</color>",
                CampaignRequirementType.STREAK_115 => $"<color={ColorUtils.TECH}>{completion.Progress:N0}x</color> / <color={ColorUtils.TECH}>{map.RequirementValue:N0}x</color>",
                CampaignRequirementType.SCORE => $"<color={ColorUtils.GREY}>{completion.Progress:N0}</color> / <color={ColorUtils.GREY}>{map.RequirementValue:N0}</color>",
                CampaignRequirementType.FC => $"<color={(completion.Completion == CompletionStatus.Complete ? "#5F5" : "#F55")}>FC</color>",
                CampaignRequirementType.PASS => $"<color={(completion.Completion == CompletionStatus.Complete ? "#5F5" : "#F55")}>Pass</color>",
                _ => $"{completion.Progress}"
            };

            MissionMapName = map.SongName;
            MissionMapArtist = $"{map.SongAuthor} [<color=#c0548f>{map.MapAuthor}</color>]";
            MissionMapNPS = $"{nps:N2}";
            MissionMapNoteCount = CurrentMaxNoteCount.ToString();
            MissionMapNJS = $"{njs:N1}";
#if NEW_VERSION
            TimeSpan Duration = TimeSpan.FromSeconds(beatmapLevel.songDuration);
#else
            TimeSpan Duration = TimeSpan.FromSeconds(beatmapLevel.level.songDuration);
#endif


            MissionMapDuration = string.Format("{0:D1}:{1:D2}", Duration.Minutes, Duration.Seconds);


            string objective = map.RequirementType switch
            {
                CampaignRequirementType.ACC => $"Set a score with at least <color={ColorUtils.RANK}>{map.RequirementValue * 100:N2}%</color> accuracy",
                CampaignRequirementType.AP => $"Set a score worth <color={ColorUtils.RANK}>{map.RequirementValue:N0} AP</color>",
                CampaignRequirementType.RANK => $"Get rank <color={ColorUtils.RANK}>#{map.RequirementValue:N0}</color> or better on the map",
                CampaignRequirementType.STREAK_115 => $"Hit <color={ColorUtils.RANK}>{map.RequirementValue:N0}</color> 115s in a row",
                CampaignRequirementType.SCORE => $"Set a score of <color={ColorUtils.RANK}>{map.RequirementValue:N0}</color> points or higher",
                CampaignRequirementType.FC => "Set a Full Combo",
                CampaignRequirementType.PASS => "Pass the map without no fail",
                _ => $"Get something with a requirement value of {map.RequirementValue:N0}"
            };

            MissionObjective = objective;

            AccSaberBasicDifficulty? mapDiff = await _serialHandler.GetDiffByIdAsync(map.MapDifficultyId);

            if (mapDiff is not null)
                _ = _missionImage.LoadCoverImage(mapDiff.Hash, map.CoverUrl);

            CurrentMap = map;

            CampaignProgressVal = completion;

            _campaignMapViewController.ScrollToNode(map.Id);

#if NEW_VERSION
            AudioClip? previewAudio = withSound ? await CurrentBeatMapLevel.previewMediaData.GetPreviewAudioClip() : null;

            if(previewAudio is not null)
            { 
                _songPreviewPlayer.CrossfadeTo(previewAudio, _SettingsManager.settings.audio.ambientVolumeScale, CurrentBeatMapLevel.previewStartTime, CurrentBeatMapLevel.previewDuration - CurrentBeatMapLevel.previewStartTime, null);
            }
#else
            AudioClip? previewAudio = withSound ? CurrentBeatMapLevel.level.beatmapLevelData.audioClip : null;

            if(previewAudio is not null)
            {
                _songPreviewPlayer.CrossfadeTo(previewAudio, 0.5f, CurrentBeatMapLevel.level.previewStartTime, CurrentBeatMapLevel.level.previewDuration - CurrentBeatMapLevel.level.previewStartTime, null);
            }
#endif
            InBarrier = false;
            InMap = true;
        }
        public async void SetBarrierInfo(AccSaberCampaignMapViewController.CampaignMapBarrier barrier, CampaignProgressValue progress)
        {
            CurrentBeatMapLevel = null;

            MissionObjective = barrier.Barrier.ConditionType switch
            {
                BarrierConditionType.AVERAGE_ACC => $"Get an average accuracy of <color={ColorUtils.RANK}>{barrier.Barrier.ConditionValue * 100f:N2}%</color>.",
                BarrierConditionType.AVERAGE_AP => $"Get an average ap of <color={ColorUtils.RANK}>{barrier.Barrier.ConditionValue:0.##}</color> AP.",
                BarrierConditionType.AP_MAX => $"Get a score worth at least <color={ColorUtils.RANK}>{barrier.Barrier.ConditionValue:0.##}</color> AP.",
                BarrierConditionType.ACC_MAX => $"Get a score greater than or equal to <color={ColorUtils.RANK}>{barrier.Barrier.ConditionValue * 100f:N2}%</color>.",
                BarrierConditionType.STREAK_115_AVERAGE => $"Get an average 115 streak of <color={ColorUtils.RANK}>{barrier.Barrier.ConditionValue:N1}x</color>.",
                BarrierConditionType.STREAK_115_MAX => $"Get a 115 streak greater than or equal to <color={ColorUtils.RANK}>{barrier.Barrier.ConditionValue:N0}x</color>.",
                BarrierConditionType.FC => $"Full combo <color={ColorUtils.RANK}>{barrier.Barrier.AffectedCampaignDifficultyIds.Count:N0}</color> map(s).",
                BarrierConditionType.AVERAGE_RANK => $"Get an average rank of <color={ColorUtils.RANK}>#{barrier.Barrier.ConditionValue:N1}</color>.",
                BarrierConditionType.MAX_RANK => $"Get a rank greater than or equal to <color={ColorUtils.RANK}>#{barrier.Barrier.ConditionValue:N0}</color>.",
                BarrierConditionType.COMPLETION_COUNT => $"Complete <color={ColorUtils.RANK}>{barrier.Barrier.ConditionValue:N0}</color> nodes.",
                BarrierConditionType.PASS => $"Pass <color={ColorUtils.RANK}>{barrier.Barrier.ConditionValue:N0}</color> nodes (without no fail).",
                _ => $"Get something with a requirement value of {barrier.Barrier.ConditionValue:0.##}"
            };

            MissionProgress = barrier.ProgressText[(barrier.ProgressText.LastIndexOf('\n') + 1)..];
            CampaignProgressVal = progress;

            MissionHasRewards = barrier.Barrier.XP > 0 || barrier.Barrier.Items.Count > 0;

            if (MissionHasRewards)
            {
                StringBuilder str = new();

                foreach (AccSaberCampaignItem item in barrier.Barrier.Items)
                    str.AppendLine($"<color={ColorUtils.RANK}>{item.Quantity}x {item.ItemName}</color>");

                str.AppendLine($"<color={ColorUtils.OVERALL}>+{barrier.Barrier.XP:N0}XP</color>");

                MissionRewards = str.ToString();
            }

            MissionLocked = false;
            _missionButton.gameObject.SetActive(false);

            InMap = false;
            InBarrier = true;
        }

        private IEnumerable<AccSaberCampaignMap> GetAllMapsOfDiffId(Guid diffId)
        {
            if (_currentCampaign is null)
                return [];

            return _currentCampaign.Difficulties
                   .Where(map =>
                       map.MapDifficultyId == diffId &&
                       _campaignMapViewController.CampaignProgress.PlayerValues.TryGetValue(map.Id, out CampaignProgressValue progress) &&
                       progress.Completion != CompletionStatus.Incomplete
                   );
        }

        private void OnPlayerScore(AccSaberLeaderboardEntry score)
        {
            _lastServerUpdate = DateTime.UtcNow;
            if (InCampaign && _currentCampaign is not null && _lastScoreSubmit > _lastUpdate)
            {
                if (MapStarted)
                    _updateOnFinish = true;
                else
                    UpdateCampaign();
            }
        }
        private async void OnPlayerScoreSubmit(AccSaberScore score, bool scoreBeaten)
        {
            if (CurrentMap is not null && _currentCampaign is not null && _currentCampaign.Difficulties is not null)
            {
                List<AccSaberCampaignMap> otherMaps = [.. GetAllMapsOfDiffId(score.MapDifficultyId).Where(map => map.Id != CurrentMap.Id)];

                _ = OnPlayerScoreSubmit(score, scoreBeaten, CurrentMap, doUpdates: otherMaps.Count == 0);

                for (int i = 0; i < otherMaps.Count; i++)
                    _ = OnPlayerScoreSubmit(score, scoreBeaten, otherMaps[i], setMap: false, doUpdates: i == otherMaps.Count - 1);
            }
        }
        private async Task OnPlayerScoreSubmit(AccSaberScore score, bool scoreBeaten, AccSaberCampaignMap currentMap, bool setMap = true, bool doUpdates = true)
        {
            DateTime now = DateTime.UtcNow;
            _lastScoreSubmit = now;

            if (!scoreBeaten)
                _lastServerUpdate = now; // if the score was not a pb, then the server is updated instantly.

            if (!InMap || (score.UncompletedMap ?? true))
                return;

            float acc = (float)score.Score / MiscUtils.MaxScoreForNotes(CurrentMaxNoteCount);

            AccSaberBasicDifficulty? diff = await _serialHandler.GetDiffByIdAsync(score.MapDifficultyId);

            if (diff is null)
            {
                Plugin.Log.Warn("Difficulty for score not found!");
                return;
            }

            float val = currentMap.RequirementType switch
            {
                CampaignRequirementType.ACC => acc,
                CampaignRequirementType.AP => _calc.GetAp(acc, diff.Complexity),
                CampaignRequirementType.SCORE => score.Score,
                CampaignRequirementType.STREAK_115 => score.Streak115,
                CampaignRequirementType.FC => score.Mistakes,
                CampaignRequirementType.PASS => score.ModifierCodes.Contains("NF") ? 1f : 0f,
                _ => -1f
            };

            if (val < 0f)
            {
                Plugin.Log.Warn("Cannot handle the campaign type that was completed.");
                return; // I can only handle certain types, those I can't will be updated once the websocket sends the score.
            }

            if (CampaignProgressVal.Progress >= val)
            {
                Plugin.Log.Info("Player did not beat old pb.");
                return; // didn't beat old progress.
            }

            _lastUpdate = now;

            if (currentMap.RequirementValue > CampaignProgressVal.Progress && currentMap.RequirementValue <= val)
            {
                CampaignProgressValue? newVal = await _campaignMapViewController.MarkNodeAsComplete(currentMap.Id, val);

                if (doUpdates)
                    UpdateGoToMapButton();

                if (newVal is null)
                    Plugin.Log.Warn("Setting the campaign node to complete failed!");
                else
                {
                    _invalidateActive = newVal.Value.Completion == CompletionStatus.Complete;

                    CampaignProgressVal = newVal.Value;
                }
            }

            if (doUpdates)
                _mainThreadDispatcher.EnqueueAction(_campaignMapViewController.UpdateDisplay);

            if (setMap && currentMap is not null && CurrentBeatMapLevel is not null)
#if NEW_VERSION
                SetMission(currentMap, CurrentBeatMapKey, CurrentBeatMapLevel, CampaignProgressVal, false);
#else
                SetMission(currentMap, CurrentBeatMapLevel, CampaignProgressVal, false);
#endif
        }
        public async Task WaitForServerUpdate(TimeSpan timeout = default)
        {
            if (timeout == default)
                timeout = TimeSpan.FromSeconds(5);

            DateTime now = DateTime.UtcNow;

            if (_lastServerUpdate >= _lastScoreSubmit && _lastServerUpdate >= _lastUpdate)
                return;

            using CancellationTokenSource source = new();
            source.CancelAfter(timeout);

            try
            {
                while (_lastServerUpdate < now)
                    await Task.Delay(500, source.Token);
            }
            catch (TaskCanceledException) when (source.IsCancellationRequested) { }
        }
        private void UpdateCampaign()
        {
            if (_currentCampaign is not null)
                _mainThreadDispatcher.EnqueueAction(async () =>
                {
                    //_currentCampaign = await _accSaberStore.GetCampaign(_currentCampaign.Id, true);

                    _lastUpdate = DateTime.UtcNow;

                    await _campaignMapViewController.UpdateCampaign();

                    UpdateGoToMapButton();

                    if (CurrentMap is not null)
                        CampaignProgressVal = _campaignMapViewController.CampaignProgress.PlayerValues[CurrentMap.Id];

#if NEW_VERSION
                    if (CurrentMap is not null && CurrentBeatMapLevel is not null)
                        SetMission(CurrentMap, CurrentBeatMapKey, CurrentBeatMapLevel, CampaignProgressVal);
#else
                    if (CurrentMap is not null && CurrentBeatMapLevel is not null)
                        SetMission(CurrentMap, CurrentBeatMapLevel, CampaignProgressVal);
#endif
                });
        }
        private void OnPluginConfigChanged(object sender, PropertyChangedEventArgs args)
        {
            switch (args.PropertyName)
            {
                case nameof(PluginConfig.StickScrolling):
                    _campaignMapViewController.StickScrolling = _config.StickScrolling;
                    break;
                case nameof(PluginConfig.ScrollSpeed):
                    _campaignMapViewController.ScrollSpeed = _config.ScrollSpeed;
                    break;
                case nameof(PluginConfig.CampaignColorBackgroundBrightness) when _campaignMapViewController.IsSolidBGColor:
                    _campaignMapViewController.BackgroundBrightness = _config.CampaignColorBackgroundBrightness;
                    break;
                case nameof(PluginConfig.CampaignImageBackgroundBrightness) when !_campaignMapViewController.IsSolidBGColor:
                    _campaignMapViewController.BackgroundBrightness = _config.CampaignImageBackgroundBrightness;
                    break;
                case nameof(PluginConfig.CampaignColorBackgroundAlpha) when _campaignMapViewController.IsSolidBGColor:
                    _campaignMapViewController.BackgroundAlpha = _config.CampaignColorBackgroundAlpha;
                    break;
                case nameof(PluginConfig.CampaignImageBackgroundAlpha) when !_campaignMapViewController.IsSolidBGColor:
                    _campaignMapViewController.BackgroundAlpha = _config.CampaignImageBackgroundAlpha;
                    break;
            }
        }

        public void Initialize()
        {
            AccSaberStore.OnPlayerScoreUpdated += OnPlayerScore;
            ScoreTracking.ScoreCounter.OnScoreSubmit += OnPlayerScoreSubmit;

            _config.PropertyChanged += OnPluginConfigChanged;

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
                Plugin.Log.Warn("Beatleader assembly not found.");
        }
        public void Dispose()
        {
            AccSaberStore.OnPlayerScoreUpdated -= OnPlayerScore;
            ScoreTracking.ScoreCounter.OnScoreSubmit -= OnPlayerScoreSubmit;

            _config.PropertyChanged -= OnPluginConfigChanged;
        }

        internal class CampaignCell(AccSaberCampaign campaign) : Utils.Safety.SafeNotifyPropertyChanged
        {
            public readonly AccSaberCampaign Data = campaign;

            private string GetTags()
            {
                string temp = "";

                if (Data.Status == CampaignStatus.CURATED)
                    temp = $"<color={ColorUtils.TRUE}>CURATED</color>";

                if (Data.Official)
                    temp = $"<color={ColorUtils.STANDARD}>OFFICIAL</color>";

                return temp;
            }

            [UIValue(nameof(Name))] private string Name => Data.Name;
            [UIValue(nameof(Author))] private string Author => Data.CreatorAlias ?? Data.CreatorName;
            [UIValue(nameof(Tags))] private string Tags => GetTags();

        }
    }
}
