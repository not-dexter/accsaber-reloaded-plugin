using AccSaber.UI.MenuButton.Campaigns.ViewControllers;
using AccSaber.UI.ViewControllers;
using HMUI;
using System;
using Zenject;

#if !NEW_VERSION
using BeatSaberMarkupLanguage;
#endif

namespace AccSaber.UI.MenuButton.Campaigns
{
    // Based off: https://github.com/HypersonicSharkz/SmartSongSuggest/blob/master/TaohSongSuggest/UI/TSSFlowCoordinator.cs unused for now
    internal class AccSaberCampaignFlow : FlowCoordinator
    {
        private AccSaberMainFlowCoordinator _parentFlow = null!;
        private AccSaberCampaignViewController _campaignController = null!;
        private GameplaySetupViewController _gameplaySetupViewController = null!;
        private PlatformLeaderboardViewController _leaderboardController = null!;
        private AccSaberPanelViewController _panelViewController = null!;
        private SongPreviewPlayer _songPreviewPlayer = null!;

        [Inject]
        protected void Construct(AccSaberCampaignViewController campaignController, AccSaberMainFlowCoordinator parentCoordinator,
            GameplaySetupViewController gameplaySetupViewController, PlatformLeaderboardViewController platformLeaderboardViewController,
            AccSaberPanelViewController panelViewController, SongPreviewPlayer songPreviewPlayer)
        {
            _campaignController = campaignController;
            _parentFlow = parentCoordinator;
            _gameplaySetupViewController = gameplaySetupViewController;
            _leaderboardController = platformLeaderboardViewController;
            _panelViewController = panelViewController;
            _songPreviewPlayer = songPreviewPlayer;
        }
        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (firstActivation)
            {
                SetTitle("AccSaber Campaigns");
                showBackButton = true;
                _gameplaySetupViewController.Setup(
                    showModifiers: true,
                    showEnvironmentOverrideSettings: true,
                    showColorSchemesSettings: true,
                    showMultiplayer: false,
                    PlayerSettingsPanelController.PlayerSettingsPanelLayout.Singleplayer);
                
                ProvideInitialViewControllers(_campaignController, _gameplaySetupViewController, null);
            }
            _panelViewController.LogoDoesTransition = false;
            _panelViewController.OnLogoClicked += OnLogoClicked;
        }

#if NEW_VERSION
        public void ShowLeaderboard(BeatmapKey beatmapkey)
        {
            _leaderboardController.SetData(beatmapkey);
            SetRightScreenViewController(_leaderboardController, ViewController.AnimationType.In);
        }
#else
        public void ShowLeaderboard(IDifficultyBeatmap beatmapkey)
        {
            SetRightScreenViewController(_leaderboardController, ViewController.AnimationType.In);
            _leaderboardController.SetData(beatmapkey);
        }
#endif
        public void HideLeaderboard(bool instant = false)
        {
            SetRightScreenViewController(null, instant ? ViewController.AnimationType.None : ViewController.AnimationType.Out);
        }
        internal void PresentFlowCoordinator(Action? callback = null, bool instant = false)
        {
            _parentFlow.PresentFlowCoordinator(this, finishedCallback: callback, immediately: instant);
        }
        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            if (_campaignController.InCampaign)
            {
                _ = _campaignController.BackPressed();
            }
            else
            {
                ExitToMenu();
            }
        }
        internal async void ExitToMenu()
        {
            if (_campaignController.InCampaign)
                await _campaignController.BackPressed(false);

            _panelViewController.OnLogoClicked -= OnLogoClicked;
            _panelViewController.LogoDoesTransition = true;

            _songPreviewPlayer.CrossfadeToDefault();
            _parentFlow.DismissFlowCoordinator(this, finishedCallback: _parentFlow.MenuShown);
        }

        private void OnLogoClicked()
        {
            Models.AccSaberCampaign? campaign = _campaignController.CurrentCampaign;

            _parentFlow.BackButtonActions.Push(() =>
            {

                void Callback()
                {
                    _ = _campaignController.OpenCampaign(campaign);
                }
                
                if (campaign is not null)
                    PresentFlowCoordinator(Callback, true);
                else
                    PresentFlowCoordinator(instant: true);
            });

            ExitToMenu();
        }
    }
}
