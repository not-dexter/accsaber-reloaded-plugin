using AccSaber.Configuration;
using AccSaber.Managers;
using AccSaber.UI;
using AccSaber.UI.MenuButton;
using AccSaber.UI.MenuButton.Campaigns;
using AccSaber.UI.MenuButton.Campaigns.ViewControllers;
using AccSaber.UI.MenuButton.ViewControllers;
using AccSaber.UI.ViewControllers;
using AccSaber.Utils;
using AccsaberLeaderboard.UI.BSML_Addons;
using Zenject;

namespace AccSaber.Installers
{
	internal sealed class AccSaberMenuInstaller : Installer
	{
		private readonly PluginConfig _pluginConfig;
		//private readonly APCalc _calc;

		public AccSaberMenuInstaller(PluginConfig pluginConfig)
		{
			_pluginConfig = pluginConfig;
			//_calc = calc;
		}
		
		public override void InstallBindings()
		{
			Plugin.Container = Container;

			Container.BindInstance(_pluginConfig).AsSingle();
			//Container.BindInstance(_calc).AsSingle();

			Container.BindInterfacesTo<MenuButtonManager>().AsSingle();
            Container.Bind<AccSaberMissionScreen>().AsSingle();

			Container.Bind<AccSaberCampaignViewController>().FromNewComponentAsViewController().AsSingle();
			Container.Bind<AccSaberCampaignFlow>().FromNewComponentOnNewGameObject().AsSingle();

			Container.Bind<AccSaberNewsViewController>().FromNewComponentAsViewController().AsSingle();
			Container.Bind<AccSaberMenuViewController>().FromNewComponentAsViewController().AsSingle();
			Container.Bind<AccSaberMilestoneViewController>().FromNewComponentAsViewController().AsSingle();
			Container.Bind<AccSaberMainFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
            Container.Bind<AccSaberNewsModal>().AsSingle();

            Container.BindInterfacesAndSelfTo<AccSaberStore>().AsSingle();
			Container.BindInterfacesAndSelfTo<AccSaberManager>().AsSingle();
			Container.BindInterfacesTo<AccSaberCustomLeaderboard>().AsSingle();

			Container.BindExecutionOrder<AccSaberCustomLeaderboard>(1000); // Make accSaber leaderboard bind at the end of the line.
			
			Container.BindInterfacesAndSelfTo<AccSaberPanelViewController>().FromNewComponentAsViewController().AsSingle();
			Container.BindInterfacesAndSelfTo<AccSaberLeaderboardViewController>().FromNewComponentAsViewController().AsSingle();
			Container.Bind<LeaderboardScoreModalController>().AsSingle();
			Container.Bind<LeaderboardUserModalController>().AsSingle();
			Container.Bind<WhereScoreModalController>().AsSingle();
			Container.Bind(typeof(IInitializable)).To<AddonAdder>().AsSingle();
			Container.Bind(typeof(IInitializable)).To<PlayerSocialLife>().AsSingle();
		}
	}
}