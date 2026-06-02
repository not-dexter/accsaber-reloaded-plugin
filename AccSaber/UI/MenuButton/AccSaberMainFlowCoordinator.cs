using AccSaber.UI.MenuButton.ViewControllers;
using HMUI;
using Zenject;
using System;


#if !NEW_VERSION
using BeatSaberMarkupLanguage;
#endif

namespace AccSaber.UI.MenuButton
{
    internal class AccSaberMainFlowCoordinator : FlowCoordinator
    {
        private MainFlowCoordinator _mainFlowCoordinator = null!;
        private FlowCoordinator? _parentFlowCoordinator;
        private AccSaberNewsViewController _accSaberRelationsViewController = null!;
        private AccSaberMenuViewController _accSaberMenuViewController = null!;
        private AccSaberMilestoneViewController _accSaberMilestoneViewController = null!;

        public event Action? HubActivated;
        public event Action? HubDeactivated;

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
                HubDeactivated += OnDismiss;
            }
        }
        internal void PresentFlowCoordinator()
        {
            _parentFlowCoordinator = _mainFlowCoordinator.YoungestChildFlowCoordinatorOrSelf();
            _parentFlowCoordinator.PresentFlowCoordinator(this);

            HubActivated?.Invoke();

            _accSaberMilestoneViewController.UpdateTabs();
        }


        private void OnDismiss()
        {
            SetRightScreenViewController(null, ViewController.AnimationType.None);
            SetLeftScreenViewController(null, ViewController.AnimationType.None);
            _accSaberRelationsViewController.HideNewsModal();
            _accSaberMilestoneViewController.StopTimer();
        }
        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            HubDeactivated?.Invoke();
            _parentFlowCoordinator?.DismissFlowCoordinator(this);
        }

        internal void Close(bool instant = false)
        {
            HubDeactivated?.Invoke();
            _parentFlowCoordinator?.DismissFlowCoordinator(this, immediately: instant);
        }
        internal void CloseToMainMenu()
        {
            HubDeactivated?.Invoke();

            _parentFlowCoordinator?.DismissFlowCoordinator(this, immediately: true);

            if (_parentFlowCoordinator is SoloFreePlayFlowCoordinator)
                _mainFlowCoordinator.DismissFlowCoordinator(_parentFlowCoordinator, immediately: true);
        }
    }
}