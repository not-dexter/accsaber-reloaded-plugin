using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using AccSaber.LeaderboardSources;
using AccSaber.Managers;
using AccSaber.Models;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace AccSaber.UI.ViewControllers
{
	[ViewDefinition("AccSaber.UI.Views.AccSaberLeaderboardView.bsml")]
	[HotReload(RelativePathToLayout = @"..\UI\Views\AccSaberLeaderboardView.bsml")]
	internal sealed class AccSaberLeaderboardViewController : BSMLAutomaticViewController, IInitializable, IDisposable
	{
		private int _pageNumber = 0;
		private int _selectedCellIndex;
		private List<Button>? _infoButtons;
		private LoadingControl? _loadingControl;

		private AccSaberStore _accSaberStore = null!;
		private List<ILeaderboardSource> _leaderboardSources = null!;
		private LeaderboardUserModalController _leaderboardUserModalController = null!;
		private float _progressTarget = 0;
		private float _progressDuration = 15;
		private float _progress = 1;

		[Inject]
        public void Construct(AccSaberStore accSaberStore, List<ILeaderboardSource> leaderboardSources, LeaderboardUserModalController leaderboardUserModalController)
        {
			_accSaberStore = accSaberStore;
			_leaderboardSources = leaderboardSources;
			_leaderboardUserModalController = leaderboardUserModalController;
		}

		[UIComponent("leaderboard")]
		private readonly LeaderboardTableView? _leaderboard = null!;

		[UIComponent("milestone-container")]
		private readonly HorizontalLayoutGroup? _milestoneContainer = null!;

		[UIComponent("vertical-icon-segments")]
		private readonly IconSegmentedControl? _iconSegmentedControl = null!;

		[UIComponent("milestone")]
		private readonly TextMeshProUGUI? _milestone = null!;

		[UIComponent("milestone-desc")]
		private readonly TextMeshProUGUI? _milestoneDesc = null!;

		[UIComponent("progress-bar")]
		private readonly LayoutElement _progressBar = null!;

		[UIComponent("progress-bar-inverse")]
		private readonly LayoutElement _progressBarInverse = null!;

		[UIComponent("progress-bar")]
		private readonly ImageView _progressBarImage = null!;

		#region Info Buttons

		// Maybe get around to making a custom leaderboard and get rid of this misery
		[UIComponent("button1")]
		private readonly Button? _button1 = null!;

		[UIComponent("button2")]
		private readonly Button? _button2 = null!;

		[UIComponent("button3")]
		private readonly Button? _button3 = null!;

		[UIComponent("button4")]
		private readonly Button? _button4 = null!;

		[UIComponent("button5")]
		private readonly Button? _button5 = null!;

		[UIComponent("button6")]
		private readonly Button? _button6 = null!;

		[UIComponent("button7")]
		private readonly Button? _button7 = null!;

		[UIComponent("button8")]
		private readonly Button? _button8 = null!;

		[UIComponent("button9")]
		private readonly Button? _button9 = null!;

		[UIComponent("button10")]
		private readonly Button? _button10 = null!;

		#endregion
		
		private int SelectedCellIndex
		{
			get => _selectedCellIndex;
			set
			{
				_selectedCellIndex = value;
				PageNumber = 0;
			}
		}
		
		private int PageNumber
		{
			get => _pageNumber;
			set
			{
				_pageNumber = value;
				
				if (_leaderboard is null || _loadingControl is null || _accSaberStore.CurrentRankedMap is null)
				{
					return;
				}

				_leaderboard.SetScores(new List<LeaderboardTableView.ScoreData>(), 0);
				LeaderboardShowLoading();
				_ = SetScores();
			}
		}
		
		[UIValue("up-enabled")]
		private bool UpEnabled => PageNumber != 0 && _leaderboardSources[SelectedCellIndex].Scrollable;

		[UIValue("down-enabled")]
		private bool DownEnabled => _leaderboardSources[SelectedCellIndex].Scrollable;
		
		[UIAction("#post-parse")]
		private async Task PostParse()
		{
			var list = new List<IconSegmentedControl.DataItem>();
			foreach (var leaderboardSource in _leaderboardSources)
			{
				list.Add(new IconSegmentedControl.DataItem(await leaderboardSource.Icon, leaderboardSource.HoverHint));
			}
			
			_iconSegmentedControl!.SetData(list.ToArray());
			
			// To set rich text, I have to iterate through all cells, set each cell to allow rich text and next time they will have it
			var leaderboardTableCells = _leaderboard!.transform.GetComponentsInChildren<LeaderboardTableCell>(true);

			foreach (var leaderboardTableCell in leaderboardTableCells)
			{
				leaderboardTableCell.transform.Find("PlayerName").GetComponent<CurvedTextMeshPro>().richText = true;
			}

			_loadingControl = _leaderboard.transform.GetComponentInChildren<LoadingControl>(true);

			_infoButtons = new List<Button>(10);
			ChangeButtonScale(_button1!, 0.425f);
			ChangeButtonScale(_button2!, 0.425f);
			ChangeButtonScale(_button3!, 0.425f);
			ChangeButtonScale(_button4!, 0.425f);
			ChangeButtonScale(_button5!, 0.425f);
			ChangeButtonScale(_button6!, 0.425f);
			ChangeButtonScale(_button7!, 0.425f);
			ChangeButtonScale(_button8!, 0.425f);
			ChangeButtonScale(_button9!, 0.425f);
			ChangeButtonScale(_button10!, 0.425f);
		}

		[UIAction("up-clicked")]
		private void UpClicked()
		{
			if (UpEnabled)
			{
				PageNumber--;
			}
		}
		
		[UIAction("down-clicked")]
		private void DownClicked()
		{
			if (DownEnabled)
			{
				PageNumber++;
			}
		}

		[UIAction("cell-selected")]
		private void OnCellSelected(SegmentedControl _, int index)
		{
			SelectedCellIndex = index;
		}
		
		#region Info Buttons Clicked
		[UIAction("b-1-click")]
		private void B1Clicked()
		{
			InfoButtonClicked(0);
		}

		[UIAction("b-2-click")]
		private void B2Clicked()
		{
			InfoButtonClicked(1);
		}

		[UIAction("b-3-click")]
		private void B3Clicked()
		{
			InfoButtonClicked(2);
		}

		[UIAction("b-4-click")]
		private void B4Clicked()
		{
			InfoButtonClicked(3);
		}

		[UIAction("b-5-click")]
		private void B5Clicked()
		{
			InfoButtonClicked(4);
		}

		[UIAction("b-6-click")]
		private void B6Clicked()
		{
			InfoButtonClicked(5);
		}

		[UIAction("b-7-click")]
		private void B7Clicked()
		{
			InfoButtonClicked(6);
		}

		[UIAction("b-8-click")]
		private void B8Clicked()
		{
			InfoButtonClicked(7);
		}

		[UIAction("b-9-click")]
		private void B9Clicked()
		{
			InfoButtonClicked(8);
		}

		[UIAction("b-10-click")]
		private void B10Clicked()
		{
			InfoButtonClicked(9);
		}
		#endregion

		protected override async void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
		{
			base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);

			_ = await _accSaberStore.HasAccSaberUpdated();

			if (!firstActivation)
			{
				return;
			}
			
			foreach (var leaderboardSource in _leaderboardSources)
			{
				leaderboardSource.ClearCache();
			}

			PageNumber = 0;
		}

		protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
		{
			base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);
			_leaderboardUserModalController.HideModal();
		}

		private async Task SetScores(List<AccSaberLeaderboardEntry>? leaderboardEntries = null)
		{
			if (leaderboardEntries is null && _accSaberStore.CurrentRankedMap is not null)
			{
				leaderboardEntries = await _leaderboardSources[SelectedCellIndex].GetScoresAsync(_accSaberStore.CurrentRankedMap, page: PageNumber);
			}

			var scores = new List<LeaderboardTableView.ScoreData>();
			var userScorePos = -1;

			if (leaderboardEntries is null || leaderboardEntries.Count == 0)
			{
				scores.Add(new LeaderboardTableView.ScoreData(0, "", 0, false));
				ToggleInfoButtons(false);
			}
			else
			{	
				var userInfo = await _accSaberStore.GetPlatformUserInfo();
				var userId = userInfo?.platformUserId;

				for (var i = 0; i < (leaderboardEntries.Count > 10 ? 10 : leaderboardEntries.Count); i++)
				{
					var pm = "";

					if (leaderboardEntries[i].Modifiers.Contains("c156e2d8-6852-4488-8e73-927f44e6ed93"))
						pm = "<color=#646464>[PM]</color>";

					scores.Add(new LeaderboardTableView.ScoreData(leaderboardEntries[i].Score, $"<size=85%>{leaderboardEntries[i].PlayerName} -  <size=75%>(<color=#FFD42A>{leaderboardEntries[i].Accuracy * 100:F2}%</color>)</size></size> - <size=75%> (<color=#00FFAE>{leaderboardEntries[i].AP:F2}<size=55%> AP</size></color>) {pm}</size>", leaderboardEntries[i].Rank, false));

					if (_infoButtons != null)
					{
						_infoButtons[i].gameObject.SetActive(true);
						var hoverHint = _infoButtons[i].GetComponent<HoverHint>();
						hoverHint.text = $"Score Set: {leaderboardEntries[i].TimeSet}";
					}

					if (leaderboardEntries[i].PlayerId == userId)
					{
						userScorePos = i;
					}
				}	
			}

			if (_loadingControl != null && _leaderboard != null)
			{
				_loadingControl?.Hide();
                _leaderboard.SetScores(scores, userScorePos);
                NotifyPropertyChanged(nameof(UpEnabled));
				NotifyPropertyChanged(nameof(DownEnabled));
			}
		}

		private void LeaderboardShowLoading()
		{
			if (_loadingControl == null || _infoButtons == null) 
				return;
			
			_loadingControl?.ShowLoading();
			ToggleInfoButtons(false);
		}

		private void ToggleInfoButtons(bool value)
		{
			if (_infoButtons == null) 
				return;
			
			foreach (var button in _infoButtons)
			{
				button.gameObject.SetActive(value);
			}
		}
		
		private void ChangeButtonScale(Button button, float scale)
		{
			var buttonTransform = button.transform;
			var localScale = buttonTransform.localScale;
			buttonTransform.localScale = localScale * scale;
			_infoButtons?.Add(button);
		}
		
		private void InfoButtonClicked(int index)
		{
			if (_infoButtons is null)
			{
				return;
			}

			var playerId = _leaderboardSources[SelectedCellIndex].GetCachedScore(PageNumber)?[index].PlayerId;
			if (playerId is null)
			{
				return;
			}
			
			_leaderboardUserModalController.ShowModal(_infoButtons[index].transform, playerId);
		}

		private void AccSaberStoreOnOnAccSaberRankedMapUpdated(AccSaberRankedMap? rankedMap)
		{
			foreach (var leaderboardSource in _leaderboardSources)
			{
				leaderboardSource.ClearCache();
			}

			PageNumber = 0;
		}
		void Update()
		{
			if (_progressDuration < 0)
			{
				_progressDuration = 0.001f;
			}
			_progress = Mathf.MoveTowards(_progress, _progressTarget, (1 / _progressDuration) * Time.deltaTime);		

			const float barLen = 75f;
			_progressBar.transform.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, barLen * _progress);
			_progressBarInverse.transform.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, barLen * (1 - _progress));
		}

		async Task ShowMilestone(AccSaberMilestone milestone)
        {
			var _color = milestone.Tier switch
			{
				"bronze" => "#cd7f32",
				"silver" => "#c0c0c0",
				"gold" => "#ffd700",
				"platinum" => "#36cfb0",
				"diamond" => "#b9f2ff",
				"apex" => "#a855f7",
				_ => "#f472b6",
			};

			await Task.Delay(200);
			_milestoneContainer!.gameObject.SetActive(true);
			_progress = 1;
			_milestone!.text = $"<color={_color}>{milestone.Title}</color>";
			_milestoneDesc!.text = milestone.Description;
			await _progressBarImage.SetImageAsync("AccSaber.Resources.Pixel.png", false);
			if (ColorUtility.TryParseHtmlString(_color, out Color newCol))
				_progressBarImage.color = newCol;
			await Task.Delay(15000);
			_milestone!.text =	"";
			_milestoneDesc!.text = "";
			_progressBarImage.sprite = null;
			_milestoneContainer!.gameObject.SetActive(false);
		}

		private async void AccSaberStoreOnOnAccSaberScoreUpdated()
		{
			foreach (var leaderboardSource in _leaderboardSources)
			{
				leaderboardSource.ClearCache();
			}

			PageNumber = 0;

			await Task.Delay(5000); // bit of delay to let the milestones update in the backend. will switch to the milestone completion websocket when thats finished
			List<AccSaberMilestone> newMilestones = await _accSaberStore.GetUserMilestones();
			if (_accSaberStore._currentUserMilestones.Count < newMilestones.Count)
			{
				var updatedMilestones = newMilestones.Take(newMilestones.Count - _accSaberStore._currentUserMilestones.Count).ToList();

				foreach (var milestone in updatedMilestones)
				{
					await ShowMilestone(milestone);
					_accSaberStore._currentUserMilestones.Insert(0, milestone);
				}
			}
		}

		public void Initialize()
		{
			_accSaberStore.OnAccSaberRankedMapUpdated += AccSaberStoreOnOnAccSaberRankedMapUpdated;
			_accSaberStore.OnAccSaberScoreUpdated += AccSaberStoreOnOnAccSaberScoreUpdated;
		}

		public void Dispose()
		{
			_accSaberStore.OnAccSaberRankedMapUpdated -= AccSaberStoreOnOnAccSaberRankedMapUpdated;
			_accSaberStore.OnAccSaberScoreUpdated -= AccSaberStoreOnOnAccSaberScoreUpdated;
		}
	}
}