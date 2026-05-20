using AccSaber.Consts;
using AccSaber.Managers;
using AccSaber.Models;
using AccSaber.Utils;
using AccsaberLeaderboard.UI.BSML_Addons.Components;
using AccsaberLeaderboard.UI.Components;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using SiraUtil.Logging;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using static IPA.Logging.Logger;

namespace AccSaber.UI.MenuButton.ViewControllers
{
    [ViewDefinition("AccSaber.UI.MenuButton.Views.AccSaberMilestoneView.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\AccSaberMilestoneView.bsml")]
    internal class AccSaberMilestoneViewController : BSMLAutomaticViewController, INotifyPropertyChanged
    {
#pragma warning disable IDE0051
		public new event PropertyChangedEventHandler? PropertyChanged;

		private string? _userId;
		private bool _parsed;
		private bool _isLoading;
		private Tabs _currentTab;

		private AccSaberStore _accSaberStore = null!;

		private List<AccSaberMilestone> _milestones = null!;

		[UIComponent("milestone-list")]
		private readonly CustomCellListTableData _milestonesList = null!;

		[UIValue("milestone-cells")]
        private readonly List<object> _milestoneCells = [];

		[UIComponent("tab-selector")]
		private readonly TabSelector _tabSelector = null!;

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

		private enum Tabs { 
			Completed = 0,
			Progress = 1
		}

		[UIValue("is-not-loading")]
		private bool IsNotLoading => !_isLoading;

		[Inject]
		public void Construct(AccSaberStore accSaberStore)
		{
			_accSaberStore = accSaberStore;
		}

		[UIAction("#post-parse")]
		void Parsed()
		{
			if (!_parsed)
			{
				_parsed = true;
			}
			_currentTab = Tabs.Completed;
			IsLoading = true;
			_ = SetMilestones(_currentTab);
		}

		[UIAction("tab-selected")]
		void TabSelected(SegmentedControl segmentedControl, int index)
		{
			IsLoading = true;
			_currentTab = (Tabs)segmentedControl.selectedCellNumber;
			_ = SetMilestones(_currentTab);	 

		}

		private async Task SetMilestones(Tabs tab)
        {
			_milestoneCells.Clear();
			_milestonesList.Data().Clear();

			UserInfo? user = await _accSaberStore.GetPlatformUserInfo();

            if (user is null)
            {
				return;
            }


			_userId = user.platformUserId;

			_milestones = await _accSaberStore.GetUserMilestones(tab == Tabs.Completed);

			foreach (var milestone in _milestones)
            {
                _milestoneCells.Add(new MilestoneCell(milestone));
            }
			_milestonesList.TableView().ReloadData();
			IsLoading = false;
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
            [UIValue(nameof(Progress))] public string Progress => $"<color=#ffffff>" + (DisplayableProgress >= 99.99f ? "99.99" : DisplayableProgress.ToString("N2")) + "%</color>";
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

                    return $"<color=#ffffff>(" + middle + ")</color>";
                }
            }
            [UIValue(nameof(Tier))] public string Tier => $"<color={ColorUtils.GetMilestoneRankColor(data.Tier)}>{char.ToUpper(data.Tier[0]) + data.Tier.Substring(1)}</color>";
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
            [UIValue(nameof(cellSize))] public const float cellSize = 13f;
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

                cellContainer.background?.color = bgColor;
            }
        }
	}
}