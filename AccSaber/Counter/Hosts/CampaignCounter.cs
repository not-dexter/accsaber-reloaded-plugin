using AccSaber.Configuration;
using AccSaber.Models;
using AccSaber.ScoreTracking;
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
using System.Text;
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
        [Inject] private readonly ScoreCounter myScoreCounter = null!;
        private GameEnergyCounter energy = null!;

        [Inject] private readonly SerializationHandler serialhandler = null!;

        private TMP_Text DisplayText = null!;
        private ImageView Checkmark = null!;
        private int max115Streak = 0, current115Streak = 0;
        private int bombHits = 0, mistakes = 0;
        private LineInfo[] lineData = null!;
        private AccSaberCampaignTarget[] targets = null!;
        private readonly List<Action> cleanupActions = [];
        private int highestSuccessIndex = int.MaxValue;
        private string goodColor = null!, badColor = null!;
        private StringBuilder outpString = null!;

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
                DiffInfo = serialhandler.GetDiffByIdAsync(Map.MapDifficultyId).GetAwaiter().GetResult()!;

                outpString = new();
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
                Action<ScoringElement> action10;
                Action<int> action11;
                Action<int, int> action2;

                switch (target.RequirementType)
                {
                    case CampaignModel.CampaignRequirementType.ACC:
                        action10 = ActionEvent1<ScoringElement>(AccCounter);
                        sc.scoringForNoteFinishedEvent += action10;
                        cleanupActions.Add(() => sc.scoringForNoteFinishedEvent -= action10);

                        lineData[i] = new(SelectContent(i, "{0:N2}% / {1:N2}%", "{0:N2}% <= {2:N2}%", "{1:N2}% <= {0:N2}% <= {2:N2}%", "{0:N2}% = {1:N2}%"), mult: 100f);
                        break;
                    case CampaignModel.CampaignRequirementType.AP:
                        Calc = Plugin.Container.TryResolve<APCalc>();

                        action10 = ActionEvent1<ScoringElement>(ApCounter);
                        sc.scoringForNoteFinishedEvent += action10;
                        cleanupActions.Add(() => sc.scoringForNoteFinishedEvent -= action10);

                        lineData[i] = new(SelectContent(i, "{0:0.##} ap / {1:0.##} ap", "{0:0.##} ap <= {2:0.##} ap", "{1:0.##} ap <= {0:0.##} ap <= {2:0.##} ap", "{0:0.##} ap = {1:0.##} ap"));
                        break;
                    case CampaignModel.CampaignRequirementType.SCORE:
                        action2 = ActionEvent2<int, int>(ScoreCounter);
                        sc.scoreDidChangeEvent += action2;
                        cleanupActions.Add(() => sc.scoreDidChangeEvent -= action2);

                        lineData[i] = new(SelectContent(i, "{0:N0} / {1:N0} score", "{0:N0} <= {2:N0} score", "{1:N0} <= {0:N0} <= {2:N0} score", "{0:N0} = {1:N0} score"));
                        break;
                    case CampaignModel.CampaignRequirementType.STREAK_115:
                        action10 = ActionEvent1<ScoringElement>(StreakCounter);
                        sc.scoringForNoteFinishedEvent += action10;
                        cleanupActions.Add(() => sc.scoringForNoteFinishedEvent -= action10);

                        lineData[i] = new(SelectContent(i, "{0:N0}x / {1:N0}x 115 streak", "{0:N0}x <= {2:N0}x 115 streak", "{1:N0}x <= {0:N0}x <= {2:N0}x 115 streak", "{0:N0}x = {1:N0}x 115 streak"));
                        break;
                    case CampaignModel.CampaignRequirementType.FC:
                        action0 = ActionEvent0(FCCounter);
                        cc.comboBreakingEventHappenedEvent += action0;
                        cleanupActions.Add(() => cc.comboBreakingEventHappenedEvent -= action0);

                        lineData[i] = new("FC!", true);
                        UpdateColor(i);
                        break;
                    case CampaignModel.CampaignRequirementType.PASS:
                        energy = Resources.FindObjectsOfTypeAll<GameEnergyCounter>().LastOrDefault(x => x.isActiveAndEnabled);

                        action0 = ActionEvent0(FCCounter);
                        energy.gameEnergyDidReach0Event += action0;
                        cleanupActions.Add(() => energy.gameEnergyDidReach0Event -= action0);

                        lineData[i] = new("Pass!", true);
                        UpdateColor(i);
                        break;
                    case CampaignModel.CampaignRequirementType.COMBO:
                        action11 = ActionEvent1<int>(ComboCounter);
                        cc.comboDidChangeEvent += action11;
                        cleanupActions.Add(() => cc.comboDidChangeEvent -= action11);

                        action0 = () => action11(0);
                        cc.comboBreakingEventHappenedEvent += action0;
                        cleanupActions.Add(() => cc.comboBreakingEventHappenedEvent -= action0);

                        lineData[i] = new(SelectContent(i, "{0:N0} / {1:N0} combo", "{0:N0} <= {2:N0} combo", "{1:N0} <= {0:N0} <= {2:N0} combo", "{0:N0} = {1:N0} combo"));
                        break;
                    case CampaignModel.CampaignRequirementType.BOMB_HITS:
                        action10 = ActionEvent1<ScoringElement>(BombHitCounter);
                        sc.scoringForNoteFinishedEvent += action10;
                        cleanupActions.Add(() => sc.scoringForNoteFinishedEvent -= action10);

                        lineData[i] = new(SelectContent(i, "{0:N0} / {1:N0} bombs", "{0:N0} <= {2:N0} bombs", "{1:N0} <= {0:N0} <= {2:N0} bombs", "{0:N0} = {1:N0} bombs"));
                        break;
                    case CampaignModel.CampaignRequirementType.MISTAKES:
                        action0 = ActionEvent0(MistakeCounter);
                        myScoreCounter.OnMistake += action0;
                        cleanupActions.Add(() => myScoreCounter.OnMistake -= action0);

                        lineData[i] = new(SelectContent(i, "{0:N0} / {1:N0} mistakes", "{0:N0} <= {2:N0} mistakes", "{1:N0} <= {0:N0} <= {2:N0} mistakes", "{0:N0} = {1:N0} mistakes"));
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
            float acc = sc.multipliedScore / (float)sc.immediateMaxPossibleMultipliedScore;
            float goalAccMin = targets[index].RequirementValue;
            float? goalAccMax = targets[index].RequirementValueMax;

            lineData[index].CurrentValue = acc;

            UpdateInfo(index, success: DoComp(ComparisonType.GTE, acc, goalAccMin - 0.00005f, goalAccMax - 0.00005f));
        }
        private void ApCounter(ScoringElement scoringElement, int index)
        {
            float acc = sc.multipliedScore / (float)sc.immediateMaxPossibleMultipliedScore;
            float ap = Calc.GetAp(acc, DiffInfo.Complexity);

            lineData[index].CurrentValue = ap;

            UpdateInfo(index, success: DoNormalComp(index, ap));
        }
        private void ScoreCounter(int multipliedScore, int modifiedScore, int index)
        {
            lineData[index].CurrentValue = multipliedScore;

            UpdateInfo(index, success: DoNormalComp(index, multipliedScore));
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

            lineData[index].CurrentValue = streak;

            UpdateInfo(index, success: DoNormalComp(index, streak));
        }
        private void FCCounter(int index)
        {
            UpdateInfo(index, success: false);
        }
        private void ComboCounter(int currentCombo, int index)
        {
            lineData[index].CurrentValue = currentCombo;

            UpdateInfo(index, success: DoNormalComp(index, currentCombo));
        }
        private void BombHitCounter(ScoringElement scoringElement, int index)
        {
            if (scoringElement.noteData.gameplayType != NoteData.GameplayType.Bomb)
                return;

            lineData[index].CurrentValue = ++bombHits;

            UpdateInfo(index, success: DoNormalComp(index, bombHits));
        }
        private void MistakeCounter(int index)
        {
            lineData[index].CurrentValue = ++mistakes;

            UpdateInfo(index, success: DoNormalComp(index, mistakes));
        }


        private string SelectContent(int index, string minOnly, string maxOnly, string minMax, string minMaxEqual)
        {
            if (targets[index].RequirementValueMax is null)
                return minOnly;

            if (Mathf.Approximately(targets[index].RequirementValue, 0f))
                return maxOnly;
            // requirementValueMax cannot be null, otherwise the previous if statement would have been true, so we can safely use !.Value here
            if (Mathf.Approximately(targets[index].RequirementValue, targets[index].RequirementValueMax!.Value))
                return minMaxEqual;

            return minMax;
        }

        // input >= ReqVal
        private bool DoNormalComp(int index, float current) =>
            DoComp(ComparisonType.GTE, current, targets[index].RequirementValue, targets[index].RequirementValueMax);
        private bool DoComp(ComparisonType comp, float current, float minTarget, float? maxTarget = null)
        {
            return comp switch
            {
                ComparisonType.GTE => current >= minTarget && (maxTarget is null || current <= maxTarget),
                ComparisonType.GT => current > minTarget && (maxTarget is null || current < maxTarget),
                ComparisonType.LTE => current <= minTarget && (maxTarget is null || current >= maxTarget),
                ComparisonType.LT => current < minTarget && (maxTarget is null || current > maxTarget),
                _ => false
            };
        }

        private void UpdateInfo(int index, bool success)
        {
            lineData[index].Success = success;

            if (enabledGoalColors)
                UpdateColor(index);

            if (success && index < highestSuccessIndex)
                highestSuccessIndex = index; // note highestSuccessIndex is the lowest index.
        }
        private void UpdateColor(int index) => lineData[index].Color = lineData[index].Success ? goodColor : badColor;
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

            outpString.Clear();
            for (int i = 0; i < lineData.Length; ++i)
            {
                LineInfo line = lineData[i];
                outpString.AppendLine(line.UpdateContent ? line.ToString(targets[i]) : line.ToString());
            }
            string outp = outpString.ToString();

            DisplayText.SetText(outp);
        }

        private struct LineInfo(string content, bool success = false, string? color = null, float? shift = null, float? mult = null)
        {
            public string Content { get; set; } = content;
            public bool Success 
            { 
                get;
                set
                {
                    if (value == field)
                        return;

                    field = value;
                    UpdateContent = true;
                }
            } = success;
            public string Color 
            {
                get;
                set
                {
                    if (value.Equals(field))
                        return;

                    field = value;
                    UpdateContent = true;
                }
            } = color ?? "#FFF";

            public readonly float? Shift = shift, Mult = mult;

            public float CurrentValue 
            { 
                get;
                set
                {
                    if (Mathf.Approximately(value, field))
                        return;

                    field = value;
                    UpdateContent = true;
                }
            } = 0f;
            public bool UpdateContent { get; set; } = true;

            private string LastOutput { get; set; } = $"<color={color ?? "#FFF"}>{content}</color>";

            public string ToString(AccSaberCampaignTarget target)
            {
                float current = CurrentValue, targetMin = target.RequirementValue, targetMax = target.RequirementValueMax ?? 0f;

                if (Shift is not null)
                {
                    current += Shift.Value;
                    targetMin += Shift.Value;
                    targetMax += Shift.Value;
                }

                if (Mult is not null)
                {
                    current *= Mult.Value;
                    targetMin *= Mult.Value;
                    targetMax *= Mult.Value;
                }

                LastOutput = $"<color={Color}>{string.Format(Content, current, targetMin, targetMax)}</color>";
                return LastOutput;
            }

            public override string ToString()
            {
                if (UpdateContent) 
                {
                    LastOutput = $"<color={Color}>{Content}</color>";
                    UpdateContent = false;
                }
                return LastOutput;
            }
        }
    }
}
