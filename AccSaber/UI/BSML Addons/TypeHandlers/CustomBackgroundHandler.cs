using AccSaber.Consts;
using AccsaberLeaderboard.UI.Components;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.TypeHandlers;
using System.Collections.Generic;
using UnityEngine;

namespace AccsaberLeaderboard.UI.BSML_Addons.TypeHandlers
{
    [ComponentHandler(typeof(CustomBackground))]
    public class CustomBackgroundHandler : TypeHandler
    {
        public override Dictionary<string, string[]> Props => new()
        {
            { "bg", ["bg", "source", "src"] },
            { "bgColor", ["bg-color", "color"] },
            { "roundedImg", ["rounded"] },
            { "borderSize", ["border-size", "border"] },
            { "borderColor", ["border-color"] },
            { "borderSrc", ["border-src", "border-source", "border-bg"] }
        };

        public override void HandleType(BSMLParser.ComponentTypeWithData componentType, BSMLParserParams parserParams)
        {
#if NEW_VERSION
            Dictionary<string, string> componentData = componentType.Data;
            CustomBackground bg = (componentType.Component as CustomBackground)!;
#else
            Dictionary<string, string> componentData = componentType.data;
            CustomBackground bg = (componentType.component as CustomBackground)!;
#endif

            Color c = default;

            if (componentData.TryGetValue("bgColor", out string color))
                ColorUtility.TryParseHtmlString(color, out c);

            if (componentData.TryGetValue("roundedImg", out string rounded) && bool.TryParse(rounded, out bool roundeded))
                bg.Rounded = roundeded;

            if (componentData.TryGetValue("bg", out string src))
                bg.Apply(src, c);

            float borderSizeNum = -1;
            string? borderSrc = null;

            if (componentData.TryGetValue("borderSize", out string borderSize))
                float.TryParse(borderSize, out borderSizeNum);

            if (componentData.TryGetValue("borderColor", out color))
                ColorUtility.TryParseHtmlString(color, out c);

            componentData.TryGetValue("borderSrc", out borderSrc);

            if (borderSizeNum > 0)
                bg.ApplyBorder(borderSizeNum, borderSrc, c);
        }
    }
}
