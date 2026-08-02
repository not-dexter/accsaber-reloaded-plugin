using AccSaber.API;
using AccSaber.Consts;
using AccSaber.Managers;
using AccSaber.Models;
using AccSaber.Models.CacheModels;
using AccSaber.Models.PlayerModels;
using AccSaber.UI.MenuButton.Campaigns;
using AccSaber.Utils;
using AccsaberLeaderboard.UI.BSML_Addons.Components;
using BeatSaberMarkupLanguage.Attributes;
using HMUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using Tweening;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using AccSaber.Configuration;
using AccSaber.Utils.Misc;
using System.Threading;
using BeatSaberMarkupLanguage.Components;





#if NEW_VERSION
using BeatSaberMarkupLanguage;
#endif


namespace AccSaber.UI.MenuButton.ViewControllers
{
    [ViewDefinition("AccSaber.UI.MenuButton.Views.AccSaberMenuView.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\AccSaberMenuView.bsml")]
    internal class AccSaberMenuViewController : Utils.Safety.BSMLSafeAutomaticViewController, IInitializable, IDisposable, AccSaberNotificationModal.IPopup
	{
        private AccSaberPlayer? _user;
        private bool _parsed;
        private bool _firstLoad;
        private bool _isLoading;
		private bool _isScoresLoading;
        private bool _isRankingLoading;
        private int _pageNumber = 0;
        private int _rankingsPageNumber = 0;
        private int _maxPage = 1;
        private int _rankingsMaxPage = 1;
        private APCategory _categoryValue = APCategory.Overall;
        private string _rankingCategory = string.Empty;
        private string _username = "";
		private string _pagnation = "";
        private string _rankingsPagnation = "";
        private string _rank = null!;
        private string _country = null!;
        private string _level = null!;
        private string _ap = null!;
        private string _xp = null!;
        private string _plays = null!;
        private string _headset = null!;
		private MenuTab _currentTab;
        private RankingsTab _rankingTab;
        private RankingsScope _rankingScope;
        private readonly Color _selectedColor = new(0.60f, 0.80f, 1);
        private enum MenuTab
        {
            Profile,
            Rankings
        }
        private enum RankingsTab
        {
            Overall,
            True,
			Standard,
			Tech
        }
		internal enum RankingsScope
		{
			Global,
			Followed,
			Country
		}
        private MenuTab CurrentTab
        {
            get => _currentTab;
            set
            {
                _currentTab = value;
                NotifyPropertyChanged(nameof(IsProfileTab));
                NotifyPropertyChanged(nameof(IsRankingsTab));
            }
        }
        private RankingsTab RankingTab
        {
            get => _rankingTab;
            set
            {
                _rankingTab = value;

				_rankingCategory = RankingTab switch
                {
                    RankingsTab.Overall => "b0000000-0000-0000-0000-000000000005",
                    RankingsTab.True => "b0000000-0000-0000-0000-000000000001",
                    RankingsTab.Standard => "b0000000-0000-0000-0000-000000000002",
                    RankingsTab.Tech => "b0000000-0000-0000-0000-000000000003",
                    _ => throw new NotImplementedException()
                };
                _ = UpdateRankings();
            }
        }

        private RankingsScope RankingScope
        {
            get => _rankingScope;
            set
            {
                _rankingScope = value;
                RankingsPageNumber = 0;
            }
        }

        [UIValue("is-profile-tab")]
        private bool IsProfileTab => CurrentTab == MenuTab.Profile;

        [UIValue("is-rankings-tab")]
        private bool IsRankingsTab => CurrentTab == MenuTab.Rankings;

        private Coroutine? titleRoutine, borderRoutine;

		private readonly AsyncLock refreshLock = new();

		[Inject] private readonly AccSaberCampaignFlow campaignFlow = null!;
		[Inject] private readonly AccSaberPlaylistModalController playlistModal = null!;
		[Inject] private readonly LevelUtils levelUtils = null!;
		[Inject] private readonly AccSaberMainFlowCoordinator parentCoordinator = null!;
        [Inject] private readonly TimeTweeningManager _timeTweeningManager = null!;
        [Inject] private readonly AccSaberNotificationModal asnm = null!;
        [Inject] private readonly PluginConfig PC = null!;
		[Inject] private readonly PlayerSocialLife playerInfo = null!;
		[Inject] private readonly AccsaberAPI api = null!;
        [Inject] private readonly AccSaberStore accSaberStore = null!;
        [Inject] private readonly SerializationHandler serialHandler = null!;


        [UIValue("score-cells")]
        private readonly List<ICellDataSource> _scoreCells = [];

        [UIValue("player-cells")]
        private readonly List<ICellDataSource> _playerCells = [];

        [UIComponent("profile-image")]
        private readonly ImageView _profileImage = null!;

        [UIComponent("global-image")]
        private readonly ClickableImage _globalImage = null!;

        [UIComponent("followed-image")]
        private readonly ClickableImage _followedImage = null!;

        [UIComponent("country-image")]
        private readonly ClickableImage _countryImage = null!;

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

        [UIComponent("rankings-list")]
        private readonly MyCustomCellListTableData _rankingsList = null!;

        [UIComponent("title-text")]
		private readonly TextMeshProUGUI _titleText = null!;

		private CanvasGroup? _userInfoCanvasGroup;
        private int PageNumber
		{
			get => _pageNumber;
			set
			{
				_pageNumber = value;
				_ = RefreshScores();
			}
		}

        private int RankingsPageNumber
        {
            get => _rankingsPageNumber;
            set
            {
                _rankingsPageNumber = value;
                _ = UpdateRankings();
            }
        }

        [UIValue("dimColor")] public const string dimColor = ColorUtils.DARK_BLUE;
        [UIValue("pixelImg")] public const string pixelImg = ResourcePaths.PIXEL;

        [UIComponent("playerImageBackground")] private readonly ImageView _playerImageBackground = null!;
        [UIComponent("playerImageBorder")] private readonly ImageView _playerImageBorder = null!;


        [UIValue("playerImageSize")] public const float playerImageSize = 13.5f;
        public const float borderSize = 1.5f;
        [UIValue("playerImageBGSize")] public const float playerImageBGSize = borderSize + playerImageSize;

        [UIValue("is-loading")]
        private bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                NotifyPropertyChanged(nameof(IsLoading));
                NotifyPropertyChanged(nameof(IsNotLoading));
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
				NotifyPropertyChanged(nameof(IsScoresLoading));
				NotifyPropertyChanged(nameof(IsScoresNotLoading));
			}
		}

		[UIValue("scores-is-not-loading")]
		private bool IsScoresNotLoading => !_isScoresLoading;

        [UIValue("ranking-is-loading")]
        private bool IsRankingLoading
        {
            get => _isRankingLoading;
            set
            {
                _isRankingLoading = value;
                NotifyPropertyChanged(nameof(IsRankingLoading));
                NotifyPropertyChanged(nameof(IsRankingNotLoading));
            }
        }

        [UIValue("ranking-is-not-loading")]
        private bool IsRankingNotLoading => !_isRankingLoading;

        [UIValue("category-value")]
        private string CategoryValue
        {
            get => _categoryValue.ToString();
            set
            {
                _categoryValue = (APCategory)Enum.Parse(typeof(APCategory), value);
                NotifyPropertyChanged(nameof(CategoryValue));
				WaitThenUpdateUserInfo();
            }
        }

        [UIValue("category-choices")]
        private readonly List<object> _categoryChoices = [.. new APCategory[] { APCategory.Overall, APCategory.True, APCategory.Standard, APCategory.Tech }.Select(a => a.ToString())];

        [UIValue("username")]
		private string Username
		{
			get => _username;
			set
			{
				_username = value;
				NotifyPropertyChanged(nameof(Username));
			}
		}

		[UIValue("rank")]
		private string Rank
		{
			get => _rank;
			set
			{
				_rank = value;
				NotifyPropertyChanged(nameof(Rank));
			}
		}

		[UIValue("country")]
		private string Country
		{
			get => _country;
			set
			{
				_country = value;
				NotifyPropertyChanged(nameof(Country));
			}
		}
		[UIValue("level")]
		private string Level
		{
			get => _level;
			set
			{
				_level = value;
				NotifyPropertyChanged(nameof(Level));
			}
		}

		[UIValue("ap")]
		private string Ap
		{
			get => _ap;
			set
			{
				_ap = value;
				NotifyPropertyChanged(nameof(Ap));
			}
		}
		[UIValue("xp")]
		private string Xp
		{
			get => _xp;
			set
			{
				_xp = value;
				NotifyPropertyChanged(nameof(Xp));
			}
		}


		[UIValue("plays")]
		private string Plays
		{
			get => _plays;
			set
			{
				_plays = value;
				NotifyPropertyChanged(nameof(Plays));
			}
		}

		[UIValue("headset")]
		private string Headset
		{
			get => _headset;
			set
			{
				_headset = value;
				NotifyPropertyChanged(nameof(Headset));
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


        [UIValue("rankings-pagnation")]
        private string RankingsPagnation
        {
            get => _rankingsPagnation;
            set
            {
                _rankingsPagnation = value;
                NotifyPropertyChanged(nameof(RankingsPagnation));
            }
        }


        [UIValue("rankings-prev-enabled")]
        private bool RankingsPrevEnabled => RankingsPageNumber != 0;

        [UIValue("rankings-next-enabled")]
        private bool RankingsNextEnabled => RankingsPageNumber + 1 < _rankingsMaxPage;


        [UIValue("pagnation")]
		private string Pagnation
		{
			get => _pagnation;
			set
			{
				_pagnation = value;
				NotifyPropertyChanged(nameof(Pagnation));
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

        [UIAction("menu-tab-selected")]
        private void MenuTabSelected(SegmentedControl segmentedControl, int index)
        {
            CurrentTab = (MenuTab)index;

            UpdateTabs();
        }

        [UIAction("rankings-tab-selected")]
        private void RankingsTabSelected(SegmentedControl segmentedControl, int index)
        {
            RankingTab = (RankingsTab)index;
        }

        public void UpdateTabs()
        {
            switch (CurrentTab)
            {
                case MenuTab.Profile:
					_ = UpdateUserInfo();
                    break;
                case MenuTab.Rankings:
                    _rankingsPageNumber = 0;
                    _ = UpdateRankings();
                    break;
            }
        }

        [UIAction("#post-parse")]
        void Parsed()
        {
			if (!_parsed)
			{
				_userInfoCanvasGroup = _userInfo.gameObject.AddComponent<CanvasGroup>();

				_profileImage.material = ResourcePaths.BORDER_MATERIAL;
				_playerImageBackground.material = ResourcePaths.BORDER_MATERIAL;
				_playerImageBorder.material = ResourcePaths.BORDER_MATERIAL;

				VersionUtils.Parse(ResourcePaths.ACC_SABER_PLAYLIST_MODAL, gameObject, playlistModal);

                _parsed = true;
            }
            CurrentTab = 0;
			RankingTab = 0;
            _globalImage.DefaultColor = _selectedColor;
            IsLoading = true;
			_firstLoad = true;
			CategoryValue = nameof(APCategory.Overall);
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

        [UIAction("rankings-prev-clicked")]
        private void RankingsPrevClicked()
        {
            if (RankingsPrevEnabled)
            {
                RankingsPageNumber--;
            }
        }

        [UIAction("rankings-next-clicked")]
        private void RankingsNextClicked()
        {
            if (RankingsNextEnabled)
            {
                RankingsPageNumber++;
            }
        }

        // unused for now
        [UIAction("show-campaign")]
        private void ShowCampaign()
        {
            campaignFlow.PresentFlowCoordinator();
        }

        [UIAction("show-playlist-modal")]
        private void ShowPlaylistModal()
        {
			playlistModal.Show();
        }

		[UIAction("on-cell-clicked")]
		private void OnCellClicked(ICellDataSource source)
		{
			if (!PC.DisablePopups)
				_ = asnm.ShowModal(_topScoresList.transform, this, source, parentCoordinator, "Would you like to go to this map?");
			else
				PopupSuccess(source);
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
        [UIAction("on-global-clicked")]
        private void OnGlobalClicked()
        {
            if (_rankingScope == RankingsScope.Global)
                return;

			RankingScope = RankingsScope.Global;
            _globalImage.DefaultColor = _selectedColor;
            _followedImage.DefaultColor = Color.white;
            _countryImage.DefaultColor = Color.white;
        }
        [UIAction("on-followed-clicked")]
        private void OnFollowedClicked()
        {
            if (_rankingScope == RankingsScope.Followed)
                return;

            RankingScope = RankingsScope.Followed;
            _globalImage.DefaultColor = Color.white;
            _followedImage.DefaultColor = _selectedColor;
            _countryImage.DefaultColor = Color.white;
        }
        [UIAction("on-country-clicked")]
        private void OnCountryClicked()
        {
            if (_rankingScope == RankingsScope.Country)
                return;

            RankingScope = RankingsScope.Country;
            _globalImage.DefaultColor = Color.white;
            _followedImage.DefaultColor = Color.white;
            _countryImage.DefaultColor = _selectedColor;
        }
        [UIAction("on-you-clicked")]
        private async void OnYouClicked()
        {
            var curUser = await accSaberStore.GetCurrentUserAsync();
            _rankingsPageNumber = (int)Math.Ceiling(curUser.Statistics.FirstOrDefault(x => x.CategoryId == Guid.Parse(_rankingCategory)).Rank / (float)5) - 1;
            _rankingScope = RankingsScope.Global;
            _globalImage.DefaultColor = _selectedColor;
            _followedImage.DefaultColor = Color.white;
            _countryImage.DefaultColor = Color.white;
            _ = UpdateRankings();
        }


        public void PopupSuccess(object source)
		{
            if (source is ScoreCell cell)
                _ = levelUtils.GoToSong(cell.Data.DifficultyId, null, () => parentCoordinator.CloseToMainMenu(), cell.UpdateStatus);
        }
		private async void OnOpen()
		{
            if (!_firstLoad)
            {
                if (_user is null)
                    await UpdateUserInfo();

                if (titleRoutine is not null)
                    StopCoroutine(titleRoutine);

                IEnumerator WaitThenUpdate()
                {
                    yield return new WaitUntil(_titleText.IsActive);
                    yield return new WaitForEndOfFrame();
                    titleRoutine = _user!.Items!.Set(this, _titleText);
                }
                StartCoroutine(WaitThenUpdate());

                if (borderRoutine is not null)
                    StopCoroutine(borderRoutine);

                borderRoutine = _user!.Items!.Set(this, _playerImageBorder, _progressBarImage);
            }
        }
		private void OnClose()
		{
            if (titleRoutine is not null)
			{
				StopCoroutine(titleRoutine);
				titleRoutine = null;
			}
			if (borderRoutine is not null)
			{
				StopCoroutine(borderRoutine);
				borderRoutine = null;
			}
        }
		private void WaitThenUpdateUserInfo()
		{
			IEnumerator WaitThenUpdate()
			{
				yield return new WaitForEndOfFrame();

				_ = UpdateUserInfo();
			}
			StartCoroutine(WaitThenUpdate());
		}
        private async Task UpdateUserInfo()
		{
			try
			{
				IsLoading = true;

				await playerInfo.LoadTask;

				string? user = playerInfo.PlayerID;

				if (user is null)
					return;

				_user = await api.GetPlayerInfo(user, true, true);

				await SetUserInfo(_user!, _user!.Statistics!.First(stat => stat.Category == _categoryValue));
			}
			catch (Exception e)
			{
				Plugin.Log.Error("There was an error trying to refresh the player!\n" + e);
			}
			finally
			{
                IsLoading = false;
            }
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
			Ap = stats.StatDiffs.ApDiff != 0 ? $"<color=#FFFFFF00><size=75%>▼{Math.Abs(stats.StatDiffs.ApDiff * -1):F2}</size></color>  {stats.AP:N2} AP  {StatDiff(stats.StatDiffs.ApDiff)}" : $"{stats.AP:N2} AP";
			Level = $"LVL {userInfo.LevelData.PlayerLevel}";
			Xp = $"{userInfo.LevelData.XPForCurrentLevel:N0} / {userInfo.LevelData.XPForNextLevel:N0} XP";
			Plays = $"{stats.Plays} ranked plays";
			Headset = userInfo.Headset ?? "";

			if (titleRoutine is not null)
				StopCoroutine(titleRoutine);

			await userInfo.LoadItems;

			IEnumerator WaitThenUpdate()
			{
                yield return new WaitUntil(_titleText.IsActive);
                yield return new WaitForEndOfFrame();
                titleRoutine = userInfo.Items!.Set(this, _titleText);
            }

			if (gameObject.activeInHierarchy)
				StartCoroutine(WaitThenUpdate());

            if (borderRoutine is not null)
                StopCoroutine(borderRoutine);

			if (gameObject.activeInHierarchy)
				borderRoutine = userInfo.Items!.Set(this, _playerImageBorder, _progressBarImage);

            const float barLen = 20f;
            _progressBar.transform.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, barLen * userInfo.LevelData.ProgressPercent / 100f);
            _progressBarInverse.transform.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, barLen * (1 - userInfo.LevelData.ProgressPercent / 100f));

            if (_firstLoad)
			{
				if (userInfo.AvatarUrl is not null)
					await _profileImage.SetImageAsync(userInfo.AvatarUrl, false);

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

				//var tween = new FloatTween(0f, 1f, val => _userInfoCanvasGroup.alpha = val, 0.5f, EaseType.OutSine);
				//_timeTweeningManager.AddTween(tween, this);
			}		
		}

		private async Task RefreshScores()
        {
			AsyncLock.Releaser? locker = await refreshLock.LockAsync();

			if (locker is null)
				return;

			using (locker.Value)
			{
				Utils.Safety.MainThreadDispatcher.AssertOnMainThread();

				IsScoresLoading = true;

				foreach (ScoreCell cell in _scoreCells.Cast<ScoreCell>())
					cell.CancelLoading();

				_scoreCells.Clear();

				try
				{
					IEnumerable<AccSaberPlayerScore> content = await api.GetPlayerScores(PageNumber, 5, _categoryValue);

					_maxPage = (int)Math.Ceiling((_categoryValue == APCategory.Overall ? serialHandler.PlayerScoreLength : serialHandler.CategoryPlayerScores[(int)_categoryValue].Count) / 5f);

					Pagnation = $"{_pageNumber + 1}/{_maxPage}";

					foreach (AccSaberPlayerScore score in content)
					{
						_scoreCells.Add(new ScoreCell(score, serialHandler.GetDiffById(score.DifficultyId)?.Hash ?? throw new Exception("Given diff id was not in cache!")));
					}

                    _topScoresList.Data = _scoreCells;
                    IsScoresLoading = false;
                }
				catch (Exception e)
				{
					Plugin.Log.Error(e);
				}
			}
        }

		private void SetRankings()
		{

		}

        private async Task UpdateRankings()
        {
            AsyncLock.Releaser? locker = await refreshLock.LockAsync();

            if (locker is null)
                return;

            using (locker.Value)
            {
                Utils.Safety.MainThreadDispatcher.AssertOnMainThread();

				IsRankingLoading = true;

                foreach (PlayerCell cell in _playerCells.Cast<PlayerCell>())
                    cell.CancelLoading();

                _playerCells.Clear();

                try
                {
                    List<AccSaberLeaderboardPlayer> content = await accSaberStore.GetLeaderboardRanking(_rankingCategory, _rankingsPageNumber, _rankingScope);

                    _rankingsMaxPage = content.First().MaxPage;

                    RankingsPagnation = $"{_rankingsPageNumber + 1}/{_rankingsMaxPage}";

                    int placement = 1;
                    foreach (AccSaberLeaderboardPlayer player in content)
                    {
                        _playerCells.Add(new PlayerCell(player, Guid.Parse(_rankingCategory), _rankingScope, _rankingsPageNumber, placement));
                        placement++;
                    }

                    _rankingsList.Data = _playerCells;
					IsRankingLoading = false;
                }
                catch (Exception e)
                {
                    Plugin.Log.Error(e);
                }
            }
        }


        private void OnAccSaberPlayerUpdated(AccSaberLeaderboardEntry entry)
        {
            _user = null;
        }


        public void Initialize()
        {
            AccSaberStore.OnPlayerScoreUpdated += OnAccSaberPlayerUpdated;
            parentCoordinator.OnHubActivated += OnOpen;
            parentCoordinator.OnHubDeactivated += OnClose;
        }

        public void Dispose()
        {
            AccSaberStore.OnPlayerScoreUpdated -= OnAccSaberPlayerUpdated;
            parentCoordinator.OnHubActivated -= OnOpen;
            parentCoordinator.OnHubDeactivated -= OnClose;
        }

        internal class PlayerCell(AccSaberLeaderboardPlayer data, Guid category, RankingsScope scope, int page, int placement) : Utils.Safety.SafeNotifyPropertyChanged, ICellDataSource
        {
            public string TemplatePath => ResourcePaths.ACC_SABER_MENU_PLAYER_CELL;
            public float CellSize => 9f;
            public int TemplateId { get; set; }

            public readonly AccSaberLeaderboardPlayer Data = data;

            private readonly CancellationTokenSource tokenSource = new();

            private readonly string rank = scope switch
            {
                RankingsScope.Global => $"#{data.Ranking}",
                RankingsScope.Followed => $"#{placement + (5 * page)}",
                RankingsScope.Country => $"#{data.CountryRanking}",
                _ => $"#{data.Ranking}"
            };

            [UIValue("player-rank")]
            private string _playerRank => rank;

            [UIValue("player-name")]
            private readonly string _playerName = data.UserName;

            [UIValue("player-ap")]
            private readonly string _playerAP = $"<color={ColorUtils.GetColor(EnumUtils.ReloadedCategoryIdToCategory(category))}>{data.AP:N2} AP</color>";

            [UIValue("play-count")]
            private readonly string _playCount = $"{data.RankedPlays}";

            [UIComponent("avatar")]
            private readonly ImageView cover = null!;

            [UIAction("#post-parse")]
            private void Parse()
            {
                cover.material = ResourcePaths.BORDER_MATERIAL;

                _ = cover.LoadImage(Data.AvatarUrl, tokenSource.Token);
            }

            internal void CancelLoading()
            {
                tokenSource.Cancel();
            }
        }

        internal class ScoreCell(AccSaberPlayerScore data, string mapHash) : Utils.Safety.SafeNotifyPropertyChanged, ICellDataSource
        {
			public string TemplatePath => ResourcePaths.ACC_SABER_MENU_CELL;
			public float CellSize => 9f;
			public int TemplateId { get; set; }

            public readonly AccSaberPlayerScore Data = data;
			public readonly string MapHash = mapHash;

			private readonly CancellationTokenSource tokenSource = new();

			#region BSML Values
			private bool _showStatus;
			private string _statusText = null!;

			[UIValue("score-rank")]
			private readonly string _scoreRank = $"#{data.Rank}";

			[UIValue("map-name")]
			private readonly string _mapName = data.SongName;

			[UIValue("map-author")]
			private readonly string _mapAuthor = data.SongAuthor ?? "Unknown Author";

			[UIValue("map-diff")]
			private string MapDiff => DiffName(EnumUtils.DiffToReloadedDiff(Data.Difficulty!.Value));

			[UIValue("score-acc")]
			private readonly string _scoreAcc = $"{data.Accuracy * 100:F2}%";

            [UIValue("score-ap")]
			private readonly string _scoreAp = $"{data.AP:F2} AP";

            [UIValue("score-weighted")]
            private readonly string _scoreWeighted = $"<color={ColorUtils.GREY}>({data.WeightedAp:F2} AP)</color>";

            [UIValue("map-category")]
			private string MapCategory => CategoryName(EnumUtils.CategoryToReloadedCategoryId(Data.Category!.Value));

			[UIValue("show-status")]
			public bool ShowStatus
			{
				get => _showStatus;
				set
				{
					_showStatus = value;
					NotifyPropertyChanged(nameof(ShowStatus));
					NotifyPropertyChanged(nameof(NotShowStatus));

                }
			}
			[UIValue("not-show-status")]
			public bool NotShowStatus => !_showStatus;

			[UIValue("status-text")]
			public string StatusText
			{
				get => _statusText;
				set
				{
					_statusText = value;
                    NotifyPropertyChanged(nameof(StatusText));
                }
			}

            #endregion

			private string DiffName(ReloadedDifficulty diff)
			{
				var returnString = diff switch
				{
                    ReloadedDifficulty.EXPERT_PLUS => "<color=#8b5cf6>Expert+</color>",
                    ReloadedDifficulty.EXPERT => "<color=#ef4444>Expert</color>",
                    ReloadedDifficulty.HARD => "<color=#f97316>Hard</color>",
                    ReloadedDifficulty.NORMAL => "<color=#4a90d9>Normal</color>",
                    ReloadedDifficulty.EASY => "<color=#3cb371>Easy</color>",
					_ => ""
				};
				return returnString;
			}


			private string CategoryName(Guid CategoryId)
			{
				var returnString = CategoryId.ToString() switch
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
			private void Parse()
            {
				cover.material = ResourcePaths.BORDER_MATERIAL;


				_ = cover.LoadCoverImage(MapHash, Data.CoverUrl, tokenSource.Token);
			}

			internal void CancelLoading()
			{
				tokenSource.Cancel();
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