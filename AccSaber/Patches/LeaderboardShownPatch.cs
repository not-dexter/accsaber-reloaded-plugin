using HarmonyLib;
using JetBrains.Annotations;
using LeaderboardCore.Models;
using System;

namespace AccSaber.Patches
{
    [HarmonyPatch(typeof(CustomLeaderboard), "Show")]
    internal static class LeaderboardShownPatch
    {
#pragma warning disable IDE0051
        public static event Action? LeaderboardShown;
        [UsedImplicitly]
        private static void Postfix()
        {
            if (UI.ViewControllers.AccSaberLeaderboardViewController.Instance?.gameObject?.activeSelf ?? false)
            {
                LeaderboardHiddenPatch.wasShown = true;
                LeaderboardShown?.Invoke();
            }
        }
    }
}
