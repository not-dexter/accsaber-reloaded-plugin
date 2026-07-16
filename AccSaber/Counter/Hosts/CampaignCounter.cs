using AccSaber.Models;
using AccSaber.UI.MenuButton.Campaigns.ViewControllers;
using AccSaber.Utils.Misc;
using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using Zenject;

namespace AccSaber.Counter.Hosts
{
    internal class CampaignCounter : IInitializable, IDisposable
    {
        private static readonly Type? CanvasUtilityType;
        private static readonly Type? SettingsType;
        private static readonly MethodInfo? CanvasCreateText;

        private object CanvasUtility = null!, Settings = null!;

        private AccSaberCampaignViewController? campaignVC = null!;
        private AccSaberCampaignMap Map = null!;
        private AccSaberBasicDifficulty DiffInfo = null!;
        private APCalc Calc = null!;
        [Inject] private readonly ScoreController sc = null!;
        [Inject] private readonly ComboController cc = null!;

        private TMP_Text DisplayText = null!;
        private int max115Streak = 0, current115Streak = 0;

        private float requiredVal;

        static CampaignCounter()
        {
            CanvasUtilityType = Plugin.CounterAssembly?.GetType("CountersPlus.Utils.CanvasUtility");
            SettingsType = Plugin.CounterAssembly?.GetType("CountersPlus.ConfigModels.ConfigModel");

            if (CanvasUtilityType is null || SettingsType is null)
                return;

            CanvasCreateText = CanvasUtilityType.GetMethod("CreateTextFromSettings");
        }


        public void Initialize()
        {
            if (CanvasUtilityType is null || SettingsType is null)
                return;

            try
            {
                CanvasUtility = Plugin.CounterGameContainer.TryResolve(CanvasUtilityType);
                Settings = Plugin.CounterGameContainer.TryResolveId(SettingsType, "Accsaber Campaign");
                campaignVC = Plugin.Container.TryResolve<AccSaberCampaignViewController>();

                if (!campaignVC.MapStarted)
                    return;

                Map = campaignVC.CurrentMap!;
                DiffInfo = Plugin.Container.TryResolve<SerializationHandler>().CachedDifficulties[Map.MapDifficultyId];

                requiredVal = Map.RequirementValue - 0.00005f;
            }
            catch (Exception e)
            {
                Plugin.Log.Error(e);
                return;
            }

            DisplayText = (TMP_Text)CanvasCreateText!.Invoke(CanvasUtility, [Settings, null]);
            DisplayText.text = "";

            switch (Map.RequirementType)
            {
                case AccSaberCampaignMap.CampaignRequirementType.ACC:
                    sc.scoringForNoteFinishedEvent += AccCounter;
                    break;
                case AccSaberCampaignMap.CampaignRequirementType.AP:
                    Calc = Plugin.Container.TryResolve<APCalc>();
                    sc.scoringForNoteFinishedEvent += ApCounter;
                    break;
                case AccSaberCampaignMap.CampaignRequirementType.SCORE:
                    sc.scoreDidChangeEvent += ScoreCounter;
                    break;
                case AccSaberCampaignMap.CampaignRequirementType.STREAK_115:
                    sc.scoringForNoteFinishedEvent += StreakCounter;
                    break;
                case AccSaberCampaignMap.CampaignRequirementType.FC:
                    UpdateColor(true);
                    DisplayText.SetText("FC!");
                    cc.comboBreakingEventHappenedEvent += FCCounter;
                    break;
                default: // TODO: Add Rank (or maybe just don't worry about that one)
                    campaignVC = null;
                    break;
            }
        }
        public void Dispose()
        {
            if (campaignVC is null || !campaignVC.MapStarted)
                return;

            switch (Map.RequirementType)
            {
                case AccSaberCampaignMap.CampaignRequirementType.ACC:
                    sc.scoringForNoteFinishedEvent -= AccCounter;
                    break;
                case AccSaberCampaignMap.CampaignRequirementType.AP:
                    sc.scoringForNoteFinishedEvent -= ApCounter;
                    break;
                case AccSaberCampaignMap.CampaignRequirementType.SCORE:
                    sc.scoreDidChangeEvent -= ScoreCounter;
                    break;
                case AccSaberCampaignMap.CampaignRequirementType.STREAK_115:
                    sc.scoringForNoteFinishedEvent -= StreakCounter;
                    break;
                case AccSaberCampaignMap.CampaignRequirementType.FC:
                    cc.comboBreakingEventHappenedEvent -= FCCounter;
                    break;
            }
        }


        private void AccCounter(ScoringElement scoringElement)
        {
            float acc = sc.multipliedScore / (float)sc.immediateMaxPossibleMultipliedScore * 100f;
            float goalAcc = requiredVal * 100f;

            UpdateColor(acc >= goalAcc);

            DisplayText.SetText($"{acc:N2}% / {goalAcc:N2}%");
        }
        private void ApCounter(ScoringElement scoringElement)
        {
            float acc = sc.multipliedScore / (float)sc.immediateMaxPossibleMultipliedScore;
            float ap = Calc.GetAp(acc, DiffInfo.Complexity);

            UpdateColor(ap >= requiredVal);

            DisplayText.SetText($"{ap:0.##} ap / {Map.RequirementValue:0.##} ap");
        }
        private void ScoreCounter(int multipliedScore, int modifiedScore)
        {
            UpdateColor(multipliedScore >= requiredVal);

            DisplayText.SetText($"{multipliedScore:N0} / {Map.RequirementValue:N0} Score");
        }
        private void StreakCounter(ScoringElement scoringElement)
        {
            bool is115 = scoringElement.cutScore == 115;

            if (is115)
            {
                ++current115Streak;
            }
            else
            {
                if (current115Streak > max115Streak)
                    max115Streak = current115Streak;

                current115Streak = 0;
            }

            int streak = Math.Max(max115Streak, current115Streak);
            UpdateColor(streak >= (int)Map.RequirementValue);

            DisplayText.SetText($"{streak}x / {Map.RequirementValue:N0}x 115 streak\n<size=70%><color=#BBB>(Current - {current115Streak}x)</color>");
        }
        private void FCCounter()
        {
            UpdateColor(false);
        }


        private void UpdateColor(bool success)
        {
            DisplayText.faceColor = success ? Color.green : Color.red;
        }

    }
}
