using AccSaber.Utils;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.TypeHandlers;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AccSaber.UI.BSML_Addons.TypeHandlers
{
    [ComponentHandler(typeof(ScrollRect))]
    internal class ScrollRectHandler : TypeHandler
    {
        public override Dictionary<string, string[]> Props => new()
        {
            { "viewportWidth", ["viewport-width"] },
            { "viewportHeight", ["viewport-height"] },
            { "contentWidth", ["content-width"] },
            { "contentHeight", ["content-height"] },
        };

        public override void HandleType(BSMLParser.ComponentTypeWithData componentType, BSMLParserParams parserParams)
        {
            Dictionary<string, string> componentData = componentType.Data();
            ScrollRect scrollRect = (componentType.Component() as ScrollRect)!;

            if (componentData.TryGetValue("viewportWidth", out string viewportWidth) && float.TryParse(viewportWidth, out float vw))
                scrollRect.viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, vw);

            if (componentData.TryGetValue("viewportHeight", out string viewportHeight) && float.TryParse(viewportHeight, out float vh))
                scrollRect.viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, vh);

            if (componentData.TryGetValue("contentWidth", out string contentWidth) && float.TryParse(contentWidth, out float cw))
                scrollRect.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, cw);

            if (componentData.TryGetValue("contentHeight", out string contentHeight) && float.TryParse(contentHeight, out float ch))
                scrollRect.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, ch);
        }
    }
}
