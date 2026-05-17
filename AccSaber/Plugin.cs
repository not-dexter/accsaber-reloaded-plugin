using AccSaber.Configuration;
using AccSaber.Installers;
using AccsaberLeaderboard.UI.BSML_Addons;
using BS_Utils.Utilities;
using IPA;
using IPA.Config;
using IPA.Config.Stores;
using SiraUtil.Zenject;
using Zenject;
using IPALogger = IPA.Logging.Logger;

namespace AccSaber
{
	[Plugin(RuntimeOptions.DynamicInit), NoEnableDisable]
	public class Plugin
	{
		internal static DiContainer Container = null!;
		internal static IPALogger Log = null!;

		private static bool loaded = false;

		[Init]
		public void Init(Zenjector zenjector, IPALogger logger, IPA.Config.Config config)
		{
			zenjector.UseLogger(logger);
			Log = logger;
            
			zenjector.Install<AccSaberMenuInstaller>(Location.Menu, config.Generated<PluginConfig>());

#if NEW_VERSION
            BeatSaberMarkupLanguage.Util.MainMenuAwaiter.MainMenuInitializing += Load;
#else
            BSEvents.menuSceneActive += Load;
#endif
        }

		private void Load()
		{
			if (loaded)
				return;
			loaded = true;

			AddonAdder.Load();
        }
    }
}