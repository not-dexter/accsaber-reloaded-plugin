using AccSaber.API;
using AccSaber.Consts;
using AccSaber.Managers;
using AccSaber.Models.CacheModels;
using AccSaber.Utils;
using AccsaberLeaderboard.UI.BSML_Addons.Components;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Tweening;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using AccSaber.Models.PlayerModels;
using AccSaber.UI.MenuButton.Campaigns;
using AccSaber.Models;


#if NEW_VERSION
using BeatSaberMarkupLanguage;
#endif


namespace AccSaber.UI.MenuButton.ViewControllers
{
    [ViewDefinition("AccSaber.UI.MenuButton.Views.AccSaberMenuView.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\AccSaberMenuView.bsml")]
    internal class AccSaberMenuViewController : BSMLAutomaticViewController, INotifyPropertyChanged, IInitializable, IDisposable
	{
#pragma warning disable IDE0051
		private AccSaberPlayer? _user;
        private bool _parsed;
        private bool _firstLoad;
        private bool _isLoading;
		private bool _isScoresLoading;
		private int _pageNumber = 0;
		private int _maxPage = 1;
		private APCategory _categoryValue = APCategory.Overall;
        private string _username = "";
		private string _pagnation = "";
		private string _rank = null!;
        private string _country = null!;
        private string _title = null!;
        private string _level = null!;
        private string _ap = null!;
        private string _xp = null!;
        private string _plays = null!;
        private string _headset = null!;

		private readonly AsyncLock refreshLock = new();

		[Inject] private AccSaberCampaignFlow campaignFlow = null!;

        [UIValue("score-cells")]
        private readonly List<ICellDataSource> _scoreCells = [];


        public new event PropertyChangedEventHandler? PropertyChanged;

        [UIComponent("profile-image")]
        private readonly ImageView _profileImage = null!;

        [UIComponent("user-info")]
        private readonly Transform _userInfo = null!;

        [UIComponent("progress-bar")]
        private readonly LayoutElement _progressBar = null!;

        [UIComponent("progress-bar")]
        private readonly ImageView _progressBarImage = null!;

        [UIComponent("progress-bar-inverse")]
        private readonly LayoutElement _progressBarInverse = null!;

		[UIComponent("top-scores-list")]
		private readonly MyCustomCellListTableData _topScoresList = null!;

		private CanvasGroup? _userInfoCanvasGroup;

		private AccSaberStore _accSaberStore = null!;
        private TimeTweeningManager _timeTweeningManager = null!;

		public event Action? HubActivated;
        public event Action? HubDeactivated;

        [Inject]
        public void Construct(AccSaberStore accSaberStore, TimeTweeningManager timeTweeningManager)
        {
            _accSaberStore = accSaberStore;
			_timeTweeningManager = timeTweeningManager;
		}


        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);

			HubActivated?.Invoke();
		}

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);

            HubDeactivated?.Invoke();
        }


        private int PageNumber
		{
			get => _pageNumber;
			set
			{
				_pageNumber = value;
				_ = RefreshScores();
			}
		}

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

		[UIValue("scores-is-loading")]
		private bool IsScoresLoading
		{
			get => _isScoresLoading;
			set
			{
				_isScoresLoading = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsScoresLoading)));
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsScoresNotLoading)));
			}
		}

		[UIValue("scores-is-not-loading")]
		private bool IsScoresNotLoading => !_isScoresLoading;

		[UIValue("category-value")]
        private string CategoryValue
        {
            get => _categoryValue.ToString();
            set
            {
                _categoryValue = (APCategory)Enum.Parse(typeof(APCategory), value);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CategoryValue)));
                _ = UpdateUserInfo();
            }
        }

        [UIValue("category-choices")]
        private List<object> _categoryChoices = [.. new APCategory[] { APCategory.Overall, APCategory.True, APCategory.Standard, APCategory.Tech }.Select(a => a.ToString())];

        [UIValue("username")]
		private string Username
		{
			get => _username.Length > 18 ? $"{_username[..15]}..." : _username + "</color>";
			set
			{
				_username = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Username)));
			}
		}

		[UIValue("rank")]
		private string Rank
		{
			get => _rank;
			set
			{
				_rank = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Rank)));
			}
		}

		[UIValue("country")]
		private string Country
		{
			get => _country;
			set
			{
				_country = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Country)));
			}
		}

		[UIValue("title")]
		private string Title
		{
			get => _title;
			set
			{
				_title = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
			}
		}
		[UIValue("level")]
		private string Level
		{
			get => _level;
			set
			{
				_level = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Level)));
			}
		}

		[UIValue("ap")]
		private string Ap
		{
			get => _ap;
			set
			{
				_ap = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Ap)));
			}
		}
		[UIValue("xp")]
		private string Xp
		{
			get => _xp;
			set
			{
				_xp = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Xp)));
			}
		}


		[UIValue("plays")]
		private string Plays
		{
			get => _plays;
			set
			{
				_plays = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Plays)));
			}
		}

		[UIValue("headset")]
		private string Headset
		{
			get => _headset;
			set
			{
				_headset = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Headset)));
			}
		}

		[UIAction("format-category")]
		private string FormatCategory(string value)
		{
			if (value == "Overall")
			{
				return value;
			}

			return value + " Acc";
		}

		[UIValue("pagnation")]
		private string Pagnation
		{
			get => _pagnation;
			set
			{
				_pagnation = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Pagnation)));
			}
		}


		[UIValue("prev-enabled")]
		private bool PrevEnabled => PageNumber != 0;

		[UIValue("next-enabled")]
		private bool NextEnabled => PageNumber + 1 < _maxPage;

		[UIValue("discordImg")]
		private const string DiscordImg = ResourcePaths.DISCORD;

        [UIValue("kofiImg")]
        private const string KofiImg = ResourcePaths.KOFI;

		[UIValue("githubImg")]
		private const string GithubImg = ResourcePaths.GITHUB;


        [UIAction("#post-parse")]
        void Parsed()
        {
			if (!_parsed)
			{
				_userInfoCanvasGroup = _userInfo.gameObject.AddComponent<CanvasGroup>();

				_profileImage.material = ResourcePaths.BORDER_MATERIAL;

				_parsed = true;
			}
			IsLoading = true;
			_firstLoad = true;
			CategoryValue = "Overall";
        }

		[UIAction("prev-clicked")]
		private void PrevClicked()
		{
			if (PrevEnabled)
			{
				PageNumber--;
			}
		}

		[UIAction("next-clicked")]
		private void NextClicked()
		{
			if (NextEnabled)
			{
				PageNumber++;
			}
		}
		// unused for now
        [UIAction("show-campaign")]
        private void ShowCampaign()
        {
            campaignFlow.PresentFlowCoordinator();
        }

		[UIAction("on-discord-clicked")]
		private void OnDiscordClicked()
		{
            System.Diagnostics.Process.Start("https://discord.gg/DmzKSgcJWe");
        }
        [UIAction("on-kofi-clicked")]
        private void OnKofiClicked()
        {
            System.Diagnostics.Process.Start("https://ko-fi.com/accsaberreloaded");
        }
        [UIAction("on-github-clicked")]
        private void OnGithubClicked()
        {
            System.Diagnostics.Process.Start("https://github.com/not-dexter/accsaber-reloaded-plugin");
        }
        private async Task UpdateUserInfo()
		{

            IsLoading = true;

            await PlayerSocialLife.LoadTask;

            var user = PlayerSocialLife.PlayerID;

			if (user is null)
				return;

            _user = await AccsaberAPI.GetPlayerInfo(user, true, true);        

            await SetUserInfo(_user!, _user!.Statistics!.First(stat => stat.Category == _categoryValue));
        }

		private async Task SetUserInfo(AccSaberPlayer userInfo, AccSaberPlayerStats stats)
		{
			var _color = userInfo.LevelData.PlayerTitle.ToLower() switch
			{
				"newcomer" => "#6B7280",
				"apprentice" => "#3b82f6",
				"adept" => "#10b981",
				"skilled" => "#cd7f32",
				"expert" => "#c0c0d0",
				"master" => "#fbbf24",
				"grandmaster" => "#8b5cf6",
				"legend" => "#f97316",
				"transcendent" => "#22d3ee",
				"mythic" => "#ef4444",
				"ascendant" => "#22d3ee",
				_ => "#f472b6",
			};

			PageNumber = 0;

            string StatDiff(float stat)
			{
				if (stat != 0)
					return (stat < 0) ? $"<color=#ef4444><size=75%>▼{Math.Abs(stat):F2}</size></color>" : $"<color=#22c55e><size=75%>▲{Math.Abs(stat):F2}</size></color>";
				else
					return "";
			}
			string StatDiffInt(int stat)
			{
				if (stat != 0)
					return (stat < 0) ? $"<color=#ef4444><size=75%>▼{Math.Abs(stat)}</size></color>" : $"<color=#22c55e><size=75%>▲{Math.Abs(stat)}</size></color>";
				else
					return "";
			}

			if (stats.StatDiffs is null)
				return;

			// this stat diff positioning fix is so lazy LMAO

			Username = $"{userInfo.PlayerName}";
			Rank = stats.StatDiffs.RankingDiff != 0 ? $"<color=#FFFFFF00><size=75%>▼{Math.Abs(stats.StatDiffs.RankingDiff * -1)}</size></color>  #{stats.Rank}  {StatDiffInt(stats.StatDiffs.RankingDiff * -1)}" : $"#{stats.Rank}";
			Country = stats.StatDiffs.CountryDiff != 0 ? $"<color=#FFFFFF00><size=75%>▼{Math.Abs(stats.StatDiffs.CountryDiff * -1)}</size></color>  #{stats.CountryRank}  {StatDiffInt(stats.StatDiffs.CountryDiff * -1)}" : $"#{stats.CountryRank}";
			Title = $"{"<color=" + _color + ">" + userInfo.LevelData.PlayerTitle}</color>";
			Ap = stats.StatDiffs.ApDiff != 0 ? $"<color=#FFFFFF00><size=75%>▼{Math.Abs(stats.StatDiffs.ApDiff * -1):F2}</size></color>  {stats.AP:N2} AP  {StatDiff(stats.StatDiffs.ApDiff)}" : $"{stats.AP:N2} AP";
			Level = $"LVL {userInfo.LevelData.PlayerLevel}";
			Xp = $"{userInfo.LevelData.XPForCurrentLevel:N0} / {userInfo.LevelData.XPForNextLevel:N0} XP";
			Plays = $"{stats.Plays} ranked plays";
			Headset = userInfo.Headset ?? "";

			userInfo.LevelData.ProgressPercent /= 100f;


			const float barLen = 20f;

			if (_firstLoad)
			{
				_progressBar.transform.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, barLen * userInfo.LevelData.ProgressPercent);
				_progressBarInverse.transform.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, barLen * (1 - userInfo.LevelData.ProgressPercent));
				await _progressBarImage.SetImageAsync(ResourcePaths.PIXEL, false);
				if (userInfo.AvatarUrl is not null)
					await _profileImage.SetImageAsync(userInfo.AvatarUrl, false);
				if (ColorUtility.TryParseHtmlString(_color, out Color newCol))
					_progressBarImage.color = newCol;

				IsLoading = false;
				_firstLoad = false;
			}
			else
			{
				IsLoading = false;

				if (_userInfoCanvasGroup is null)
				{
					return;
				}

				var tween = new FloatTween(0f, 1f, val => _userInfoCanvasGroup.alpha = val, 0.5f, EaseType.OutSine);
				_timeTweeningManager.AddTween(tween, this);
			}		
		}

		private async Task RefreshScores()
        {
			AsyncLock.Releaser? locker = await refreshLock.LockAsync();

			if (locker is null)
				return;

			using (locker.Value)
			{

				IsScoresLoading = true;
				_scoreCells.Clear();

				try
				{
					/*// Use callAPI_String for getting strings like we are here. When directly calling, make sure to provide the throttler so we don't go over the limit.
					// (throttler keeps track of all api calls and will throttle connection if to many are sent in a period of time)
					string? response = await APIHandler.CallAPI_String($"{APAPI}users/{_userId}/scores?page={PageNumber}&size=5{CategoryId}&sort=weightedAp,desc&sort=ap,desc", AccsaberAPI.throttler)

					// If the api response is null, then it failed, so we can return.
					if (response is null)
						return;

					// Instead of worrying about getting the page info, I made this generic model for getting page information while also parsing the main content.
					// (I'm pretty sure this is how Tiku handles it on his end as well)
					AccSaberPagedContent<AccSaberLeaderboardEntry>? content = JsonConvert.DeserializeObject<AccSaberPagedContent<AccSaberLeaderboardEntry>>(response);*/

					// Or instead of the above stuff, just use an AccsaberAPI call (or make one if it doesn't exist)
					IEnumerable<AccSaberPlayerScore>? content = await AccsaberAPI.GetPlayerScores(PageNumber, 5, _categoryValue);

					// This is probably never null, but check just in case.
					if (content is null)
						return;

					// If you call with directly, you can get the page info directly.
					//_maxPage = content.TotalPages;

					// Otherwise, AccsaberAPI will save it to cache (well, it'll save the number of elements, gotta divide by page length).
					_maxPage = (int)Math.Ceiling(SerializerHandler.CachedPlayerScoreLength / 5f);

					Pagnation = $"{_pageNumber + 1}/{_maxPage}";

					// From here on, the loading is the same
					//foreach (AccSaberLeaderboardEntry score in content.Content!)
					//{
					//	_scoreCells.Add(new ScoreCell(score.Rank.ToString(), score.SongName, score.SongAuthor, score.Difficulty, score.Accuracy.ToString(), score.AP.ToString(), score.CategoryId, score.CoverUrl));
					//}

					// Just gotta use a different type with AccsaberAPI (I did this so that I wouldn't have to cache a full LeaderboardEntry, just the important parts.
					foreach (AccSaberPlayerScore score in content)
					{
						_scoreCells.Add(new ScoreCell(score.Rank.ToString(), score.SongName, score.SongAuthor, score.Difficulty.ToString(), score.Accuracy.ToString(), score.AP.ToString(), score.AP.ToString(), EnumUtils.EnumToReloadedCategory(score.Category)!, score.CoverUrl));
					}


					// Make sure to do UI updates at the end of frame. Otherwise you are spinning a wheel on whether Unity will crash the game or not.
					IEnumerator WaitThenUpdate()
					{
						yield return new WaitForEndOfFrame();

						_topScoresList.Data = _scoreCells;
						IsScoresLoading = false;
					}
					StartCoroutine(WaitThenUpdate());

				}
				catch (Exception e)
				{
					// Since errors are not thrown in this function, throw them ourselves
					Plugin.Log.Error(e);
				}
			}
        }

		internal class ScoreCell : ICellDataSource
		{
			public string TemplatePath => ResourcePaths.ACC_SABER_MENU_CELL;
			public float CellSize => 9f;
			public int TemplateId { get; set; }

			#region BSML Values
			[UIValue("score-rank")]
			private readonly string _scoreRank;

			[UIValue("map-name")]
			private readonly string _mapName;

			[UIValue("map-author")]
			private readonly string _mapAuthor;

			[UIValue("map-diff")]
			private readonly string _mapDiff;

			[UIValue("score-acc")]
			private readonly string _scoreAcc;

			[UIValue("score-ap")]
			private readonly string _scoreAp;

            [UIValue("score-weighted")]
            private readonly string _scoreWeighted;

            [UIValue("map-category")]
			private readonly string _mapCategory;

			[UIValue("map-cover")]
			private readonly string _mapCover;


			#endregion
			public ScoreCell(string scoreRank, string mapName, string mapAuthor, string mapDiff, string scoreAcc, string scoreAp, string scoreWeighted, string mapCategory, string mapCover)
			{
				_scoreRank = $"#{scoreRank}";
				_mapName = mapName;
				_mapAuthor = mapAuthor;
				_mapDiff = DiffName(mapDiff);
				_scoreAcc = $"{float.Parse(scoreAcc) * 100:F2}%";
				_scoreAp = $"{float.Parse(scoreAp):F2} AP";
                _scoreWeighted = $"<color={ColorUtils.GREY}>({float.Parse(scoreWeighted):F2} AP)</color>";
                _mapCategory = CategoryName(mapCategory);
				_mapCover = mapCover;
            }

			private string DiffName(string CategoryId)
			{
				var returnString = CategoryId switch
				{
					"EXPERT_PLUS" => "<color=#8b5cf6>Expert+</color>",
					"EXPERT" => "<color=#ef4444>Expert</color>",
					"HARD" => "<color=#f97316>Hard</color>",
					"NORMAL" => "<color=#4a90d9>Normal</color>",
					"EASY" => "<color=#3cb371>Easy</color>",
					_ => ""
				};
				return returnString;
			}


			private string CategoryName(string CategoryId)
			{
				var returnString = CategoryId switch
				{
					"b0000000-0000-0000-0000-000000000001" => "<color=#22c55e>True</color>",
					"b0000000-0000-0000-0000-000000000003" => "<color=#ef4444>Tech</color>",
					"b0000000-0000-0000-0000-000000000002" => "<color=#3b82f6>Standard</color>",
					_ => ""
				};
				return returnString;
			}
			[UIComponent("cover")]
			private readonly ImageView cover = null!;

			[UIAction("#post-parse")]
			void Parse()
            {
				cover.material = ResourcePaths.BORDER_MATERIAL;
			}

		}

		private async void OnAccSaberPlayerUpdated(AccSaberLeaderboardEntry entry)
        {
			await PlayerSocialLife.LoadTask;

			if (entry.PlayerId == PlayerSocialLife.PlayerID)
            {
				_user = null;
				UpdateUserInfo();
			}
        }


		public void Initialize()
		{
			AccSaberStore.OnScoreUpdated += OnAccSaberPlayerUpdated;

        }

		public void Dispose()
		{
            AccSaberStore.OnScoreUpdated -= OnAccSaberPlayerUpdated;
		}

	}
}