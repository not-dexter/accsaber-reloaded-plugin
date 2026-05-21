using HarmonyLib;
using IPA.Loader;
using System;
using System.Reflection;

namespace AccSaber.Patches
{
    // This class adapted from: https://github.com/BeatLeader/beatleader-mod/blob/master/Source/0_Harmony/SubmissionPatches/SiraUtilSubmissionPatch.cs
    internal class SubmissionPatch
    {
        public static void ApplyPatch(Harmony harmony, string pluginId, string classPath, string methodName, MethodInfo method)
        {
            Type? type = PluginManager.GetPluginFromId(pluginId)?.Assembly?.GetType(classPath);
            if (type is not null)
            {
                MethodInfo mOriginal = AccessTools.Method(type, methodName);

                harmony.Patch(mOriginal, new HarmonyMethod(method));
            }
        }
        public static void ApplyPatch(Harmony harmony, SubmissionPatchInfo info, MethodInfo method) => 
            ApplyPatch(harmony, info.PluginId, info.ClassPath, info.MethodName, method);

        internal readonly struct SubmissionPatchInfo(string pluginId, string classPath, string methodName)
        {
            public readonly string PluginId = pluginId; 
            public readonly string ClassPath = classPath;
            public readonly string MethodName = methodName;
        }
    }
}
