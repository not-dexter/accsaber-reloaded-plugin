using AccSaber.UI.BSML_Addons.TypeHandlers;
using AccSaber.UI.ViewControllers;
using AccSaber.Utils;
using AccsaberLeaderboard.UI.BSML_Addons.Tags;
using AccsaberLeaderboard.UI.BSML_Addons.TypeHandlers;
using BeatSaberMarkupLanguage;
using Zenject;

#if !NEW_VERSION
using AccSaber.Utils.OldVersion;
#endif

namespace AccsaberLeaderboard.UI.BSML_Addons
{
    internal class AddonAdder : IInitializable
    {
        [Inject] private readonly AccSaberLeaderboardViewController _leaderboardVC = null!;

#if !NEW_VERSION
        private static bool inited = false;
#endif
        public void Initialize()
        {
            _leaderboardVC.OnGameRefresh();

#if !NEW_VERSION
            if (inited) return;
            inited = true;
#endif
            BSMLParser instance = VersionUtils.BSMLParser_Instance;

            instance.RegisterTag(new BetterVertical());
            instance.RegisterTag(new BetterHorizontal());
            instance.RegisterTag(new MyCustomList());

            instance.RegisterTypeHandler(new CustomBackgroundHandler());
            instance.RegisterTypeHandler(new MyCustomCellListTableDataHandler());
            instance.RegisterTypeHandler(new SkewAdder());

#if !NEW_VERSION
            instance.RegisterTypeHandler(new RectTransformHandler());
#endif

            AccSaber.Plugin.Log.Info("Tags Loaded.");
        }
    }
}
