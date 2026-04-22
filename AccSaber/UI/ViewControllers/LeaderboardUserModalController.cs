// TODO: Add ACC Campaign badges when the API properly exposes the information

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AccSaber.Configuration;
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
		private AccSaberUser? _userOverall;
		private AccSaberUser? _userTrue;
		private AccSaberUser? _userStandard;
		private AccSaberUser? _userTech;
		private readonly PluginConfig _pluginConfig = null!;
		private bool _parsed;
		private bool _firstLoad;
		private bool _isLoading;
		private string _categoryValue = "Overall";
		private string _username = "";
		private string _rank = null!;
		private string _country = null!;
		private string _title = null!;
		private string _level = null!;
		private string _ap = null!;
		private string _xp = null!;
		private string _plays = null!;
		private string _headset = null!;
		
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

		#endregion

		[UIObject("add-friend")]
		private readonly GameObject _addFriendButton = null!;

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
		private void AddFriendClicked()
		{
			if (_userId is null)
				return;

			if (_pluginConfig.IsFriend(_userId))
			{
				_pluginConfig.RemoveFriend(_userId);
				_addFriendButton.gameObject.GetComponent<Button>().SetButtonText("Add Friend");
			}
			else
			{
				_pluginConfig.AddFriend(_userId);
				_addFriendButton.gameObject.GetComponent<Button>().SetButtonText("Remove Friend");
			}
		}
		private void Parse(Transform parentTransform)
		{
			if (!_parsed)
			{
				BSMLParser.Instance.Parse(Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), "AccSaber.UI.Views.LeaderboardUserModal.bsml"), parentTransform.gameObject, this);
				_modalView.name = "AccSaberLeaderboardUserModal";
				_modalView.blockerClickedEvent += OnModalClosed;

				var canvasGroup = _modalView.gameObject.AddComponent<CanvasGroup>();
				var dropdownModalView = _categoryDropdown.Find("DropdownTableView").GetComponent<ModalView>();
				dropdownModalView.SetupView(_modalView.transform);
				dropdownModalView.SetField("_parentCanvasGroup", canvasGroup);
				
				_userInfoCanvasGroup = _userInfo.gameObject.AddComponent<CanvasGroup>();
				
				_profileImage.material = Resources.FindObjectsOfTypeAll<Material>().Last(x => x.name == "UINoGlowRoundEdge");

				_parsed = true;
			}
			
			_modalView.transform.SetParent(parentTransform.transform);
			Accessors.ViewValidAccessor(ref _modalView) = false;
		}

		public void ShowModal(Transform parentTransform, string userId)
		{
			Parse(parentTransform);

			_userId = userId;
			_firstLoad = true;


			if (!_pluginConfig.IsFriend(userId) && _accSaberStore.GetCurrentUser().Result.PlayerId != userId)
			{
				_addFriendButton.gameObject.GetComponent<Button>().SetButtonText("Add Friend");
				_addFriendButton.gameObject.GetComponent<Button>().gameObject.SetActive(true);
			}
			else if (_accSaberStore.GetCurrentUser().Result.PlayerId == userId)
			{
				_addFriendButton.gameObject.GetComponent<Button>().gameObject.SetActive(false);
			}
			else
			{
				_addFriendButton.gameObject.GetComponent<Button>().SetButtonText("Remove Friend");
				_addFriendButton.gameObject.GetComponent<Button>().gameObject.SetActive(true);
			}

			CategoryValue = "Overall";
			_parserParams.EmitEvent("close-modal");
			_parserParams.EmitEvent("open-modal");
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

			switch (CategoryValue)
			{
				case "Overall":
				{
					if (_userOverall is null)
					{
						IsLoading = true;
						var userInfo = await _accSaberStore.GetUserFromId(_userId);
						_userOverall = userInfo;
					}

					await SetUserInfo(_userOverall);
					
					break;
				}
				case "True":
				{
					if (_userTrue is null)
					{
						IsLoading = true;
						var userInfo = await _accSaberStore.GetUserFromId(_userId, AccSaberStore.AccSaberMapCategories.True);
						_userTrue = userInfo;
					}

					await SetUserInfo(_userTrue);
					break;
				}
				case "Standard":
				{
					if (_userStandard is null)
					{
						IsLoading = true;
						var userInfo = await _accSaberStore.GetUserFromId(_userId, AccSaberStore.AccSaberMapCategories.Standard);
						_userStandard = userInfo;
					}

					await SetUserInfo(_userStandard);
					break;
				}
				case "Tech":
				{
					if (_userTech is null)
					{
						IsLoading = true;
						var userInfo = await _accSaberStore.GetUserFromId(_userId, AccSaberStore.AccSaberMapCategories.Tech);
						_userTech = userInfo;
					}

					await SetUserInfo(_userTech);
					break;
				}
			}
		}

		private async Task SetUserInfo(AccSaberUser userInfo) // ty person for the progress bar
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

            Username = $"{userInfo.PlayerName}";
			Rank = $"#{userInfo.Rank}";
			Country = $"#{userInfo.CountryRank}";
			Title = $"{"<color=" + _color + ">" +userInfo.LevelData.PlayerTitle}</color>";
			Ap = $"{userInfo.AP:N2} AP";
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

		private void OnModalClosed()
		{
			_userId = null;
			_userOverall = null;
			_userTrue = null;
			_userStandard = null;
			_userTech = null;
		}

		public void Dispose()
		{
			_modalView.blockerClickedEvent -= OnModalClosed;
		}
	}
}