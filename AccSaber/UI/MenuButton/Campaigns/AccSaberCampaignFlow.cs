using AccSaber.UI.MenuButton.Campaigns.ViewControllers;
using HMUI;
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
        private SongPreviewPlayer _songPreviewPlayer = null!;
        public bool disableLogo;

        [Inject]
        protected void Construct(AccSaberCampaignViewController campaignController, AccSaberMainFlowCoordinator parentCoordinator,
            GameplaySetupViewController gameplaySetupViewController, PlatformLeaderboardViewController platformLeaderboardViewController,
            SongPreviewPlayer songPreviewPlayer)
        {
            _campaignController = campaignController;
            _parentFlow = parentCoordinator;
            _gameplaySetupViewController = gameplaySetupViewController;
            _leaderboardController = platformLeaderboardViewController;
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
            disableLogo = true;
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
            disableLogo = false;
            _songPreviewPlayer.CrossfadeToDefault();
            _parentFlow.DismissFlowCoordinator(this);
        }
    }
}
