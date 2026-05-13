using AccSaber.Utils;
using AccsaberLeaderboard.UI.BSML_Addons.Tags;
using AccsaberLeaderboard.UI.BSML_Addons.TypeHandlers;
using BeatSaberMarkupLanguage;

namespace AccsaberLeaderboard.UI.BSML_Addons
{
    internal static class AddonAdder
    {
        public static void Load()
        {
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
