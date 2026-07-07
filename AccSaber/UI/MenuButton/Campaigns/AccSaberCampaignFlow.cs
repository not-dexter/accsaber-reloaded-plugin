using AccSaber.Managers;
using AccSaber.UI.MenuButton.Campaigns.ViewControllers;
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
        private FlowCoordinator _parentFlow = null!;
        private AccSaberCampaignViewController _campaignController = null!;
        private GameplaySetupViewController _gameplaySetupViewController = null!;
        private PlatformLeaderboardViewController _leaderboardController = null!;
        private AccSaberStore _accSaberStore = null!;
        private StandardLevelDetailViewController _standardLevelDetailViewController = null!;
        public Action<StandardLevelDetailViewController>? CampaignMapUpdated;

        [Inject]
        protected void Construct(AccSaberCampaignViewController campaignController, AccSaberMainFlowCoordinator parentCoordinator,
            GameplaySetupViewController gameplaySetupViewController, PlatformLeaderboardViewController platformLeaderboardViewController, 
            AccSaberStore accSaberStore,
            StandardLevelDetailViewController standardLevelDetailViewController)
        {
            _campaignController = campaignController;
            _parentFlow = parentCoordinator;
            _gameplaySetupViewController = gameplaySetupViewController;
            _leaderboardController = platformLeaderboardViewController;
            _accSaberStore = accSaberStore;
            _standardLevelDetailViewController = standardLevelDetailViewController;
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
        }

        public void ShowLeaderboard(BeatmapKey beatmapkey)
        {
            _leaderboardController.SetData(beatmapkey);
            SetRightScreenViewController(_leaderboardController, ViewController.AnimationType.In);
        }
        public void HideLeaderboard()
        {
            SetRightScreenViewController(null, ViewController.AnimationType.Out);
        }
        internal void PresentFlowCoordinator()
        {
            _parentFlow.PresentFlowCoordinator(this);
        }
        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            _parentFlow.DismissFlowCoordinator(this);
        }
    }
}
