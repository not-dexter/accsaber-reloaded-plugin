using AccSaber.Consts;
using AccSaber.Managers;
using AccSaber.Models;
using AccSaber.UI.ViewControllers;
using AccSaber.Utils;
using AccsaberLeaderboard.UI.BSML_Addons.Components;
using BeatSaberMarkupLanguage.Attributes;
using HMUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using TMPro;
using System.Collections;

#if !NEW_VERSION
using BeatSaberMarkupLanguage;
#endif

namespace AccSaber.UI.MenuButton.ViewControllers
{

    [ViewDefinition("AccSaber.UI.MenuButton.Views.AccSaberMissionScreen.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\AccSaberMissionScreen.bsml")]
    internal class AccSaberMissionScreen : INotifyPropertyChanged
    {
#pragma warning disable IDE0051
        private bool _isLoading, _parsed = false;
        private string _dailyTime = null!, _weeklyTime = null!;
        private DateTime _dailyRefreshDate, _weeklyRefreshDate;

        private CancellationTokenSource? TimeUpdaterCanceller = null;

        private readonly AsyncLock _missionLock = new();
        private static readonly WaitForEndOfFrame LoopWaitInstruction = new();

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

        public event PropertyChangedEventHandler? PropertyChanged;

        [Inject] private readonly AccSaberStore _accSaberStore = null!;
        [Inject] private readonly AccSaberMainFlowCoordinator _parentFlowCoordinator = null!;
        [Inject] private readonly LevelUtils _levelUtils = null!;

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
                    _ = _levelUtils.GoToSong(cell.Data.TargetMapDifficultyId!, cell.Data.TargetPlayerId, cell.UpdateStatus);
                    break;
                case MissionType.PLAY_N_MAPS or MissionType.SCORES_N or MissionType.STREAK_N_IN_CATEGORY:
                    _ = _levelUtils.LoadPlaylist(cell.Data.Category, cell.UpdateStatus);
                    break;
                case MissionType.PB_ABOVE_THRESHOLD:
                    _ = _levelUtils.LoadPlaylist(cell.Data.Category, PlayerSocialLife.PlayerID!, cell.Data.TargetThresholdAp!.Value, cell.UpdateStatus);
                    break;
            }
        }

        [UIAction("#post-parse")]
        private void PostParse()
        {
            _weeklyTimeText.enableAutoSizing = true;
            _weeklyTimeText.fontSizeMin = 2.75f;
            _weeklyTimeText.fontSizeMax = 4f;

            _parsed = true;
        }

        private void UpdateTimer()
        {
            IEnumerator UpdateTime()
            {
                yield return LoopWaitInstruction;

                DailyTime = $"<color={ColorUtils.GREY}>Resets {_dailyRefreshDate.ToRelativeTime(2).ToLower()}</color>";
                WeeklyTime = $"<color={ColorUtils.GREY}>Resets {_weeklyRefreshDate.ToRelativeTime(3).ToLower()}</color>";

                if (_dailyRefreshDate <= DateTime.UtcNow)
                {
                    StopTimer();
                    SetMissions(true).ContinueWith(finish => UpdateTimer());
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

                AccSaberLeaderboardViewController.Instance.MissionTargets.Clear();

                await PlayerSocialLife.LoadTask;

                try
                {
                    List<AccSaberMission> missions = await _accSaberStore.GetMissions();

                    while (forceNewContent && missions.First(mission => mission.MissionPool == MissionPool.Daily).ExpiresAt <= expiration)
                    {
                        await Task.Delay(15000);
                        missions = await _accSaberStore.GetMissions();
                    }

                    bool setDailyTime = false, setWeeklyTime = false;

                    foreach (AccSaberMission post in missions)
                    {
                        if (_parsed)
                            switch (post.MissionPool)
                            {
                                case MissionPool.Daily: _dailyCells.Add(new MissionCell(post)); break;
                                case MissionPool.Weekly: _weeklyCells.Add(new MissionCell(post)); break;
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

        internal class MissionCell(AccSaberMission data) : ICellDataSource, INotifyPropertyChanged
        {
            public string TemplatePath => ResourcePaths.ACC_SABER_MISSION_CELL;

            public float CellSize => 12;

            public int TemplateId { get; set; }

            public readonly AccSaberMission Data = data;

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
                    bool update = value ^ _showStatus;
                    _showStatus = value;

                    if (update)
                    {
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowStatus)));
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NotShowStatus)));
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
