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

        [Inject]
        protected void Construct(AccSaberCampaignViewController campaignController, AccSaberMainFlowCoordinator parentCoordinator)
        {
            _campaignController = campaignController;
            _parentFlow = parentCoordinator;
        }
        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (firstActivation)
            {
                SetTitle("AccSaber Campaigns");
                showBackButton = true;
                ProvideInitialViewControllers(_campaignController);
            } 
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
