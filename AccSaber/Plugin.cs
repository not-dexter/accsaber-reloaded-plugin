using AccSaber.Configuration;
using AccSaber.Installers;
using AccSaber.UI.ViewControllers;
using AccsaberLeaderboard.UI.BSML_Addons;
using BS_Utils.Utilities;
using HarmonyLib;
using IPA;
using IPA.Config;
using IPA.Config.Stores;
using SiraUtil.Zenject;
using System.Reflection;
using Zenject;
using IPALogger = IPA.Logging.Logger;

namespace AccSaber
{
	[Plugin(RuntimeOptions.DynamicInit), NoEnableDisable]
	public class Plugin
	{
		internal static DiContainer Container = null!;
		internal static IPALogger Log = null!;

		[Init]
		public void Init(Zenjector zenjector, IPALogger logger, IPA.Config.Config config)
		{
			zenjector.UseLogger(logger);
			Log = logger;

			zenjector.Install<AccSaberMenuInstaller>(Location.Menu, config.Generated<PluginConfig>());
			zenjector.Install<AccSaberAppInstaller>(Location.App);
		}
    }
}