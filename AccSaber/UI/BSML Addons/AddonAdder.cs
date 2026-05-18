using AccSaber.UI.ViewControllers;
using AccSaber.Utils;
using AccsaberLeaderboard.UI.BSML_Addons.Tags;
using AccsaberLeaderboard.UI.BSML_Addons.TypeHandlers;
using BeatSaberMarkupLanguage;
using Zenject;

namespace AccsaberLeaderboard.UI.BSML_Addons
{
    internal class AddonAdder : IInitializable
    {
#if !NEW_VERSION
        private static bool inited = false;
#endif
        public void Initialize()
        {
#if !NEW_VERSION
            if (inited) return;
            inited = true;
#endif
            AccSaberLeaderboardViewController.Instance?.OnGameRefresh();

            BSMLParser instance = VersionUtils.BSMLParser_Instance;

            instance.RegisterTag(new BetterVertical());
            instance.RegisterTag(new BetterHorizontal());
            instance.RegisterTag(new MyCustomList());

            instance.RegisterTypeHandler(new CustomBackgroundHandler());
            instance.RegisterTypeHandler(new MyCustomCellListTableDataHandler());

            AccSaber.Plugin.Log.Info("Tags Loaded.");
        }
    }
}
