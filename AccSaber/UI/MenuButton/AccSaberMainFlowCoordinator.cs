using AccSaber.UI.MenuButton.ViewControllers;
using BeatSaberMarkupLanguage;
using HMUI;
using Zenject;

namespace AccSaber.UI.MenuButton
{
    internal class AccSaberMainFlowCoordinator : FlowCoordinator
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

            //Plugin.Log.Info($"parent flow coordinator: type = {_parentFlowCoordinator.GetType()}, name = {_parentFlowCoordinator.name}");

            _accSaberMilestoneViewController.UpdateTabs();
        }


        private void OnDismiss()
        {
            _accSaberRelationsViewController.HideNewsModal();
            _accSaberMilestoneViewController.StopTimer();
            SetRightScreenViewController(null, ViewController.AnimationType.None);
            SetLeftScreenViewController(null, ViewController.AnimationType.None);
        }
        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            OnDismiss();
            _parentFlowCoordinator?.DismissFlowCoordinator(this);
        }

        internal void Close(bool instant = false)
        {
            OnDismiss();
            _parentFlowCoordinator?.DismissFlowCoordinator(this, immediately: instant);
        }
        internal void CloseToMainMenu()
        {
            OnDismiss();

            _parentFlowCoordinator?.DismissFlowCoordinator(this, immediately: true);

            if (_parentFlowCoordinator is SoloFreePlayFlowCoordinator)
                _mainFlowCoordinator.DismissFlowCoordinator(_parentFlowCoordinator, immediately: true);
        }
    }
}