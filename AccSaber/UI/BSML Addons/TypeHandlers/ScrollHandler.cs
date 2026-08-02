using AccSaber.Utils;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.TypeHandlers;
using System.Collections.Generic;
using UnityEngine;

namespace AccSaber.UI.BSML_Addons.TypeHandlers
{
    internal abstract class ScrollHandler<T> : TypeHandler where T : Component
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
            T scrollComponent = (T)componentType.Component();
            if (componentData.TryGetValue("viewportWidth", out string viewportWidth) && float.TryParse(viewportWidth, out float vw))
            {
                SetViewportWidth(scrollComponent, vw);
            }
            if (componentData.TryGetValue("viewportHeight", out string viewportHeight) && float.TryParse(viewportHeight, out float vh))
            {
                SetViewportHeight(scrollComponent, vh);
            }
            if (componentData.TryGetValue("contentWidth", out string contentWidth) && float.TryParse(contentWidth, out float cw))
            {
                SetContentWidth(scrollComponent, cw);
            }
            if (componentData.TryGetValue("contentHeight", out string contentHeight) && float.TryParse(contentHeight, out float ch))
            {
                SetContentHeight(scrollComponent, ch);
            }
        }

        protected abstract void SetViewportWidth(T scrollComponent, float width);
        protected abstract void SetViewportHeight(T scrollComponent, float height);
        protected abstract void SetContentWidth(T scrollComponent, float width);
        protected abstract void SetContentHeight(T scrollComponent, float height);
    }
}
