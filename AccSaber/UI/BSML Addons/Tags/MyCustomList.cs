using AccsaberLeaderboard.UI.BSML_Addons.Components;
using AccsaberLeaderboard.UI.Components;
using BeatSaberMarkupLanguage.Tags;
using UnityEngine;
using UnityEngine.UI;

namespace AccsaberLeaderboard.UI.BSML_Addons.Tags
{
    public class MyCustomList : BSMLTag
    {
        public override string[] Aliases => [ "my-custom-list" ];
        public override bool AddChildren => false;

        public override GameObject CreateObject(Transform parent)
        {
            GameObject gameObject = new("MyCustomList");
            gameObject.transform.SetParent(parent, false);
            gameObject.AddComponent<VerticalLayoutGroup>().childForceExpandHeight = false;

            ContentSizeFitter csf = gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            gameObject.AddComponent<CustomBackground>();

            RectTransform transform = gameObject.transform as RectTransform;
            transform.anchorMin = Vector2.zero;
            transform.anchorMax = Vector2.one;
            transform.sizeDelta = Vector2.zero;

            gameObject.AddComponent<LayoutElement>();

            gameObject.AddComponent<MyCustomCellListTableData>();

            return gameObject;
        }
    }
}
