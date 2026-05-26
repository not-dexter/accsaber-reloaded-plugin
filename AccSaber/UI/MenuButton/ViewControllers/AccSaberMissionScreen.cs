using AccSaber.Consts;
using AccSaber.Managers;
using AccSaber.Models;
using AccSaber.Utils;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.FloatingScreen;
using HMUI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace AccSaber.UI.MenuButton.ViewControllers
{

    [ViewDefinition("AccSaber.UI.MenuButton.Views.AccSaberMissionScreen.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\AccSaberMissionScreen.bsml")]
    internal class AccSaberMissionScreen : IInitializable, IDisposable, INotifyPropertyChanged
    {
        public FloatingScreen missionScreen = null!;
        private bool _parsed;
        private bool _isLoading;

        [UIComponent("daily-list")]
        private readonly CustomCellListTableData _dailyList = null!;

        [UIValue("daily-cells")]
        private readonly List<object> _dailyCells = [];

        [UIComponent("weekly-list")]
        private readonly CustomCellListTableData _weeklyList = null!;

        [UIValue("weekly-cells")]
        private readonly List<object> _weeklyCells = [];

        private List<Action> _lsPropertyChangedActions = new List<Action>();

        public event PropertyChangedEventHandler? PropertyChanged;

        private AccSaberStore _accSaberStore = null!;
        private AccSaberMenuViewController _accSaberMenuViewController = null!;
        [Inject]
        public void Construct(AccSaberStore accSaberStore, AccSaberMenuViewController accSaberMenuViewController)
        {
            _accSaberStore = accSaberStore;
            _accSaberMenuViewController = accSaberMenuViewController;
        }
        public void Initialize()
        {
            missionScreen = FloatingScreen.CreateFloatingScreen(new Vector2(70, 90), true, new Vector3(0f, 0.05f, 1.8f), new Quaternion(0, 60, 0, 0));
            missionScreen.gameObject.name = "AccSaberMissionScreen";
            missionScreen.gameObject.SetActive(false);
            missionScreen.transform.eulerAngles = new Vector3(90, 0, 0);
            missionScreen.transform.localScale = new Vector3(0.03f, 0.03f, 0.03f);

            missionScreen.Handle().SetActive(false);
            VersionUtils.BSMLParser_Instance.Parse(Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), "AccSaber.UI.MenuButton.Views.AccSaberMissionScreen.bsml"), missionScreen.gameObject, this);

            _accSaberMenuViewController.HubActivated += ShowMissions;
            _accSaberMenuViewController.HubDeactivated += HideMissions;
        }

        [UIValue("is-loading")]
        private bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLoading)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsNotLoading)));
            }
        }


        [UIValue("is-not-loading")]
        private bool IsNotLoading => !_isLoading;

        public void ShowMissions()
        {
            missionScreen.gameObject.SetActive(false);
        }
        public void HideMissions()
        {
            missionScreen.gameObject.SetActive(false);
        }
        [UIAction("#post-parse")]
        void Parsed()
        {
            if (!_parsed)
            {
                _parsed = true;
            }
            IsLoading = true;
            _ = SetMissions();
        }
        private async Task SetMissions()
        {
            var session = PlayerSocialLife.AuthInfo;
            if(session is null)
            {
                await Task.Delay(5000);
                await SetMissions();
            }

            _dailyCells.Clear();
            _weeklyCells.Clear();
            _dailyList.Data().Clear();
            _weeklyList.Data().Clear();

            try
            {
                var missions = await _accSaberStore.GetMissions();
                foreach (var post in missions)
                {
                    switch (post.MissionPool)
                    {
                        case MissionPool.Daily: _dailyCells.Add(new MissionCell(post)); break;
                        case MissionPool.Weekly: _weeklyCells.Add(new MissionCell(post)); break;
                    }
                }
                _dailyList.TableView().ReloadData();
                _weeklyList.TableView().ReloadData();
                IsLoading = false;
            }
            catch (Exception e)
            {
                Plugin.Log.Error(e);
            }
        }

        public void Dispose()
        {
            _accSaberMenuViewController.HubActivated -= ShowMissions;
            _accSaberMenuViewController.HubDeactivated -= HideMissions;
        }

        internal class MissionCell(AccSaberMission data)
        {
            #region BSML Values

            private float Progress()
            {
                if (data.TargetCount is not null || data.TargetCount < 0)
                {
                    if (data.ProgressCount > 0)
                        return (float)(data.ProgressCount / data.TargetCount);
                    else
                        return (float)(0.01f / data.TargetCount);
                }
                return 0f;
            }

            private string color = data.Band switch
            {
                "extreme" => "#ffd700",
                /*"hard" => "#f97316",
                "medium" => "#3cb371",
                "easy" => "#3cb371",*/
                _ => ColorUtils.GREY
            };
            private string GetCategoryName(string category)
            {
                return category switch
                {
                    "b0000000-0000-0000-0000-000000000001" => "True",
                    "b0000000-0000-0000-0000-000000000002" => "Standard",
                    "b0000000-0000-0000-0000-000000000003" => "Tech",

                    _ => "Overall"
                };
            }
            [UIValue(nameof(showProgress))]
            public bool showProgress => data.TargetCount is not null;


            [UIValue(nameof(target))]
            public string target => $"{data.ProgressCount}/{data.TargetCount}";
            public string missionCategory => $"<color={ColorUtils.GetColor(EnumUtils.ReloadedCategoryToEnum(data.CategoryId ?? "b0000000-0000-0000-0000-000000000005"))}>{GetCategoryName(data.CategoryId ?? "b0000000-0000-0000-0000-000000000005").ToUpper()}</color>";

            [UIValue(nameof(mission))] public string mission =>  $"{data.Name}  <size=80%>{missionCategory}</size>";

            [UIValue(nameof(missionBand))] public string missionBand => $"<color={color}>{data.Band.ToUpper()}</color>";

            [UIValue(nameof(description))] public string description => descriptionRegex(data.Description);

            public string descriptionRegex(string input)
            {
                var pattern = new Regex(@"(?<=\().+?(?=\))");
                var match = pattern.Match(input);

                if (match.Success)
                {
                    var newString = "";

                    if (EnumUtils.ReloadedDiffToDiff(match.Groups[0].Value) == BeatmapDifficulty.ExpertPlus)
                        newString = input.Replace(match.Groups[0].Value, "Expert Plus");
                    else
                        newString = input.Replace(match.Groups[0].Value, EnumUtils.ReloadedDiffToDiff(match.Groups[0].Value).ToString());


                    return $"<color={ColorUtils.GREY}>{newString}</color>";
                }
                else
                    return $"<color={ColorUtils.GREY}>{input}</color>";
            }

            [UIValue(nameof(missionXP))] public string missionXP => $"<color={ColorUtils.AP}>+{data.XpReward} XP</color>";

            [UIValue(nameof(ExactProgress))]
            public string ExactProgress
            {
                get
                {
                    if (data.TargetCount is not null || data.Status != "completed")
                        return $"<color={ColorUtils.GREY}>({data.ProgressCount} / {data.TargetCount})</color>";
                    else 
                        return "";
                }
            }

            [UIValue(nameof(completed))] private readonly bool completed = data.Status == "completed";

            [UIValue(nameof(targetExists))] private readonly bool targetExists = data.TargetCount is not null;

            [UIValue(nameof(oneXonePic))] public const string oneXonePic = ResourcePaths.PIXEL;

            [UIComponent(nameof(PercentBarTop))] private readonly LayoutElement PercentBarTop = null!;
            [UIComponent(nameof(PercentBarTop))] private readonly ImageView PercentBarTop_image = null!;
            [UIComponent(nameof(PercentBarBottom))] private readonly LayoutElement PercentBarBottom = null!;
            [UIComponent(nameof(PercentBarBottom))] private readonly ImageView PercentBarBottom_image = null!; 
            
            [UIValue(nameof(listWidth))] public const float listWidth = 55f;
            [UIValue(nameof(barSpacer))] public const float barSpacer = 3f;
            [UIValue(nameof(exactProgLen))] public const float exactProgLen = 4f;
            [UIValue(nameof(barLen))] public const float barLen = listWidth - barSpacer  - exactProgLen;

            [UIAction("#post-parse")]
            private void PostParse()
            {
                PercentBarTop?.transform.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, barLen * Progress());
                PercentBarBottom?.transform.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, barLen * (1 - Progress()));

                PercentBarTop_image?.color = ColorUtils.GetColor(EnumUtils.ReloadedCategoryToEnum(data.CategoryId ?? "b0000000-0000-0000-0000-000000000005")).Color();
                PercentBarBottom_image?.color = ColorUtils.GREY.Color();
            }
            #endregion
        }
    }
}
