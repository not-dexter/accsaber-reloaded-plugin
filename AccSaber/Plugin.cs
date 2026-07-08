using AccSaber.Configuration;
using AccSaber.Consts;
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
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Zenject;
using IPALogger = IPA.Logging.Logger;

namespace AccSaber
{
	[Plugin(RuntimeOptions.DynamicInit), NoEnableDisable]
	public class Plugin
	{
        private static Type _generatedConfigModelType = null!;
        internal static Assembly CounterAssembly { get; private set; } = null!;

        internal static DiContainer Container = null!, CounterGameContainer = null!;
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

            try
            {
                InstallCounters();
            }
            catch (Exception e)
            {
                Log.Error("There was an exception handling counters+ interop!\n" + e);
            }

            zenjector.Install<AccSaberMenuInstaller>(Location.Menu, config.Generated<PluginConfig>());
			zenjector.Install<AccSaberAppInstaller>(Location.App);
			zenjector.Install<AccSaberGameInstaller>(Location.StandardPlayer);

			harmony = new("AccSaber.Leaderboard");

			SubmissionPatch.ApplyKnownPatches(harmony);
            MiscPatch.ApplyPatches(harmony);
		}


		private void InstallCounters()
		{
            CounterAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assemb => assemb.GetName().Name.Equals("Counters+"));
            CountersInstalled = CounterAssembly is not null;

            if (!CountersInstalled)
            {
                Log.Warn("Counters+ not found.");
                return;
            }

            Type dynamicConfigType = CreateEmptyConfigModelSubclass(CounterAssembly!.GetType("CountersPlus.ConfigModels.ConfigModel")); // This is so jank

            Type customCounterFeature = CounterAssembly!.GetType("CountersPlus.Custom.CustomCounterFeature");

            object counterFeature = customCounterFeature.GetConstructor([]).Invoke([]);

            MethodInfo counterInit = customCounterFeature.GetMethod("Initialize", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo counterAfterInit = customCounterFeature.GetMethod("AfterInit", BindingFlags.Public | BindingFlags.Instance, null, [typeof(PluginMetadata)], null);

            FieldInfo incompleteCounters = customCounterFeature.GetField("incompleteCustomCounters", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            JObject counter = JObject.Parse(Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), ResourcePaths.CAMPAIGN_COUNTER_FEATURE));

            counterInit.Invoke(counterFeature, [Metadata, counter]);
            counterAfterInit.Invoke(counterFeature, [Metadata]);

            object counterDictionaryObject = incompleteCounters.GetValue(counterFeature);
            object customCounterInstance = ((IDictionary)counterDictionaryObject)[Metadata];

            Type customCounterType = CounterAssembly.GetType("CountersPlus.Custom.CustomCounter");
            Type bsmlSettings = customCounterType.GetNestedType("BSMLSettings");

            object bsmlInstance = customCounterType.GetField("BSML").GetValue(customCounterInstance);
            bsmlSettings.GetField("HostType").SetValue(bsmlInstance, dynamicConfigType);
        }

        public static Type CreateEmptyConfigModelSubclass(Type configModelType)
        {
            if (_generatedConfigModelType is not null)
                return _generatedConfigModelType;

            if (configModelType is null)
                throw new ArgumentNullException(nameof(configModelType));

            if (!configModelType.IsClass)
                throw new ArgumentException("ConfigModel type must be a class.", nameof(configModelType));

            if (!configModelType.IsAbstract)
                throw new ArgumentException("ConfigModel type was expected to be abstract.", nameof(configModelType));

            ConstructorInfo baseCtor = configModelType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null
            ) ?? throw new InvalidOperationException("ConfigModel does not have a parameterless constructor.");

            AssemblyName assemblyName = new("AccSaberConfigModelAssembly");

            AssemblyBuilder assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(
                assemblyName,
                AssemblyBuilderAccess.Run
            );

            ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule(
                "AccSaberConfigModelModule"
            );

            TypeBuilder typeBuilder = moduleBuilder.DefineType(
                "AccSaberConfigModel",
                TypeAttributes.Public | TypeAttributes.Class,
                parent: configModelType
            );

            ConstructorBuilder ctorBuilder = typeBuilder.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                Type.EmptyTypes
            );

            ILGenerator il = ctorBuilder.GetILGenerator();

            // this.base()
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, baseCtor);
            il.Emit(OpCodes.Ret);

            _generatedConfigModelType = typeBuilder.CreateType();

            return _generatedConfigModelType;
        }
    }
}