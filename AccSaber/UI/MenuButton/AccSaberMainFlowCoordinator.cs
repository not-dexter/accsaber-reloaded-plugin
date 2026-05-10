using AccSaber.UI.MenuButton.ViewControllers;
using BeatSaberMarkupLanguage;
using HMUI;
using SiraUtil.Logging;
using System;
using System.Linq;
using UnityEngine;
using Zenject;

namespace AccSaber.UI.MenuButton
{
    class AccSaberMainFlowCoordinator : FlowCoordinator
    {
        private MainFlowCoordinator _mainFlowCoordinator = null!;
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
            _mainFlowCoordinator.PresentFlowCoordinator(this);
        }


        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            _mainFlowCoordinator.DismissFlowCoordinator(this);
        }
    }
}