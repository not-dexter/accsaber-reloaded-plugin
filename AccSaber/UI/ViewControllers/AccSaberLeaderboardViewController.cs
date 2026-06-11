using AccSaber.API;
using AccSaber.Configuration;
using AccSaber.Consts;
using AccSaber.Managers;
using AccSaber.Models;
using AccSaber.Utils;
using AccSaber.Utils.Misc;
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
    internal sealed class AccSaberLeaderboardViewController : BSMLAutomaticViewController, INotifyPropertyChanged
    {
#pragma warning disable IDE0044, IDE0051

        #region Static Variables & Properties

        public const float BIG_CELL_SIZE = 5.9f;
        public const float BIG_FONT_SIZE = 3.2f;

        public const float SMALL_CELL_SIZE = 5.3f;
        public const float SMALL_FONT_SIZE = 3f;

        public const string RANKED_HEADER = "Accsaber";
        public const string UNRANKED_HEADER = "Not Accsaber";

        private static TextSpacer Spacer = new();

        #endregion Static Variables & Properties

        #region Instance Variables & Fields

        private readonly List<LeaderboardEntryDisplay> scoreDatas = [];
        private int page, nextPage, currentPage = -1, currentPlayerPage;
        private AccSaberLeaderboardEntry? currentPlayerScore;
        private AsyncLock loadLeaderboardLock = new(), forceRefreshLock = new(), swapCategoriesLock = new();
        private Color selectorDefaultColor, defaultPageTopColor, defaultPageUpColor, defaultPageYouColor, defaultPageDownColor;
        private Color highlightPageTopColor, highlightPageYouColor;
        private AccSaberBasicDifficulty? difficultyInfo;
        private bool refreshRequested = false, loadRequested = false, loaded = false;
        private int refreshVersion, playerScoreVersion;

        private string? titlePanelTitle;
        private bool titlePanelRich;
        private TextMeshProUGUI? titlePaneTitleText = null;

        internal HashSet<(string playerId, Guid diffId)> MissionTargets = [];

        public event Action<bool>? OnMapChanged; // bool == is map ranked or not.

        public new event PropertyChangedEventHandler? PropertyChanged;
        public string RankedHeader => $"{RANKED_HEADER} | <color={GetColor(CurrentCategory)}>{CurrentCategory}</color>";
        public LeaderboardDisplayType DisplayType { get; private set; }
        public Guid? DifficultyId => difficultyInfo?.DifficultyId;
        public bool ValidMapSelected => !string.IsNullOrEmpty(CurrentHash) && CurrentDiff != default;
        public string? CurrentHash { get; private set; }
        public BeatmapDifficulty CurrentDiff { get; private set; }
        public float CurrentComplexity => difficultyInfo?.Complexity ?? 0f;

        public int AccDecimals => PC.AccDecimals;
        public bool ShowCombo => PC.ShowCombo;

        public APCategory? CurrentCategory => difficultyInfo?.Category;

        internal Func<AccSaberLeaderboardEntry, bool>? CurrentFilter
        {
            get
            {
                return DisplayType switch
                {
                    LeaderboardDisplayType.Global => null,
                    LeaderboardDisplayType.Country => currentPlayerScore is null ? null : api.CountryFilterMaker(currentPlayerScore.Country),
                    _ => token => playerInfo.GetIds_Internal(DisplayType)!.Contains(token.PlayerId) //GetIds Internal only returns null if input isn't a valid enum.
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
        [Inject] private readonly AccsaberAPI api = null!;
        [Inject] private readonly PlayerSocialLife playerInfo = null!;
        [Inject] private readonly StandardLevelDetailViewController sldvc = null!;
        [Inject] private readonly AccSaberStore store = null!;
        [Inject] private readonly AccSaberPanelViewController aspvc = null!;
        [Inject] private readonly LeaderboardScoreModalController lsmc = null!;
        [Inject] private readonly LeaderboardSettingsModalController lbsmc = null!;
        [Inject] private readonly MainThreadDispatcher mainThreadDispatcher = null!;

        #endregion Injects

        #region UI Values & Components

        [UIValue("colorGrey")] private const string grey = GREY;
        [UIValue("mapStarColor")] private const string mapStarColor = OVERALL_DIM;

        [UIValue("topArrowPic")] private const string topArrowPic = ResourcePaths.TOP_ARROW;
        [UIValue("youPic")] private const string youPic = ResourcePaths.PLAYER_ICON;
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

        [UIComponent("leaderboard")] private MyCustomCellListTableData leaderboard = null!;

        [UIValue("leaderboard-infos")]
        private List<ICellDataSource> LeaderboardInfos
        {
            get
            {
                IEnumerable<ICellDataSource> outp = scoreDatas;
                if (currentPlayerScore is not null && !OnPlayerPage)
                    return BelowPlayerPage ? [.. outp.Prepend(Spacer).Prepend(new LeaderboardEntryDisplay(currentPlayerScore, this, playerInfo, lbsmc))] :
                        [.. outp.Append(Spacer).Append(new LeaderboardEntryDisplay(currentPlayerScore, this, playerInfo, lbsmc))];
                return [.. outp];
            }
        }

        private float CellSize => OnPlayerPage ? BIG_CELL_SIZE : SMALL_CELL_SIZE;

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
        [UIAction("OnCellHighlightChanged")]
        private void OnCellHighlightChanged(ICellDataSource cell, bool highlighted)
        {
            if (cell is not LeaderboardEntryDisplay dataInfo)
                return;

            const float duration = 0.1f; // in seconds

            IEnumerator FadeToColorUnderline(Color targetColor)
            {
                if (dataInfo.UnderlineFadeRoutine is not null)
                    StopCoroutine(dataInfo.UnderlineFadeRoutine);

                float time = 0;
                Color startColor = dataInfo.UnderlineColor;
                while (time < duration)
                {
                    time += Time.deltaTime;
                    dataInfo.UnderlineColor = Color.Lerp(startColor, targetColor, time / duration);
                    yield return null;
                }

                dataInfo.UnderlineColor = targetColor;
                dataInfo.UnderlineFadeRoutine = null;
            }

            IEnumerator FadeToColorBackground(Color targetColor)
            {
                if (dataInfo.BackgroundFadeRoutine is not null)
                    StopCoroutine(dataInfo.BackgroundFadeRoutine);

                float time = 0;
                Color startColor = dataInfo.BackgroundColor;
                while (time < duration)
                {
                    time += Time.deltaTime;
                    dataInfo.BackgroundColor = Color.Lerp(startColor, targetColor, time / duration);
                    yield return null;
                }

                dataInfo.BackgroundColor = targetColor;
                dataInfo.BackgroundFadeRoutine = null;
            }

            const string underlineHighlightedColor = "#FFFA";

            if (dataInfo.AllowUnderline)
                mainThreadDispatcher.EnqueueRoutine(highlighted ? FadeToColorUnderline(underlineHighlightedColor.Color()) : FadeToColorUnderline(LeaderboardEntryDisplay.DefaultUnderlineColor.Color()));
            
            if (!dataInfo.BGColor.Equals(DIMMER))
                mainThreadDispatcher.EnqueueRoutine(highlighted ? FadeToColorBackground(dataInfo.BGColor.DimColor(-4).Color()) : FadeToColorBackground(dataInfo.BGColor.Color()));
        }

        [UIAction("ToggleCombinedIcons")]
        private async void ToggleCombinedIcons()
        {
            AsyncLock.Releaser? locker = await swapCategoriesLock.TryLockAsync();

            if (locker is null)
                return; // Another toggle is already in progress, so we won't allow this one to execute to prevent issues.

            IEnumerator UpdateUI()
            {
                yield return new WaitForEndOfFrame();

                try
                {
                    followedSelector.gameObject.SetActive(!PC.CombineRelations);
                    rivalsSelector.gameObject.SetActive(!PC.CombineRelations);
                    relationsSelector.gameObject.SetActive(PC.CombineRelations);

                    selectorContainer.preferredHeight = PC.CombineRelations ? globeIconSize + iconSize * 2 + 3 : globeIconSize + iconSize * 3 + 3;

                    if (PC.CombineRelations)
                    {
                        if (DisplayType is LeaderboardDisplayType.Followed or LeaderboardDisplayType.Rivals)
                            ChangeFilter(LeaderboardDisplayType.Relations);
                    }
                    else if (DisplayType is LeaderboardDisplayType.Relations)
                        ChangeFilter(LeaderboardDisplayType.Followed);
                }
                catch (Exception e)
                {
                    Plugin.Log.Error("There was an error swapping the selector icons!\n" + e);
                }
                finally
                {
                    locker.Value.Dispose();
                }
            }
            mainThreadDispatcher.EnqueueRoutine(UpdateUI());
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
                ToggleCombinedIcons();

            // Optionally, load leaderboard for the current map if available
            TryUpdateCurrentMap();
        }

        [UIAction("OnPageTop")]
        private void OnPageTop()
        {
            if (page == 1 || CurrentHash is null) return; // Already on the first page

            Interlocked.Increment(ref refreshVersion);

            page = 1;
            ReloadLeaderboard();
        }

        [UIAction("OnPageUp")]
        private void OnPageUp()
        {
            if (page == 1 || CurrentHash is null) return; // Can't go back from the first page

            Interlocked.Increment(ref refreshVersion);

            --page;
            ReloadLeaderboard();
        }

        [UIAction("OnYouClicked")]
        private void OnYouClicked()
        {
            if (currentPlayerPage < 1 || page == currentPlayerPage || CurrentHash is null) return;

            Interlocked.Increment(ref refreshVersion);

            page = currentPlayerPage;
            ReloadLeaderboard();
        }

        [UIAction("OnPageDown")]
        private void OnPageDown()
        {
            if (scoreDatas.Count < PAGE_LENGTH || CurrentHash is null)
                return;

            Interlocked.Increment(ref refreshVersion);

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

        #region Methods

        private void OnEnable()
        {
            UpdateHeaderTitle();
            DoEnableUpdate();
        }
        private void OnDisable()
        {
            if (titlePaneTitleText is not null)
            {
                TextMeshProUGUI text = titlePaneTitleText;

                text.richText = titlePanelRich;
                text.SetText(titlePanelTitle);

                titlePanelTitle = null;
            }
        }
        private void Awake()
        {
            sldvc?.didChangeDifficultyBeatmapEvent += Handler1;
            sldvc?.didChangeContentEvent += Handler2;
            aspvc?.OnSettingsClicked += OnSettingsClicked;
            store?.OnPlayerScoreUpdated += OnPlayerScoreUpdated;
            lbsmc?.OnCombineRelations += ToggleCombinedIcons;
        }
        private new void OnDestroy()
        {
            base.OnDestroy();

            sldvc?.didChangeDifficultyBeatmapEvent -= Handler1;
            sldvc?.didChangeContentEvent -= Handler2;
            aspvc?.OnSettingsClicked -= OnSettingsClicked;
            store?.OnPlayerScoreUpdated -= OnPlayerScoreUpdated;
            lbsmc?.OnCombineRelations -= ToggleCombinedIcons;
        }
        internal void OnGameRefresh()
        {
            api.InvalidateCache();

            Interlocked.Increment(ref refreshVersion);

            page = 1;
            currentPage = -1;
            refreshRequested = true;
        }

        private void DoEnableUpdate()
        {
            IEnumerator WaitUntilValidUpdate()
            {
                yield return new WaitUntil(() => gameObject.activeInHierarchy);
                yield return new WaitForEndOfFrame();

                try
                {
                    if (loadRequested)
                    {
                        ShowLoading(true);
                        loadRequested = false;
                    }
                    ForceRefresh(false);
                }
                catch (Exception e)
                {
                    Plugin.Log.Error(e);
                }
            }

            if (loadRequested)
                mainThreadDispatcher.EnqueueAction(() => loadRequested = !ShowLoading(true));

            if (!TryUpdateCurrentMap() && refreshRequested)
            {
                mainThreadDispatcher.EnqueueRoutine(WaitUntilValidUpdate());
                refreshRequested = false;
            }
        }

        private void OnSettingsClicked() => lbsmc?.ShowModal(leaderboardContainer.transform);
        private void OnPlayerScoreUpdated(AccSaberLeaderboardEntry token)
        {
            Plugin.Log.Info("Player score recieved");

            mainThreadDispatcher.EnqueueAction(() =>
            {
                if (currentPlayerScore is null || currentPlayerScore.Accuracy <= token.Accuracy)
                {
                    currentPlayerScore = token;
                    playerScoreVersion = refreshVersion;
                }

                store.InvalidateCurrentMapCache();

                _ = RequestRefresh();
            });
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
            return leaderboardVC.transform.Find("HeaderPanel")?.gameObject;
        }

        private void HandleHeaderPane()
        {
            GameObject? headerPane = GetHeaderPane();
            if (headerPane is null) return;

            titlePaneTitleText = headerPane.GetComponentInChildren<TextMeshProUGUI>();
            UpdateHeaderTitle();
        }

        private void UpdateHeaderTitle()
        {
            if (titlePaneTitleText is not null)
            {
                titlePanelTitle = titlePaneTitleText.text;
                titlePanelRich = titlePaneTitleText.richText;

                titlePaneTitleText.richText = true;
                titlePaneTitleText.SetText(RankedHeader);
            }
        }

        private void ChangeFilter(LeaderboardDisplayType type)
        {
            if (DisplayType == type || CurrentHash is null)
                return;

            Interlocked.Increment(ref refreshVersion);

            page = 1;
            currentPage = 0;
            UpdateSelectors(type);
            _ = FullyReloadLeaderboard();
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

        private async Task FullyReloadLeaderboard()
        {
            currentPlayerPage = await GetPlayerPage(false);
            await LoadLeaderboardAsync();
        }

        private void ReloadLeaderboard() => _ = LoadLeaderboardAsync();

#if NEW_VERSION
        private void Handler1(StandardLevelDetailViewController controller)
        {
            TryUpdateCurrentMap();
        }
#else
        private void Handler1(StandardLevelDetailViewController controller, IDifficultyBeatmap beatmap)
        {
            if (beatmap is not null)
                UpdateDiff(beatmap);
        }
#endif
        private void Handler2(StandardLevelDetailViewController controller, StandardLevelDetailViewController.ContentType contentType)
        {
            if (contentType is > StandardLevelDetailViewController.ContentType.Loading and < StandardLevelDetailViewController.ContentType.Error)
                TryUpdateCurrentMap();
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
            try
            {
                if (!loaded || !gameObject.activeInHierarchy)
                    return false;

                // Get hash from the level (custom levels use levelID format: "custom_level_HASH")
#if NEW_VERSION
                string levelId = beatmap.levelID;
#else
                string levelId = beatmap.level.levelID;
#endif
                string hash;
                string[] parts = levelId.Split('_');

                if (parts.Length >= 3 && parts[0].Equals("custom") && parts[1].Equals("level"))
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
                currentPlayerScore = null;

                // reload leaderboard for the new map
                int version = Interlocked.Increment(ref refreshVersion);
                ForceRefresh(true, version);

                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.Error(e);
                return false;
            }
        }

        public async Task RequestRefresh() // Only call from main thread.
        {
            page = 1;
            currentPage = -1;

            if (!gameObject.activeInHierarchy || !await DoRefresh(false))
                refreshRequested = true;
        }
        private void ForceRefresh(bool overridePlayerScore, int version = -1)
        {
            if (!gameObject.activeInHierarchy)
                return;

            _ = ForceRefresh_Internal(overridePlayerScore, version);
        }
        private async Task ForceRefresh_Internal(bool overridePlayerScore, int version)
        {
            try
            {
                _ = DoRefresh(overridePlayerScore, version); // if the return value is needed, use DoRefresh
            }
            catch (Exception e)
            {
                Plugin.Log.Error(e);
            }
        }
        private async Task<bool> DoRefresh(bool overridePlayerScore, int version = -1)
        {
            AsyncLock.Releaser? theLock = await forceRefreshLock.LockAsync();

            if (theLock is null) 
                return false;

            using (theLock.Value)
            {
                try
                {
                    if (!IPA.Utilities.UnityGame.OnMainThread)
                    {
                        Plugin.Log.Critical($"{nameof(DoRefresh)} is not on the main thread!!!");
                        return false;
                    }

                    if (version < 0)
                        version = Interlocked.Increment(ref refreshVersion);

                    string? hash = CurrentHash;
                    BeatmapDifficulty diff = CurrentDiff;

                    if (hash is null)
                        return false;

                    AccSaberBasicDifficulty? newDifficultyInfo = api.GetLeaderboard(hash, diff); // just a dictionary lookup
                    bool ranked = newDifficultyInfo is not null;

                    if (version != refreshVersion)
                        return false;

                    difficultyInfo = newDifficultyInfo;

                    //Plugin.Log.Info($"ranked = {ranked}, diffId = {difficultyInfo?.DifficultyId}");

                    OnMapChanged?.Invoke(ranked);

                    if (!ranked)
                    {
                        CurrentHash = null;
                        CurrentDiff = default;
                        IEnumerator ShowBad()
                        {
                            yield return new WaitForEndOfFrame();

                            try
                            {
                                titlePaneTitleText?.SetText(UNRANKED_HEADER);

                                Unranked = true;
                            }
                            catch (Exception e)
                            {
                                Plugin.Log.Error(e);
                            }
                        }
                        mainThreadDispatcher.EnqueueRoutine(ShowBad());
                        return true;
                    }

                    mainThreadDispatcher.EnqueueAction(() => ShowLoading());

                    currentPlayerPage = await GetPlayerPage(overridePlayerScore, hash, diff, version);

                    if (version != refreshVersion)
                        return false;

                    await LoadLeaderboardAsync();
                } 
                catch (Exception e)
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
                return true;

            if (forceLoad)
            {
                Loading = true;
                return true;
            }

            int version = refreshVersion;
            Guid difficultyId = DifficultyId.Value;
            int requestedPage = page;
            LeaderboardDisplayType displayType = DisplayType;
            Func<AccSaberLeaderboardEntry, bool>? filter = CurrentFilter;
            int relationLen = playerInfo.GetIds_Internal(displayType)?.Count ?? -1;

            _ = UpdateLoadingStateFromCache(
                version,
                difficultyId,
                requestedPage,
                displayType,
                filter,
                relationLen
            );

            return true;
        }

        private async Task UpdateLoadingStateFromCache(
            int version,
            Guid difficultyId,
            int requestedPage,
            LeaderboardDisplayType displayType,
            Func<AccSaberLeaderboardEntry, bool>? filter,
            int relationLen
        )
        {
            try
            {
                bool gotCachedData;

                if (displayType == LeaderboardDisplayType.Country)
                {
                    string country = (await store.GetCurrentUserAsync()).Country;
                    gotCachedData = api.ScoreDataCached(difficultyId, requestedPage, country);
                }
                else
                {
                    gotCachedData = api.ScoreDataCached(difficultyId, requestedPage, filter, relationLen);
                }

                mainThreadDispatcher.EnqueueAction(() =>
                {
                    if (version != refreshVersion)
                        return;

                    if (difficultyId != DifficultyId)
                        return;

                    if (requestedPage != page)
                        return;

                    if (displayType != DisplayType)
                        return;

                    if (!gotCachedData)
                        Loading = true;
                    else
                        Unranked = false;
                });
            }
            catch (Exception e)
            {
                Plugin.Log.Error("Error checking leaderboard cache:\n" + e);

                mainThreadDispatcher.EnqueueAction(() =>
                {
                    if (version == refreshVersion)
                        Loading = true;
                });
            }
        }

        public void ForceShowLeaderboard()
        {
            mainThreadDispatcher.EnqueueAction(() => Unranked = false);
        }
        public async void ShowPlayerPage(string playerId)
        {
            try
            {
                string? hash = CurrentHash;
                BeatmapDifficulty diff = CurrentDiff;
                int version = refreshVersion;

                if (hash is null)
                {
                    Plugin.Log.Warn("Cannot show player page, no ranked map is currently selected.");
                    return;
                }

                using AsyncLock.Releaser theLock = await loadLeaderboardLock.LockAsync();

                ShowLoading();

                AccSaberLeaderboardEntry? playerScore = await api.GetScoreData(playerId, hash, diff);

                if (version != refreshVersion || CurrentHash != hash || !CurrentDiff.Equals(diff))
                    return;

                Interlocked.Increment(ref refreshVersion);

                if (DisplayType != LeaderboardDisplayType.Global)
                {
                    currentPage = 0;
                    UpdateSelectors(LeaderboardDisplayType.Global);
                    currentPlayerPage = await GetPlayerPage(false, hash, diff, refreshVersion);
                }

                if (playerScore is not null)
                    page = (int)Math.Ceiling(playerScore.Rank / (float)PAGE_LENGTH);

                await LoadLeaderboardAsyncNoLock();
            }
            catch (Exception e)
            {
                Plugin.Log.Error("There was an error setting the leaderboard page to target!\n" + e);
            }
        }
        private async Task LoadLeaderboardAsync(bool force = false)
        {
            if ((page == currentPage || DifficultyId is null) && !force) return; // already on this page or no leaderboard selected, no need to reload
            AsyncLock.Releaser theLock = await loadLeaderboardLock.LockAsync();
            using (theLock)
                await LoadLeaderboardAsyncNoLock();
        }

        private async Task LoadLeaderboardAsyncNoLock()
        {
            try
            {
                int version = refreshVersion;
                Guid? difficultyId = DifficultyId;
                int requestedPage = page;
                LeaderboardDisplayType requestedDisplayType = DisplayType;

                if (difficultyId is null)
                    return;

                ShowLoading();

                await playerInfo.LoadTask;

                AccSaberLeaderboardEntry[]? scores;

                switch (requestedDisplayType)
                {
                    case LeaderboardDisplayType.Global:
                        scores = await api.GetScoreData(requestedPage, difficultyId.Value);
                        break;

                    case LeaderboardDisplayType.Relations:
                        scores = await api.GetScoreData(requestedPage, difficultyId.Value, RelationType.follower, RelationType.rival);
                        break;

                    case LeaderboardDisplayType.Followed:
                    case LeaderboardDisplayType.Rivals:
                        scores = await api.GetScoreData(requestedPage, difficultyId.Value, requestedDisplayType.Convert());
                        break;

                    case LeaderboardDisplayType.Country:
                        string country = (await store.GetCurrentUserAsync()).Country;
                        scores = await api.GetScoreData(requestedPage, difficultyId.Value, country);
                        break;

                    default:
                        scores = null;
                        break;
                }

                mainThreadDispatcher.EnqueueRoutine(ReloadData());

                IEnumerator ReloadData()
                {
                    yield return new WaitForEndOfFrame();

                    try
                    {
                        if (version != refreshVersion)
                            yield break;

                        if (difficultyId != DifficultyId)
                            yield break;

                        if (requestedPage != page)
                            yield break;

                        if (requestedDisplayType != DisplayType)
                            yield break;

                        currentPage = requestedPage;
                        nextPage = requestedPage + 1;

                        scoreDatas.Clear();

                        if (scores is not null)
                            scoreDatas.AddRange(scores.Select(score => new LeaderboardEntryDisplay(score, this, playerInfo, lbsmc)));

                        SetSelectorButtonSelectability(currentPlayerPage > 0 || currentPlayerPage == 0 && AttemptToSetPlayerPage());

                        leaderboard.PrefNumberOfCells = OnPlayerPage ? PAGE_LENGTH : PAGE_LENGTH + 2;
                        leaderboard.MainCellSize = CellSize;
                        leaderboard.Data = LeaderboardInfos;

                        titlePaneTitleText?.SetText(RankedHeader);

                        Unranked = false;
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.Error(e);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Error loading leaderboard: {ex}");
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
            else if (api.TryGetRankWithFilter(DifficultyId!.Value, playerInfo.PlayerID!, CurrentFilter, out int rank))
                currentPlayerPage = (int)Math.Ceiling(rank / (float)PAGE_LENGTH);
            else return false;
            return true;
        }

        private async Task<int> GetPlayerPage(bool overrideLastScore) => CurrentHash is not null ? await GetPlayerPage(overrideLastScore, CurrentHash, CurrentDiff, refreshVersion) : 0;
        private async Task<int> GetPlayerPage(bool overrideLastScore, string hash, BeatmapDifficulty diff, int version)
        {
            await playerInfo.LoadTask;

            AccSaberLeaderboardEntry? score = currentPlayerScore;

            if (overrideLastScore || score is null || playerScoreVersion != version)
                score = await api.GetScoreData(playerInfo.PlayerID!, hash, diff);

            if (version != refreshVersion)
                return -1;

            currentPlayerScore = score;
            playerScoreVersion = version;

            if (currentPlayerScore is null)
                return -1; // Player has no score on this map

            return DisplayType switch
            {
                LeaderboardDisplayType.Global => (int)Math.Ceiling(currentPlayerScore.Rank / (float)PAGE_LENGTH),
                _ => 0 // At this point, we do not have the information needed to get the rank of any other display type.
            };
        }
        #endregion

        private class TextSpacer : ICellDataSource
        {
            public string TemplatePath => "<vertical anchor-pos-y='0.5'><text text='...' align='Center' font-size='3'/></vertical>";

            public float CellSize => 1.5f;

            public int TemplateId { get; set; }
        }
    }
}