using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.MenuButtons;
using HMUI;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace AccSaber.Utils
{
    internal static class VersionUtils
    {
#if NEW_VERSION
        public static Task<Sprite> LoadSpriteFromAssemblyAsync(string path) => Utilities.LoadSpriteFromAssemblyAsync(path);
        public static Task SetImageAsync(this Image image, string location, bool animated) => Task.Run(() => image.SetImage(location));
        public static ref List<object> Data(this CustomCellListTableData ccltd) => ref ccltd.Data;
        public static ref TableView TableView(this CustomCellListTableData ccltd) => ref ccltd.TableView;
        public static ref Image Background(this Backgroundable bg) => ref bg.Background;
        public static BSMLParser BSMLParser_Instance => BSMLParser.Instance;
        public static MenuButtons MenuButtons_Instance => MenuButtons.Instance;
#else
        public static Task<Sprite> LoadSpriteFromAssemblyAsync(string path) =>
            Task.Run(() => Utilities.LoadSpriteRaw(Utilities.GetResource(Assembly.GetExecutingAssembly(), path)));
        public static Task SetImageAsync(this Image image, string location, bool animated) => Task.Run(() => image.SetImage(location));
        public static ref List<object> Data(this CustomCellListTableData ccltd) => ref ccltd.data;
        public static ref TableView TableView(this CustomCellListTableData ccltd) => ref ccltd.tableView;
        public static ref Image Background(this Backgroundable bg) => ref bg.background;
        public static BSMLParser BSMLParser_Instance => BSMLParser.instance;
        public static MenuButtons MenuButtons_Instance => MenuButtons.instance;
#endif
    }
}
