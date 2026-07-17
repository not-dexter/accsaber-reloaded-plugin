using AccSaber.API;
using AccSaber.Configuration;
using AccSaber.Consts;
using AccSaber.Managers;
using AccSaber.Models;
using AccSaber.UI.ViewControllers;
using AccSaber.Utils;
using AccSaber.Utils.Misc;
using AccSaber.Utils.Safety;
using AccsaberLeaderboard.UI.BSML_Addons.Components;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using HMUI;
using IPA.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

#if !NEW_VERSION
#endif

namespace AccSaber.UI.MenuButton.ViewControllers
{

    [ViewDefinition("AccSaber.UI.MenuButton.Views.AccSaberMissionScreen.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\AccSaberMissionScreen.bsml")]
    internal class AccSaberMissionScreen : SafeNotifyPropertyChanged, AccSaberNotificationModal.IPopup
    {
        private bool _parsed = false;
        private DateTime _dailyRefreshDate, _weeklyRefreshDate, _lastUpdate = DateTime.UtcNow;
        private IEnumerator? _updateTimeRoutine;

        private CancellationTokenSource? TimeUpdaterCanceller = null;

        private readonly AsyncLock _missionLock = new();
        private readonly AsyncLock _eventLock = new();

        [UIComponent("container")] 
        private readonly Backgroundable _container = null!;

        [UIComponent("daily-list")]
        private readonly MyCustomCellListTableData _dailyList = null!;

        [UIValue("daily-cells")]
        private readonly List<ICellDataSource> _dailyCells = [];

        [UIComponent("weekly-list")]
        private readonly MyCustomCellListTableData _weeklyList = null!;

        [UIValue("weekly-cells")]
        private readonly List<ICellDataSource> _weeklyCells = [];

        [UIComponent("weekly-time-text")]
        private readonly TextMeshProUGUI _weeklyTimeText = null!;

        [UIComponent("event-description")]
        private readonly TextMeshProUGUI _eventDescriptionText = null!;

        [UIComponent("event-title")]
        private readonly TextMeshProUGUI _eventTitleText = null!;

        [UIComponent("begin-event-status")]
        private readonly TextMeshProUGUI _eventBeginStatus = null!;

        [UIComponent("event-list")]
        private readonly CustomCellListTableData _eventList = null!;

        [UIValue("event-cells")]
        private readonly List<object> _eventCells = [];

        [UIValue("event-mission-list-content")]
        private readonly string eventMissionListContent = Utilities.GetResourceContent(System.Reflection.Assembly.GetExecutingAssembly(), ResourcePaths.ACC_SABER_MISSION_CELL);

        [UIComponent("event-prev")]
        private readonly PageButton _eventPrev = null!;

        [UIComponent("event-next")]
        private readonly PageButton _eventNext = null!;

        [Inject] private readonly AccSaberStore _accSaberStore = null!;
        [Inject] private readonly AccSaberMainFlowCoordinator _parentFlowCoordinator = null!;
        [Inject] private readonly LevelUtils _levelUtils = null!;
        [Inject] private readonly PlaylistUtils _playlistUtils = null!;
        [Inject] private readonly PluginConfig PC = null!;
        [Inject] private readonly AccSaberNotificationModal _asnm = null!;
        [Inject] private readonly PlayerSocialLife _playerData = null!;
        [Inject] private readonly SerializationHandler _serialHandler = null!;
        [Inject] private readonly AccSaberLeaderboardViewController _leaderboardViewController = null!;

        private AccSaberEventResponse? CurrentEvent => _serialHandler.CurrentEvent;

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

        [UIValue("is-event-loading")]
        private bool IsEventLoading
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged(nameof(IsEventLoading));
                NotifyPropertyChanged(nameof(IsEventNotLoading));
            }
        }

        [UIValue("is-in-event")]
        public bool IsInEvent
        {
            get;
            set
            {
                field = value;

                if (value == true)
                    _ = SetEventInfo();

                NotifyPropertyChanged(nameof(IsInEvent));
                NotifyPropertyChanged(nameof(NotInEvent));
            }
        }

        [UIValue("not-in-event")]
        private bool NotInEvent => !IsInEvent;

        [UIValue("event-begun")]
        public bool IsEventBegun
        {
            get;
            set
            {
                field = value;

                NotifyPropertyChanged(nameof(IsEventBegun));
                NotifyPropertyChanged(nameof(IsEventNotBegun));
            }
        }

        [UIValue("event-not-begun")]
        private bool IsEventNotBegun => !IsEventBegun;

        [UIValue("is-not-loading")]
        private bool IsNotLoading => !IsLoading;

        [UIValue("is-event-not-loading")]
        private bool IsEventNotLoading => !IsEventLoading;

        [UIValue("daily-time")]
        private string DailyTime
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged();
            }
        } = null!;
        [UIValue("weekly-time")]
        private string WeeklyTime
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged();
            }
        } = null!;

        [UIValue("event-title")]
        private string EventTitle
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged();
            }
        } = null!;

        [UIValue("event-duration")]
        private string EventDuration
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged();
            }
        } = null!;

        [UIValue("event-current-week")]
        private string EventCurrentWeek
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged();
            }
        } = null!;

        [UIValue("event-description")]
        private string EventDescription
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged();
            }
        } = null!;

        [UIValue("week-current")]
        private string WeekCurrent
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged();
            }
        } = null!;

        [UIValue("week-pagnation")]
        private string WeekPagnation
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged();
            }
        } = null!;

#pragma warning disable IDE0060
        [UIAction("on-cell-click-event")]
        private void OnCellClickEvent(TableView table, object data)
        {
            if (!PC.DisablePopups)
            {
                if (data is not MissionCell cell)
                    return;

                string prompt;

                switch (cell.Data.Type)
                {
                    case >= MissionType.ACC_ON_MAP and <= MissionType.STREAK_ON_MAP or MissionType.COMEBACK_PB:
                        prompt = "Would you like to go to this map?";
                        break;
                    case
                        MissionType.PLAY_N_MAPS or
                        MissionType.XP_IN_WINDOW or
                        MissionType.PB_ABOVE_THRESHOLD or
                        MissionType.STREAK_N_IN_CATEGORY or
                        MissionType.SCORES_N or
                        > MissionType.SNIPE_RIVAL_ANY_MAP and < MissionType.PB_RANKED_BEFORE_N: // SNIPE_RIVAL_ANY_MAP and PB_RANKED_BEFORE_N are not implemented
                        prompt = "Would you like to go to this Playlist?";
                        break;
                    default: return; // ignore: CAMPAIGN_COMPLETE_N
                }

                _ = _asnm.ShowModal(_container.transform, this, data, _parentFlowCoordinator, prompt);
            }
            else
                PopupSuccess(data);
        }
#pragma warning restore IDE0060

        [UIAction("on-cell-click")]
        private void OnCellClick(ICellDataSource data)
        {
            if (!PC.DisablePopups)
            {
                if (data is not MissionCell cell)
                    return;

                string prompt;

                switch (cell.Data.Type)
                {
                    case >= MissionType.ACC_ON_MAP and <= MissionType.STREAK_ON_MAP or MissionType.COMEBACK_PB:
                        prompt = "Would you like to go to this map?";
                        break;
                    case MissionType.PLAY_N_MAPS or MissionType.SCORES_N or MissionType.STREAK_N_IN_CATEGORY or MissionType.PB_ABOVE_THRESHOLD or MissionType.XP_IN_WINDOW:
                        prompt = "Would you like to go to this Playlist?";
                        break;
                    default: return;
                }

                _ = _asnm.ShowModal(_container.transform, this, data, _parentFlowCoordinator, prompt);
            }
            else
                PopupSuccess(data);
        }

        [UIAction("on-event-begin")]
        private async void BeginEvent()
        {
            if (CurrentEvent is null)
                return;

            if (await _accSaberStore.StartEvent(CurrentEvent.Event.Id))
                IsEventBegun = true;
            else
                _eventBeginStatus.SetText("There was an error starting the event!");

        }


        private int WeekPage
        {
            get;
            set
            {
                field = value;
                WeekPagnation = $"{value}/4";
                WeekCurrent = $"Week {value}";
                _ = SetEventMissions(false);
            }
        }


        [UIAction("OnEventPrev")]
        private void OnEventPrev()
        {
            if (CurrentEvent is not null)
            {
                _eventNext.enabled = true;

                if (WeekPage > 1)
                    WeekPage--;


                if (WeekPage - 1 == 1)
                    _eventPrev.enabled = false;

                WeekPagnation = $"{WeekPage}/{4}";
            }
        }

        [UIAction("OnEventNext")]
        private void OnEventNext()
        {
            if (CurrentEvent is not null)
            {
                _eventPrev.enabled = true;

                if (WeekPage < CurrentEvent.Event.TotalWeeks)
                    WeekPage++;

                if (WeekPage == CurrentEvent.Event.TotalWeeks)
                    _eventNext.enabled = false;


                WeekPagnation = $"{WeekPage}/{4}";
            }
        }

        public async Task SetEventInfo()
        {
            if (CurrentEvent is not null)
            {
                EventTitle = CurrentEvent.Event.Title;
                EventDuration = $"Ends {MiscUtils.ToRelativeTime(CurrentEvent.Event.EndsAt, 2)}";
                EventCurrentWeek = $"Week {CurrentEvent.Event.CurrentWeek} of {CurrentEvent.Event.TotalWeeks}";
                EventDescription = CurrentEvent.Event.Description;
            } 
        }

        private async Task SetEventMissions(bool forceNewContent)
        {
            AsyncLock.Releaser? locker = await _eventLock.TryLockAsync();

            if (locker is null)
                return;

            using (locker.Value)
            {
                if (!IsEventLoading)
                    IsEventLoading = true;

                _eventCells.Clear();
                _eventList.Data().Clear();

                await _playerData.LoadTask;
                await _serialHandler.InitTask;

                try
                {
                    DateTime expiration = ((MissionCell?)_eventCells.FirstOrDefault())?.Data.ExpiresAt ?? DateTime.MinValue;

                    List<AccSaberEventMe> missions = await _accSaberStore.GetEventMissions(WeekPage, false, overrideCache: _lastUpdate < SerializationHandler.LastScoreTime);

                    _lastUpdate = DateTime.UtcNow;

                    while (forceNewContent && missions.First(mission => mission.Mission.Week == WeekPage).Mission.CompletableUntil <= expiration)
                    {
                        await Task.Delay(15000);
                        missions = await _accSaberStore.GetEventMissions(WeekPage, false);
                    }

                    if (missions is null)
                        return;

                    foreach (AccSaberEventMe mission in missions)
                    {
                        if (mission.Mission.Week != WeekPage) 
                            continue;

                        AccSaberBasicDifficulty? targetDiff = mission.Mission.TargetMapDifficultyId is null ?
                        null : _serialHandler.GetDiffById(mission.Mission.TargetMapDifficultyId.Value);

                        _eventCells.Add(new MissionCell(mission.Current ?? mission.Mission, targetDiff)); // change this to current when live
                    }
                    
                    _eventList.TableView().ReloadData();
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error(ex);
                }
                finally
                {
                    IsEventLoading = false;
                }
            }
        }

        [UIAction("#post-parse")]
        private async void PostParse()
        {
            if (_parsed)
                return;

            _weeklyTimeText.enableAutoSizing = true;
            _weeklyTimeText.fontSizeMin = 2.75f;
            _weeklyTimeText.fontSizeMax = 4f;

            _eventTitleText.enableAutoSizing = true;
            _eventTitleText.fontSizeMin = 2.75f;
            _eventTitleText.fontSizeMax = 6f;

            _eventDescriptionText.enableAutoSizing = true;
            _eventDescriptionText.fontSizeMin = 2.75f;
            _eventDescriptionText.fontSizeMax = 4f;
            _parsed = true;

            WeekPage = Math.Max(1, CurrentEvent?.Event.CurrentWeek ?? 0);

            if (CurrentEvent is not null)
            {
                var eventInfo = await _accSaberStore.GetEventBegun(CurrentEvent.Event.Id);
                IsEventBegun = eventInfo.Begun;
            }

            if (!_eventList.TableView().canSelectSelectedCell)
                _eventList.TableView().SetField("_canSelectSelectedCell", true);

            }

        private void UpdateTimer()
        {
            if (TimeUpdaterCanceller is not null)
            {
                TimeUpdaterCanceller.Cancel();
                TimeUpdaterCanceller.Dispose();
            }
            TimeUpdaterCanceller = new();

            CancellationToken ct = TimeUpdaterCanceller.Token;

            WaitForEndOfFrame WaitInstruction = new();
            WaitForSeconds DelayInstruction = new(1);

            IEnumerator UpdateTime()
            {

                yield return WaitInstruction;

                if (ct.IsCancellationRequested)
                    yield break;

                DailyTime = $"<color={ColorUtils.GREY}>Resets {_dailyRefreshDate.ToRelativeTime(2).ToLower()}</color>";
                WeeklyTime = $"<color={ColorUtils.GREY}>Resets {_weeklyRefreshDate.ToRelativeTime(3).ToLower()}</color>";

                if (_dailyRefreshDate <= DateTime.UtcNow)
                {
                    StopTimer();
                    //Plugin.Log.Info($"daily = {_dailyRefreshDate}, now = {DateTime.UtcNow}");
                    SetMissions(true).ContinueWith(finish => UpdateTimer());
                }

                if (ct.IsCancellationRequested)
                    yield break;

                yield return DelayInstruction;
            }

            if (_updateTimeRoutine is not null)
            {
                IEnumerator currentRoutine = _updateTimeRoutine;
                _mainThreadDispatcher.EnqueueStopRoutine(currentRoutine);
            }

            _updateTimeRoutine = UpdateTime();

            _mainThreadDispatcher.EnqueueRoutine(_updateTimeRoutine);
        }
        public void StopTimer()
        {
            if (TimeUpdaterCanceller is null)
                return;

            if (_updateTimeRoutine is not null)
            {
                IEnumerator currentRoutine = _updateTimeRoutine;
                _mainThreadDispatcher.EnqueueStopRoutine(currentRoutine);
                _updateTimeRoutine = null;
            }

            TimeUpdaterCanceller.Cancel();
            TimeUpdaterCanceller.Dispose();
            TimeUpdaterCanceller = null;
        }

        public void PopupSuccess(object cell)
        {
            if (cell is MissionCell missionCell)
                GoToMission(missionCell);
        }

        internal void GoToMission(MissionCell cell)
        {
            void CloseMenu() => _parentFlowCoordinator.CloseToMainMenu();

            switch (cell.Data.Type)
            {
                case >= MissionType.ACC_ON_MAP and <= MissionType.STREAK_ON_MAP or MissionType.COMEBACK_PB:
                    _ = _levelUtils.GoToSong(cell.Data.TargetMapDifficultyId!.Value, cell.Data.TargetPlayerId, CloseMenu, cell.UpdateStatus);
                    break;
                case MissionType.PLAY_N_MAPS or MissionType.SCORES_N or MissionType.STREAK_N_IN_CATEGORY:
                    _ = _levelUtils.LoadPlaylist(cell.Data.Category, CloseMenu, cell.UpdateStatus);
                    break;
                case MissionType.PB_ABOVE_THRESHOLD:
                    _ = _levelUtils.LoadPlaylistAp(cell.Data.Category, _playerData.PlayerID!, cell.Data.TargetThresholdAp!.Value, ComparisonType.GTE, CloseMenu, cell.UpdateStatus);
                    break;
                case MissionType.XP_IN_WINDOW or MissionType.STREAK_SUM_N or MissionType.AP_GAIN_OVERALL:
                    _ = _levelUtils.LoadPlaylist(APCategory.Overall, CloseMenu, cell.UpdateStatus);
                    break;
                case MissionType.SNIPE_RIVAL_ANY_MAP:
                    // Cannot be done really at all, would have to load 1000s of scores even if a player has like 2 rivals.
                    _ = _levelUtils.LoadPlaylist(APCategory.Overall, CloseMenu, cell.UpdateStatus);
                    break;
                case MissionType.BATCH_PLAY_N:
                    async Task LoadBatch()
                    {
                        AccSaberPagedContent<AccSaberBatch>? batchData = await APIHandler.CallAPI_Json<AccSaberPagedContent<AccSaberBatch>>(string.Format(HelpfulPaths.APAPI_BATCHES, 0, 1) + "&sort=releasedAt,desc", AccsaberAPI.Throttler);

                        if (batchData is null || batchData.Content is null)
                            return;

                        AccSaberBatch batch = batchData.Content.First();

                        string filename = $"accsaber-reloaded-{AccSaberPlaylistModalController.FilenameEscapeRegex.Replace(batch.Name, "-")}";
                        string playlistName = $"Accsaber {batch.Name}";

                        await _levelUtils.LoadPlaylist(filename, playlistName, _playlistUtils.GetPlaylistData(batch.Difficulties.Select(diff => diff.DifficultyId)), null, CloseMenu, cell.UpdateStatus);
                    }
                    _ = LoadBatch();
                    break;
                case MissionType.PB_RANKED_BEFORE_N:
                    // Cannot be done until there is an easy way to get all maps ranked before a time.
                    break;
                    //CAMPAIGN_COMPLETE_N (cannot generate a playlist for a campaign)
            }
        }

        public void ShowMissions()
        {
            _ = SetMissions(false);
        }
        private async Task SetMissions(bool forceNewContent)
        {
            AsyncLock.Releaser? locker = await _missionLock.TryLockAsync();

            if (locker is null)
                return;

            using (locker.Value)
            {
                if (!IsLoading)
                    IsLoading = true;

                DateTime expiration = ((MissionCell?)_dailyCells.FirstOrDefault())?.Data.ExpiresAt ?? DateTime.MinValue;

                _dailyCells.Clear();
                _weeklyCells.Clear();

                _leaderboardViewController.MissionTargets.Clear();

                await _playerData.LoadTask;

                try
                {
                    List<AccSaberMission> missions = await _accSaberStore.GetMissions(overrideCache: _lastUpdate < SerializationHandler.LastScoreTime);

                    _lastUpdate = DateTime.UtcNow;

                    while (forceNewContent && missions.First(mission => mission.MissionPool == MissionPool.Daily).ExpiresAt <= expiration)
                    {
                        await Task.Delay(15000);
                        missions = await _accSaberStore.GetMissions();
                    }

                    bool setDailyTime = false, setWeeklyTime = false;

                    foreach (AccSaberMission post in missions)
                    {
                        if (_parsed)
                        {
                            AccSaberBasicDifficulty? targetDiff = post.TargetMapDifficultyId is null ?
                                null : _serialHandler.GetDiffById(post.TargetMapDifficultyId.Value);

                            switch (post.MissionPool)
                            {
                                case MissionPool.Daily: _dailyCells.Add(new MissionCell(post, targetDiff)); break;
                                case MissionPool.Weekly: _weeklyCells.Add(new MissionCell(post, targetDiff)); break;
                            }
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
                            _leaderboardViewController.MissionTargets.Add((post.TargetPlayerId, post.TargetMapDifficultyId.Value));
                    }

                    UpdateTimer();

                    if (_parsed)
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

        internal class MissionCell(AccSaberMission data, AccSaberBasicDifficulty? targetDiff) : SafeNotifyPropertyChanged, ICellDataSource
        {
            public string TemplatePath => ResourcePaths.ACC_SABER_MISSION_CELL;

            public float CellSize => 12;

            public int TemplateId { get; set; }

            public readonly AccSaberMission Data = data;
            public readonly AccSaberBasicDifficulty? TargetDiff = targetDiff;

            private bool _showStatus = false;
            private string _statusText = null!;

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
                    bool update = value ^ _showStatus;
                    _showStatus = value;

                    if (update)
                    {
                        NotifyPropertyChanged(nameof(ShowStatus));
                        NotifyPropertyChanged(nameof(NotShowStatus));
                    }
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
#if NEW_VERSION
                    _statusText = value;
#else
                    _statusText = $"<size=20%>{value}</size>";
#endif
                    NotifyPropertyChanged(nameof(StatusText));
                }
            }

            [UIValue("target")]
            public string Target = $"{data.ProgressCount}/{data.TargetCount}";

            [UIValue("mission")] public string Mission = $"{data.Name} <size=80%><color={ColorUtils.GetColor(data.Category)}>{data.Category.ToString().ToUpper()}</color></size>";

            [UIValue("missionBand")] public string MissionBand => $"<color={color}>{Data.Band.ToString().ToUpper()}</color>";

            [UIValue("description")] public string Description => $"<color={ColorUtils.GREY}>{DescriptionParser()}</color>";

            private string DescriptionParser()
            {
                if (TargetDiff is null)
                    return Data.Description;
                else
                    return Data.Description.Replace(EnumUtils.DiffToReloadedDiff(TargetDiff.Difficulty).ToString(), TargetDiff.Difficulty.ToString());
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

            [UIValue(nameof(targetExists))] private readonly bool targetExists = (data.TargetCount is not null || data.TargetXp is not null) && !data.Completed;

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

                PercentBarTop_image?.color = ColorUtils.GetColor(Data.CategoryId == default ? APCategory.Overall : EnumUtils.ReloadedCategoryIdToCategory(Data.CategoryId)).Color();
                PercentBarBottom_image?.color = ColorUtils.GREY.Color().ColorWithAlpha(0.15f);

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
            internal void UpdateStatus(string? text)
            {
                bool update = text is not null;
                ShowStatus = update;

                if (update)
                    StatusText = text!;
            }
        }
    }
}
