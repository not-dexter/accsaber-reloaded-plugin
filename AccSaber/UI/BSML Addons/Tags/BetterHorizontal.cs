using AccsaberLeaderboard.UI.Components;
using BeatSaberMarkupLanguage.Tags;
using UnityEngine;
using UnityEngine.UI;

namespace AccsaberLeaderboard.UI.BSML_Addons.Tags
{
    public class BetterHorizontal : BSMLTag
    {
        public override string[] Aliases => ["my-horizontal", "my-hori", "my-h"];

        public override GameObject CreateObject(Transform parent)
        {
            GameObject gameObject = new("BetterHorizontalLayoutGroup");

            gameObject.transform.SetParent(parent, false);
            gameObject.AddComponent<HorizontalLayoutGroup>();

            ContentSizeFitter csf = gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            gameObject.AddComponent<CustomBackground>();

            RectTransform rectTransform = gameObject.transform as RectTransform;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;

            gameObject.AddComponent<LayoutElement>();
            return gameObject;
        }
    }
}
