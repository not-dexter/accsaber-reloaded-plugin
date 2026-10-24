using AccSaber.Consts;
using AccSaber.Managers;
using AccSaber.Models;
using AccSaber.Utils;
using AccSaber.Utils.Misc;
using AccSaber.Utils.Safety;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using Zenject;
using static AccSaber.UI.MenuButton.ViewControllers.AccSaberPlayerMissionController;

namespace AccSaber.UI.MenuButton.ViewControllers
{
    [ViewDefinition("AccSaber.UI.MenuButton.Views.AccSaberEventMissionView.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\AccSaberEventMissionView.bsml")]
    internal class AccSaberEventMissionController : SafeNotifyPropertyChanged
    {
        [Inject] private readonly AccSaberStore _accSaberStore = null!;
        [Inject] private readonly PlayerSocialLife _playerData = null!;
        [Inject] private readonly SerializationHandler _serialHandler = null!;

        private readonly AsyncLock _eventLock = new();
        private AccSaberEventResponse? CurrentEvent => _serialHandler.CurrentEvent;
        private bool _parsed = false;
        private DateTime _lastUpdate = DateTime.UtcNow;

        [UIValue("active")]
        private bool Active
        {
            get;
            set
            {
                if (field == value)
                    return;

                field = value;
                NotifyPropertyChanged(nameof(Active));
            }
        } = false;

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

        [UIValue("lockImg")]
        private const string LockImg = ResourcePaths.LOCK;

        [UIComponent("event-prev")]
        private readonly PageButton _eventPrev = null!;

        [UIComponent("event-next")]
        private readonly PageButton _eventNext = null!;


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

        [UIValue("is-event-not-loading")]
        private bool IsEventNotLoading => !IsEventLoading;

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

        [UIValue("show-lock")]
        private bool ShowLock
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged();
            }
        } = false;

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

        private int WeekPage
        {
            get;
            set
            {
                field = value;
                WeekPagnation = $"{value}/{CurrentEvent?.Event.TotalWeeks}";
                WeekCurrent = $"Week {value}";
                _ = SetEventMissions(false);
            }
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

                WeekPagnation = $"{WeekPage}/{CurrentEvent.Event.TotalWeeks}";
                ShowLock = WeekPage > CurrentEvent.Event.CurrentWeek;
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


                WeekPagnation = $"{WeekPage}/{CurrentEvent.Event.TotalWeeks}";
                ShowLock = WeekPage > CurrentEvent.Event.CurrentWeek;
            }
        }

        [UIAction("#post-parse")]
        private async void PostParse()
        {
            if (_parsed)
                return;
            _parsed = true;

            _eventTitleText.enableAutoSizing = true;
            _eventTitleText.fontSizeMin = 2.75f;
            _eventTitleText.fontSizeMax = 6f;

            _eventDescriptionText.enableAutoSizing = true;
            _eventDescriptionText.fontSizeMin = 2.75f;
            _eventDescriptionText.fontSizeMax = 4f;

            if (!_eventList.TableView().canSelectSelectedCell)
                _eventList.TableView().SetField("_canSelectSelectedCell", true);

            await _serialHandler.InitTask;

            WeekPage = Math.Max(1, CurrentEvent?.Event.CurrentWeek ?? 0);

            if (CurrentEvent is not null)
            {
                AccSaberEventBegun eventInfo = await _accSaberStore.GetEventBegun(CurrentEvent.Event.Id);
                IsEventBegun = eventInfo.Begun;
            }
        }

        public async Task SetEventInfo()
        {
            if (CurrentEvent is not null)
            {
                EventTitle = CurrentEvent.Event.Title;
                EventDuration = $"Ends {CurrentEvent.Event.EndsAt.ToRelativeTime(2)}";
                EventCurrentWeek = $"Week {CurrentEvent.Event.CurrentWeek} of {CurrentEvent.Event.TotalWeeks}";
                EventDescription = CurrentEvent.Event.Description;
            }
        }

        public void HideMissions()
        {
            Active = false;
        }
        public void ShowMissions()
        {
            Active = true;
            _ = SetEventMissions(false);
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

                    await SetEventInfo();

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

                        _eventCells.Add(new MissionCell(mission.Current ?? mission.Mission, targetDiff));
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
    }
}
