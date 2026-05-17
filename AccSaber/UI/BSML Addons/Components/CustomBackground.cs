using AccSaber.Utils;
using BeatSaberMarkupLanguage;
using HMUI;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace AccsaberLeaderboard.UI.Components
{
    public class CustomBackground : MonoBehaviour
    {
        private static readonly Dictionary<string, Sprite> cachedSprites = [];

        public ImageView? background = null;

        public void Apply(string src, Color tint = default)
        {
            if (tint == default)
                tint = Color.white;

            if (background is not null) 
                Destroy(background);

            ImageView img = gameObject.AddComponent<ImageView>();
            img.material = Utilities.ImageResources.NoGlowMat;
            img.rectTransform.SetParent(transform, false);
            img.sprite = GetSprite(src);
            img.type = Image.Type.Simple;
            img.color = tint;

            background = img;
        }

        private Sprite GetSprite(string src)
        {
            if (cachedSprites.TryGetValue(src, out Sprite outp))
                return outp;
            outp = VersionUtils.LoadSpriteFromAssemblyAsync(src).GetAwaiter().GetResult();
            cachedSprites.Add(src, outp);
            return outp;
        }
    }
}
