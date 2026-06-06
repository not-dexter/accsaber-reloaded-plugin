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
    internal class AccSaberMainFlowCoordinator : FlowCoordinator
    {
        private MainFlowCoordinator _mainFlowCoordinator = null!;
        private FlowCoordinator? _parentFlowCoordinator;
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
        internal void PresentFlowCoordinator()
        {
            IEnumerator PresentRoutine()
            {
                _parentFlowCoordinator = _mainFlowCoordinator.YoungestChildFlowCoordinatorOrSelf();

                yield return new WaitForEndOfFrame();

                _parentFlowCoordinator.PresentFlowCoordinator(this, () =>
                {
                    OnHubActivated?.Invoke();
                    _accSaberMilestoneViewController.UpdateTabs();
                });
            }

            _mainFlowCoordinator.StartCoroutine(PresentRoutine());
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
        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            OnHubDeactivated?.Invoke();
            _parentFlowCoordinator?.DismissFlowCoordinator(this);
        }

        internal void Close(bool instant = false)
        {
            OnHubDeactivated?.Invoke();
            _parentFlowCoordinator?.DismissFlowCoordinator(this, immediately: instant);
        }
        internal void CloseToMainMenu()
        {
            OnHubDeactivated?.Invoke();

            _parentFlowCoordinator?.DismissFlowCoordinator(this, immediately: true);

            if (_parentFlowCoordinator is SoloFreePlayFlowCoordinator)
                _mainFlowCoordinator.DismissFlowCoordinator(_parentFlowCoordinator, immediately: true);
        }
    }
}