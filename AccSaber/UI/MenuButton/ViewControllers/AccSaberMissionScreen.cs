using AccSaber.API;
using AccSaber.Consts;
using AccSaber.Managers;
using AccSaber.Models;
using AccSaber.Models.CacheModels;
using AccSaber.UI.ViewControllers;
using AccSaber.Utils;
using AccsaberLeaderboard.UI.BSML_Addons.Components;
using BeatSaberMarkupLanguage.Attributes;
using HMUI;
using IPA.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using IPA.Config.Data;
using System.Security.Policy;
using TMPro;
using BeatSaberMarkupLanguage.ViewControllers;
using System.Collections;





#if NEW_VERSION
using System.Reflection;
#else
using BeatSaberMarkupLanguage;
#endif

namespace AccSaber.UI.MenuButton.ViewControllers
{

    [ViewDefinition("AccSaber.UI.MenuButton.Views.AccSaberMissionScreen.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\AccSaberMissionScreen.bsml")]
    internal class AccSaberMissionScreen : INotifyPropertyChanged, IInitializable, IDisposable
    {
#pragma warning disable IDE0051
        private bool _isLoading, _parsed = false;
        private string _dailyTime = null!, _weeklyTime = null!;
        private DateTime _dailyRefreshDate, _weeklyRefreshDate;

        private CancellationTokenSource? TimeUpdaterCanceller = null;

        private readonly AsyncLock _missionLock = new();
        private static readonly object LoadWaiterLock = new();

        private static SongPresentInfo? _songPresentInfo;

        [UIComponent("daily-list")]
        private readonly MyCustomCellListTableData _dailyList = null!;

        [UIValue("daily-cells")]
        private readonly List<ICellDataSource> _dailyCells = [];

        [UIComponent("weekly-list")]
        private readonly MyCustomCellListTableData _weeklyList = null!;

        [UIValue("weekly-cells")]
        private readonly List<ICellDataSource> _weeklyCells = [];

        public event PropertyChangedEventHandler? PropertyChanged;

        [Inject] private readonly AccSaberStore _accSaberStore = null!;
        [Inject] private readonly AccSaberMainFlowCoordinator _parentFlowCoordinator = null!;
        [Inject] private readonly MainFlowCoordinator _mainFlowCoordinator = null!;

        [UIValue("is-loading")]
        private bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLoading)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsNotLoading)));
            }
        }


        [UIValue("is-not-loading")]
        private bool IsNotLoading => !_isLoading;

        [UIValue("daily-time")]
        private string DailyTime
        {
            get => _dailyTime;
            set
            {
                _dailyTime = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DailyTime)));
            }
        }
        [UIValue("weekly-time")]
        private string WeeklyTime
        {
            get => _weeklyTime;
            set
            {
                _weeklyTime = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WeeklyTime)));
            }
        }

        [UIAction("on-cell-click")]
        private void OnCellClick(ICellDataSource data)
        {
            if (data is not MissionCell cell)
                return;

            switch (cell.Data.Type)
            {
                case >= MissionType.ACC_ON_MAP and <= MissionType.STREAK_ON_MAP or MissionType.COMEBACK_PB:
                    cell.GoToSong();
                    break;
            }
        }

        [UIAction("#post-parse")]
        private void PostParse()
        {
            _parsed = true;
        }

        public async void Initialize()
        {
            SongCore.Loader.SongsLoadedEvent += LoadWaiter;
            //await Task.Run(ShowMissions);
        }
        public void Dispose()
        {
            SongCore.Loader.SongsLoadedEvent -= LoadWaiter;
        }

#if NEW_VERSION
        private static void LoadWaiter(SongCore.Loader loader, ConcurrentDictionary<string, BeatmapLevel> maps)
#else
        private static void LoadWaiter(SongCore.Loader loader, ConcurrentDictionary<string, CustomPreviewBeatmapLevel> maps)
#endif
        {
            lock (LoadWaiterLock)
                Monitor.PulseAll(LoadWaiterLock);
        }

        private void UpdateTimer()
        {
            IEnumerator UpdateTime()
            {
                yield return new WaitForEndOfFrame();

                DailyTime = $"<color={ColorUtils.GREY}>Resets {_dailyRefreshDate.ToRelativeTime(2).ToLower()}</color>";
                WeeklyTime = $"<color={ColorUtils.GREY}>Resets {_weeklyRefreshDate.ToRelativeTime(3).ToLower()}</color>";

                if (_dailyRefreshDate <= DateTime.UtcNow)
                {
                    StopTimer();
                    SetMissions().ContinueWith(finish => UpdateTimer());
                }
            }

            if (TimeUpdaterCanceller is not null) 
            {
                TimeUpdaterCanceller.Cancel();
                TimeUpdaterCanceller.Dispose();
            }
            TimeUpdaterCanceller = new();

            CancellationToken ct = TimeUpdaterCanceller.Token;

            Task.Run(() =>
            {
                while (!ct.IsCancellationRequested)
                {
                    _parentFlowCoordinator.StartCoroutine(UpdateTime());
                    Task.Delay(1000, ct).Wait();
                }
            }, ct);
        }
        public void StopTimer()
        {
            if (TimeUpdaterCanceller is null)
                return;

            TimeUpdaterCanceller.Cancel();
            TimeUpdaterCanceller.Dispose();
            TimeUpdaterCanceller = null;
        }

        public void ShowMissions()
        {
            _ = SetMissions();
        }
        private async Task SetMissions()
        {
            AsyncLock.Releaser? locker = await _missionLock.TryLockAsync();

            if (locker is null)
                return;

            using (locker.Value)
            {
                if (!IsLoading)
                    IsLoading = true;

                _songPresentInfo ??= new(
                _mainFlowCoordinator,
                UnityEngine.Object.FindObjectOfType<SoloFreePlayFlowCoordinator>(),
                UnityEngine.Object.FindObjectOfType<MultiplayerLevelSelectionFlowCoordinator>(),
                _parentFlowCoordinator
                );

                //Plugin.Log.Info($"solo: {_songPresentInfo.Value.SoloCoordinator is null}, multi: {_songPresentInfo.Value.MultiCoordinator is null}, main flow: {_songPresentInfo.Value.MainFlowCoordinator is null}");

                if (_songPresentInfo.Value.SoloCoordinator is null || _songPresentInfo.Value.MultiCoordinator is null || _songPresentInfo.Value.MainFlowCoordinator is null)
                    _songPresentInfo = null;

                bool updateUI = _songPresentInfo is not null && _parsed;

                _dailyCells.Clear();
                _weeklyCells.Clear();

                AccSaberLeaderboardViewController.Instance.MissionTargets.Clear();

                await PlayerSocialLife.LoadTask;

                try
                {
                    List<AccSaberMission> missions = await _accSaberStore.GetMissions();

                    bool setDailyTime = false, setWeeklyTime = false;

                    foreach (AccSaberMission post in missions)
                    {
                        if (updateUI)
                            switch (post.MissionPool)
                            {
                                case MissionPool.Daily: _dailyCells.Add(new MissionCell(post, _songPresentInfo!.Value)); break;
                                case MissionPool.Weekly: _weeklyCells.Add(new MissionCell(post, _songPresentInfo!.Value)); break;
                            }

                        if (!setDailyTime && post.MissionPool == MissionPool.Daily)
                        {
                            _dailyRefreshDate = post.ExpiresAt;
                            setDailyTime = true;
                        }
                        if (!setWeeklyTime && post.MissionPool == MissionPool.Weekly)
                        {
                            _weeklyRefreshDate = post.ExpiresAt;
                            setWeeklyTime = true;
                        }

                        if (post.TargetPlayerId is not null && post.TargetMapDifficultyId is not null)
                            AccSaberLeaderboardViewController.Instance.MissionTargets.Add((post.TargetPlayerId, post.TargetMapDifficultyId));
                    }

                    UpdateTimer();

                    if (updateUI)
                    {
                        _dailyList.Data = _dailyCells;
                        _weeklyList.Data = _weeklyCells;
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.Error(e);
                }
                finally
                {
                    IsLoading = false;
                }
            }
        }

        internal readonly struct SongPresentInfo(MainFlowCoordinator mainFlowCoordinator,
            SoloFreePlayFlowCoordinator soloCoordinator, MultiplayerLevelSelectionFlowCoordinator multiCoordinator,
            AccSaberMainFlowCoordinator parentFlowCoordinator)
        {
            //public readonly ButtonClickedEvent? OpenSolo = openSolo;
            public readonly MainFlowCoordinator MainFlowCoordinator = mainFlowCoordinator;
            public readonly SoloFreePlayFlowCoordinator SoloCoordinator = soloCoordinator;
            public readonly MultiplayerLevelSelectionFlowCoordinator MultiCoordinator = multiCoordinator;
            public readonly AccSaberMainFlowCoordinator ParentFlowCoordinator = parentFlowCoordinator;
        }

        internal class MissionCell(AccSaberMission data, SongPresentInfo songPresentInfo) : ICellDataSource, INotifyPropertyChanged
        {
            public string TemplatePath => ResourcePaths.ACC_SABER_MISSION_CELL;

            public float CellSize => 12;

            public int TemplateId { get; set; }

            private static readonly AsyncLock OpenMapLock = new();

            public readonly AccSaberMission Data = data;
            private readonly SongPresentInfo SongPresentInfo = songPresentInfo;

            private bool _showStatus = false;
            private string _statusText = null!;

            public event PropertyChangedEventHandler? PropertyChanged;

            private readonly string color = data.Band switch
            {
                Utils.MissionBand.extreme => "#ffd700",
                Utils.MissionBand.hard => "#f97316",
                Utils.MissionBand.medium => "#3cb371",
                /*"easy" => "#3cb371",*/
                _ => ColorUtils.GREY
            };

            [UIValue("showProgress")]
            public bool ShowProgress = (data.TargetCount is not null || data.TargetXp is not null) && !data.Completed;

            [UIValue("showStatus")]
            public bool ShowStatus
            {
                get => _showStatus;
                set
                {
                    _showStatus = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowStatus)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NotShowStatus)));
                }
            }

            [UIValue("notShowStatus")]
            public bool NotShowStatus => !ShowStatus;

            [UIValue("statusText")]
            public string StatusText
            {
                get => _statusText;
                set
                {
                    _statusText = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
                }
            }

            [UIValue("target")]
            public string Target = $"{data.ProgressCount}/{data.TargetCount}";

            [UIValue("mission")] public string Mission = $"{data.Name} <size=80%><color={ColorUtils.GetColor(data.Category)}>{data.Category.ToString().ToUpper()}</color></size>";

            [UIValue("missionBand")] public string MissionBand => $"<color={color}>{Data.Band.ToString().ToUpper()}</color>";

            [UIValue("description")] public string Description => $"<color={ColorUtils.GREY}>{DescriptionParser()}</color>";

            private string DescriptionParser()
            {
                if (Data.TargetMapDifficultyId is null)
                    return Data.Description;

                AccSaberBasicDifficulty cachedDiff;

                foreach (string currentHash in SerializerHandler.CachedMaps.Keys)
                {
                    cachedDiff = SerializerHandler.CachedMaps[currentHash].Difficulties.FirstOrDefault(diff => diff.DifficultyId.Equals(Data.TargetMapDifficultyId));

                    if (cachedDiff is not null)
                        return Data.Description.Replace(EnumUtils.DiffToReloadedDiff(cachedDiff.Difficulty), cachedDiff.Difficulty.ToString());
                }

                return Data.Description;
            }

            [UIValue("extraText")]
            public string ExtraText = data.Type switch
            {
                MissionType.SNIPE_PLAYER_ON_MAP => $"<color={ColorUtils.GREY}>(Get <color={ColorUtils.AP}>{data.TargetAp:N2}ap</color> or <color={ColorUtils.GetColor(data.Category)}>{data.TargetAcc:N2}%</color>)</color>",
                _ => "",
            };

            [UIValue("showExtraText")] public bool ShowExtraText => ExtraText.Length > 0;

            [UIValue("missionXP")] public string MissionXP = $"<color={ColorUtils.AP}>+{data.XpReward} XP</color>";

            [UIValue("exactProgress")]
            public string ExactProgress => ShowProgress ? $"<color={ColorUtils.GREY}>({Data.ProgressCount} / {Data.TargetCount ?? Data.TargetXp}{(Data.TargetXp is null ? "" : " XP")})</color>" : "";

            [UIValue(nameof(completed))] private readonly bool completed = data.Completed;

            [UIValue(nameof(targetExists))] private readonly bool targetExists = data.TargetCount is not null || data.TargetXp is not null;

            [UIValue(nameof(oneXonePic))] public const string oneXonePic = ResourcePaths.PIXEL;

            [UIComponent(nameof(PercentBarTop))] private readonly LayoutElement PercentBarTop = null!;
            [UIComponent(nameof(PercentBarTop))] private readonly ImageView PercentBarTop_image = null!;
            [UIComponent(nameof(PercentBarBottom))] private readonly LayoutElement PercentBarBottom = null!;
            [UIComponent(nameof(PercentBarBottom))] private readonly ImageView PercentBarBottom_image = null!;
            [UIComponent(nameof(DescriptionText))] private readonly TextMeshProUGUI DescriptionText = null!;

            [UIValue(nameof(listWidth))] public const float listWidth = 55f;
            [UIValue(nameof(barSpacer))] public const float barSpacer = 0f;
            [UIValue(nameof(exactProgLen))] public const float exactProgLen = 8f;
            [UIValue(nameof(barLen))] public const float barLen = listWidth - barSpacer - exactProgLen;

            [UIAction("#post-parse")]
            private void PostParse()
            {
                float progress = Progress();

                PercentBarTop?.transform.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, barLen * progress);
                PercentBarBottom?.transform.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, barLen * (1 - progress));

                PercentBarTop_image?.color = ColorUtils.GetColor(Data.CategoryId is null ? APCategory.Overall : EnumUtils.ReloadedCategoryToEnum(Data.CategoryId)).Color();
                PercentBarBottom_image?.color = ColorUtils.GREY.Color();

                DescriptionText.enableAutoSizing = true;
                DescriptionText.fontSizeMin = 2f;
                DescriptionText.fontSizeMax = 2.5f;
            }

            private float Progress()
            {
                float progress = Data.ProgressCount;
                int target = 0;

                switch(Data.Type)
                {
                    case MissionType.XP_IN_WINDOW: 
                        target = Data.TargetXp!.Value; break;
                    default:
                        if (Data.TargetCount is not null)
                        target = Data.TargetCount!.Value; break;
                }

                if (target > 0)
                {
                    if (progress > 0)
                        return progress / target;
                    else
                        return 0.01f / target;
                }
                return 0f;
            }

            private const string header = "custom_level_";
            private static readonly Regex FilenameRegex = new("(?<=filename=\")[^\"]+(?=\";)");

            // Interpreted from: https://github.com/kinsi55/BeatSaber_BetterSongSearch/blob/master/UI/SelectedSongView.cs#L186
            public async void GoToSong()
            {
                AsyncLock.Releaser? locker = await OpenMapLock.TryLockAsync();

                if (locker is null)
                    return;

                using (locker.Value)
                {

                    StatusText = "Loading...";
                    ShowStatus = true;

                    string diffId = Data.TargetMapDifficultyId!;

                    AccSaberBasicMap map = null!;
                    AccSaberBasicDifficulty? cachedDiff = null;
                    string? hash = null;

                    foreach (string currentHash in SerializerHandler.CachedMaps.Keys)
                    {
                        map = SerializerHandler.CachedMaps[currentHash];
                        cachedDiff = map.Difficulties.FirstOrDefault(diff => diff.DifficultyId.Equals(diffId));

                        if (cachedDiff is not null)
                        {
                            hash = currentHash;
                            break;
                        }
                    }

                    if (hash is null || cachedDiff is null)
                    {
                        Plugin.Log.Critical("Somehow you have a mission for a level that isn't cached!! Please report this on Discord.");
                        ShowStatus = false;
                        return;
                    }

#if NEW_VERSION
                    BeatmapLevel? level = SongCore.Loader.BeatmapLevelsModelSO.GetBeatmapLevel(header + hash.ToUpper()) ?? await DownloadSong(map);
#else
                    IBeatmapLevel? level = (await SongCore.Loader.BeatmapLevelsModelSO.GetBeatmapLevelAsync(header + hash.ToUpper(), CancellationToken.None)).beatmapLevel ?? await DownloadSong(map);
#endif

                    ShowStatus = false;

                    if (level is null)
                    {
                        Plugin.Log.Warn("Cannot open the map for this mission (" + header + hash.ToUpper() + ").");
                        return;
                    }

                    try
                    {

                        SongPresentInfo.ParentFlowCoordinator.CloseToMainMenu();

#if NEW_VERSION
                        BeatmapKey key = level.GetBeatmapKeys().First(k => k.difficulty == cachedDiff.Difficulty);

                        LevelSelectionFlowCoordinator.State flow = new(SelectLevelCategoryViewController.LevelCategory.All, SongCore.Loader.CustomLevelsPack, in key, level);
#else
                        IDifficultyBeatmapSet diffSet = level.beatmapLevelData.difficultyBeatmapSets.First(set => set.beatmapCharacteristic.serializedName.Equals("Standard", StringComparison.OrdinalIgnoreCase));
                        IDifficultyBeatmap diff = diffSet.difficultyBeatmaps.First(difficulty => difficulty.difficulty == cachedDiff.Difficulty);

                        LevelSelectionFlowCoordinator.State flow = new(SelectLevelCategoryViewController.LevelCategory.All, SongCore.Loader.CustomLevelsPack, diff);
#endif

                        SongPresentInfo.MultiCoordinator.Setup(flow);
                        SongPresentInfo.SoloCoordinator.Setup(flow);

                        SongPresentInfo.MainFlowCoordinator.YoungestChildFlowCoordinatorOrSelf().PresentFlowCoordinator(SongPresentInfo.SoloCoordinator, immediately: true);

                        StandardLevelDetailViewController? sldvc = UnityEngine.Object.FindObjectOfType<StandardLevelDetailViewController>();
                        StandardLevelDetailView? sldv = sldvc?.GetField<StandardLevelDetailView, StandardLevelDetailViewController>("_standardLevelDetailView");

#if NEW_VERSION
                        if (sldv is not null && sldv.beatmapKey != key)
                        {
                            sldv.SetContent(level, BeatmapDifficultyMask.All, [], key.difficulty, key.beatmapCharacteristic,
                                sldv.GetField<PlayerData, StandardLevelDetailView>("_playerData"));
                            typeof(StandardLevelDetailView).GetMethod("TriggerEvent", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).Invoke(sldv, []);
                        }
#else
                        if (sldv is not null && sldv.selectedDifficultyBeatmap.difficulty != diff.difficulty)
                        {
                            sldv.SetContent(level, diff.difficulty, diff.parentDifficultyBeatmapSet.beatmapCharacteristic, sldv.GetField<PlayerData, StandardLevelDetailView>("_playerData"));
                            sldv.GetField<Action<StandardLevelDetailView, IDifficultyBeatmap>, StandardLevelDetailView>("didChangeDifficultyBeatmapEvent").Invoke(sldv, diff);
                        }
#endif
                        if (Data.TargetPlayerId is not null)
                            AccSaberLeaderboardViewController.Instance.ShowPlayerPage(Data.TargetPlayerId);
                    } catch (Exception e)
                    {
                        Plugin.Log.Error("There was an error going to the map!\n" + e);
                    }
                }
            }
#if NEW_VERSION
            private async Task<BeatmapLevel?> DownloadSong(AccSaberBasicMap cachedMap)
#else
            private async Task<IBeatmapLevel?> DownloadSong(AccSaberBasicMap cachedMap)
#endif
            {
                StatusText = "Downloading...";

                var (map, headers) = await APIHandler.CallAPI_Bytes(string.Format(HelpfulPaths.BEATSAVER_DOWNLOAD, cachedMap.Hash.ToLowerInvariant()), null);

                if (map is null)
                    return null;

                using Stream stream = new MemoryStream(map);
                using ZipArchive zip = new(stream, ZipArchiveMode.Read);

                string folderPath = Path.Combine(ResourcePaths.CUSTOM_SONGS, FilenameRegex.Match(headers!.GetValues("Content-Disposition").First()).Value[..^4]);

                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                zip.ExtractToDirectory(folderPath);

                SongCore.Loader.Instance.RefreshSongs(false);

                await Task.Run(() =>
                {
                    lock (LoadWaiterLock)
                        Monitor.Wait(LoadWaiterLock);
                });

#if NEW_VERSION
                return SongCore.Loader.BeatmapLevelsModelSO.GetBeatmapLevel(header + cachedMap.Hash.ToUpper());
#else
                //return SongCore.Loader.BeatmapLevelsModelSO.GetBeatmapLevelIfLoaded(header + cachedMap.Hash.ToUpper());
                return (await SongCore.Loader.BeatmapLevelsModelSO.GetBeatmapLevelAsync(header + cachedMap.Hash.ToUpper(), CancellationToken.None)).beatmapLevel;
#endif
            }
        }
    }
}
