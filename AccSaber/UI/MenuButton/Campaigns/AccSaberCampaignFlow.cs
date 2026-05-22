using AccSaber.UI.MenuButton.Campaigns.ViewControllers;
using BeatSaberMarkupLanguage;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace AccSaber.UI.MenuButton.Campaigns
{
    // Based off: https://github.com/HypersonicSharkz/SmartSongSuggest/blob/master/TaohSongSuggest/UI/TSSFlowCoordinator.cs unused for now
    internal class AccSaberCampaignFlow : FlowCoordinator, IInitializable
    {
        private static FlowCoordinator _parentFlow = null!;
        private static AccSaberCampaignFlow _flow = null!;
        private AccSaberCampaignViewController _campaignController = null!;
        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if(firstActivation)
            {
                SetTitle("AccSaber Campaigns");
                showBackButton = true;
                ProvideInitialViewControllers(_campaignController);
            } 
        }

        [Inject]
        internal void Construct(AccSaberCampaignViewController campaignController)
        {
            _campaignController = campaignController;
        }
        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            _parentFlow.DismissFlowCoordinator(this);
        }

        internal static FlowCoordinator DeepestChildFlowCoordinator(FlowCoordinator root)
        {

            var flow = root.childFlowCoordinator;
            if (flow == null) return root;
            if (flow.childFlowCoordinator == null || flow.childFlowCoordinator == flow)
            {
                return flow;
            }
            return DeepestChildFlowCoordinator(flow);
        }

        internal static void ShowCampaignFlowCoordinator()
        {
            if (_flow == null)
                _flow = BeatSaberUI.CreateFlowCoordinator<AccSaberCampaignFlow>();

            _parentFlow = BeatSaberUI.MainFlowCoordinator.YoungestChildFlowCoordinatorOrSelf();

            BeatSaberUI.PresentFlowCoordinator(_parentFlow, _flow);
        }
        public void Initialize()
        {

        }
    }
}
