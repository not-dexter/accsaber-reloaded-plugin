using AccSaber.Configuration;
using AccSaber.LeaderboardSources;
using AccSaber.Managers;
using AccSaber.UI;
using AccSaber.UI.MenuButton;
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

		public AccSaberMenuInstaller(PluginConfig pluginConfig)
		{
			_pluginConfig = pluginConfig;
		}
		
		public override void InstallBindings()
		{
			Plugin.Container = Container;

			Container.BindInstance(_pluginConfig).AsSingle();
			Container.BindInterfacesTo<MenuButtonManager>().AsSingle();
			Container.Bind<AccSaberNewsViewController>().FromNewComponentAsViewController().AsSingle();
			Container.Bind<AccSaberMenuViewController>().FromNewComponentAsViewController().AsSingle();
			Container.Bind<AccSaberMilestoneViewController>().FromNewComponentAsViewController().AsSingle();
			Container.Bind<AccSaberMainFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();

			Container.BindInterfacesAndSelfTo<AccSaberStore>().AsSingle();
			Container.BindInterfacesAndSelfTo<AccSaberManager>().AsSingle();
			Container.BindInterfacesTo<AccSaberCustomLeaderboard>().AsSingle();
			
			Container.BindInterfacesAndSelfTo<AccSaberPanelViewController>().FromNewComponentAsViewController().AsSingle();
			Container.BindInterfacesAndSelfTo<AccSaberLeaderboardViewController>().FromNewComponentAsViewController().AsSingle();
			Container.BindInterfacesTo<GlobalLeaderboardSource>().AsSingle();
			Container.BindInterfacesTo<FriendsLeaderboardSource>().AsSingle();
			Container.BindInterfacesTo<CountryLeaderboardSource>().AsSingle();
			Container.Bind<LeaderboardScoreModalController>().AsSingle();
			Container.Bind<LeaderboardUserModalController>().AsSingle();
			Container.Bind<WhereScoreModalController>().AsSingle();
#if NEW_VERSION
			Container.Bind(typeof(IInitializable)).To<AddonAdder>().AsSingle();
#else
			new AddonAdder().Initialize(); // scuffed, but gets the job done.
#endif
			Container.Bind(typeof(IInitializable)).To<PlayerSocialLife>().AsSingle();
		}
	}
}