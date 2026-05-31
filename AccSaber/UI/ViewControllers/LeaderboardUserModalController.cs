// TODO: Add ACC Campaign badges when the API properly exposes the information

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AccSaber.API;
using AccSaber.Consts;
using AccSaber.Models.PlayerModels;
using AccSaber.Utils;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Parser;
using HMUI;
using TMPro;
using Tweening;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace AccSaber.UI.ViewControllers
{
	internal sealed class LeaderboardUserModalController : INotifyPropertyChanged, IDisposable
	{
#pragma warning disable IDE0051
		private string? _userId;
		private AccSaberPlayer? _user;
		private bool _parsed;
		private bool _firstLoad;
		private bool _isLoading;
		private bool _relationLoading;
		private APCategory _categoryValue = APCategory.Overall;
		private string _username = "";
		private string _rank = null!;
		private string _country = null!;
		private Coroutine? titleRoutine, borderRoutine;
		private string _level = null!;
		private string _ap = null!;
		private string _xp = null!;
		private string _plays = null!;
		private string _headset = null!;

		//private bool friendColorSwapped = false, rivalColorSwapped = false;

		private MonoBehaviour? _host;

		public event PropertyChangedEventHandler? PropertyChanged;

		[UIValue("pixelImg")]
		private const string PixelImg = ResourcePaths.PIXEL;

		[UIValue("followColor")]
		private const string FollowColor = ColorUtils.RELATIONS_ACC;

		[UIValue("rivalColor")]
		private const string RivalColor = ColorUtils.RELATIONS_TARGETED;

		[UIComponent("modal")]
		private ModalView _modalView = null!;

		[UIComponent("category-dropdown")]
		private readonly Transform _categoryDropdown = null!;

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

		[UIComponent("title-text")]
		private readonly TextMeshProUGUI _titleText = null!;

        [UIValue("dimColor")] public const string dimColor = ColorUtils.DARK_BLUE;
        [UIValue("playerImageBorder")] public const string playerImageBorderPath = ResourcePaths.GRADIENT_CORNER;

        [UIComponent("playerImageBackground")] private readonly ImageView _playerImageBackground = null!;
        [UIComponent("playerImageBorder")] private readonly ImageView _playerImageBorder = null!;


        [UIValue("playerImageSize")] public const float playerImageSize = 15f;
        public const float borderSize = 1.5f;
        [UIValue("playerImageBGSize")] public const float playerImageBGSize = borderSize + playerImageSize;

        [UIParams]
		private readonly BSMLParserParams _parserParams = null!;

		private CanvasGroup? _userInfoCanvasGroup;

		[Inject] private readonly TimeTweeningManager _timeTweeningManager = null!;

		#region UI Values

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
			get => _categoryValue.ToString();
			set
			{
				_categoryValue = (APCategory)Enum.Parse(typeof(APCategory), value);
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CategoryValue)));
				_ = UpdateUserInfo();
			}
		}

		[UIValue("category-choices")]
        private readonly List<object> _categoryChoices = [.. new APCategory[] { APCategory.Overall, APCategory.True, APCategory.Standard, APCategory.Tech }.Select(a => a.ToString())];

        [UIValue("username")]
		private string Username
		{
			get => _username;//_username.Length > 18 ? $"{_username[..15]}..." : _username + "</color>";
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

		/*[UIValue("title")]
		private string Title
		{
			get => _title;
			set
			{
				_title = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
			}
		}*/
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

		#endregion

		[UIComponent("add-friend")]
		private readonly Button _friendButton = null!;
		[UIComponent("add-rival")]
		private readonly Button _rivalButton = null!;
        //[UIComponent("add-friend-bg")]
        //private readonly CustomBackground _friendButtonBG = null!;
        //[UIComponent("add-rival-bg")]
        //private readonly CustomBackground _rivalButtonBG = null!;

        [UIValue("relation-loading")]
		private bool RelationLoading
		{
			get => _relationLoading;
			set
			{
				_relationLoading = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RelationLoading)));
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

		[UIAction("friend-clicked")]
		private async void FriendClicked()
		{
			if (_userId is null)
				return;

			await PlayerSocialLife.LoadTask;

			if (PlayerSocialLife.PlayerFollowedIDs_Internal.Contains(_userId))
			{
                await PlayerSocialLife.RemoveId(_userId, LeaderboardDisplayType.Followed);
				_friendButton.gameObject.GetComponent<Button>().SetButtonText("Add Friend");
            }
			else
            {
                await PlayerSocialLife.AddId(_userId, LeaderboardDisplayType.Followed);
                _friendButton.gameObject.GetComponent<Button>().SetButtonText("Remove Friend");
            }
        }

		[UIAction("rival-clicked")]
		private async void RivalClicked()
		{
			if (_userId is null)
				return;

			await PlayerSocialLife.LoadTask;

			if (PlayerSocialLife.PlayerRivalIDs_Internal.Contains(_userId))
			{
                await PlayerSocialLife.RemoveId(_userId, LeaderboardDisplayType.Rivals);
                _rivalButton.gameObject.GetComponent<Button>().SetButtonText("Add Rival");
			}
			else
			{
                await PlayerSocialLife.AddId(_userId, LeaderboardDisplayType.Rivals);
                _rivalButton.gameObject.GetComponent<Button>().SetButtonText("Remove Rival");
			}
        }
		private void Parse(Transform parentTransform)
		{
			if (!_parsed)
			{
				VersionUtils.BSMLParser_Instance.Parse(Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), ResourcePaths.LEADERBOARD_USER_MODAL), parentTransform.gameObject, this);
				_modalView.name = "AccSaberLeaderboardUserModal";
				_modalView.blockerClickedEvent += OnModalClosed;

				var canvasGroup = _modalView.gameObject.AddComponent<CanvasGroup>();
				var dropdownModalView = _categoryDropdown.Find("DropdownTableView").GetComponent<ModalView>();
				// Idk why I can get away with this, but I can and will.
				typeof(ModalView).GetMethod("SetupView", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).Invoke(dropdownModalView, [_modalView.transform]);

                _userInfoCanvasGroup = _userInfo.gameObject.AddComponent<CanvasGroup>();

				_profileImage.material = ResourcePaths.BORDER_MATERIAL;
                _playerImageBackground.material = ResourcePaths.BORDER_MATERIAL;
                _playerImageBorder.material = ResourcePaths.BORDER_MATERIAL;
                //_friendButtonBG.background!.material = ResourcePaths.BORDER_MATERIAL;
                //_rivalButtonBG.background!.material = ResourcePaths.BORDER_MATERIAL;

                //_friendButton.HighlightColor = ColorUtils.RELOADED.Color();
                //_rivalButton.HighlightColor = ColorUtils.TARGETED.Color();

                _parsed = true;
			}
			
			_modalView.transform.SetParent(parentTransform.transform);
			Accessors.ViewValidAccessor(ref _modalView) = false;
		}

		public void ShowModal(Transform parentTransform, MonoBehaviour host, string userId)
		{
            Parse(parentTransform);

            _host = host;
            _userId = userId;
			_firstLoad = true;

			if (!userId.Equals(_user?.PlayerId))
				_user = null;

			IEnumerator Show()
			{
				yield return new WaitForEndOfFrame();

				if (PlayerSocialLife.PlayerID != userId)
				{
                    _friendButton.gameObject.SetActive(true);
                    _rivalButton.gameObject.SetActive(true);

                    if (PlayerSocialLife.PlayerFollowedIDs_Internal.Contains(userId))
						_friendButton.gameObject.GetComponent<Button>().SetButtonText("Remove Friend");
                    else
                        _friendButton.gameObject.GetComponent<Button>().SetButtonText("Add Friend");

                    if (PlayerSocialLife.PlayerRivalIDs_Internal.Contains(userId))
                        _rivalButton.gameObject.GetComponent<Button>().SetButtonText("Remove Rival");
                    else
                        _rivalButton.gameObject.GetComponent<Button>().SetButtonText("Add Rival");
                } else
				{
					_friendButton.gameObject.SetActive(false);
                    _rivalButton.gameObject.SetActive(false);
                }

                CategoryValue = APCategory.Overall.ToString();

				yield return new WaitForFixedUpdate();

                _parserParams.EmitEvent("close-modal");
                _parserParams.EmitEvent("open-modal");
            }

			host.StartCoroutine(Show());
        }

		public void HideModal()
		{
			if (!_parsed)
			{
				return;
			}
			
			_parserParams.EmitEvent("close-modal");
			OnModalClosed();
		}
		
		private async Task UpdateUserInfo()
		{
			if (_userId is null)
			{
				return;
			}

            if (_user is null)
            {
                IsLoading = true;
				_user = await AccsaberAPI.GetPlayerInfo(_userId, true, false);
            }

			if (_user is not null)
				await SetUserInfo(_user, _user.Statistics!.First(stat => stat.Category == _categoryValue));
        }

		private async Task SetUserInfo(AccSaberPlayer userInfo, AccSaberPlayerStats stats) // ty person for the progress bar -- you're welcome :)
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


			string StatDiff(float stat)
            {
				if (stat != 0)
					return (stat < 0) ? $"<color=#ef4444><size=65%>▼{Math.Abs(stat):F2}</size></color>" : $"<color=#22c55e><size=65%>▲{Math.Abs(stat):F2}</size></color>";
				else
					return "";
			}
			string StatDiffInt(int stat)
			{
				if (stat != 0)
					return (stat < 0) ? $"<color=#ef4444><size=65%>▼{Math.Abs(stat)}</size></color>" : $"<color=#22c55e><size=65%>▲{Math.Abs(stat)}</size></color>";
				else
					return "";
			}

			if (stats.StatDiffs is null && !await stats.LoadStatDiff())
				return;

            // this stat diff positioning fix is so lazy LMAO

            Username = $"{userInfo.PlayerName}";
			Rank = stats.StatDiffs!.RankingDiff != 0 ? $"<color=#FFFFFF00><size=65%>▼{Math.Abs(stats.StatDiffs.RankingDiff * -1)}</size></color> #{stats.Rank} {StatDiffInt(stats.StatDiffs.RankingDiff * -1)}" : $"#{stats.Rank}";
			Country = stats.StatDiffs.CountryDiff != 0 ? $"<color=#FFFFFF00><size=65%>▼{Math.Abs(stats.StatDiffs.CountryDiff * -1)}</size></color> #{stats.CountryRank} {StatDiffInt(stats.StatDiffs.CountryDiff * -1)}" : $"#{stats.CountryRank}";
			Ap = stats.StatDiffs.ApDiff != 0 ? $"<color=#FFFFFF00><size=65%>▼{Math.Abs(stats.StatDiffs.ApDiff * -1):F2}</size></color> {stats.AP:N2} AP {StatDiff(stats.StatDiffs.ApDiff)}": $"{stats.AP:N2} AP";
			Level = $"LVL {userInfo.LevelData.PlayerLevel}";
			Xp = $"{userInfo.LevelData.XPForCurrentLevel:N0} / {userInfo.LevelData.XPForNextLevel:N0} XP";
			Plays = $"{stats.Plays} ranked plays";
			Headset = userInfo.Headset ?? "";

			if (titleRoutine is not null)
                _host!.StopCoroutine(titleRoutine);

			await userInfo.LoadItems;

			IEnumerator WaitThenUpdate()
			{
				yield return new WaitUntil(_titleText.IsActive);
				yield return new WaitForEndOfFrame();
                titleRoutine = userInfo.Items!.SetTitle(_titleText, _host!);
            }

			_host!.StartCoroutine(WaitThenUpdate());


            if (_firstLoad)
			{
				if (borderRoutine is not null)
					_host!.StopCoroutine(borderRoutine);

                borderRoutine = userInfo.Items!.SetProfileBorder(_playerImageBorder, _host!);

                await _progressBarImage.SetImageAsync(ResourcePaths.PIXEL, false);
				if (userInfo.AvatarUrl is not null)
					await _profileImage.SetImageAsync(userInfo.AvatarUrl, false);
				if (ColorUtility.TryParseHtmlString(_color, out Color newCol))
					_progressBarImage.color = _color.Color();

                const float barLen = 20f;

                IEnumerator SetBarLen()
				{
					yield return new WaitForEndOfFrame();

                    _progressBar.transform.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, barLen * userInfo.LevelData.ProgressPercent / 100f);
                    _progressBarInverse.transform.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, barLen * (1 - userInfo.LevelData.ProgressPercent / 100f));
                }
				_host!.StartCoroutine(SetBarLen());

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

		private void OnModalClosed()
		{
			_userId = null;
			if (titleRoutine is not null)
			{
				_host!.StopCoroutine(titleRoutine);
				titleRoutine = null;
			}
            if (borderRoutine is not null)
			{
                _host!.StopCoroutine(borderRoutine);
				borderRoutine = null;
            }
        }

		public void Dispose()
		{
			_modalView.blockerClickedEvent -= OnModalClosed;
		}
	}
}