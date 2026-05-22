using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccSaber.UI.MenuButton.Campaigns.ViewControllers
{
    [ViewDefinition("AccSaber.UI.MenuButton.Campaigns.Views.AccSaberCampaignView.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\AccSaberCampaignView.bsml")]
    internal class AccSaberCampaignViewController : BSMLAutomaticViewController
    {

    }
}
