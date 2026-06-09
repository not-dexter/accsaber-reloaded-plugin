using AccSaber.Configuration;
using AccSaber.Installers;
using AccSaber.Patches;
using HarmonyLib;
using IPA;
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
		internal static Harmony harmony = null!;

        [Init]
		public void Init(Zenjector zenjector, IPALogger logger, IPA.Config.Config config)
		{
			zenjector.UseLogger(logger);
			Log = logger;

			//APCalc calc = new();

            zenjector.Install<AccSaberMenuInstaller>(Location.Menu, config.Generated<PluginConfig>());
			zenjector.Install<AccSaberAppInstaller>(Location.App);
			zenjector.Install<AccSaberGameInstaller>(Location.StandardPlayer);

			harmony = new("AccSaber.Leaderboard");

			SubmissionPatch.ApplyKnownPatches(harmony);
		}
    }
}