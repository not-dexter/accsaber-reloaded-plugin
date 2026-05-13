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
            { "bgColor", ["bg-color", "color"] }
        };

        public override void HandleType(BSMLParser.ComponentTypeWithData componentType, BSMLParserParams parserParams)
        {
#if NEW_VERSION
            CustomBackground bg = componentType.Component as CustomBackground;
#else
            CustomBackground bg = componentType.component as CustomBackground;
#endif
            Color c = default;

#if NEW_VERSION
            if (componentType.Data.TryGetValue("bgColor", out string color))
                ColorUtility.TryParseHtmlString(color, out c);
            if (componentType.Data.TryGetValue("bg", out string src))
                bg.Apply(src, c);
#else
            if (componentType.data.TryGetValue("bgColor", out string color))
                ColorUtility.TryParseHtmlString(color, out c);
            if (componentType.data.TryGetValue("bg", out string src))
                bg.Apply(src, c);
#endif
        }
    }
}
