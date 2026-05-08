using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.ViewControllers;
using System.ComponentModel;
using SiraUtil.Logging;
using Zenject;
using BeatSaberMarkupLanguage.Components;
using HMUI;
using UnityEngine;
using UnityEngine.UI;
using AccSaber.Utils;
using System.Threading.Tasks;
using AccSaber.Models;
using System;
using AccSaber.Managers;
using System.Linq;
using System.Collections.Generic;
using Tweening;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace AccSaber.UI.MenuButton.ViewControllers
{
    [ViewDefinition("AccSaber.UI.MenuButton.Views.AccSaberMenuView.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\AccSaberMenuView.bsml")]
    internal class AccSaberMenuViewController : BSMLAutomaticViewController, INotifyPropertyChanged, IInitializable, IDisposable
	{
		private string? _userId;
		private AccSaberUser? _userOverall;
        private AccSaberUser? _userTrue;
        private AccSaberUser? _userStandard;
        private AccSaberUser? _userTech;
        private bool _parsed;
        private bool _firstLoad;
        private bool _isLoading;
		private int _pageNumber = 0;
		private int _maxPage = 1;
		private string _categoryValue = "Overall";
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

		[UIValue("score-cells")]
		private readonly List<ScoreCell> _scoreCells = new List<ScoreCell>();


        public event PropertyChangedEventHandler? PropertyChanged;

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
		private readonly CustomCellListTableData _topScoresList = null!;

		private CanvasGroup? _userInfoCanvasGroup;

		private AccSaberStore _accSaberStore = null!;
		private TimeTweeningManager _timeTweeningManager = null!;

		[Inject]
        public void Construct(AccSaberStore accSaberStore, TimeTweeningManager timeTweeningManager)
        {
            _accSaberStore = accSaberStore;
			_timeTweeningManager = timeTweeningManager;
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

		[UIValue("category-value")]
		private string CategoryValue
		{
			get => _categoryValue;
			set
			{
				_categoryValue = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CategoryValue)));
				_ = UpdateUserInfo();
			}
		}

		[UIValue("category-choices")]
		private List<object> _categoryChoices = new() { "Overall", "True", "Standard", "Tech" };

		[UIValue("username")]
		private string Username
		{
			get => _username.Length > 18 ? $"{_username.Substring(0, 15)}..." : _username + "</color>";
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

		[UIAction("#post-parse")]
        void Parsed()
        {
			if (!_parsed)
			{
				_userInfoCanvasGroup = _userInfo.gameObject.AddComponent<CanvasGroup>();

				_profileImage.material = Resources.FindObjectsOfTypeAll<Material>().Last(x => x.name == "UINoGlowRoundEdge");

				_parsed = true;
			}
			IsLoading = true;
			var userInfo = _accSaberStore.GetCurrentUser().Result;
			_userId = userInfo.PlayerId;
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
		private async Task UpdateUserInfo()
		{
			if (_userId is null)
			{
				return;
			}
			switch (CategoryValue)
			{
				case "Overall":
					{
						if (_userOverall is null)
						{
							var userInfo = await _accSaberStore.GetUserFromId(_userId, null, true);
							_userOverall = userInfo;
						}

						await SetUserInfo(_userOverall);

						break;
					}
				case "True":
					{
						if (_userTrue is null)
						{
							var userInfo = await _accSaberStore.GetUserFromId(_userId, AccSaberStore.AccSaberMapCategories.True, true);
							_userTrue = userInfo;
						}

						await SetUserInfo(_userTrue);
						break;
					}
				case "Standard":
					{
						if (_userStandard is null)
						{
							var userInfo = await _accSaberStore.GetUserFromId(_userId, AccSaberStore.AccSaberMapCategories.Standard, true);
							_userStandard = userInfo;
						}

						await SetUserInfo(_userStandard);
						break;
					}
				case "Tech":
					{
						if (_userTech is null)
						{
							var userInfo = await _accSaberStore.GetUserFromId(_userId, AccSaberStore.AccSaberMapCategories.Tech, true);
							_userTech = userInfo;
						}

						await SetUserInfo(_userTech);
						break;
					}
			}
		}

		private async Task SetUserInfo(AccSaberUser userInfo)
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

			// this stat diff positioning fix is so lazy LMAO

			Username = $"{userInfo.PlayerName}";
			Rank = userInfo.StatsDiff.RankingDiff != 0 ? $"<color=#FFFFFF00><size=75%>▼{Math.Abs(userInfo.StatsDiff.RankingDiff * -1)}</size></color>  #{userInfo.Rank}  {StatDiffInt(userInfo.StatsDiff.RankingDiff * -1)}" : $"#{userInfo.Rank}";
			Country = userInfo.StatsDiff.CountryDiff != 0 ? $"<color=#FFFFFF00><size=75%>▼{Math.Abs(userInfo.StatsDiff.CountryDiff * -1)}</size></color>  #{userInfo.CountryRank}  {StatDiffInt(userInfo.StatsDiff.CountryDiff * -1)}" : $"#{userInfo.CountryRank}";
			Title = $"{"<color=" + _color + ">" + userInfo.LevelData.PlayerTitle}</color>";
			Ap = userInfo.StatsDiff.ApDiff != 0 ? $"<color=#FFFFFF00><size=75%>▼{Math.Abs(userInfo.StatsDiff.ApDiff * -1):F2}</size></color>  {userInfo.AP:N2} AP  {StatDiff(userInfo.StatsDiff.ApDiff)}" : $"{userInfo.AP:N2} AP";
			Level = $"LVL {userInfo.LevelData.PlayerLevel}";
			Xp = $"{userInfo.LevelData.XPForCurrentLevel:N0} / {userInfo.LevelData.XPForNextLevel:N0} XP";
			Plays = $"{userInfo.RankedPlays} ranked plays";
			Headset = userInfo.Hmd ?? "";

			userInfo.LevelData.ProgressPercent /= 100f;


			const float barLen = 20f;

			if (_firstLoad)
			{
				_progressBar.transform.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, barLen * userInfo.LevelData.ProgressPercent);
				_progressBarInverse.transform.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, barLen * (1 - userInfo.LevelData.ProgressPercent));
				await _progressBarImage.SetImageAsync("AccSaber.Resources.Pixel.png", false);
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

			_scoreCells.Clear();
			_topScoresList.Data.Clear();
			var CategoryId = _categoryValue switch
			{
				"Overall" => "",
				"Tech" => "&categoryId=b0000000-0000-0000-0000-000000000003",
				"True" => "&categoryId=b0000000-0000-0000-0000-000000000001",
				"Standard" => "&categoryId=b0000000-0000-0000-0000-000000000002",
				_ => ""
			};

			var response = await Plugin.WebClient.GetAsync($"/v1/users/{_userId}/scores?page={PageNumber}&size=5{CategoryId}&sort=weightedAp,desc&sort=ap,desc");

			if (response is null)
			{
				return;
			}
			var parsedStr = await response.Content.ReadAsStringAsync();

			if (parsedStr != null)
			{
				var parsed = JObject.Parse(parsedStr);

				_maxPage = parsed["totalPages"]!.ToObject<int>();

				Pagnation = $"{_pageNumber + 1}/{_maxPage}";

				if (parsed["content"] is JArray content)
				{
					var scores = JsonConvert.DeserializeObject<List<AccSaberLeaderboardEntry>>(content.ToString());

					foreach (var score in scores)
					{
						_scoreCells.Add(new ScoreCell(score.Rank.ToString(), score.SongName, score.SongAuthor, score.Difficulty, score.Accuracy.ToString(), score.AP.ToString(), score.CategoryId, score.CoverUrl));
					}
					_topScoresList.TableView.ReloadData();
				}
			}

        }

		internal class ScoreCell
		{
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

			[UIValue("map-category")]
			private readonly string _mapCategory;

			[UIValue("map-cover")]
			private readonly string _mapCover;


			#endregion
			public ScoreCell(string scoreRank, string mapName, string mapAuthor, string mapDiff, string scoreAcc, string scoreAp, string mapCategory, string mapCover)
			{
				_scoreRank = $"#{scoreRank}";
				_mapName = mapName;
				_mapAuthor = mapAuthor;
				_mapDiff = DiffName(mapDiff);
				_scoreAcc = $"{(float.Parse(scoreAcc) * 100):F2}%";
				_scoreAp = $"{float.Parse(scoreAp):F2} AP";
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
				cover.material = Resources.FindObjectsOfTypeAll<Material>().Last(x => x.name == "UINoGlowRoundEdge");
			}

		}

		private void OnAccSaberUserUpdated(bool isNew)
        {
			if(isNew)
            {
				_userOverall = null;
				_userTrue = null;
				_userStandard = null;
				_userTech = null;
			}
        }


		public void Initialize()
		{
			_accSaberStore.OnUpdatedFromAccSaberAPI += OnAccSaberUserUpdated;
		}

		public void Dispose()
		{
			_accSaberStore.OnUpdatedFromAccSaberAPI -= OnAccSaberUserUpdated;
		}

	}
}