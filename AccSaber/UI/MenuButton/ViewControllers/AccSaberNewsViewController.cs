using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using System.ComponentModel;
using HMUI;
using SiraUtil.Logging;
using Zenject;
using System.Collections.Generic;

namespace AccSaber.UI.MenuButton.ViewControllers
{
    [ViewDefinition("AccSaber.UI.MenuButton.Views.AccSaberNewsView.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\AccSaberNewsView.bsml")]
    internal class AccSaberNewsViewController : BSMLAutomaticViewController, INotifyPropertyChanged
    {

        public new event PropertyChangedEventHandler? PropertyChanged;

        [UIComponent("tab-selector")]
        protected readonly TabSelector _tabSelector = null!;

    }
}