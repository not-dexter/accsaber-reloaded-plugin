using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.ViewControllers;
using AccSaber.Managers;
using System.ComponentModel;
using SiraUtil.Logging;
using Zenject;
using System.Collections.Generic;
using HMUI;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage.Components;
using AccSaber.Models;
using UnityEngine;

namespace AccSaber.UI.MenuButton.ViewControllers
{
    [ViewDefinition("AccSaber.UI.MenuButton.Views.AccSaberMilestoneView.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\AccSaberMilestoneView.bsml")]
    internal class AccSaberMilestoneViewController : BSMLAutomaticViewController, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

		private string? _userId;
		private bool _parsed;
		private bool _isLoading;
		private Tabs _currentTab;

		private AccSaberStore _accSaberStore = null!;

		private List<AccSaberMilestone> _milestones = null!;

		[UIComponent("milestone-list")]
		private readonly CustomCellListTableData _milestonesList = null!;

		[UIValue("milestone-cells")]
        private readonly List<MilestoneCell> _milestoneCells = new List<MilestoneCell>();

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
			_milestonesList.Data.Clear();

			var user = await _accSaberStore.GetPlatformUserInfo();

            if (user is null)
            {
				return;
            }


			_userId = user.platformUserId;

			_milestones = await _accSaberStore.GetUserMilestones(tab == Tabs.Completed);

			foreach (var milestone in _milestones)
            {
                _milestoneCells.Add(new MilestoneCell(milestone.Title, milestone.Description, milestone.Tier, milestone.Completed, milestone.NormalizedProgress));
            }
			_milestonesList.TableView.ReloadData();
			IsLoading = false;
        }
        internal class MilestoneCell
		{
			#region BSML Values
			[UIValue("milestone-name")]
			private readonly string _milestoneName;

			[UIValue("milestone-desc")]
			private readonly string _milestoneDesc;

			[UIValue("milestone-color")]
			private readonly string _milestoneColor;

			[UIValue("milestone-progress")]
			private readonly string _milestoneProgress;
			#endregion
			public MilestoneCell(string milestoneName, string milestoneDesc, string tier, bool completed, float progress)
			{

				_milestoneColor = tier switch
				{
					"bronze" => "#cd7f32",
					"silver" => "#c0c0c0",
					"gold" => "#ffd700",
					"platinum" => "#36cfb0",
					"diamond" => "#b9f2ff",
					"apex" => "#a855f7",
					_ => "#f472b6",
				};

				_milestoneName = $"<color={_milestoneColor}>{milestoneName}</color>"; ;
				_milestoneDesc = milestoneDesc;
				_milestoneProgress = completed ? "<color=#22c55e>Completed</color>" : $"{progress * 100:F1}%";
			}
		}
	}
}