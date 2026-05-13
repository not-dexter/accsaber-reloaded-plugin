using AccsaberLeaderboard.UI.Components;
using BeatSaberMarkupLanguage.Tags;
using UnityEngine;
using UnityEngine.UI;

namespace AccsaberLeaderboard.UI.BSML_Addons.Tags
{
    public class BetterVertical : BSMLTag
    {
        public override string[] Aliases => [ "my-vertical", "my-vert", "my-v" ];

        public override GameObject CreateObject(Transform parent)
        {
            GameObject gameObject = new("BetterVerticalLayoutGroup");

            gameObject.transform.SetParent(parent, false);
            gameObject.AddComponent<VerticalLayoutGroup>();

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
