using AccSaber.UI.MenuButton.ViewControllers;
using HMUI;
using Zenject;
using System;
using System.Collections;
using UnityEngine;


#if !NEW_VERSION
using BeatSaberMarkupLanguage;
#endif

namespace AccSaber.UI.MenuButton
{
    internal class AccSaberMainFlowCoordinator : Utils.Safety.SafeFlowCoordinator
    {
        protected override FlowCoordinator ParentFlowCoordinator { get; set; } = null!;

        private MainFlowCoordinator _mainFlowCoordinator = null!;
        private AccSaberNewsViewController _accSaberRelationsViewController = null!;
        private AccSaberMenuViewController _accSaberMenuViewController = null!;
        private AccSaberMilestoneViewController _accSaberMilestoneViewController = null!;

        public event Action? OnHubActivated;
        public event Action? OnHubDeactivated;

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
                OnHubDeactivated += OnDismiss;
            }
        }
        private void OnDestroy()
        {
            OnHubDeactivated -= OnDismiss;
        }
        public override void PresentFlowCoordinator(Action? callback = null, bool immediately = false)
        {
            ParentFlowCoordinator = _mainFlowCoordinator.YoungestChildFlowCoordinatorOrSelf();

            void OnCallback()
            {
                OnHubActivated?.Invoke();
                _accSaberMilestoneViewController.UpdateTabs();
                callback?.Invoke();
            }

            base.PresentFlowCoordinator(OnCallback, immediately);
        }
        public override void DismissFlowCoordinator(Action? callback = null, bool immediately = false)
        {
            OnHubDeactivated?.Invoke();
            base.DismissFlowCoordinator(callback, immediately);
        }


        private void OnDismiss()
        {
#if NEW_VERSION
            SetRightScreenViewController(null, ViewController.AnimationType.None);
            SetLeftScreenViewController(null, ViewController.AnimationType.None);
            _accSaberRelationsViewController.HideNewsModal();
            _accSaberMilestoneViewController.StopTimer();
#else
            _accSaberRelationsViewController.HideNewsModal();
            _accSaberMilestoneViewController.StopTimer();
            SetRightScreenViewController(null, ViewController.AnimationType.None);
            SetLeftScreenViewController(null, ViewController.AnimationType.None);
#endif
        }
        internal void CloseToMainMenu()
        {
            DismissFlowCoordinator(immediately: true);

            if (ParentFlowCoordinator is SoloFreePlayFlowCoordinator)
                _mainFlowCoordinator.DismissFlowCoordinator(ParentFlowCoordinator, immediately: true);
        }
    }
}