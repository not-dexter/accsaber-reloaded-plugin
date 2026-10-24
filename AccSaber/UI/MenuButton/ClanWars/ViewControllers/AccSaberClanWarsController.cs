using BeatSaberMarkupLanguage.Attributes;
using HMUI;

namespace AccSaber.UI.MenuButton.ClanWars.ViewControllers
{
    [ViewDefinition("AccSaber.UI.MenuButton.ClanWars.Views.AccSaberClanWarsView.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\AccSaberClanWarsView.bsml")]
    internal class AccSaberClanWarsController : Utils.Safety.BSMLSafeAutomaticViewController
    {
        [UIAction("#post-parse")]
        private void PostParse()
        {

        }
    }
}
