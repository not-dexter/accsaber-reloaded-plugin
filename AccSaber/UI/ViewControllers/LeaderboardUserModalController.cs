// TODO: Add ACC Campaign badges when the API properly exposes the information

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AccSaber.API;
using AccSaber.Configuration;
using AccSaber.Consts;
using AccSaber.Managers;
using AccSaber.Models;
using AccSaber.Utils;﻿
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Parser;
using HMUI;
using IPA.Utilities;
using Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace AccSaber.UI.ViewControllers
{
	internal sealed class LeaderboardUserModalController : INotifyPropertyChanged, IDisposable
	{
		private string? _userId;
		private AccSaberUser? _user;
		private readonly PluginConfig _pluginConfig = null!;
		private bool _parsed;
		private bool _firstLoad;
		private bool _isLoading;
		private bool _relationLoading;
		private APCategory _categoryValue = APCategory.Overall;
		private string _username = "";
		private string _rank = null!;
		private string _country = null!;
		private string _title = null!;
		private string _level = null!;
		private string _ap = null!;
		private string _xp = null!;
		private string _plays = null!;
		private string _headset = null!;

		private MonoBehaviour? _host;

		public event PropertyChangedEventHandler? PropertyChanged;

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

		[UIParams]
		private readonly BSMLParserParams _parserParams = null!;

		private CanvasGroup? _userInfoCanvasGroup;

		private readonly AccSaberStore _accSaberStore;
		private readonly TimeTweeningManager _timeTweeningManager;

		public LeaderboardUserModalController(AccSaberStore accSaberStore, TimeTweeningManager timeTweeningManager, PluginConfig pluginConfig)
		{
			_accSaberStore = accSaberStore;
			_timeTweeningManager = timeTweeningManager;
			_pluginConfig = pluginConfig;
		}

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
		private List<object> _categoryChoices = [.. new APCategory[] { APCategory.Overall, APCategory.True, APCategory.Standard, APCategory.Tech }.Select(a => a.ToString())];	

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

		#endregion

		[UIObject("add-friend")]
		private readonly GameObject _addFriendButton = null!;

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

		[UIAction("add-friend-clicked")]
		private async void AddFriendClicked()
		{
			if (_userId is null)
				return;

			await PlayerSocialLife.LoadTask;

			if (PlayerSocialLife.PlayerFollowedIDs_Internal.Contains(_userId))
			{
				_addFriendButton.gameObject.GetComponent<Button>().enabled = false;
				_addFriendButton.gameObject.SetActive(false);
				RelationLoading = true;
                await PlayerSocialLife.RemoveId(_userId, LeaderboardDisplayType.Followed);
				RelationLoading = false;
				_addFriendButton.gameObject.GetComponent<Button>().SetButtonText("Add Friend");
				_addFriendButton.gameObject.SetActive(true);
				_addFriendButton.gameObject.GetComponent<Button>().enabled = true;
			}
			else
			{
				_addFriendButton.gameObject.GetComponent<Button>().enabled = false;
				_addFriendButton.gameObject.SetActive(false);
				RelationLoading = true;
				await PlayerSocialLife.AddId(_userId, LeaderboardDisplayType.Followed);
                RelationLoading = false;
				_addFriendButton.gameObject.GetComponent<Button>().SetButtonText("Remove Friend");
				_addFriendButton.gameObject.SetActive(true);
				_addFriendButton.gameObject.GetComponent<Button>().enabled = true;
			}
		}
		private void Parse(Transform parentTransform)
		{
			if (!_parsed)
			{
				VersionUtils.BSMLParser_Instance.Parse(Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), "AccSaber.UI.Views.LeaderboardUserModal.bsml"), parentTransform.gameObject, this);
				_modalView.name = "AccSaberLeaderboardUserModal";
				_modalView.blockerClickedEvent += OnModalClosed;

				var canvasGroup = _modalView.gameObject.AddComponent<CanvasGroup>();
				var dropdownModalView = _categoryDropdown.Find("DropdownTableView").GetComponent<ModalView>();
				//dropdownModalView.SetupView(_modalView.transform);
				//dropdownModalView.transform.SetParent(_modalView.transform);
                dropdownModalView.SetField("_parentCanvasGroup", canvasGroup);
				
				_userInfoCanvasGroup = _userInfo.gameObject.AddComponent<CanvasGroup>();
				
				_profileImage.material = Resources.FindObjectsOfTypeAll<Material>().Last(x => x.name == "UINoGlowRoundEdge");

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

				if (!PlayerSocialLife.PlayerFollowedIDs_Internal.Contains(userId) && PlayerSocialLife.PlayerID != userId)
				{
					_addFriendButton.gameObject.GetComponent<Button>().SetButtonText("Add Friend");
					_addFriendButton.gameObject.GetComponent<Button>().gameObject.SetActive(true);
				}
				else if (PlayerSocialLife.PlayerID == userId)
				{
					_addFriendButton.gameObject.GetComponent<Button>().gameObject.SetActive(false);
				}
				else
				{
					_addFriendButton.gameObject.GetComponent<Button>().SetButtonText("Remove Friend");
					_addFriendButton.gameObject.GetComponent<Button>().gameObject.SetActive(true);
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
				_user = await AccsaberAPI.GetPlayerInfo(_userId, true, true);
            }

			if (_user is not null)
				await SetUserInfo(_user, _user.Statistics!.First(stat => stat.Category == _categoryValue));
        }

		private async Task SetUserInfo(AccSaberUser userInfo, PlayerStats stats) // ty person for the progress bar -- you're welcome :)
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

			if (stats.StatsDiff is null)
				return;

			// this stat diff positioning fix is so lazy LMAO

			Username = $"{userInfo.PlayerName}";
			Rank = stats.StatsDiff.RankingDiff != 0 ? $"<color=#FFFFFF00><size=75%>▼{Math.Abs(stats.StatsDiff.RankingDiff * -1)}</size></color>  #{stats.Rank}  {StatDiffInt(stats.StatsDiff.RankingDiff * -1)}" : $"#{stats.Rank}";
			Country = stats.StatsDiff.CountryDiff != 0 ? $"<color=#FFFFFF00><size=75%>▼{Math.Abs(stats.StatsDiff.CountryDiff * -1)}</size></color>  #{stats.CountryRank}  {StatDiffInt(stats.StatsDiff.CountryDiff * -1)}" : $"#{stats.CountryRank}";
			Title = $"{"<color=" + _color + ">" +userInfo.LevelData.PlayerTitle}</color>";
			Ap = stats.StatsDiff.ApDiff != 0 ? $"<color=#FFFFFF00><size=75%>▼{Math.Abs(stats.StatsDiff.ApDiff * -1):F2}</size></color>  {stats.AP:N2} AP  {StatDiff(stats.StatsDiff.ApDiff)}": $"{stats.AP:N2} AP";
			Level = $"LVL {userInfo.LevelData.PlayerLevel}";
			Xp = $"{userInfo.LevelData.XPForCurrentLevel:N0} / {userInfo.LevelData.XPForNextLevel:N0} XP";
			Plays = $"{stats.Plays} ranked plays";
			Headset = userInfo.Headset ?? "";

			const float barLen = 20f;

            if (_firstLoad)
			{
				await _progressBarImage.SetImageAsync(ResourcePaths.PIXEL, false);
				if (userInfo.AvatarUrl is not null)
					await _profileImage.SetImageAsync(userInfo.AvatarUrl, false);
				if (ColorUtility.TryParseHtmlString(_color, out Color newCol))
					_progressBarImage.color = _color.Color();

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
		}

		public void Dispose()
		{
			_modalView.blockerClickedEvent -= OnModalClosed;
		}
	}
}