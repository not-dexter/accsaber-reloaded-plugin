using AccSaber.Configuration;
using AccSaber.Models;
using AccSaber.UI.MenuButton.Campaigns.ViewControllers;
using AccSaber.Utils;
using AccSaber.Utils.Misc;
using BeatSaberMarkupLanguage;
using HMUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        private static readonly PropertyInfo? SettingsCanvasID;
        private static readonly MethodInfo? CanvasCreateText;
        private static readonly MethodInfo? CanvasGetPos;
        private static readonly MethodInfo? GetCanvasFromId;

        private object CanvasUtility = null!, Settings = null!;

        private AccSaberCampaignViewController? campaignVC = null!;
        private AccSaberCampaignMap Map = null!;
        private AccSaberBasicDifficulty DiffInfo = null!;
        private PluginConfig PluginSettings = null!;
        private APCalc Calc = null!;
        [Inject] private readonly ScoreController sc = null!;
        [Inject] private readonly ComboController cc = null!;
        private GameEnergyCounter energy = null!;

        private TMP_Text DisplayText = null!;
        private ImageView Checkmark = null!;
        private int max115Streak = 0, current115Streak = 0;
        private LineInfo[] lineData = null!;
        private AccSaberCampaignTarget[] targets = null!;
        private readonly List<Action> cleanupActions = [];
        private int highestSuccessIndex = int.MaxValue;
        private string goodColor = null!, badColor = null!;

        private bool enabledGoalColors;

        static CampaignCounter()
        {
            CanvasUtilityType = Plugin.CounterAssembly?.GetType("CountersPlus.Utils.CanvasUtility");
            SettingsType = Plugin.CounterAssembly?.GetType("CountersPlus.ConfigModels.ConfigModel");

            if (CanvasUtilityType is null || SettingsType is null)
                return;

            SettingsCanvasID = SettingsType.GetProperty("CanvasID");
            CanvasCreateText = CanvasUtilityType.GetMethod("CreateTextFromSettings");
            CanvasGetPos = CanvasUtilityType.GetMethod("GetAnchoredPositionFromConfig");
            GetCanvasFromId = CanvasUtilityType.GetMethod("GetCanvasFromID");
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
                PluginSettings = Plugin.Container.TryResolve<PluginConfig>();

                if (!campaignVC.MapStarted)
                    return;

                goodColor = PluginSettings.CampaignCounterGoodColor.Color();
                badColor = PluginSettings.CampaignCounterBadColor.Color();

                enabledGoalColors = PluginSettings.CampaignCounterGoalColors;

                Map = campaignVC.CurrentMap!;
                DiffInfo = Plugin.Container.TryResolve<SerializationHandler>().GetDiffByIdAsync(Map.MapDifficultyId).GetAwaiter().GetResult()!;

                lineData = new LineInfo[Map.Targets.Count];
                targets = [.. Map.Targets];

                DisplayText = (TMP_Text)CanvasCreateText!.Invoke(CanvasUtility, [Settings, null]);

                Color displayColor = enabledGoalColors ? Color.yellow : PluginSettings.CampaignCounterNeutralColor;

                DisplayText.text = $"<color={displayColor.Color()}>Campaign Counter</color>";
                DisplayText.fontSize = PluginSettings.CampaignCounterFontSize;

                object canvasId = SettingsCanvasID!.GetValue(Settings);
                Canvas? canvas = (Canvas?)GetCanvasFromId!.Invoke(CanvasUtility, [canvasId]);

                if (canvas is null)
                {
                    object hudCanvas = CanvasUtilityType.GetMethod("GetCanvasSettingsFromID").Invoke(CanvasUtility, [canvasId]);
                    canvas = (Canvas)CanvasUtilityType.GetMethod("CreateCanvasWithConfig").Invoke(CanvasUtility, [hudCanvas]);
                    ((IDictionary)CanvasUtilityType.GetField("CanvasIDToCanvas", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(CanvasUtility))[canvasId] = canvas;
                    ((IDictionary)CanvasUtilityType.GetField("CanvasToSettings", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(CanvasUtility))[canvas] = hudCanvas;
                }

                GameObject go = new("CampaignCounterDisplay");
                go.transform.SetParent(canvas.transform, false);

                RectTransform rt = go.AddComponent(DisplayText.rectTransform);
                rt.sizeDelta = Vector2.one * (DisplayText.fontSize * PluginSettings.CampaignCounterCheckmarkScale);
                DisplayText.rectTransform.anchoredPosition = new(DisplayText.rectTransform.anchoredPosition.x, DisplayText.rectTransform.anchoredPosition.y - rt.sizeDelta.y / 2f);

                ImageView img = go.AddComponent<ImageView>();
                img.transform.SetParent(go.transform, false);

                img.material = Utilities.ImageResources.NoGlowMat;
                img.sprite = Utilities.FindSpriteCached("Checkmark");
                img.color = Color.white;

                Checkmark = img;
            }
            catch (Exception e)
            {
                Plugin.Log.Error(e);
                return;
            }

            cleanupActions.Clear();

            for (int i = 0; i < targets.Length; ++i)
            {
                int current = i;

                AccSaberCampaignTarget target = targets[current];

                Action ActionEvent0(Action<int> action) => () => action(current);

                Action<T> ActionEvent1<T>(Action<T, int> action) => item => action(item, current);

                Action<T1, T2> ActionEvent2<T1, T2>(Action<T1, T2, int> action) => (item1, item2) => action(item1, item2, current);

                Action action0;
                Action<ScoringElement> action1;
                Action<int, int> action2;

                lineData[i] = new("");

                switch (target.RequirementType)
                {
                    case CampaignModel.CampaignRequirementType.ACC:
                        action1 = ActionEvent1<ScoringElement>(AccCounter);
                        sc.scoringForNoteFinishedEvent += action1;
                        cleanupActions.Add(() => sc.scoringForNoteFinishedEvent -= action1);
                        break;
                    case CampaignModel.CampaignRequirementType.AP:
                        Calc = Plugin.Container.TryResolve<APCalc>();

                        action1 = ActionEvent1<ScoringElement>(ApCounter);
                        sc.scoringForNoteFinishedEvent += action1;
                        cleanupActions.Add(() => sc.scoringForNoteFinishedEvent -= action1);
                        break;
                    case CampaignModel.CampaignRequirementType.SCORE:
                        action2 = ActionEvent2<int, int>(ScoreCounter);
                        sc.scoreDidChangeEvent += action2;
                        cleanupActions.Add(() => sc.scoreDidChangeEvent -= action2);
                        break;
                    case CampaignModel.CampaignRequirementType.STREAK_115:
                        action1 = ActionEvent1<ScoringElement>(StreakCounter);
                        sc.scoringForNoteFinishedEvent += action1;
                        cleanupActions.Add(() => sc.scoringForNoteFinishedEvent -= action1);
                        break;
                    case CampaignModel.CampaignRequirementType.FC:
                        lineData[i].Success = true;
                        lineData[i].Content = "FC!";

                        action0 = ActionEvent0(FCCounter);
                        cc.comboBreakingEventHappenedEvent += action0;
                        cleanupActions.Add(() => cc.comboBreakingEventHappenedEvent -= action0);
                        break;
                    case CampaignModel.CampaignRequirementType.PASS:
                        lineData[i].Success = true;
                        lineData[i].Content = "Pass!";

                        energy = Resources.FindObjectsOfTypeAll<GameEnergyCounter>().LastOrDefault(x => x.isActiveAndEnabled);

                        action0 = ActionEvent0(FCCounter);
                        energy.gameEnergyDidReach0Event += action0;
                        cleanupActions.Add(() => energy.gameEnergyDidReach0Event -= action0);
                        break;
                    default: // TODO: Add Rank (or maybe just don't worry about that one)
                        break;
                }
            }

            if (cleanupActions.Count > 0)
                sc.scoringForNoteFinishedEvent += UpdateDisplay;
        }
        public void Dispose()
        {
            if (cleanupActions.Count == 0)
                return;

            foreach (Action action in cleanupActions)
                action.Invoke();

            sc.scoringForNoteFinishedEvent -= UpdateDisplay;
        }


        private void AccCounter(ScoringElement scoringElement, int index)
        {
            float acc = sc.multipliedScore / (float)sc.immediateMaxPossibleMultipliedScore * 100f;
            float goalAcc = targets[index].RequirementValue * 100f;

            UpdateInfo(index, success: acc >= goalAcc - 0.005f, content: $"{acc:N2}% / {goalAcc:N2}%");
        }
        private void ApCounter(ScoringElement scoringElement, int index)
        {
            float acc = sc.multipliedScore / (float)sc.immediateMaxPossibleMultipliedScore;
            float ap = Calc.GetAp(acc, DiffInfo.Complexity);

            UpdateInfo(index, success: ap >= targets[index].RequirementValue, content: $"{ap:0.##} ap / {targets[index].RequirementValue:0.##} ap");
        }
        private void ScoreCounter(int multipliedScore, int modifiedScore, int index)
        {
            UpdateInfo(index, success: multipliedScore >= targets[index].RequirementValue, content: $"{multipliedScore:N0} / {targets[index].RequirementValue:N0} Score");
        }
        private void StreakCounter(ScoringElement scoringElement, int index)
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

            UpdateInfo(index, success: streak >= (int)targets[index].RequirementValue, content: $"{streak}x / {targets[index].RequirementValue:N0}x 115 streak");
        }
        private void FCCounter(int index)
        {
            UpdateInfo(index, success: false);
        }


        private void UpdateInfo(int index, bool? success = null, string? content = null)
        {
            if (success is not null)
            {
                if (enabledGoalColors)
                    lineData[index].Color = success.Value ? goodColor : badColor;

                if (success.Value && index < highestSuccessIndex)
                    highestSuccessIndex = index;
            }

            if (content is not null)
                lineData[index].Content = content;
        }
        private void UpdateDisplay(ScoringElement _) => UpdateDisplay();
        private void UpdateDisplay()
        {
            bool success = Map.TargetMode switch
            {
                CampaignModel.CampaignPrerequisiteMode.AND => lineData.All(line => line.Success),
                CampaignModel.CampaignPrerequisiteMode.OR => lineData.Any(line => line.Success),
                _ => false,
            };

            Checkmark.color = success ? PluginSettings.CampaignCounterCheckmarkGoodColor : PluginSettings.CampaignCounterCheckmarkBadColor;

            if (success && Map.TargetMode == CampaignModel.CampaignPrerequisiteMode.OR && highestSuccessIndex >= 0)
                lineData[highestSuccessIndex].Color = lineData[highestSuccessIndex].Color.BrightenColor(5);

            highestSuccessIndex = -1;

            string outp = lineData.Aggregate("", (total, current) => total + current + '\n')[..^1];

            DisplayText.SetText(outp);
        }

        private struct LineInfo(string content, bool success = false, string? color = null)
        {
            public string Content { get; set; } = content;
            public bool Success { get; set; } = success;
            public string Color { get; set; } = color ?? "#FFF";

            public override readonly string ToString() => 
                $"<color={Color}>{Content}</color>";
        }
    }
}
