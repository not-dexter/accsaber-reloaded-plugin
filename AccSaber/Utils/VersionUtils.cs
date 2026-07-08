using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.MenuButtons;
using BeatSaberMarkupLanguage.Parser;
using HMUI;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BeatSaberMarkupLanguage.FloatingScreen;


#if V41
using OculusStudios.Platform.Core;
#endif

#if NEW_VERSION
using System.Collections;
#endif

namespace AccSaber.Utils
{
    internal static class VersionUtils
    {
#if NEW_VERSION
        #region Higher Version Only
#if V41
        public static async Task<UserInfo> GetUserInfo(this IPlatform model)
        {
            UserInfo.Platform platform = model.vendor switch
            {
                Vendor.Valve => UserInfo.Platform.Steam,
                Vendor.Meta => UserInfo.Platform.Oculus,
                _ => throw new NotImplementedException()
            };

            return new(platform, model.user.userId.ToString(), model.user.displayName);
        }
#else
        public static async Task<UserInfo> GetUserInfo(this IPlatformUserModel model) => await model.GetUserInfo(default);
#endif
        public static Color ColorWithAlpha(this Color c, float alpha) => new(c.r, c.g, c.b, alpha);
        #endregion
#if V41
        public static Task<Sprite> LoadSpriteAsync(string path) => Utilities.LoadSpriteAsync(Utilities.GetResourceAsync(Assembly.GetExecutingAssembly(), path).GetAwaiter().GetResult());
#else
        public static Task<Sprite> LoadSpriteAsync(string path) => Utilities.LoadSpriteAsync(Utilities.GetResource(Assembly.GetExecutingAssembly(), path));
#endif
        public static Task<Sprite> LoadSpriteAsync(byte[] data, float PixelsPerUnit = 100) => Utilities.LoadSpriteAsync(data, PixelsPerUnit);
        public static Task<Texture2D> LoadImageAsync(byte[] data) => Utilities.LoadImageAsync(data);
        public static IList Data(this CustomCellListTableData ccltd) => ccltd.Data;
        public static TableView TableView(this CustomCellListTableData ccltd) => ccltd.TableView;
        public static Image Background(this Backgroundable bg) => bg.Background;
        public static Image Image(this ButtonIconImage bii) => bii.Image;
        public static GameObject Handle(this FloatingScreen screen) => screen.Handle;
        public static Component Component(this BSMLParser.ComponentTypeWithData ctwd) => ctwd.Component;
        public static Dictionary<string, string> Data(this BSMLParser.ComponentTypeWithData ctwd) => ctwd.Data;
        public static Dictionary<string, BSMLValue> Values(this BSMLParserParams _params) => _params.Values;
        public static Dictionary<string, BSMLAction> Actions(this BSMLParserParams _params) => _params.Actions;
        public static BSMLParser BSMLParser_Instance => BSMLParser.Instance;
        public static MenuButtons MenuButtons_Instance => MenuButtons.Instance;
#else
        #region Lower Version Only
#pragma warning disable IDE0060
        public static async Task SetImageAsync(this Image image, string location, bool animated = true) => image.SetImage(location);
#pragma warning restore IDE0060
        #endregion
        public static async Task<Sprite> LoadSpriteAsync(string path) => Utilities.LoadSpriteRaw(Utilities.GetResource(Assembly.GetExecutingAssembly(), path));
        public static async Task<Sprite> LoadSpriteAsync(byte[] data, float PixelsPerUnit = 100) => Utilities.LoadSpriteRaw(data, PixelsPerUnit);
        public static async Task<Texture2D> LoadImageAsync(byte[] data) => Utilities.LoadTextureRaw(data);
        public static ref List<object> Data(this CustomCellListTableData ccltd) => ref ccltd.data;
        public static ref TableView TableView(this CustomCellListTableData ccltd) => ref ccltd.tableView;
        public static ref Image Background(this Backgroundable bg) => ref bg.background;
        public static ref Image Image(this ButtonIconImage bii) => ref bii.image;
        public static ref GameObject Handle(this FloatingScreen screen) => ref screen.handle;
        public static Component Component(this BSMLParser.ComponentTypeWithData ctwd) => ctwd.component;
        public static Dictionary<string, string> Data(this BSMLParser.ComponentTypeWithData ctwd) => ctwd.data;
        public static Dictionary<string, BSMLValue> Values(this BSMLParserParams _params) => _params.values;
        public static Dictionary<string, BSMLAction> Actions(this BSMLParserParams _params) => _params.actions;
        public static BSMLParser BSMLParser_Instance => BSMLParser.instance;
        public static MenuButtons MenuButtons_Instance => MenuButtons.instance;
#endif
#if V40
        public static bool IsShown(this ModalView view) => view.isShown;
#else
        public static bool IsShown(this ModalView view) => (bool)typeof(ModalView).GetField("_isShown", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
#endif

        public static BSMLParserParams Parse(string resourcePath, Component parent, object controller) => Parse(resourcePath, parent.gameObject, controller);
        public static BSMLParserParams Parse(string resourcePath, GameObject parent, object controller) =>
            BSMLParser_Instance.Parse(Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), resourcePath), parent, controller);
    }
}
