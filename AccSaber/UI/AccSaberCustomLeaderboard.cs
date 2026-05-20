using System;
using AccSaber.UI.ViewControllers;
using HMUI;
using LeaderboardCore.Managers;
using LeaderboardCore.Models;
using Zenject;

namespace AccSaber.UI
{
	internal sealed class AccSaberCustomLeaderboard : CustomLeaderboard, IInitializable, IDisposable
	{
		private readonly CustomLeaderboardManager _customLeaderboardManager;
		private readonly AccSaberPanelViewController _accSaberPanelViewController;
		private readonly AccSaberLeaderboardViewController _accSaberLeaderboardViewController;

		public AccSaberCustomLeaderboard(CustomLeaderboardManager customLeaderboardManager, AccSaberPanelViewController accSaberPanelViewController, AccSaberLeaderboardViewController accSaberLeaderboardViewController)
		{
			_customLeaderboardManager = customLeaderboardManager;
			_accSaberPanelViewController = accSaberPanelViewController;
			_accSaberLeaderboardViewController = accSaberLeaderboardViewController;
		}

		protected override ViewController panelViewController => _accSaberPanelViewController;
		protected override ViewController leaderboardViewController => _accSaberLeaderboardViewController;

		public void Initialize()
		{
			_customLeaderboardManager.Register(this);
		}

		public void Dispose()
		{
			_customLeaderboardManager.Unregister(this);
		}
	}
}