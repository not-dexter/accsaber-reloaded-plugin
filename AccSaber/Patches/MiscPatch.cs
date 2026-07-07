using HarmonyLib;
using System;
using System.Reflection;
using Zenject;

namespace AccSaber.Patches
{
    internal static class MiscPatch
    {
        internal static void ApplyPatches(Harmony harmony)
        {
            GetCounterContainer(harmony);
        }
        private static void GetCounterContainer(Harmony harmony)
        {
            if (!Plugin.CountersInstalled)
                return;

            Type installerType = Plugin.CounterAssembly.GetType("CountersPlus.Installers.CountersInstaller");
            MethodInfo installBindingsMethod = installerType.GetMethod("InstallBindings");

            MethodInfo postfix = AccessTools.Method(typeof(MiscPatch), nameof(InstallBindingsPostfix));

            harmony.Patch(installBindingsMethod, postfix: new(postfix));
        }
        private static void InstallBindingsPostfix(object __instance)
        {
            try
            {
                Plugin.CounterGameContainer = (DiContainer)__instance.GetType().GetProperty("Container", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance);
            }
            catch (Exception e)
            {
                Plugin.Log.Error(e);
            }
        }
    }
}
