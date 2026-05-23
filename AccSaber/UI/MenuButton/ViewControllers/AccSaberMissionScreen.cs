using AccSaber.Utils;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.FloatingScreen;
using BeatSaberMarkupLanguage.Parser;
using IPA.Utilities;
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

        public event PropertyChangedEventHandler? PropertyChanged;
        [Inject]
        public void Construct()
        {
        }
        public void Initialize()
        {
            missionScreen = FloatingScreen.CreateFloatingScreen(new Vector2(75, 100), true, new Vector3(0f, 0.05f, 2.5f), new Quaternion(0, 60, 0, 0));
            missionScreen.gameObject.name = "AccSaberMissionScreen";
            missionScreen.gameObject.SetActive(false);
            missionScreen.transform.eulerAngles = new Vector3(90, 0, 0);
            missionScreen.transform.localScale = new Vector3(0.03f, 0.03f, 0.03f);

            missionScreen.Handle.SetActive(false);
            VersionUtils.BSMLParser_Instance.Parse(Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), "AccSaber.UI.MenuButton.Views.AccSaberMissionScreen.bsml"), missionScreen.gameObject, this);
        }

        public void ShowMissions()
        {
            missionScreen.gameObject.SetActive(true);
        }


        public void HideMissions()
        {
            missionScreen.gameObject.SetActive(false);
        }

        public void Dispose()
        {

        }    
    }
}
