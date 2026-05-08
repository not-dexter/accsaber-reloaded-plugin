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

		private AccSaberStore _accSaberStore = null!;

		[UIComponent("milestone-list")]
		private readonly CustomCellListTableData _topScoresList = null!;

		[UIValue("milestone-cells")]
        private readonly List<MilestoneCell> _milestoneCells = new List<MilestoneCell>();


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
			IsLoading = true;
			var userInfo = _accSaberStore.GetCurrentUser().Result;
			_userId = userInfo.PlayerId;
			SetMilestones();
		}

        private void SetMilestones()
        {
            if (_userId is null)
            {
				return;
            }


            foreach (var milestone in _accSaberStore._currentUserMilestones)
            {
                _milestoneCells.Add(new MilestoneCell(milestone.Title, milestone.Description, milestone.Tier));
            }
            _topScoresList.TableView.ReloadData();
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


			#endregion
			public MilestoneCell(string milestoneName, string milestoneDesc, string tier)
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
			}

		}
	}
}