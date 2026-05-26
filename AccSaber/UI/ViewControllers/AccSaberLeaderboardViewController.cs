using AccSaber.Configuration;
using AccSaber.Consts;
using AccSaber.Managers;
using AccSaber.Models;
using AccSaber.Utils;
using AccsaberLeaderboard.UI.BSML_Addons.Components;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
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
    internal sealed class AccSaberLeaderboardViewController : BSMLAutomaticViewController, INotifyPropertyChanged, IInitializable
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
        private object updateDiffLock = new();
        private Color selectorDefaultColor, defaultPageTopColor, defaultPageUpColor, defaultPageYouColor, defaultPageDownColor;
        private Color highlightPageTopColor, highlightPageYouColor;
        private AccSaberBasicDifficulty? difficultyInfo;
        private bool refreshRequested = false, loadRequested = false, loaded = false;

        private string? titlePanelTitle;
        private bool titlePanelRich;
        private TextMeshProUGUI? titlePaneTitleText = null;

        public new event PropertyChangedEventHandler? PropertyChanged;
        public string RankedHeader => $"<color={GetColor(CurrentCategory)}>{CurrentCategory}</color> " + RANKED_HEADER;
        public LeaderboardDisplayType DisplayType { get; private set; }
        public string? DifficultyId => difficultyInfo?.DifficultyId;
        public bool ValidMapSelected => !string.IsNullOrEmpty(CurrentHash) && CurrentDiff != default;
        public string? CurrentHash { get; private set; }
        public BeatmapDifficulty CurrentDiff { get; private set; }
        public float CurrentComplexity => difficultyInfo?.Complexity ?? 0f;

        public int AccDecimals => PC.AccDecimals;

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
                    LeaderboardDisplayType.Global => currentPage <= currentPlayerPage && (nextPage > currentPlayerPage || currentPage == nextPage),
                    _ => scoreDatas.FirstOrDefault()?.ScoreData.Rank <= currentPlayerScore?.Rank && scoreDatas.LastOrDefault()?.ScoreData.Rank >= currentPlayerScore?.Rank
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
                    LeaderboardDisplayType.Global => currentPage > currentPlayerPage,
                    _ => scoreDatas.FirstOrDefault()?.ScoreData.Rank > currentPlayerScore?.Rank
                };
            }
        }

        #endregion Instance Variables & Fields

        #region Injects

        [Inject] private readonly PluginConfig PC = null!;
        [Inject] private readonly StandardLevelDetailViewController sldvc = null!;
        [Inject] private readonly AccSaberStore store = null!;
        [Inject] private readonly LeaderboardScoreModalController lsmc = null!; //psmvc

        #endregion Injects

        #region UI Values & Components

        [UIValue("colorGrey")] private const string grey = GREY;
        [UIValue("mapStarColor")] private const string mapStarColor = OVERALL_DIM;

        [UIValue("topArrowPic")] private const string topArrowPic = ResourcePaths.TOP_ARROW;
        [UIValue("youPic")] private const string youPic = ResourcePaths.PLAYER_ICON;
        [UIValue("swapPic")] private const string swapPic = ResourcePaths.SWAP;
        [UIValue("globalPic")] private const string globalPic = ResourcePaths.GLOBAL;
        [UIValue("friendsPic")] private const string friendsPic = ResourcePaths.FRIEND;
        [UIValue("followedPic")] private const string followedPic = ResourcePaths.FOLLOWED;
        [UIValue("rivalsPic")] private const string rivalsPic = ResourcePaths.RIVALS;
        [UIValue("relationsPic")] private const string relationsPic = ResourcePaths.RELATIONS;
        [UIValue("countryPic")] private const string countryPic = ResourcePaths.COUNTRY;
        [UIValue("complexityBG")] private const string complexityBG = ResourcePaths.GRADIENT_CORNER;

        [UIValue("containerWidth")] public const float containerWidth = 80f;
        [UIValue("containerHeight")] public const float containerHeight = 80f;

        [UIValue("complexityFontSize")] public const float complexityFontSize = 5f;

        [UIValue("iconSize")] public const float iconSize = 7f;
        [UIValue("globeIconSize")] public const float globeIconSize = iconSize - 2f;

        //[UIParams] private BSMLParserParams parserParams = null!; // Currently unused.
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

        [UIObject("titleContainer")] private GameObject titleContainer = null!;
        [UIObject("leaderboard")] private GameObject leaderboardContainer = null!;

        [UIComponent("GlobalSelector")] private ClickableImage globalSelector = null!;
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

        private bool _loading, _unranked;
        [UIValue("loading")] private bool Loading
        {
            get => _loading; 
            set
            {
                _loading = value;
                _unranked = false;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Unranked)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Loading)));

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Ranked)));
            }
        }
        [UIValue("unranked")] private bool Unranked
        {
            get => _unranked; 
            set
            {
                _unranked = value;
                _loading = false;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Loading)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Unranked)));

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Ranked)));
            }
        }
        [UIValue("ranked")] private bool Ranked => !_unranked && !_loading;

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

                followedSelector.gameObject.SetActive(!toggle);
                rivalsSelector.gameObject.SetActive(!toggle);
                relationsSelector.gameObject.SetActive(toggle);

                selectorContainer.preferredHeight = toggle ? globeIconSize + iconSize * 2 + 2 : globeIconSize + iconSize * 3 + 2;
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

            globalSelector.DefaultColor = GREY.Color();
            selectorDefaultColor = globalSelector.DefaultColor;
            DisplayType = LeaderboardDisplayType.Global;
            UpdateSelectors(LeaderboardDisplayType.Global);

            highlightPageTopColor = pageTopSelector.HighlightColor;
            highlightPageYouColor = pageYouSelector.HighlightColor;

            defaultPageTopColor = pageTopSelector.DefaultColor;
            defaultPageUpColor = pageUpImage.Image().color;
            defaultPageYouColor = pageYouSelector.DefaultColor;
            defaultPageDownColor = pageDownImage.Image().color;

            pageUpSelector.selectionStateDidChangeEvent += state =>
                pageUpImage.Image().color = state == NoTransitionsButton.SelectionState.Disabled ? GREYED_OUT.Color() : defaultPageUpColor;
            pageDownSelector.selectionStateDidChangeEvent += state =>
                pageDownImage.Image().color = state == NoTransitionsButton.SelectionState.Disabled ? GREYED_OUT.Color() : defaultPageDownColor;

            lsmc?.BindModal(leaderboardContainer);

            if (PC.CombineRelations)
            {
                PC.CombineRelations = false;
                ToggleCombinedIcons();
            }

            lock (updateDiffLock)
                Monitor.PulseAll(updateDiffLock);

            // Subscribe to the websocket
            AccSaberStore.OnPlayerScoreUpdated += token =>
            {
                if (currentPlayerScore is not null && currentPlayerScore.Accuracy > token.Accuracy)
                    return;

                currentPlayerScore = token;
                page = 1;
                currentPage = -1;

                Task.Run(async () =>
                {
                    if (!await ForceRefresh(false))
                        refreshRequested = true;
                });
            };

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
            ReloadLeaderboard();
        }

        [UIAction("OnPageUp")]
        private void OnPageUp()
        {
            if (page == 1 || CurrentHash is null) return; // Can't go back from the first page
            --page;
            ReloadLeaderboard();
        }

        [UIAction("OnYouClicked")]
        private void OnYouClicked()
        {
            if (currentPlayerPage < 1 || page == currentPlayerPage || CurrentHash is null) return;
            page = currentPlayerPage;
            ReloadLeaderboard();
        }

        [UIAction("OnPageDown")]
        private void OnPageDown()
        {
            if (scoreDatas.Count < PAGE_LENGTH || CurrentHash is null)
                return;
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
        }
        public void Dispose()
        {
            
        }

        private void OnEnable()
        {
            titleContainer?.SetActive(true);

            if (titlePaneTitleText is not null)
            {
                titlePanelTitle = titlePaneTitleText.text;
                titlePanelRich = titlePaneTitleText.richText;

                titlePaneTitleText.richText = true;
                titlePaneTitleText.SetText(RankedHeader);
            }

            Task.Run(DoEnableUpdate);
        }
        private void OnDisable()
        {
            titleContainer?.SetActive(false);

            if (titlePaneTitleText is not null)
            {
                titlePaneTitleText.richText = titlePanelRich;
                titlePaneTitleText.SetText(titlePanelTitle);

                titlePanelTitle = null;
            }
        }
        internal void OnGameRefresh()
        {
            InvalidateCache();
            refreshRequested = true;
        }

        private async void DoEnableUpdate()
        {
            IEnumerator WaitUntilValidUpdate()
            {
                yield return new WaitUntil(() => gameObject.activeInHierarchy);
                if (loadRequested)
                {
                    ShowLoading(true);
                    loadRequested = false;
                }
                Task.Run(() => ForceRefresh(false));
            }

            if (loadRequested)
                loadRequested = !ShowLoading(true);

            if (!TryUpdateCurrentMap() && refreshRequested)
            {
                StartCoroutine(WaitUntilValidUpdate());
                refreshRequested = false;
            }
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
            OnEnable();

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
            lock (updateDiffLock)
            {
                if (!loaded)
                    Monitor.Wait(updateDiffLock);

                if (!gameObject.activeInHierarchy)
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
                if (hash.Equals(CurrentHash) && key.difficulty.Equals(CurrentDiff))
                    return false; // same map, no need to update
                CurrentDiff = key.difficulty;
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
                Task.Run(() => ForceRefresh(true));
                return true;
            }
        }

        private async Task<bool> ForceRefresh(bool overridePlayerScore)
        {
            if (!gameObject.activeInHierarchy)
                return false;
            AsyncLock.Releaser? theLock = await forceRefreshLock.TryLockAsync();
            if (theLock is null) 
                return false;
            using (theLock.Value)
            {
                try
                {
                    difficultyInfo = GetLeaderboard(CurrentHash!, CurrentDiff);
                    bool ranked = difficultyInfo is not null;

                    //Plugin.Log.Info($"ranked = {ranked}, diffId = {difficultyInfo?.DifficultyId}");

                    OnMapChanged?.Invoke(ranked);

                    if (!ranked)
                    {
                        CurrentHash = null;
                        CurrentDiff = default;
                        IEnumerator ShowBad()
                        {
                            yield return new WaitForEndOfFrame();

                            titlePaneTitleText?.SetText(UNRANKED_HEADER);

                            Unranked = true;
                        }
                        StartCoroutine(ShowBad());
                        return true;
                    }

                    ShowLoading();

                    currentPlayerPage = await GetPlayerPage(overridePlayerScore);

                    await LoadLeaderboardAsync();
                } catch (Exception e)
                {
                    Plugin.Log.Error(e);
                    return false;
                }
            }
            return true;
        }

        public void LoadUntilNextRefreshIfScoreBeaten(int score)
        {
            if ((currentPlayerScore?.Score ?? -1) < score)
                LoadUntilNextRefresh();
        }
        public void LoadUntilNextRefresh()
        {
            if (!ShowLoading(true))
                loadRequested = true;
        }
        private bool ShowLoading(bool forceLoad = false)
        {
            if (!gameObject.activeInHierarchy)
                return false;

            if (Loading || DifficultyId is null)
                return true; // Return true because load is already happening, or it isn't valid to want to load on an unranked map.

            if (forceLoad)
            {
                Loading = true;

                return true;
            }

            int relationLen = PlayerSocialLife.GetIds_Internal(DisplayType)?.Count ?? -1;

            bool gotCachedData = !forceLoad && (DisplayType != LeaderboardDisplayType.Country ?
                ScoreDataCached(DifficultyId, page, CurrentFilter, relationLen) : ScoreDataCached(DifficultyId, page, store.GetCurrentUserAsync().GetAwaiter().GetResult().Country));

            IEnumerator WaitThenUpdate()
            {
                yield return new WaitForEndOfFrame();

                

                if (!gotCachedData)
                {
                    Loading = true;
                } 
                else
                {
                    Unranked = false;
                }
            }
            StartCoroutine(WaitThenUpdate());

            return true;
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

                    scoreDatas.RemoveRange(0, Math.Min(PAGE_LENGTH, scoreDatas.Count));

                    AccSaberLeaderboardEntry[]? scores;

                    switch (DisplayType)
                    {
                        case LeaderboardDisplayType.Global:
                            scores = await GetScoreData(page, DifficultyId);
                            nextPage = page + 1;
                            break;

                        case LeaderboardDisplayType.Relations:
                            IEnumerable<AccSaberLeaderboardEntry>? totalData;
                            totalData = await GetScoreData(page, DifficultyId, RelationType.follower);
                            totalData = (await GetScoreData(page, DifficultyId, RelationType.rival)).Union(totalData, new EntryComparer());

                            scores = totalData is null ? null : [.. totalData];
                            Array.Sort(scores, (a, b) => a.Rank - b.Rank);

                            nextPage = page + 1;

                            break;

                        case LeaderboardDisplayType.Followed:
                        case LeaderboardDisplayType.Rivals:
                            scores = await GetScoreData(page, DifficultyId, DisplayType.Convert());
                            nextPage = page + 1;
                            break;

                        case LeaderboardDisplayType.Country:
                            string country = store.GetCurrentUserAsync().GetAwaiter().GetResult().Country;

                            scores = await GetScoreData(page, DifficultyId, country);
                            nextPage = page + 1;

                            break;

                        default:
                            scores = null;
                            break;
                    }
                    if (scores is not null)
                        scoreDatas.AddRange(scores.Select(score => new LeaderboardEntryDisplay(score)));

                    bool knowCurrentPlayerPage = currentPlayerPage > 0 || currentPlayerPage == 0 && AttemptToSetPlayerPage();

                    IEnumerator ReloadData()
                    {
                        yield return new WaitForEndOfFrame();

                        SetSelectorButtonSelectability(knowCurrentPlayerPage);

                        yield return new WaitForFixedUpdate();

                        leaderboard.PrefNumberOfCells = OnPlayerPage ? PAGE_LENGTH : PAGE_LENGTH + 2;
                        leaderboard.MainCellSize = CellSize;
                        leaderboard.Data = LeaderboardInfos;

                        titlePaneTitleText?.SetText(RankedHeader);

                        Unranked = false;
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
        private class EntryComparer : IEqualityComparer<AccSaberLeaderboardEntry>
        {
            public bool Equals(AccSaberLeaderboardEntry x, AccSaberLeaderboardEntry y) => x.PlayerId.Equals(y.PlayerId);

            public int GetHashCode(AccSaberLeaderboardEntry obj)
            {
                return obj.PlayerId.GetHashCode();
            }
        }
    }
}