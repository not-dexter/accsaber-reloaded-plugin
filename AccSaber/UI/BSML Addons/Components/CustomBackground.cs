using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Tags;
using HMUI;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace AccsaberLeaderboard.UI.Components
{
    public class CustomBackground : MonoBehaviour
    {
        private static readonly Dictionary<string, Sprite> cachedSprites = [];
        private static ImageView? _roundImage;
        private GameObject? _borderTemplate;
        private static ImageView RoundImage
        {
            get
            {
                if (_roundImage is not null)
                    return _roundImage;

                Dictionary<string, ImageView> cache = (Dictionary<string, ImageView>)typeof(Backgroundable).GetField("BackgroundCache", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

                if (!cache.TryGetValue("panel-top", out ImageView imageView))
                    imageView = (ImageView)typeof(Backgroundable).GetMethod("FindTemplate", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, ["panel-top", "RoundRect10"]);

                _roundImage = imageView;
                return _roundImage;
            }
        }

        private readonly object applyLock = new();
        public ImageView? Background = null;
        public ImageView? Border = null;
        public bool Rounded = false;

        public void Apply(string src, Color tint = default)
        {
            if (tint == default)
                tint = Color.white;

            if (Background is not null) 
                Destroy(Background);

            ImageView img = Rounded ? gameObject.AddComponent(RoundImage) : gameObject.AddComponent<ImageView>();
            img.material = Utilities.ImageResources.NoGlowMat;
            img.rectTransform.SetParent(transform, false);
            img.sprite = GetSprite(src);
            img.color = tint;

            if (!Rounded)
                img.type = Image.Type.Simple;

            Background = img;

            lock (applyLock)
                Monitor.PulseAll(applyLock);
        }
        public void ApplyBorder(float size, string? src = null, Color tint = default)
        {
            if (tint == default)
                tint = Color.white;

            if (Background is null)
                lock (applyLock)
                    Monitor.Wait(applyLock);

            if (Border is not null)
                Destroy(Border);

            if (_borderTemplate is null)
            {
                Button button = Resources.FindObjectsOfTypeAll<Button>().FirstOrDefault(x => x.name == "ActionButton");
                Transform? borderTransform = button != null ? button.transform.Find("Border") : null;
                if (borderTransform is null)
                {
                    return;
                }

                _borderTemplate = borderTransform.gameObject;
            }

            RectTransform? rt = Instantiate(_borderTemplate, Background!.transform).transform as RectTransform;

            if (rt is null)
            {
                AccSaber.Plugin.Log.Info("transform is null");
                return;
            }

            rt.transform.SetParent(Background!.transform, false);

            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            rt.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            ImageView img = rt.GetComponent<ImageView>();
            img.material = Utilities.ImageResources.NoGlowMat;
            //img.rectTransform.sizeDelta = new Vector2(size + img.rectTransform.sizeDelta.x, size + img.rectTransform.sizeDelta.y);
            //img.sprite = src is null ? Utilities.ImageResources.WhitePixel : GetSprite(src);
            img.color = tint;

            if (!Rounded)
                img.type = Image.Type.Simple;

            Border = img;
        }

        private Sprite GetSprite(string src)
        {
            if (cachedSprites.TryGetValue(src, out Sprite? outp))
                return outp;
            byte[] file = Utilities.GetResource(Assembly.GetExecutingAssembly(), src);
            if (file.Length != 0)
            {
                Texture2D texture2D = new(0, 0, TextureFormat.RGBA32, false, false);
                if (texture2D.LoadImage(file))
                    outp = Utilities.LoadSpriteFromTexture(texture2D);
            }
            cachedSprites.Add(src, outp);
            return outp;
        }
    }
}
