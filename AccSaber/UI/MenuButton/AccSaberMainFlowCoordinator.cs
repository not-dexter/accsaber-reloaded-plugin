using AccSaber.UI.MenuButton.ViewControllers;
using BeatSaberMarkupLanguage;
using HMUI;
using Zenject;

namespace AccSaber.UI.MenuButton
{
    class AccSaberMainFlowCoordinator : FlowCoordinator
    {
        private MainFlowCoordinator _mainFlowCoordinator = null!;
        private FlowCoordinator? _parentFlowCoordinator;
        private AccSaberNewsViewController _accSaberRelationsViewController = null!;
        private AccSaberMenuViewController _accSaberMenuViewController = null!;
        private AccSaberMilestoneViewController _accSaberMilestoneViewController = null!;

        // Called immediately when the flow coordinator is activated


        [Inject]
        protected void Construct(MainFlowCoordinator mainFlowCoordinator, AccSaberMenuViewController accSaberMenuViewController, AccSaberMilestoneViewController accSaberMilestoneViewController, AccSaberNewsViewController accSaberRelationsViewController)
        {
            _mainFlowCoordinator = mainFlowCoordinator;
            _accSaberRelationsViewController = accSaberRelationsViewController;
            _accSaberMenuViewController = accSaberMenuViewController;
            _accSaberMilestoneViewController = accSaberMilestoneViewController;
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (firstActivation)
            {
                // Sets the title text in the top bar
                SetTitle("Accsaber Reloaded");
                showBackButton = true;
                ProvideInitialViewControllers(_accSaberMenuViewController, _accSaberRelationsViewController, _accSaberMilestoneViewController);
            }
        }
        internal void PresentFlowCoordinator()
        {
            _parentFlowCoordinator = _mainFlowCoordinator.YoungestChildFlowCoordinatorOrSelf();
            _parentFlowCoordinator.PresentFlowCoordinator(this);
        }


        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            _accSaberRelationsViewController.HideNewsModal();
            SetRightScreenViewController(null, ViewController.AnimationType.None);
            SetLeftScreenViewController(null, ViewController.AnimationType.None);
            _parentFlowCoordinator?.DismissFlowCoordinator(this);
        }
    }
}