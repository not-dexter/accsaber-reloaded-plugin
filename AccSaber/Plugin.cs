using AccSaber.Configuration;
using AccSaber.Installers;
using AccSaber.Managers;
using AccSaber.Patches;
using HarmonyLib;
using IPA;
using IPA.Config.Stores;
using SiraUtil.Zenject;
using System.Linq;
using Zenject;
using IPALogger = IPA.Logging.Logger;

namespace AccSaber
{
	[Plugin(RuntimeOptions.DynamicInit), NoEnableDisable]
	public class Plugin
	{
		internal static DiContainer Container = null!;
		internal static IPALogger Log = null!;

		private readonly SubmissionPatch.SubmissionPatchInfo[] submissionPatches =
		[
			new("SiraUtil", "SiraUtil.Submissions.SubmissionDataContainer", "SSS"),
			new("BS Utils", "BS_Utils.Gameplay.ScoreSubmission", "DisableScoreSaberScoreSubmission")
		];
		private static readonly bool[] submissions = [true, true, true];
		public static bool Submit => submissions.All(x => x);

		[Init]
		public void Init(Zenjector zenjector, IPALogger logger, IPA.Config.Config config)
		{
			zenjector.UseLogger(logger);
			Log = logger;

			zenjector.Install<AccSaberMenuInstaller>(Location.Menu, config.Generated<PluginConfig>());
			zenjector.Install<AccSaberAppInstaller>(Location.App);
			zenjector.Install<AccSaberGameInstaller>(Location.StandardPlayer | Location.CampaignPlayer);

			Harmony harmony = new("AccSaber.Leaderboard");

			SubmissionPatch.ApplyPatch(harmony, submissionPatches[0], SymbolExtensions.GetMethodInfo<bool>(val => SetSiraSubmission(val)));
			SubmissionPatch.ApplyPatch(harmony, submissionPatches[1], SymbolExtensions.GetMethodInfo(() => SetBSUtilsSubmission()));
		}

		private static void SetSiraSubmission(bool value) => submissions[0] = value;
		private static void SetBSUtilsSubmission() => submissions[1] = false;
		internal static void SetPracticeSubmission() => submissions[2] = false;
		internal static void ResetSubmissions()
		{
			submissions[0] = true;
			submissions[1] = true;
			submissions[2] = true;
        }
    }
}