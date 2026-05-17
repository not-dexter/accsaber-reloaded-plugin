using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.MenuButtons;
using BeatSaberMarkupLanguage.Parser;
using HMUI;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

#if NEW_VERSION
using System.Collections;
#else
using System.Collections.Generic;
#endif

namespace AccSaber.Utils
{
    internal static class VersionUtils
    {
#if NEW_VERSION
        #region Higher Version Only
        public static async Task<UserInfo> GetUserInfo(this IPlatformUserModel model) => await model.GetUserInfo(default);
        public static Color ColorWithAlpha(this Color c, float alpha) => new(c.r, c.g, c.b, alpha);
        #endregion
        public static Task<Sprite> LoadSpriteFromAssemblyAsync(string path) => Utilities.LoadSpriteFromAssemblyAsync(path);
        public static IList Data(this CustomCellListTableData ccltd) => ccltd.Data;
        public static TableView TableView(this CustomCellListTableData ccltd) => ccltd.TableView;
        public static Image Background(this Backgroundable bg) => bg.Background;
        public static Image Image(this ButtonIconImage bii) => bii.Image;
        public static BSMLParser BSMLParser_Instance => BSMLParser.Instance;
        public static MenuButtons MenuButtons_Instance => MenuButtons.Instance;
#else
        #region Lower Version Only
        public static async Task SetImageAsync(this Image image, string location, bool animated = true) => image.SetImage(location);
        #endregion
        public static Task<Sprite> LoadSpriteFromAssemblyAsync(string path) =>
            Task.Run(() => Utilities.LoadSpriteRaw(Utilities.GetResource(Assembly.GetExecutingAssembly(), path)));
        public static ref List<object> Data(this CustomCellListTableData ccltd) => ref ccltd.data;
        public static ref TableView TableView(this CustomCellListTableData ccltd) => ref ccltd.tableView;
        public static ref Image Background(this Backgroundable bg) => ref bg.background;
        public static ref Image Image(this ButtonIconImage bii) => ref bii.image;
        public static BSMLParser BSMLParser_Instance => BSMLParser.instance;
        public static MenuButtons MenuButtons_Instance => MenuButtons.instance;
#endif

        public static BSMLParserParams Parse(string resourcePath, Transform parent, object controller) =>
            BSMLParser_Instance.Parse(Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), resourcePath), parent.gameObject, controller);
    }
}
