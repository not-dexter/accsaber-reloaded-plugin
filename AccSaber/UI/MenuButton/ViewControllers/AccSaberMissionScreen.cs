using AccSaber.Managers;
using AccSaber.Models;
using AccSaber.Utils;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.FloatingScreen;
using BeatSaberMarkupLanguage.Parser;
using HMUI;
using IPA.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
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

            [UIValue(nameof(description))] public string description => $"<color={ColorUtils.GREY}>{data.Description}</color>";

            [UIValue(nameof(missionXP))] public string missionXP => $"<color={ColorUtils.AP}>+{data.XpReward} XP</color>";

            #endregion
        }
    }
}
