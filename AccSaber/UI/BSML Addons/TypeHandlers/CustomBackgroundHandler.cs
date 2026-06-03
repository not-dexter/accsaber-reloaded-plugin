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
            { "border", ["border"] },
            { "borderColor", ["border-color"] },
            { "borderSrc", ["border-src", "border-source", "border-bg"] },
            { "underline", ["underline"] },
            { "underlineSize", ["underline-size"] },
            { "underlineColor", ["underline-color"] },
            { "underlineSrc", ["underline-src", "underline-source", "underline-bg"] }
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

            c = default;

            if (componentData.TryGetValue("borderColor", out color))
                ColorUtility.TryParseHtmlString(color, out c);

            componentData.TryGetValue("borderSrc", out src);

            if (componentData.TryGetValue("border", out string border) && bool.TryParse(border, out bool allowBorder) && allowBorder)
                bg.ApplyBorder(src, c);

            if (componentData.TryGetValue("underline", out string underline) && bool.TryParse(underline, out bool allowUnderline) && allowUnderline)
            {
                c = default;

                if (componentData.TryGetValue("underlineColor", out color))
                    ColorUtility.TryParseHtmlString(color, out c);

                componentData.TryGetValue("underlineSrc", out src);

                if (componentData.TryGetValue("underlineSize", out string size) && float.TryParse(size, out float sizeNum))
                    bg.ApplyUnderline(sizeNum, src, c);
            }
        }
    }
}
