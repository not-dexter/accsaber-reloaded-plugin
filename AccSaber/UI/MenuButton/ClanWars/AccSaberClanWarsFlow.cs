using AccSaber.UI.MenuButton.ClanWars.ViewControllers;
using HMUI;
using System;
using Zenject;

namespace AccSaber.UI.MenuButton.ClanWars
{
    internal class AccSaberClanWarsFlow : FlowCoordinator
    {
        [Inject] private readonly AccSaberMainFlowCoordinator _parentFlow = null!;
        [Inject] private readonly AccSaberClanWarsController _clanWarsController = null!;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (firstActivation)
            {
                SetTitle("Clan Wars");
                showBackButton = true;
                ProvideInitialViewControllers(_clanWarsController);
            }
        }

        internal void PresentFlowCoordinator(Action? callback = null, bool instant = false)
        {
            _parentFlow.PresentFlowCoordinator(this, finishedCallback: callback, immediately: instant);
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            _parentFlow.DismissFlowCoordinator(this, ViewController.AnimationDirection.Horizontal);
        }

    }
}
