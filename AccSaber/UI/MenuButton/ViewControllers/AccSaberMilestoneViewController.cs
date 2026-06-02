using AccSaber.Consts;
using AccSaber.Managers;
using AccSaber.Models;
using AccSaber.Utils;
using AccsaberLeaderboard.UI.Components;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace AccSaber.UI.MenuButton.ViewControllers
{
    [ViewDefinition("AccSaber.UI.MenuButton.Views.AccSaberMilestoneView.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\AccSaberMilestoneView.bsml")]
    internal class AccSaberMilestoneViewController : BSMLAutomaticViewController, INotifyPropertyChanged
    {
#pragma warning disable IDE0051
		public new event PropertyChangedEventHandler? PropertyChanged;

		private bool _parsed;
		private bool _isLoading;
		private CategoryTab _currentTab;
        private MilestoneTab _currentMilestoneTab = 0;

        private CategoryTab CurrentTab
        {
            get => _currentTab;
            set
            {
                _currentTab = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMilestoneTab)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMissionTab)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ContainerOffset)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ContainerWidth)));
            }
        }

        private readonly AsyncLock milestoneLock = new();

        [Inject] private readonly AccSaberStore _accSaberStore = null!;
        [Inject] private readonly AccSaberMissionScreen mc = null!;

		private List<AccSaberMilestone> _milestones = null!;

        [UIObject("content-container")]
        private readonly GameObject _contentContainer = null!;

		[UIComponent("milestone-list")]
		private readonly CustomCellListTableData _milestonesList = null!;

		[UIValue("milestone-cells")]
        private readonly List<object> _milestoneCells = [];

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

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);

            mc.ShowMissions();
        }

        [UIValue("is-not-loading")]
        private bool IsNotLoading => !_isLoading;

        [UIValue("milestone-cell")]
        private string MilestoneCellBsml => Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), ResourcePaths.ACC_SABER_MILESTONE_CELL);

        [UIValue("is-milestone-tab")]
        private bool IsMilestoneTab => CurrentTab == CategoryTab.Milestones;

        [UIValue("is-mission-tab")]
        private bool IsMissionTab => CurrentTab == CategoryTab.Missions;

        [UIValue("container-offset")]
        private float ContainerOffset => CurrentTab == CategoryTab.Missions ? 7.5f : 0f;

        [UIValue("container-width")]
        private float ContainerWidth => CurrentTab == CategoryTab.Missions ? 135f : 120f;


        private enum CategoryTab {
            Missions = 0,
			Milestones
		}
        private enum MilestoneTab
        {
            Progress = 0,
            Completed
        }


        [UIAction("#post-parse")]
		private void Parsed()
		{
			if (!_parsed)
			{
				_parsed = true;
			}

            VersionUtils.Parse(ResourcePaths.ACC_SABER_MISSION_SCREEN, _contentContainer, mc);

            CurrentTab = 0;

			_ = SetMilestones(0);
        }

#pragma warning disable IDE0060 // index is needed for this function to be called correctly.
        [UIAction("category-tab-selected")]
		private void CategoryTabSelected(SegmentedControl segmentedControl, int index)
		{
            CurrentTab = (CategoryTab)index;

            UpdateTabs();
		}

        [UIAction("milestone-tab-selected")]
        private void MilestoneTabSelected(SegmentedControl segmentedControl, int index)
        {
            _currentMilestoneTab = (MilestoneTab)index;

            _ = SetMilestones(_currentMilestoneTab);
        }
#pragma warning restore IDE0060

        public void UpdateTabs()
        {
            if (IsMilestoneTab)
                _ = SetMilestones(_currentMilestoneTab);
            else
                mc.ShowMissions();
        }
        public void StopTimer()
        {
            mc.StopTimer();
        }

        private async Task SetMilestones(MilestoneTab tab)
        {
            AsyncLock.Releaser? locker = await milestoneLock.LockAsync();

            if (locker is null)
                return;

            using (locker.Value)
            {
                IsLoading = true;

                _milestoneCells.Clear();
                _milestonesList.Data().Clear();
                await PlayerSocialLife.LoadTask;

                if (PlayerSocialLife.PlayerID is null)
                {
                    return;
                }

                _milestones = await _accSaberStore.GetUserMilestones(tab == MilestoneTab.Completed);

                foreach (AccSaberMilestone milestone in _milestones)
                {
                    _milestoneCells.Add(new MilestoneCell(milestone));
                }

                _milestonesList.TableView().ReloadData();
                IsLoading = false;
            }
        }
		internal class MilestoneCell
		{
            private readonly AccSaberMilestone data;

            private readonly bool flip;
            private readonly float progressPercent;

            private readonly Color bgColor, rankColor;
            private float DisplayableProgress => progressPercent * 100f;
            private readonly float Prog, Targ;

            public MilestoneCell(AccSaberMilestone milestoneData)
            {
                data = milestoneData;

                completed = data.Completed;
                notCompleted = !completed;

                flip = milestoneData.Completed ^ milestoneData.Progress > milestoneData.TargetValue;
                Prog = flip ? data.TargetValue : data.Progress;
                Targ = flip ? data.Progress : data.TargetValue;

                progressPercent = milestoneData.Completed ? 1f : AccSaberMilestone.CalcProgress(milestoneData.TargetValue, milestoneData.Progress, flip);

                rankColor = ColorUtils.GetMilestoneRankColor(data.Tier).Color();

                const float brightnessThreshold = 0.6f;

                Color c = rankColor;
                c.a = 0.35f;
                float maxColor = c.maxColorComponent;
                if (maxColor > brightnessThreshold)
                {
                    float curve = maxColor - brightnessThreshold;
                    c.r -= curve;
                    c.g -= curve;
                    c.b -= curve;
                }
                bgColor = c;

            }

            [UIValue(nameof(completed))] private readonly bool completed;
            [UIValue(nameof(notCompleted))] private readonly bool notCompleted;
            [UIValue(nameof(Progress))] public string Progress => $"<color={ColorUtils.GetMilestoneRankColor(data.Tier).DimColor(2)}>" + (DisplayableProgress >= 99.99f ? "99.99" : DisplayableProgress.ToString("N2")) + "%</color>";
            [UIValue(nameof(ExactProgress))]
            public string ExactProgress
            {
                get
                {
                    string middle;
                    if (data.TargetValue >= 1000)
                        middle = $"{Prog:N0} / {Targ:N0}";
                    else if (data.TargetValue < 1)
                        middle = $"{Prog * 100f:0.####}% / {Targ * 100f:0.####}%";
                    else
                        middle = $"{Prog:0.####} / {Targ:0.####}";

                    return $"<color={ColorUtils.GetMilestoneRankColor(data.Tier).DimColor(6)}>({middle})</color>";
                }
            }
            [UIValue(nameof(Tier))] public string Tier => $"<color={ColorUtils.GetMilestoneRankColor(data.Tier)}>{data.Tier.Capitialize()}</color>";
            [UIValue(nameof(Title))] public string Title => $"{data.Title}";
            [UIValue(nameof(Description))] public string Description => $"<color={ColorUtils.GREY}>{data.Description}</color>";


            [UIComponent(nameof(PercentBarTop))] private readonly LayoutElement PercentBarTop = null!;
            [UIComponent(nameof(PercentBarTop))] private readonly ImageView PercentBarTop_image = null!;
            [UIComponent(nameof(PercentBarBottom))] private readonly LayoutElement PercentBarBottom = null!;
            [UIComponent(nameof(PercentBarBottom))] private readonly ImageView PercentBarBottom_image = null!;

            [UIComponent(nameof(cellContainer))] private readonly CustomBackground cellContainer = null!;


            [UIValue(nameof(oneXonePic))] public const string oneXonePic = ResourcePaths.PIXEL;
            [UIValue(nameof(bgPath))] public const string bgPath = ResourcePaths.CELL_PIXEL;

            [UIValue(nameof(greenColor))] public const string greenColor = ColorUtils.LEVEL_DIM;

            [UIValue(nameof(listWidth))] public const float listWidth = 100f;
            [UIValue(nameof(cellSize))] public const float cellSize = 13.5f;
            [UIValue(nameof(FontSize))] public const float FontSize = 3f;
            [UIValue(nameof(barSpacer))] public const float barSpacer = 5f;
            [UIValue(nameof(progLen))] public const float progLen = 10f;
            [UIValue(nameof(exactProgLen))] public const float exactProgLen = 25f;
            [UIValue(nameof(barLen))] public const float barLen = listWidth - barSpacer - progLen - exactProgLen;

            [UIAction("#post-parse")]
            private void PostParse()
            {
                PercentBarTop.transform.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, barLen * progressPercent);
                PercentBarBottom.transform.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, barLen * (1 - progressPercent));

                PercentBarTop_image.color = rankColor;
                PercentBarBottom_image.color = ColorUtils.TECH.Color();

                cellContainer.Background?.color = bgColor;
            }
        }
	}
}