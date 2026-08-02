using BeatSaberMarkupLanguage.TypeHandlers;
using UnityEngine;
using UnityEngine.UI;

namespace AccSaber.UI.BSML_Addons.TypeHandlers
{
    [ComponentHandler(typeof(ScrollRect))]
    internal class ScrollRectHandler : ScrollHandler<ScrollRect>
    {
        protected override void SetContentHeight(ScrollRect scrollComponent, float height)
        {
            scrollComponent.viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, height);
            scrollComponent.horizontalScrollbar.value = 0;
        }

        protected override void SetContentWidth(ScrollRect scrollComponent, float width)
        {
            scrollComponent.viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, width);
            scrollComponent.verticalScrollbar.value = 0;
        }

        protected override void SetViewportHeight(ScrollRect scrollComponent, float height)
        {
            scrollComponent.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, height);
        }

        protected override void SetViewportWidth(ScrollRect scrollComponent, float width)
        {
            scrollComponent.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, width);
        }
    }
}
