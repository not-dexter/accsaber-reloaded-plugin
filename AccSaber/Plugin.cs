using AccSaber.Configuration;
using AccSaber.Consts;
using AccSaber.Counter;
using AccSaber.Installers;
using AccSaber.Patches;
using BeatSaberMarkupLanguage;
using HarmonyLib;
using IPA;
using IPA.Config.Stores;
using IPA.Loader;
using Newtonsoft.Json.Linq;
using SiraUtil.Zenject;
using System;
using System.Linq;
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
		internal static Harmony harmony = null!;

		internal static PluginMetadata Metadata { get; private set; } = null!;
		internal static bool CountersInstalled { get; private set; } = false;

        [Init]
		public void Init(Zenjector zenjector, IPALogger logger, IPA.Config.Config config, PluginMetadata metadata)
		{
			zenjector.UseLogger(logger);
			Log = logger;
			Metadata = metadata;

			InstallCounters();

            zenjector.Install<AccSaberMenuInstaller>(Location.Menu, config.Generated<PluginConfig>());
			zenjector.Install<AccSaberAppInstaller>(Location.App);
			zenjector.Install<AccSaberGameInstaller>(Location.StandardPlayer);

			harmony = new("AccSaber.Leaderboard");

			SubmissionPatch.ApplyKnownPatches(harmony);
		}

		private void InstallCounters()
		{
            Assembly? counterAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assemb => assemb.GetName().Name.Equals("Counters+"));
            CountersInstalled = counterAssembly is not null;

            if (!CountersInstalled)
            {
                Log.Warn("Counters+ not found.");
                return;
            }

            Type customCounterFeature = counterAssembly!.GetType("CountersPlus.Custom.CustomCounterFeature");

            object counterFeature = customCounterFeature.GetConstructor([]).Invoke([]);

            MethodInfo counterInit = customCounterFeature.GetMethod("Initialize", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo counterAfterInit = customCounterFeature.GetMethod("AfterInit", BindingFlags.Public | BindingFlags.Instance, null, [typeof(PluginMetadata)], null);


            JObject counter = JObject.Parse(Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), ResourcePaths.CAMPAIGN_COUNTER_FEATURE));

            counterInit.Invoke(counterFeature, [Plugin.Metadata, counter]);
            counterAfterInit.Invoke(counterFeature, [Plugin.Metadata]);
        }
    }
}