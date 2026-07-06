#define PRINT_DEBUG

using AccSaber.Consts;
using AccSaber.Models;
using AccSaber.Utils;
using AccSaber.Utils.Misc;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using HMUI;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace AccSaber.UI.MenuButton.Campaigns.ViewControllers
{
    internal class AccSaberCampaignMapViewController
    {
        public const float NODE_PADDING = 2f;

        [Inject] private readonly SerializationHandler serialHandler = null!;

        private bool parsed = false;
        private readonly List<CampaignMapNode> campaignMapNodes = [];
        private readonly List<GameObject> mapNodeArrows = [];

        [UIObject(nameof(ScrollContainer))]
        private readonly GameObject ScrollContainer = null!;

        [UIObject(nameof(NodeContainer))]
        private readonly GameObject NodeContainer = null!;


        [UIValue(nameof(NodeContainerPadding))]
        public const int NodeContainerPadding = 5;


        [UIAction("#post-parse")]
        private void PostParse()
        {
            if (!parsed)
                parsed = true;
        }

        public void SetCampaign(AccSaberCampaign campaign)
        {
            if (!parsed || campaign.Difficulties is null)
                return;

            foreach (CampaignMapNode node in campaignMapNodes)
                node.Dispose();

            foreach (GameObject arrow in mapNodeArrows)
                UnityEngine.Object.Destroy(arrow);

            campaignMapNodes.Clear();
            mapNodeArrows.Clear();

            int minHeight = int.MaxValue, maxHeight = int.MinValue, minWidth = int.MaxValue, maxWidth = int.MinValue;
            float minSize = float.MaxValue, maxSize = float.MinValue;
            float minHeightMaxSize = 0f, maxHeightMaxSize = 0f, minWidthMaxSize = 0f, maxWidthMaxSize = 0f;

            foreach (AccSaberCampaignMap map in campaign.Difficulties)
            {
                float size = map.Size * CampaignMapNode.SCALE_FACTOR;


                if (minHeight > map.PositionY)
                {
                    minHeight = map.PositionY;
                    minHeightMaxSize = size;
                }

                if (maxHeight < map.PositionY)
                {
                    maxHeight = map.PositionY;
                    maxHeightMaxSize = size;
                }

                if (minWidth > map.PositionX)
                {
                    minWidth = map.PositionX;
                    minWidthMaxSize = size;
                }

                if (maxWidth < map.PositionX)
                {
                    maxWidth = map.PositionX;
                    maxWidthMaxSize = size;
                }

                if (minSize > size)
                    minSize = size;

                if (maxSize < size)
                    maxSize = size;
            }

            LayoutElement scrollLayout = NodeContainer.GetComponent<LayoutElement>();

            float offsetSize = minSize + NODE_PADDING;
            float width = (maxWidth - minWidth) * offsetSize + minWidthMaxSize / 2f + maxWidthMaxSize / 2f + NodeContainerPadding * 2;
            float height = (maxHeight - minHeight) * offsetSize + minHeightMaxSize / 2f + maxHeightMaxSize / 2f + NodeContainerPadding * 2;

#if PRINT_DEBUG
            Plugin.Log.Info($"minWidth = {minWidth}, maxWidth = {maxWidth}, minWidthMaxSize = {minWidthMaxSize}, maxWidthMaxSize = {maxWidthMaxSize}");
            Plugin.Log.Info($"minHeight = {minHeight}, maxHeight = {maxHeight}, minHeightMaxSize = {minHeightMaxSize}, maxHeightMaxSize = {maxHeightMaxSize}");
#endif

            scrollLayout.preferredWidth = width;
            scrollLayout.preferredHeight = height;

            ScrollRect scrollableContainer = ScrollContainer.transform.parent.parent.GetComponent<ScrollRect>();
            scrollableContainer.content.sizeDelta = new(width, height);

#if PRINT_DEBUG
            Plugin.Log.Info("Width = " + width + ", Height = " + height);
#endif

            float yShift = 0;
            if (!Mathf.Approximately(minHeightMaxSize, maxHeightMaxSize))
            {
                float sign = minHeightMaxSize < maxHeightMaxSize ? -1f : 1f;
                yShift = sign * (Mathf.Abs(minHeightMaxSize - maxHeightMaxSize) / 4f);
            }

            float xOffset = 0f, yOffset = (maxHeight - minHeight) * offsetSize / 2f + yShift;

#if PRINT_DEBUG
            Plugin.Log.Info($"xOffset = {xOffset}, yOffset = {yOffset}, yShift = {yShift}, offsetSize = {offsetSize}");
#endif

            Dictionary<Guid, PositionData> knownPositions = [];
            Queue<(Guid prereq, PositionData to)> neededPositions = [];

            foreach (AccSaberCampaignMap map in campaign.Difficulties)
            {
                CampaignMapNode node = new(map, serialHandler.CachedDifficulties[map.MapDifficultyId].Hash, xOffset, yOffset, offsetSize);

                campaignMapNodes.Add(node);

                VersionUtils.Parse(ResourcePaths.ACC_SABER_CAMPAIGN_MAP_CELL, NodeContainer, node);

                PositionData current = new(node);
                knownPositions.Add(map.Id, current);

                foreach (Guid mapId in map.PrerequisiteMapIds)
                {
                    if (knownPositions.TryGetValue(mapId, out PositionData from) && CreateArrow(NodeContainer.transform, from, current, Color.grey) is GameObject go)
                    {
                        go.transform.SetAsFirstSibling();
                        mapNodeArrows.Add(go);
                    }
                    else
                        neededPositions.Enqueue((mapId, current));
                }
            }

            while (neededPositions.Count > 0)
            {
                var (prereq, to) = neededPositions.Dequeue();
                if (knownPositions.TryGetValue(prereq, out PositionData from) && CreateArrow(NodeContainer.transform, from, to, Color.grey) is GameObject go)
                {
                    go.transform.SetAsFirstSibling();
                    mapNodeArrows.Add(go);
                }
                else
                    Plugin.Log.Error("There is an invalid prereq!\n" + prereq + ", " + to);
            }
        }

        public static GameObject? CreateArrow(Transform parent, PositionData from, PositionData to, Color color, float shaftThickness = 1f, float headLength = 4f, float headWidth = 4f, string name = "UI Arrow")
        {
            if (!TryGetClippedArrowPoints(from, to, out Vector2 fromPos, out Vector2 toPos))
            {
                fromPos = from.Position;
                toPos = to.Position;
            }

            Vector2 direction = toPos - fromPos;
            float length = direction.magnitude;

            if (length <= 0.001f)
                return null;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            headLength = Mathf.Min(headLength, length);
            float shaftLength = Mathf.Max(0f, length - headLength);

            GameObject arrow = new(name, typeof(RectTransform));
            arrow.transform.SetParent(parent, false);

            arrow.AddComponent<LayoutElement>().ignoreLayout = true;

            RectTransform arrowRect = arrow.GetComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
            arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
            arrowRect.pivot = new Vector2(0f, 0.5f);
            arrowRect.anchoredPosition = fromPos;
            arrowRect.sizeDelta = new Vector2(length, headWidth);
            arrowRect.localRotation = Quaternion.Euler(0f, 0f, angle);

            // Shaft
            GameObject shaft = new("Shaft", typeof(RectTransform));
            shaft.transform.SetParent(arrow.transform, false);

            RectTransform shaftRect = shaft.GetComponent<RectTransform>();
            shaftRect.anchorMin = new Vector2(0f, 0.5f);
            shaftRect.anchorMax = new Vector2(0f, 0.5f);
            shaftRect.pivot = new Vector2(0f, 0.5f);
            shaftRect.anchoredPosition = Vector2.zero;
            shaftRect.sizeDelta = new Vector2(shaftLength, shaftThickness);

            ImageView shaftImage = shaft.AddComponent<ImageView>();
            shaftImage.sprite = Utilities.ImageResources.WhitePixel;
            shaftImage.material = Utilities.ImageResources.NoGlowMat;
            shaftImage.color = color;
            shaftImage.raycastTarget = false;

            // Arrow head
            GameObject head = new("Head", typeof(RectTransform));
            head.transform.SetParent(arrow.transform, false);

            RectTransform headRect = head.GetComponent<RectTransform>();
            headRect.anchorMin = new Vector2(0f, 0.5f);
            headRect.anchorMax = new Vector2(0f, 0.5f);
            headRect.pivot = new Vector2(0f, 0.5f);
            headRect.anchoredPosition = new Vector2(shaftLength, 0f);
            headRect.sizeDelta = new Vector2(headLength, headWidth);

            ImageView headImage = head.AddComponent<ImageView>();
            headImage.sprite = TriangleArrowHeadSprite;
            headImage.material = Utilities.ImageResources.NoGlowMat;
            headImage.type = Image.Type.Simple;
            headImage.color = color;
            headImage.raycastTarget = false;

            return arrow;
        }

        private static bool TryGetClippedArrowPoints(PositionData from, PositionData to, out Vector2 arrowStart, out Vector2 arrowEnd, float padding = 0f)
        {
            arrowStart = from.Position;
            arrowEnd = to.Position;

            Vector2 delta = to.Position - from.Position;

            if (delta.sqrMagnitude < 0.0001f)
                return false;

            Vector2 direction = delta.normalized;

            arrowStart = GetRectEdgePoint(from.Position, from.Size, direction, padding);
            arrowEnd = GetRectEdgePoint(to.Position, to.Size, -direction, padding);

            // If the objects overlap or are too close, there may be no usable arrow length.
            if (Vector2.Dot(arrowEnd - arrowStart, direction) <= 0.001f)
                return false;

            return true;
        }
        private static Vector2 GetRectEdgePoint(Vector2 rectCenter, Vector2 rectSize, Vector2 direction, float padding = 0f)
        {
            Vector2 halfSize = new(Mathf.Abs(rectSize.x) * 0.5f, Mathf.Abs(rectSize.y) * 0.5f);

            float distanceToVerticalEdge =
                Mathf.Abs(direction.x) > 0.0001f
                    ? halfSize.x / Mathf.Abs(direction.x)
                    : float.PositiveInfinity;

            float distanceToHorizontalEdge =
                Mathf.Abs(direction.y) > 0.0001f
                    ? halfSize.y / Mathf.Abs(direction.y)
                    : float.PositiveInfinity;

            float distanceToEdge = Mathf.Min(distanceToVerticalEdge, distanceToHorizontalEdge);

            return rectCenter + direction * (distanceToEdge + padding);
        }

        private static Sprite? _triangleArrowHeadSprite;

        public static Sprite TriangleArrowHeadSprite
        {
            get
            {
                if (_triangleArrowHeadSprite == null)
                    _triangleArrowHeadSprite = CreateTriangleArrowHeadSprite();

                return _triangleArrowHeadSprite;
            }
        }

        private static Sprite CreateTriangleArrowHeadSprite()
        {
            const int width = 64;
            const int height = 64;

            Texture2D texture = new(width, height, TextureFormat.RGBA32, false)
            {
                name = "Generated Triangle Arrow Head",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color clear = new(1f, 1f, 1f, 0f);
            Color white = Color.white;

            float midY = (height - 1) * 0.5f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Triangle points:
                    // left-top, left-bottom, right-middle
                    float t = x / (float)(width - 1);
                    float allowedHalfHeight = (1f - t) * midY;

                    bool insideTriangle = Mathf.Abs(y - midY) <= allowedHalfHeight;

                    texture.SetPixel(x, y, insideTriangle ? white : clear);
                }
            }

            texture.Apply();

            return Sprite.Create(texture, new(0f, 0f, width, height), new(0f, 0.5f), 100f);
        }

        public record struct PositionData(Vector2 Position, Vector2 Size)
        {
            internal PositionData(CampaignMapNode node) : this(new(node.NodeXPos, node.NodeYPos), new(node.NodeWidth, node.NodeHeight)) { }
        }
        internal class CampaignMapNode(AccSaberCampaignMap map, string mapHash, float xOffset, float yOffset, float offsetSize) : IDisposable
        {
            public const float SCALE_FACTOR = 0.2f;

            public readonly AccSaberCampaignMap Map = map;
            public readonly string Hash = mapHash;

            [UIObject("container")]
            private readonly GameObject Container = null!;

            [UIComponent("borderImage")]
            private readonly ImageView BorderImage = null!;

            [UIComponent("coverImage")]
            private readonly ImageView CoverImage = null!;

            [UIValue("NodeWidth")]
            public readonly float NodeWidth = map.Size * SCALE_FACTOR;

            [UIValue("NodeHeight")]
            public readonly float NodeHeight = map.Size * SCALE_FACTOR;

            [UIValue("NodeXPos")]
            public readonly float NodeXPos = map.PositionX * offsetSize + xOffset;

            [UIValue("NodeYPos")]
            public readonly float NodeYPos = -map.PositionY * offsetSize - yOffset;


            [UIAction("#post-parse")]
            private void PostParse()
            {
                BorderImage.sprite = Utilities.ImageResources.WhitePixel;

                CoverImage.transform.localScale *= 0.9f;
                _ = CoverImage.LoadCoverImage(Hash, Map.CoverUrl);

#if PRINT_DEBUG
                Plugin.Log.Info($"Pos = ({Map.PositionX}, {Map.PositionY}) Node Pos = ({NodeXPos}, {NodeYPos}), Width = {NodeWidth}, Height = {NodeHeight}");
#endif
            }

            [UIAction("OnClicked")]
            private void OnClicked()
            {

            }

            public void Dispose()
            {
                UnityEngine.Object.Destroy(Container);
            }
        }
    }
}
