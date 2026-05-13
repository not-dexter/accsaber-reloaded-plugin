using AccSaber.API;
using AccSaber.Configuration;
using AccSaber.Consts;
using AccSaber.Managers;
using AccSaber.Models;
using AccSaber.Patches;
using AccSaber.Utils;
using AccsaberLeaderboard.UI.BSML_Addons.Components;
using AccsaberLeaderboard.UI.Components;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

using static AccSaber.API.AccsaberAPI;
using static AccSaber.Utils.ColorUtils;

namespace AccSaber.UI.ViewControllers
{
    [ViewDefinition("AccSaber.UI.Views.AccSaberLeaderboardView.bsml")]
    [HotReload(RelativePathToLayout = @"..\UI\Views\AccSaberLeaderboardView.bsml")]
    internal sealed class AccSaberLeaderboardViewController : BSMLAutomaticViewController, IInitializable, IDisposable
    {
#pragma warning disable IDE0044, IDE0051

        #region Static Variables & Properties

        public const float BIG_CELL_SIZE = 5.9f;
        public const float BIG_FONT_SIZE = 3.5f;

        public const float SMALL_CELL_SIZE = 5.3f;
        public const float SMALL_FONT_SIZE = 3f;

        public const string RANKED_HEADER = "Accsaber";
        public const string UNRANKED_HEADER = "Not Accsaber";

        public static bool LeaderboardOnPlayerPage => Instance.OnPlayerPage;

        public static event Action<bool>? OnMapChanged; // bool == is map ranked or not.

        public static AccSaberLeaderboardViewController Instance { get; private set; } = null!;
        private static TextSpacer Spacer = new();

        #endregion Static Variables & Properties

        #region Instance Variables & Fields

        private readonly List<LeaderboardEntryDisplay> scoreDatas = [];
        private int page, nextPage, currentPage = -1, currentPlayerPage;
        private AccSaberLeaderboardEntry? currentPlayerScore;
        private AsyncLock loadLeaderboardLock = new(), forceRefreshLock = new();
        private Color selectorDefaultColor, defaultPageTopColor, defaultPageUpColor, defaultPageYouColor, defaultPageDownColor;
        private Color highlightPageTopColor, highlightPageYouColor;
        private Stack<int> previousPages = [];
        private AccSaberDifficulty? difficultyInfo;
        private bool refreshRequested = false, loaded = false;

        private string? titlePanelTitle;
        private TextMeshProUGUI? titlePaneTitleText = null;

        public LeaderboardDisplayType DisplayType { get; private set; }
        public string? DifficultyId => difficultyInfo?.DifficultyId;
        public bool ValidMapSelected => !string.IsNullOrEmpty(CurrentHash) && CurrentDiff != default;
        public string? CurrentHash { get; private set; }
        public BeatmapDifficulty CurrentDiff { get; private set; }
        public float CurrentComplexity => difficultyInfo?.Complexity ?? 0f;

        public APCategory? CurrentCategory => difficultyInfo?.Category;

        internal Func<AccSaberLeaderboardEntry, bool>? CurrentFilter
        {
            get
            {
                return DisplayType switch
                {
                    LeaderboardDisplayType.Global => null,
                    LeaderboardDisplayType.Country => CountryFilterMaker(currentPlayerScore!.Country),
                    _ => token => PlayerSocialLife.GetIds_Internal(DisplayType)!.Contains(token.PlayerId)
                };
            }
        }

        public bool OnPlayerPage
        {
            get
            {
                if (currentPlayerPage == -1) return true;
                //Plugin.Log.Info($"current player page = {currentPlayerPage}, current page = {currentPage}, next page = {nextPage}");
                return DisplayType switch
                {
                    LeaderboardDisplayType.Country or LeaderboardDisplayType.Followed or LeaderboardDisplayType.Rivals =>
                        scoreDatas.First().ScoreData.Rank <= currentPlayerScore?.Rank && scoreDatas.Last().ScoreData.Rank >= currentPlayerScore?.Rank,
                    _ => currentPage <= currentPlayerPage && (nextPage > currentPlayerPage || currentPage == nextPage)
                };
            }
        }

        public bool BelowPlayerPage
        {
            get
            {
                if (currentPlayerPage == -1) return false;
                return DisplayType switch
                {
                    LeaderboardDisplayType.Country or LeaderboardDisplayType.Followed or LeaderboardDisplayType.Rivals => scoreDatas.First().ScoreData.Rank > currentPlayerScore?.Rank,
                    _ => currentPage > currentPlayerPage
                };
            }
        }

        public bool UsesPreviousPages => DisplayType is LeaderboardDisplayType.Relations;

        #endregion Instance Variables & Fields

        #region Injects

        [Inject] private readonly PluginConfig PC = null!;
        [Inject] private readonly StandardLevelDetailViewController sldvc = null!;
        [Inject] private readonly AccSaberStore store = null!;
        [Inject] private readonly AccSaberPanelViewController aspvc = null!;
        [Inject] private readonly LeaderboardScoreModalController lsmc = null!; //psmvc
        [Inject] private readonly LeaderboardUserModalController lumc = null!; //pmmvc

        #endregion Injects

        #region Loading UI objects

        [UIObject("leaderboard_loading")] private GameObject leaderboardLoader = null!;
        [UIObject("leaderboard")] private GameObject leaderboardContainer = null!;

        #endregion Loading UI objects

        #region UI Values & Components

        [UIValue("colorGrey")] private const string grey = GREY;
        [UIValue("mapStarColor")] private const string mapStarColor = OVERALL_DIM;

        [UIValue("topArrowPic")] private const string topArrowPic = ResourcePaths.TOP_ARROW;
        [UIValue("youPic")] private const string youPic = ResourcePaths.YOU;
        [UIValue("swapPic")] private const string swapPic = ResourcePaths.SWAP;
        [UIValue("globalPic")] private const string globalPic = ResourcePaths.GLOBAL_ICON;
        [UIValue("friendsPic")] private const string friendsPic = ResourcePaths.FRIEND;
        [UIValue("followedPic")] private const string followedPic = ResourcePaths.FOLLOWED;
        [UIValue("rivalsPic")] private const string rivalsPic = ResourcePaths.RIVALS;
        [UIValue("relationsPic")] private const string relationsPic = ResourcePaths.RELATIONS;
        [UIValue("countryPic")] private const string countryPic = ResourcePaths.COUNTRY;
        [UIValue("complexityBG")] private const string complexityBG = ResourcePaths.GRADIENT_CORNER;

        [UIValue("containerWidth")] public const float containerWidth = 80f;
        [UIValue("containerHeight")] public const float containerHeight = 80f;

        [UIValue("complexityFontSize")] public const float complexityFontSize = 5f;

        [UIParams] private BSMLParserParams parserParams = null!;
        [UIComponent("leaderboard")] private MyCustomCellListTableData leaderboard = null!;

        [UIValue("leaderboard-infos")]
        private List<ICellDataSource> LeaderboardInfos
        {
            get
            {
                IEnumerable<ICellDataSource> outp = scoreDatas;
                if (currentPlayerScore is not null && !OnPlayerPage)
                    return BelowPlayerPage ? [.. outp.Prepend(Spacer).Prepend(new LeaderboardEntryDisplay(currentPlayerScore))] :
                        [.. outp.Append(Spacer).Append(new LeaderboardEntryDisplay(currentPlayerScore))];
                return [.. outp];
            }
        }

        private float CellSize => OnPlayerPage ? BIG_CELL_SIZE : SMALL_CELL_SIZE;

        [UIObject("leaderboard_badMap")] private GameObject badMapMessage = null!;

        [UIObject("titleContainer")] private GameObject titleContainer = null!;

        [UIComponent("GlobalSelector")] private ClickableImage globalSelector = null!;
        [UIComponent("FriendsSelector")] private ClickableImage friendsSelector = null!;
        [UIComponent("FollowedSelector")] private ClickableImage followedSelector = null!;
        [UIComponent("RivalsSelector")] private ClickableImage rivalsSelector = null!;
        [UIComponent("RelationsSelector")] private ClickableImage relationsSelector = null!;
        [UIComponent("CountrySelector")] private ClickableImage countrySelector = null!;

        [UIComponent("PageTopSelector")] private ClickableImage pageTopSelector = null!;
        [UIComponent("PageUpSelector")] private NoTransitionsButton pageUpSelector = null!;
        [UIComponent("PageUpSelector")] private ButtonIconImage pageUpImage = null!;
        [UIComponent("PageYouSelector")] private ClickableImage pageYouSelector = null!;
        [UIComponent("PageDownSelector")] private NoTransitionsButton pageDownSelector = null!;
        [UIComponent("PageDownSelector")] private ButtonIconImage pageDownImage = null!;

        [UIComponent("selectorContainer")] private LayoutElement selectorContainer = null!;

        #endregion UI Values & Components

        #region UI Actions

        [UIAction("OnCellSelected")]
        private void OnCellSelected(ICellDataSource cell)
        {
            if (cell is LeaderboardEntryDisplay dataInfo && lsmc is not null)
                lsmc.ShowModal(this, dataInfo.ScoreData);
        }

        [UIAction("ToggleCombinedIcons")]
        private void ToggleCombinedIcons()
        {
            bool toggle = !PC.CombineRelations;
            IEnumerator UpdateUI()
            {
                yield return new WaitForEndOfFrame();

                friendsSelector.gameObject.SetActive(!toggle);
                followedSelector.gameObject.SetActive(!toggle);
                rivalsSelector.gameObject.SetActive(!toggle);
                relationsSelector.gameObject.SetActive(toggle);

                selectorContainer.preferredHeight = toggle ? 25f : 35f;
            }
            StartCoroutine(UpdateUI());
            PC.CombineRelations = toggle;
        }

        [UIAction("#post-parse")]
        private void PostParse()
        {
            if (loaded)
                return;
            loaded = true;

            HandleHeaderPane();

            selectorDefaultColor = globalSelector.DefaultColor;
            DisplayType = LeaderboardDisplayType.Global;
            UpdateSelectors(LeaderboardDisplayType.Global);

            highlightPageTopColor = pageTopSelector.HighlightColor;
            highlightPageYouColor = pageYouSelector.HighlightColor;

#if NEW_VERSION
            defaultPageTopColor = pageTopSelector.DefaultColor;
            defaultPageUpColor = pageUpImage.Image.color;
            defaultPageYouColor = pageYouSelector.DefaultColor;
            defaultPageDownColor = pageDownImage.Image.color;

            pageUpSelector.selectionStateDidChangeEvent += state =>
                pageUpImage.Image.color = state == NoTransitionsButton.SelectionState.Disabled ? GREYED_OUT.Color() : defaultPageUpColor;
            pageDownSelector.selectionStateDidChangeEvent += state =>
                pageDownImage.Image.color = state == NoTransitionsButton.SelectionState.Disabled ? GREYED_OUT.Color() : defaultPageDownColor;
#else
            defaultPageTopColor = pageTopSelector.DefaultColor;
            defaultPageUpColor = pageUpImage.image.color;
            defaultPageYouColor = pageYouSelector.DefaultColor;
            defaultPageDownColor = pageDownImage.image.color;

            pageUpSelector.selectionStateDidChangeEvent += state =>
                pageUpImage.image.color = state == NoTransitionsButton.SelectionState.Disabled ? GREYED_OUT.Color() : defaultPageUpColor;
            pageDownSelector.selectionStateDidChangeEvent += state =>
                pageDownImage.image.color = state == NoTransitionsButton.SelectionState.Disabled ? GREYED_OUT.Color() : defaultPageDownColor;
#endif

            lsmc?.BindModal(leaderboardContainer);

            if (PC.CombineRelations)
            {
                PC.CombineRelations = false;
                ToggleCombinedIcons();
            }

            // Subscribe to player picture click event & logo clicked event from PanelViewController
            //PanelViewController.OnPlayerPictureClicked += () => psmvc.ppmvc.ShowPlayer(PlayerSocialLife.PlayerID, this);
            //PanelViewController.OnLogoClicked += () => pmmvc.ShowMilestoneModal(PlayerSocialLife.PlayerID, this);

            LeaderboardShownPatch.LeaderboardShown += () =>
            {
                titleContainer.SetActive(true);

                if (!TryUpdateCurrentMap() && refreshRequested)
                    Task.Run(ForceRefresh);
            };
            LeaderboardHiddenPatch.LeaderboardHidden += () =>
            {
                titleContainer.SetActive(false);
            };

            // Subscribe to the websocket
            AccSaberStore.OnPlayerScoreUpdated += token =>
            {
                currentPlayerScore = token;
                currentPage = 1;
                Task.Run(async () =>
                {
                    if (!await ForceRefresh(true))
                        refreshRequested = true;
                });
            };

            //MiscUtils.Parse(ResourcePaths.BSML_LEADERBOARD_CELL, leaderboard.transform, );

            // Subscribe to map selection event
            TrySubscribeToMapSelection();
            // Optionally, load leaderboard for the current map if available
            TryUpdateCurrentMap();
        }

        [UIAction("OnPageTop")]
        private void OnPageTop()
        {
            if (page == 1 || CurrentHash is null) return; // Already on the first page
            page = 1;
            if (UsesPreviousPages)
                previousPages.Clear();
            ReloadLeaderboard();
        }

        [UIAction("OnPageUp")]
        private void OnPageUp()
        {
            if (page == 1 || CurrentHash is null) return; // Can't go back from the first page
            page = UsesPreviousPages ? previousPages.Pop() : page - 1;
            ReloadLeaderboard();
        }

        [UIAction("OnYouClicked")]
        private void OnYouClicked()
        {
            if (currentPlayerPage < 1 || page == currentPlayerPage || CurrentHash is null) return;
            if (UsesPreviousPages)
                previousPages.Push(page);
            page = currentPlayerPage;
            ReloadLeaderboard();
        }

        [UIAction("OnPageDown")]
        private void OnPageDown()
        {
            if (scoreDatas.Count < PAGE_LENGTH || CurrentHash is null)
                return;
            if (UsesPreviousPages)
                previousPages.Push(page);
            page = nextPage;
            ReloadLeaderboard();
        }

        [UIAction("ShowGlobal")]
        private void ShowGlobal() => ChangeFilter(LeaderboardDisplayType.Global);

        [UIAction("ShowFollowed")]
        private void ShowFollowed() => ChangeFilter(LeaderboardDisplayType.Followed);

        [UIAction("ShowRivals")]
        private void ShowRivals() => ChangeFilter(LeaderboardDisplayType.Rivals);

        [UIAction("ShowRelations")]
        private void ShowRelations() => ChangeFilter(LeaderboardDisplayType.Relations);

        [UIAction("ShowCountry")]
        private void ShowCountry() => ChangeFilter(LeaderboardDisplayType.Country);

        #endregion UI Actions



        #region Private Methods

        public void Initialize()
        {
            Plugin.Log.Debug("LeaderboardViewController Init");
            Instance = this;

            aspvc.OnPanelLogoClicked += () => lumc.ShowModal(leaderboard.transform, this, store.GetCurrentUserAsync().Result.PlayerId);
        }
        public void Dispose()
        {
            
        }

        private GameObject? GetHeaderPane()
        {
            // Code for finding this header taken from: https://github.com/BeatLeader/beatleader-mod/blob/1.29.4/Source/2_Core/Managers/Leaderboard/LeaderboardHeaderManager.cs
            HMUI.Screen screen = gameObject.GetComponentInParent<HMUI.Screen>();
            if (screen is null) return null;

            //var leaderboardVC = screen.transform.FindChildRecursive("PlatformLeaderboardViewController");
            Transform leaderboardVC = screen.transform.Find("PlatformLeaderboardViewController");
            if (leaderboardVC is null) return null;

            //GameObject header = leaderboardVC.transform.FindChildRecursive("HeaderPanel").gameObject;
            return leaderboardVC.transform.Find("HeaderPanel").gameObject;
        }

        private void HandleHeaderPane()
        {
            GameObject? headerPane = GetHeaderPane();
            if (headerPane is null) return;

            titlePaneTitleText = headerPane.GetComponentInChildren<TextMeshProUGUI>();
            ImageView headerBG = headerPane.GetComponentInChildren<ImageView>();

            LeaderboardShownPatch.LeaderboardShown += () =>
            {
                titlePanelTitle = titlePaneTitleText.text;
                titlePaneTitleText.SetText(RANKED_HEADER);

            };
            LeaderboardHiddenPatch.LeaderboardHidden += () =>
            {
                titlePaneTitleText.SetText(titlePanelTitle);
                titlePanelTitle = null;
            };

            VersionUtils.Parse(ResourcePaths.LEADERBOARD_TITLE_PANEL, headerPane.transform, this);
        }

        private void ChangeFilter(LeaderboardDisplayType type)
        {
            if (DisplayType == type || CurrentHash is null)
                return;
            page = 1;
            currentPage = 0;
            UpdateSelectors(type);
            FullyReloadLeaderboard();
        }

        private ClickableImage? GetSelector(LeaderboardDisplayType displayType) => displayType switch
        {
            LeaderboardDisplayType.Global => globalSelector,
            LeaderboardDisplayType.Country => countrySelector,
            LeaderboardDisplayType.Followed => followedSelector,
            LeaderboardDisplayType.Rivals => rivalsSelector,
            LeaderboardDisplayType.Relations => relationsSelector,
            _ => null
        };

        private void UpdateSelectors(LeaderboardDisplayType newDisplayType)
        {
            ClickableImage? ci = GetSelector(DisplayType);

            if (ci is null)
                return;

            ci.DefaultColor = selectorDefaultColor;

            if (UsesPreviousPages)
                previousPages.Clear();

            ci = GetSelector(newDisplayType);

            if (ci is null)
                return;

            selectorDefaultColor = ci.DefaultColor;
            ci.DefaultColor = ci.HighlightColor;

            DisplayType = newDisplayType;
        }

        private void FullyReloadLeaderboard()
        {
            Task.Run(async () =>
            {
                currentPlayerPage = await GetPlayerPage(false);
                await LoadLeaderboardAsync();
            });
        }

        private void ReloadLeaderboard() => Task.Run(LoadLeaderboardAsync);

        private void TrySubscribeToMapSelection()
        {
            if (sldvc is not null)
            {
#if NEW_VERSION
                void Handler1(StandardLevelDetailViewController controller)
                {
                    TryUpdateCurrentMap();
                }
#else
                void Handler1(StandardLevelDetailViewController controller, IDifficultyBeatmap beatmap)
                {
                    if (beatmap is not null)
                        UpdateDiff(beatmap);
                }
#endif
                void Handler2(StandardLevelDetailViewController controller, StandardLevelDetailViewController.ContentType contentType)
                {
                    if (contentType is > StandardLevelDetailViewController.ContentType.Loading and < StandardLevelDetailViewController.ContentType.Error)
                        TryUpdateCurrentMap();
                }

                sldvc.didChangeDifficultyBeatmapEvent -= Handler1;
                sldvc.didChangeContentEvent -= Handler2;

                sldvc.didChangeDifficultyBeatmapEvent += Handler1;
                sldvc.didChangeContentEvent += Handler2;
            }
        }

        private bool TryUpdateCurrentMap()
        {
#if NEW_VERSION
            if (sldvc is not null && sldvc.beatmapLevel is not null && sldvc.beatmapKey != default)
                return UpdateDiff(sldvc.beatmapLevel, sldvc.beatmapKey);
#else
            if (sldvc is not null && sldvc.selectedDifficultyBeatmap is not null)
                return UpdateDiff(sldvc.selectedDifficultyBeatmap);
#endif
            return false;
        }

#if NEW_VERSION
        private bool UpdateDiff(BeatmapLevel beatmap, BeatmapKey key)
        {
#else

        private bool UpdateDiff(IDifficultyBeatmap beatmap)
        {
#endif
            //Plugin.Log.Info("Update called.");
            if (!gameObject.activeSelf)
                return false;

            // Get hash from the level (custom levels use levelID format: "custom_level_HASH")
#if NEW_VERSION
            string levelId = beatmap.levelID;
#else
            string levelId = beatmap.level.levelID;
#endif
            string hash;
            if (levelId.Contains('_'))
                hash = levelId.Split('_')[2];
            else
                hash = levelId; // fallback for official levels

#if NEW_VERSION
            if (hash.Equals(currentHash) && key.difficulty.Equals(currentDifficulty))
                return false; // same map, no need to update
            currentDifficulty = key.difficulty;
#else
            if (hash.Equals(CurrentHash) && beatmap.difficulty.Equals(CurrentDiff))
                return false; // same map, no need to update
            CurrentDiff = beatmap.difficulty;
#endif
            CurrentHash = hash;

            page = 1; // reset to first page on map change
            currentPage = 0;
            currentPlayerPage = 0;

            // reload leaderboard for the new map
            Task.Run(ForceRefresh);
            return true;
        }

        private async Task<bool> ForceRefresh() => await ForceRefresh(true);

        private async Task<bool> ForceRefresh(bool overridePlayerScore)
        {
            AsyncLock.Releaser? theLock = await forceRefreshLock.LockAsync();
            if (theLock is null || !gameObject.activeSelf) return false;
            using (theLock.Value)
            {
                difficultyInfo = await GetLeaderboard(CurrentHash!, CurrentDiff);
                bool ranked = difficultyInfo is not null;

                OnMapChanged?.Invoke(ranked);

                if (!ranked)
                {
                    CurrentHash = null;
                    CurrentDiff = default;
                    IEnumerator ShowBad()
                    {
                        yield return new WaitForEndOfFrame();

                        titlePaneTitleText?.SetText(UNRANKED_HEADER);

                        leaderboardContainer.SetActive(false);
                        badMapMessage.SetActive(true);
                    }
                    StartCoroutine(ShowBad());
                    return true;
                }

                ShowLoading();

                currentPlayerPage = await GetPlayerPage(overridePlayerScore);

                await LoadLeaderboardAsync();
            }
            return true;
        }

        private void ShowLoading()
        {
            if (leaderboardLoader.activeSelf || DifficultyId is null)
                return;

            int relationLen = PlayerSocialLife.GetIds_Internal(DisplayType)?.Count ?? -1;

            bool gotCachedData = DisplayType != LeaderboardDisplayType.Country ?
                ScoreDataCached(DifficultyId, page, CurrentFilter, relationLen) : ScoreDataCached(DifficultyId, page, store.GetCurrentUserAsync().GetAwaiter().GetResult().Country);

            IEnumerator StartLoading()
            {
                yield return new WaitForEndOfFrame();

                badMapMessage.SetActive(false);

                if (!gotCachedData)
                {
                    leaderboardContainer.SetActive(false);
                    leaderboardLoader.SetActive(true);
                }
            }
            StartCoroutine(StartLoading());
        }

        private async Task LoadLeaderboardAsync()
        {
            if (page == currentPage || DifficultyId is null) return; // already on this page or no leaderboard selected, no need to reload
            AsyncLock.Releaser? theLock = await loadLeaderboardLock.LockAsync();
            if (theLock is null) return;
            using (theLock.Value)
            {
                try
                {
                    currentPage = page;

                    ShowLoading();

                    await PlayerSocialLife.LoadTask;

                    scoreDatas.Clear();

                    AccSaberLeaderboardEntry[]? scores;
                    (AccSaberLeaderboardEntry[] scores, int truePage) scoreData;

                    switch (DisplayType)
                    {
                        case LeaderboardDisplayType.Global:
                            scores = await GetScoreData(page - 1, DifficultyId);
                            nextPage = page + 1;
                            break;

                        case LeaderboardDisplayType.Relations:
                            int neededScores = Math.Min(PlayerSocialLife.GetIds_Internal(DisplayType)!.Count - previousPages.Count * PAGE_LENGTH, PAGE_LENGTH);

                            scoreData = await GetScoreData(page - 1, DifficultyId, CurrentFilter!, DisplayType, neededScores);
                            scores = scoreData.scores;
                            nextPage = scoreData.truePage;

                            break;

                        case LeaderboardDisplayType.Followed:
                        case LeaderboardDisplayType.Rivals:
                            scores = await GetScoreData(page - 1, DifficultyId, DisplayType.Convert());
                            nextPage = page + 1;
                            break;

                        case LeaderboardDisplayType.Country:
                            string country = store.GetCurrentUserAsync().GetAwaiter().GetResult().Country;

                            scores = await GetScoreData(page - 1, DifficultyId, country);
                            nextPage = page + 1;

                            break;

                        default:
                            scores = null;
                            break;
                    }
                    if (scores is not null)
                        scoreDatas.AddRange(scores.Select(score => new LeaderboardEntryDisplay(score)));

                    bool knowCurrentPlayerPage = currentPlayerPage > 0 || currentPlayerPage <= 0 && AttemptToSetPlayerPage();

                    IEnumerator ReloadData()
                    {
                        yield return new WaitForEndOfFrame();

                        SetSelectorButtonSelectability(knowCurrentPlayerPage);

                        yield return new WaitForFixedUpdate();

                        leaderboard.PrefNumberOfCells = OnPlayerPage ? PAGE_LENGTH : PAGE_LENGTH + 2;
                        leaderboard.MainCellSize = CellSize;
                        leaderboard.Data = LeaderboardInfos;

                        titlePaneTitleText?.SetText("Accsaber");

                        leaderboardContainer.SetActive(true);
                        leaderboardLoader.SetActive(false);
                    }

                    StartCoroutine(ReloadData());
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error($"Error loading leaderboard: {ex}");
                }
            }
        }

        private void SetSelectorButtonSelectability(bool knownPlayerPage)
        {
            Color greyedOut = GREYED_OUT.Color();

            bool atTop = currentPage == 1, atBottom = scoreDatas.Count < PAGE_LENGTH;

            if (!atTop)
            {
                pageTopSelector.DefaultColor = defaultPageTopColor;
                pageTopSelector.HighlightColor = highlightPageTopColor;

                pageUpSelector.interactable = true;
            }
            else
            {
                pageTopSelector.DefaultColor = greyedOut;
                pageTopSelector.HighlightColor = greyedOut;

                pageUpSelector.interactable = false;
            }

            if (knownPlayerPage)
            {
                pageYouSelector.DefaultColor = defaultPageYouColor;
                pageYouSelector.HighlightColor = highlightPageYouColor;
            }
            else
            {
                pageYouSelector.DefaultColor = greyedOut;
                pageYouSelector.HighlightColor = greyedOut;
            }

            pageDownSelector.interactable = !atBottom;
        }

        private bool AttemptToSetPlayerPage()
        {
            if (OnPlayerPage)
                currentPlayerPage = page;
            else if (TryGetRankWithFilter(DifficultyId!, PlayerSocialLife.PlayerID!, CurrentFilter, out int rank))
                currentPlayerPage = (int)Math.Ceiling(rank / (float)PAGE_LENGTH);
            else return false;
            return true;
        }

        private async Task<int> GetPlayerPage(bool overrideLastScore)
        {
            await PlayerSocialLife.LoadTask;

            if (overrideLastScore || currentPlayerScore is null)
                currentPlayerScore = await GetScoreData(PlayerSocialLife.PlayerID!, CurrentHash!, CurrentDiff);
            if (currentPlayerScore is null) return -1; // Player has no score on this map
            return DisplayType switch
            {
                LeaderboardDisplayType.Global => (int)Math.Ceiling(currentPlayerScore.Rank / (float)PAGE_LENGTH),
                _ => 0 // At this point, we do not have the information needed to get the rank of any other display type.
            };
        }
        #endregion Private Methods

        private class TextSpacer : ICellDataSource
        {
            public string TemplatePath => "<vertical anchor-pos-y='0.5'><text text='...' align='Center' font-size='3'/></vertical>";

            public float CellSize => 1.5f;

            public int TemplateId { get; set; }
        }
    }

    /*internal sealed class AccSaberLeaderboardViewController : BSMLAutomaticViewController, IInitializable, IDisposable
	{
		private int _pageNumber = 0;
		private int _selectedCellIndex;
		private List<Button>? _infoButtons;
		private List<ImageView>? _pfps;
		private LoadingControl? _loadingControl;

		private AccSaberStore _accSaberStore = null!;
		private AccSaberPanelViewController _accSaberPanelView = null!;
		private List<ILeaderboardSource> _leaderboardSources = null!;
		private LeaderboardUserModalController _leaderboardUserModalController = null!;
		private float _progressTarget = 0;
		private float _progressDuration = 15;
		private float _progress = 1;

		[Inject]
        public void Construct(AccSaberStore accSaberStore, List<ILeaderboardSource> leaderboardSources, LeaderboardUserModalController leaderboardUserModalController, AccSaberPanelViewController accSaberPanelView)
        {
			_accSaberStore = accSaberStore;
			_accSaberPanelView = accSaberPanelView;
			_leaderboardSources = leaderboardSources;
			_leaderboardUserModalController = leaderboardUserModalController;
		}

		[UIComponent("leaderboard")]
		private readonly LeaderboardTableView? _leaderboard = null!;

		[UIComponent("milestone-container")]
		private readonly HorizontalLayoutGroup? _milestoneContainer = null!;

		[UIComponent("vertical-icon-segments")]
		private readonly IconSegmentedControl? _iconSegmentedControl = null!;

		[UIComponent("milestone")]
		private readonly TextMeshProUGUI? _milestone = null!;

		[UIComponent("milestone-desc")]
		private readonly TextMeshProUGUI? _milestoneDesc = null!;

		[UIComponent("progress-bar")]
		private readonly LayoutElement _progressBar = null!;

		[UIComponent("progress-bar-inverse")]
		private readonly LayoutElement _progressBarInverse = null!;

		[UIComponent("progress-bar")]
		private readonly ImageView _progressBarImage = null!;

		#region Info Buttons

		// Maybe get around to making a custom leaderboard and get rid of this misery
		[UIComponent("button1")]
		private readonly Button? _button1 = null!;

		[UIComponent("button2")]
		private readonly Button? _button2 = null!;

		[UIComponent("button3")]
		private readonly Button? _button3 = null!;

		[UIComponent("button4")]
		private readonly Button? _button4 = null!;

		[UIComponent("button5")]
		private readonly Button? _button5 = null!;

		[UIComponent("button6")]
		private readonly Button? _button6 = null!;

		[UIComponent("button7")]
		private readonly Button? _button7 = null!;

		[UIComponent("button8")]
		private readonly Button? _button8 = null!;

		[UIComponent("button9")]
		private readonly Button? _button9 = null!;

		[UIComponent("button10")]
		private readonly Button? _button10 = null!;

		#endregion Info Buttons

		#region Profile Pictures

		[UIComponent("pfp1")]
		private readonly ImageView? _pfp1 = null!;

		[UIComponent("pfp2")]
		private readonly ImageView? _pfp2 = null!;

		[UIComponent("pfp3")]
		private readonly ImageView? _pfp3 = null!;

		[UIComponent("pfp4")]
		private readonly ImageView? _pfp4 = null!;

		[UIComponent("pfp5")]
		private readonly ImageView? _pfp5 = null!;

		[UIComponent("pfp6")]
		private readonly ImageView? _pfp6 = null!;

		[UIComponent("pfp7")]
		private readonly ImageView? _pfp7 = null!;

		[UIComponent("pfp8")]
		private readonly ImageView? _pfp8 = null!;

		[UIComponent("pfp9")]
		private readonly ImageView? _pfp9 = null!;

		[UIComponent("pfp10")]
		private readonly ImageView? _pfp10 = null!;

		#endregion Profile Pictures

		private int SelectedCellIndex
		{
			get => _selectedCellIndex;
			set
			{
				_selectedCellIndex = value;
				PageNumber = 0;
			}
		}

		private int PageNumber
		{
			get => _pageNumber;
			set
			{
				_pageNumber = value;

				if (_leaderboard is null || _loadingControl is null || _accSaberStore.CurrentRankedMap is null)
				{
					return;
				}

				_leaderboard.SetScores(new List<LeaderboardTableView.ScoreData>(), 0);
				LeaderboardShowLoading();
				_ = SetScores();
			}
		}

		[UIValue("up-enabled")]
		private bool UpEnabled => PageNumber != 0;

		[UIValue("down-enabled")]
		private bool DownEnabled => PageNumber + 1 < _leaderboardSources[SelectedCellIndex].TotalPages;

		[UIAction("#post-parse")]
		private void PostParse()
		{
			IEnumerator DoPostParse()
			{
                List<IconSegmentedControl.DataItem> list = [];
                foreach (ILeaderboardSource leaderboardSource in _leaderboardSources)
                {
                    list.Add(new IconSegmentedControl.DataItem(leaderboardSource.Icon.GetAwaiter().GetResult(), leaderboardSource.HoverHint));
                }

                yield return new WaitForFixedUpdate();
                yield return new WaitForEndOfFrame();

                _iconSegmentedControl!.SetData([.. list]);

                // To set rich text, I have to iterate through all cells, set each cell to allow rich text and next time they will have it
                LeaderboardTableCell[]? leaderboardTableCells = _leaderboard!.transform.GetComponentsInChildren<LeaderboardTableCell>(true);

                foreach (LeaderboardTableCell? leaderboardTableCell in leaderboardTableCells)
                {
                    leaderboardTableCell.transform.Find("PlayerName").GetComponent<CurvedTextMeshPro>().richText = true;
                }

                _loadingControl = _leaderboard.transform.GetComponentInChildren<LoadingControl>(true);

				yield return new WaitForSeconds(0.5f);
				yield return new WaitForEndOfFrame();

                _infoButtons = new List<Button>(10);
                ChangeButtonScale(_button1!, 0.425f);
                ChangeButtonScale(_button2!, 0.425f);
                ChangeButtonScale(_button3!, 0.425f);
                ChangeButtonScale(_button4!, 0.425f);
                ChangeButtonScale(_button5!, 0.425f);
                ChangeButtonScale(_button6!, 0.425f);
                ChangeButtonScale(_button7!, 0.425f);
                ChangeButtonScale(_button8!, 0.425f);
                ChangeButtonScale(_button9!, 0.425f);
                ChangeButtonScale(_button10!, 0.425f);

				yield return new WaitForFixedUpdate();

                _pfps = new List<ImageView>(10);
                SetImageSprites(_pfp1!, 0.225f);
                SetImageSprites(_pfp2!, 0.225f);
                SetImageSprites(_pfp3!, 0.225f);
                SetImageSprites(_pfp4!, 0.225f);
                SetImageSprites(_pfp5!, 0.225f);
                SetImageSprites(_pfp6!, 0.225f);
                SetImageSprites(_pfp7!, 0.225f);
                SetImageSprites(_pfp8!, 0.225f);
                SetImageSprites(_pfp9!, 0.225f);
                SetImageSprites(_pfp10!, 0.225f);
            }

			StartCoroutine(DoPostParse());
        }

		[UIAction("up-clicked")]
		private void UpClicked()
		{
			if (UpEnabled)
			{
				PageNumber--;
			}
		}

		[UIAction("down-clicked")]
		private void DownClicked()
		{
			if (DownEnabled)
			{
				PageNumber++;
			}
		}

		[UIAction("cell-selected")]
		private void OnCellSelected(SegmentedControl _, int index)
		{
			SelectedCellIndex = index;
		}

		#region Info Buttons Clicked

		[UIAction("b-1-click")]
		private void B1Clicked()
		{
			InfoButtonClicked(0);
		}

		[UIAction("b-2-click")]
		private void B2Clicked()
		{
			InfoButtonClicked(1);
		}

		[UIAction("b-3-click")]
		private void B3Clicked()
		{
			InfoButtonClicked(2);
		}

		[UIAction("b-4-click")]
		private void B4Clicked()
		{
			InfoButtonClicked(3);
		}

		[UIAction("b-5-click")]
		private void B5Clicked()
		{
			InfoButtonClicked(4);
		}

		[UIAction("b-6-click")]
		private void B6Clicked()
		{
			InfoButtonClicked(5);
		}

		[UIAction("b-7-click")]
		private void B7Clicked()
		{
			InfoButtonClicked(6);
		}

		[UIAction("b-8-click")]
		private void B8Clicked()
		{
			InfoButtonClicked(7);
		}

		[UIAction("b-9-click")]
		private void B9Clicked()
		{
			InfoButtonClicked(8);
		}

		[UIAction("b-10-click")]
		private void B10Clicked()
		{
			InfoButtonClicked(9);
		}

		#endregion Info Buttons Clicked

		protected override async void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
		{
			base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);

			_ = await _accSaberStore.HasAccSaberUpdated();

			if (!firstActivation)
				return;

			PageNumber = 0;
		}

		protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
		{
			base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);
			_leaderboardUserModalController.HideModal();
		}

		private async Task SetScores(List<AccSaberLeaderboardEntry>? leaderboardEntries = null)
		{
			if (leaderboardEntries is null && _accSaberStore.CurrentRankedMap is not null)
			{
				leaderboardEntries = (await _leaderboardSources[SelectedCellIndex].GetScoresAsync(_accSaberStore.CurrentRankedMap, page: PageNumber))?.ToList();
			}

			var scores = new List<LeaderboardTableView.ScoreData>();
			var userScorePos = -1;
			if (leaderboardEntries is null || leaderboardEntries.Count == 0)
			{
				if (_accSaberStore.CurrentRankedMap is not null && _accSaberStore.CurrentRankedMap.RankedStatus != "RANKED")
				{
					scores.Add(new LeaderboardTableView.ScoreData(0, "  Unranked maps do not have leaderboards", 0, false));
					ToggleInfoButtons(false);
				}
				else
				{
					scores.Add(new LeaderboardTableView.ScoreData(0, "", 0, false));
					ToggleInfoButtons(false);
				}
			}
			else
			{
				var userInfo = await _accSaberStore.GetPlatformUserInfo();
				var userId = userInfo?.platformUserId;

				for (var i = 0; i < (leaderboardEntries.Count > 10 ? 10 : leaderboardEntries.Count); i++)
				{
					var pm = "";

					if (leaderboardEntries[i].ModifierIds.Contains("c156e2d8-6852-4488-8e73-927f44e6ed93"))
						pm = "<color=#646464>[PM]</color>";

					scores.Add(new LeaderboardTableView.ScoreData(leaderboardEntries[i].Score, $"<size=85%>{leaderboardEntries[i].PlayerName} -  <size=75%>(<color=#FFD42A>{leaderboardEntries[i].Accuracy * 100:F2}%</color>)</size></size> - <size=75%> (<color=#00FFAE>{leaderboardEntries[i].AP:F2}<size=55%> AP</size></color>) {pm}</size>", leaderboardEntries[i].Rank, false));

					if (_infoButtons != null)
					{
						_infoButtons[i].gameObject.SetActive(true);
						var hoverHint = _infoButtons[i].GetComponent<HoverHint>();
						hoverHint.text = $"Score Set: {leaderboardEntries[i].TimeSet}";
					}

					if(_pfps != null)
                    {
						_pfps[i].gameObject.SetActive(true);
						_ = _pfps[i].SetImageAsync(leaderboardEntries[i].AvatarURL, true);
                    }

					if (leaderboardEntries[i].PlayerId == userId)
					{
						userScorePos = i;
					}
				}
			}

			if (_loadingControl != null && _leaderboard != null)
			{
				_loadingControl?.Hide();
                _leaderboard.SetScores(scores, userScorePos);
				NotifyPropertyChanged(nameof(UpEnabled));
				NotifyPropertyChanged(nameof(DownEnabled));
			}
		}

		private void LeaderboardShowLoading()
		{
			if (_loadingControl == null || _infoButtons == null)
				return;

			_loadingControl?.ShowLoading();
			ToggleInfoButtons(false);
			TogglePfps(false);
		}

		private void ToggleInfoButtons(bool value)
		{
			if (_infoButtons == null)
				return;

			foreach (var button in _infoButtons)
			{
				button.gameObject.SetActive(value);
			}
		}
		private void TogglePfps(bool value)
		{
			if (_pfps == null)
				return;

			foreach (var pfp in _pfps)
			{
				pfp.gameObject.SetActive(value);
			}
		}
		private void ChangeButtonScale(Button button, float scale)
		{
			var buttonTransform = button.transform;
			var localScale = buttonTransform.localScale;
			buttonTransform.localScale = localScale * scale;
			_infoButtons?.Add(button);
		}
		private void SetImageSprites(ImageView pfp, float scale)
		{
			pfp.material = Resources.FindObjectsOfTypeAll<Material>().Last(x => x.name == "UINoGlowRoundEdge");
			var pfpTransform = pfp.transform;
			var localScale = pfpTransform.localScale;
			pfpTransform.localScale = localScale * scale;
			_pfps?.Add(pfp);
		}
		private void InfoButtonClicked(int index)
		{
			if (_infoButtons is null)
			{
				return;
			}

			var playerId = _leaderboardSources[SelectedCellIndex].GetLastCachedScore(index)?.PlayerId;
			if (playerId is null)
			{
				return;
			}

			_leaderboardUserModalController.ShowModal(_infoButtons[index].transform, playerId);
		}

		private void AccSaberStoreOnOnAccSaberRankedMapUpdated(AccSaberBasicDifficulty? diff)
		{
			PageNumber = 0;
		}
		void Update()
		{
			if (_progressDuration < 0)
			{
				_progressDuration = 0.001f;
			}
			_progress = Mathf.MoveTowards(_progress, _progressTarget, (1 / _progressDuration) * Time.deltaTime);

			const float barLen = 75f;
			_progressBar.transform.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, barLen * _progress);
			_progressBarInverse.transform.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, barLen * (1 - _progress));
		}

		async Task ShowMilestone(AccSaberMilestone milestone)
        {
			var _color = milestone.Tier switch
			{
				"bronze" => "#cd7f32",
				"silver" => "#c0c0c0",
				"gold" => "#ffd700",
				"platinum" => "#36cfb0",
				"diamond" => "#b9f2ff",
				"apex" => "#a855f7",
				_ => "#f472b6",
			};

			await Task.Delay(200);
			_milestoneContainer!.gameObject.SetActive(true);
			_progress = 1;
			_milestone!.text = $"<color={_color}>{milestone.Title}</color>";
			_milestoneDesc!.text = milestone.Description;
			if (ColorUtility.TryParseHtmlString(_color, out Color newCol))
				_progressBarImage.color = newCol;
			await Task.Delay(15000);
			_milestone!.text =	"";
			_milestoneDesc!.text = "";
			_milestoneContainer!.gameObject.SetActive(false);
		}

		private async void AccSaberStoreOnOnAccSaberScoreUpdated()
		{
			PageNumber = 0;

			await Task.Delay(5000); // bit of delay to let the milestones update in the backend. will switch to the milestone completion websocket when thats finished
			List<AccSaberMilestone> newMilestones = await _accSaberStore.GetUserMilestones(true);
			if (_accSaberStore._currentUserMilestones.Count < newMilestones.Count)
			{
				var updatedMilestones = newMilestones.Take(newMilestones.Count - _accSaberStore._currentUserMilestones.Count).ToList();

				foreach (var milestone in updatedMilestones)
				{
					await ShowMilestone(milestone);
				}
				_accSaberStore._currentUserMilestones = newMilestones;
			}
		}

		private void AccSaberAuthRelationStatusChanged()
        {
		}

		private void OnPanelLogoClicked()
        {
			if (_leaderboard is null)
				return;

			_leaderboardUserModalController.ShowModal(_leaderboard.transform, _accSaberStore.GetCurrentUserAsync().Result.PlayerId);
		}

		public void Initialize()
		{
			_accSaberStore.OnAccSaberRankedMapUpdated += AccSaberStoreOnOnAccSaberRankedMapUpdated;
			_accSaberStore.OnAccSaberScoreUpdated += AccSaberStoreOnOnAccSaberScoreUpdated;
			PlayerSocialLife.OnRelationChanged += AccSaberAuthRelationStatusChanged;
			_accSaberPanelView.OnPanelLogoClicked += OnPanelLogoClicked;
		}

		public void Dispose()
		{
			_accSaberStore.OnAccSaberRankedMapUpdated -= AccSaberStoreOnOnAccSaberRankedMapUpdated;
			_accSaberStore.OnAccSaberScoreUpdated -= AccSaberStoreOnOnAccSaberScoreUpdated;
            PlayerSocialLife.OnRelationChanged -= AccSaberAuthRelationStatusChanged;
			_accSaberPanelView.OnPanelLogoClicked -= OnPanelLogoClicked;
		}
	}*/
}