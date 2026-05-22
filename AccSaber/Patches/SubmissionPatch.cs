using HarmonyLib;
using IPA.Loader;
using System;
using System.Reflection;

namespace AccSaber.Patches
{
    // This class adapted from: https://github.com/BeatLeader/beatleader-mod/blob/master/Source/0_Harmony/SubmissionPatches/SiraUtilSubmissionPatch.cs
    internal static class SubmissionPatch
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

        private static readonly SubmissionPatchInfo[] submissionPatches =
        [
            new("SiraUtil", "SiraUtil.Submissions.SubmissionDataContainer", "SSS"),
            new("BS Utils", "BS_Utils.Gameplay.ScoreSubmission", "DisableScoreSaberScoreSubmission")
        ];
        private static bool siraSubmit, bsUtilsSubmit, practiceSubmit;
        public static bool Submit => siraSubmit && bsUtilsSubmit && practiceSubmit;

        internal static void ApplyKnownPatches(Harmony harmony)
        {
            ApplyPatch(harmony, submissionPatches[0], SymbolExtensions.GetMethodInfo<bool>(val => SetSiraSubmission(val)));
            ApplyPatch(harmony, submissionPatches[1], SymbolExtensions.GetMethodInfo(() => SetBSUtilsSubmission()));
        }

        private static void SetSiraSubmission(bool value) => siraSubmit = value;
        private static void SetBSUtilsSubmission() => bsUtilsSubmit = false;
        internal static void SetPracticeSubmission() => practiceSubmit = false;

        internal static void EnableSubmissions()
        {
            siraSubmit = true;
            bsUtilsSubmit = true;
            practiceSubmit = true;
        }
    }
}
