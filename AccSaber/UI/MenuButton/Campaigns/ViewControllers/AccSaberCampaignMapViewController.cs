//#define PRINT_DEBUG

using AccSaber.Consts;
using AccSaber.Managers;
using AccSaber.Models;
using AccSaber.Utils;
using AccSaber.Utils.Misc;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using HMUI;
using SongCore;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using static AccSaber.UI.MenuButton.Campaigns.ViewControllers.NodeShapeTextures;

namespace AccSaber.UI.MenuButton.Campaigns.ViewControllers
{
    internal class AccSaberCampaignMapViewController
    {
        public const float NODE_PADDING = 2f;
        public const float SCALE_FACTOR = 0.2f;

        [Inject] private readonly SerializationHandler serialHandler = null!;
        [Inject] private readonly LevelUtils levelUtils = null!;
        [Inject] private readonly AccSaberStore store = null!;
        [Inject] private readonly AccSaberCampaignFlow accCampaignFlow = null!;
        [Inject] private readonly AccSaberCampaignViewController acvc = null!;


        private bool parsed = false;
        private readonly List<CampaignMapNode> campaignMapNodes = [];
        private readonly List<CampaignMapBarrier> campaignMapBarriers = [];
        private readonly List<(Guid fromNode, Guid toNode, GameObject go)> mapNodeArrows = [];

        private CampaignProgress campaignProgress;

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

        public async void SetCampaign(AccSaberCampaign campaign)
        {
            if (!parsed || campaign.Difficulties is null)
                return;

            foreach (IDisposable node in campaignMapNodes.Cast<IDisposable>().Concat(campaignMapBarriers))
                node.Dispose();

            foreach (var (_, _, go) in mapNodeArrows)
                UnityEngine.Object.Destroy(go);

            campaignMapNodes.Clear();
            campaignMapBarriers.Clear();
            mapNodeArrows.Clear();

            campaignProgress = await store.GetCampaignProgress(campaign.Id);

            int minHeight = int.MaxValue, maxHeight = int.MinValue, minWidth = int.MaxValue, maxWidth = int.MinValue;
            float minSize = float.MaxValue, maxSize = float.MinValue;
            float minHeightMaxSize = 0f, maxHeightMaxSize = 0f, minWidthMaxSize = 0f, maxWidthMaxSize = 0f;

            foreach (AccSaberCampaignPositionable map in campaign.Difficulties)
            {
                float size = map.Size * SCALE_FACTOR;


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

            float offsetSize = minSize + NODE_PADDING;
            float width = (maxWidth - minWidth) * offsetSize + minWidthMaxSize / 2f + maxWidthMaxSize / 2f + NodeContainerPadding * 2;
            float height = (maxHeight - minHeight) * offsetSize + minHeightMaxSize / 2f + maxHeightMaxSize / 2f + NodeContainerPadding * 2;

#if PRINT_DEBUG
            Plugin.Log.Info($"minWidth = {minWidth}, maxWidth = {maxWidth}, minWidthMaxSize = {minWidthMaxSize}, maxWidthMaxSize = {maxWidthMaxSize}");
            Plugin.Log.Info($"minHeight = {minHeight}, maxHeight = {maxHeight}, minHeightMaxSize = {minHeightMaxSize}, maxHeightMaxSize = {maxHeightMaxSize}");
#endif

            Utils.Safety.MainThreadDispatcher.AssertOnMainThread();

            LayoutElement scrollLayout = NodeContainer.GetComponent<LayoutElement>();

            scrollLayout.preferredWidth = width;
            scrollLayout.preferredHeight = height;

            ScrollRect scrollableContainer = ScrollContainer.transform.parent.parent.GetComponent<ScrollRect>();
            scrollableContainer.content.sizeDelta = new(width, height);
            scrollableContainer.horizontalScrollbar.value = 0;
            scrollableContainer.verticalScrollbar.value = 0;

#if PRINT_DEBUG
            Plugin.Log.Info("Width = " + width + ", Height = " + height);
#endif

            float yShift = 0;
            if (!Mathf.Approximately(minHeightMaxSize, maxHeightMaxSize))
            {
                float sign = minHeightMaxSize < maxHeightMaxSize ? -1f : 1f;
                yShift = sign * (Mathf.Abs(minHeightMaxSize - maxHeightMaxSize) / 4f);
            }

            float xOffset = -(minWidth + maxWidth) / 2f * offsetSize, yOffset = -(minHeight + maxHeight) / 2f * offsetSize + yShift;

#if PRINT_DEBUG
            Plugin.Log.Info($"xOffset = {xOffset}, yOffset = {yOffset}, yShift = {yShift}, offsetSize = {offsetSize}");
#endif

            Dictionary<Guid, PositionData> knownPositions = [];
            Queue<(Guid prereq, Guid toNode)> neededPositions = [];

            void HandleArrows(AccSaberCampaignPositionablePrereq node, PositionData current)
            {
                knownPositions.Add(node.Id, current);

                foreach (Guid id in node.PrerequisiteIds)
                {
                    if (knownPositions.TryGetValue(id, out PositionData from) && CreateArrow(NodeContainer.transform, from, current, campaignProgress.CompletedItems.Contains(id) ? Color.white : Color.grey) is GameObject go)
                    {
                        go.transform.SetAsFirstSibling();
                        mapNodeArrows.Add((id, node.Id, go));
                    }
                    else
                        neededPositions.Enqueue((id, node.Id));
                }
            }

            if (campaign.Barriers is not null)
                foreach (AccSaberCampaignBarrier barrier in campaign.Barriers)
                {
                    CampaignMapBarrier barrierNode = new(
                        barrier: barrier,
                        parent: NodeContainer.transform,
                        progress: campaignProgress.PlayerValues[barrier.Id],
                        xOffset: xOffset,
                        yOffset: yOffset,
                        offsetSize: offsetSize
                    );

                    campaignMapBarriers.Add(barrierNode);

                    HandleArrows(barrier, new(barrierNode));
                }

            foreach (AccSaberCampaignMap map in campaign.Difficulties)
            {
                CampaignMapNode node = new(
                    map: map,
                    progress: campaignProgress.PlayerValues[map.Id],
                    mapHash: serialHandler.CachedDifficulties[map.MapDifficultyId].Hash,
                    xOffset: xOffset,
                    yOffset: yOffset,
                    offsetSize: offsetSize,
                    flow: accCampaignFlow,
                    campaignViewController: acvc,
                    levelUtils: levelUtils,
                    serialUtils: serialHandler
                    );

                campaignMapNodes.Add(node);

                VersionUtils.Parse(ResourcePaths.ACC_SABER_CAMPAIGN_MAP_CELL, NodeContainer, node);

                HandleArrows(map, new(node));
            }

            while (neededPositions.Count > 0)
            {
                var (prereq, toNode) = neededPositions.Dequeue();

                if (knownPositions.TryGetValue(prereq, out PositionData from) &&
                    knownPositions.TryGetValue(toNode, out PositionData to) &&
                    CreateArrow(NodeContainer.transform, from, to,
                        campaignProgress.CompletedItems.Contains(prereq) ? Color.white : Color.grey)
                        is GameObject go)
                {
                    go.transform.SetAsFirstSibling();
                    mapNodeArrows.Add((prereq, toNode, go));
                }
                else
                    Plugin.Log.Error("There is an invalid prereq!\n" + prereq + ", " + toNode);
            }

            Dictionary<Guid, CampaignMapBarrier> barrierNodeIds = [with(campaignMapBarriers.Select(barrier => new KeyValuePair<Guid, CampaignMapBarrier>(barrier.Barrier.Id, barrier)))];
            Dictionary<Guid, List<Vector2>> barrierArrowDirections = [];

            foreach (var (fromNode, toNode, go) in mapNodeArrows)
            {
                bool fromBarrierExists = barrierNodeIds.ContainsKey(fromNode);
                bool toBarrierExists = barrierNodeIds.ContainsKey(toNode);

                if (!fromBarrierExists && !toBarrierExists)
                    continue;

                if (!knownPositions.TryGetValue(fromNode, out PositionData fromPositionData))
                    continue;

                if (!knownPositions.TryGetValue(toNode, out PositionData toPositionData))
                    continue;

                Vector2 arrowDirection = toPositionData.Position - fromPositionData.Position;

                if (arrowDirection.sqrMagnitude < 0.0001f)
                    continue;

                arrowDirection.Normalize();

                if (fromBarrierExists)
                {
                    if (!barrierArrowDirections.TryGetValue(fromNode, out List<Vector2> directions))
                    {
                        directions = [];
                        barrierArrowDirections[fromNode] = directions;
                    }

                    directions.Add(arrowDirection);
                }

                if (toBarrierExists)
                {
                    if (!barrierArrowDirections.TryGetValue(toNode, out List<Vector2> directions))
                    {
                        directions = [];
                        barrierArrowDirections[toNode] = directions;
                    }

                    directions.Add(arrowDirection);
                }
            }

            Dictionary<Guid, Quaternion> finalBarrierRotations = [];

            foreach (var (barrierNodeId, directions) in barrierArrowDirections)
            {
                if (!barrierNodeIds.TryGetValue(barrierNodeId, out CampaignMapBarrier barrier))
                    continue;

                if (!TryGetPerpendicularBarrierRotation(directions, out Quaternion desiredRotation))
                    continue;

                Quaternion finalRotation = desiredRotation;

                barrier.Rotation = finalRotation;
                finalBarrierRotations[barrierNodeId] = finalRotation;
            }

            foreach (var (fromNode, toNode, go) in mapNodeArrows)
            {
                if (!knownPositions.TryGetValue(fromNode, out PositionData fromPositionData))
                    continue;

                if (!knownPositions.TryGetValue(toNode, out PositionData toPositionData))
                    continue;

                Quaternion fromRotation = finalBarrierRotations.TryGetValue(fromNode, out Quaternion foundFromRotation)
                    ? foundFromRotation
                    : Quaternion.identity;

                Quaternion toRotation = finalBarrierRotations.TryGetValue(toNode, out Quaternion foundToRotation)
                    ? foundToRotation
                    : Quaternion.identity;

                if (TryGetClippedArrowPoints(
                        fromPositionData,
                        fromRotation,
                        toPositionData,
                        toRotation,
                        out Vector2 newStart,
                        out Vector2 newEnd,
                        padding: 0f))
                {
                    UpdateExistingArrow(go, newStart, newEnd);
                }
            }
        }

        private static bool TryGetPerpendicularBarrierRotation(List<Vector2> arrowDirections, out Quaternion rotation)
        {
            rotation = Quaternion.identity;

            if (arrowDirections is null || arrowDirections.Count == 0)
                return false;

            Vector2 averageArrowDirection = Vector2.zero;

            foreach (Vector2 direction in arrowDirections)
            {
                if (direction.sqrMagnitude < 0.0001f)
                    continue;

                averageArrowDirection += direction.normalized;
            }

            // Normal case:
            // Use the average directed arrow direction.
            //
            // Example:
            // incoming at -135 and outgoing at -45 average to -90,
            // so the barrier becomes perpendicular, i.e. vertical.
            if (averageArrowDirection.sqrMagnitude >= 0.0001f)
            {
                averageArrowDirection.Normalize();

                Vector2 barrierAxis = GetPerpendicular(averageArrowDirection);

                rotation = RotationThatPointsUpAlong(barrierAxis);
                return true;
            }

            // Fallback case:
            // If directions cancel each other out, use an axis-based average.
            //
            // This handles cases like arrows going exactly opposite directions.
            if (TryGetAverageAxisDirection(arrowDirections, out Vector2 averageAxisDirection))
            {
                Vector2 barrierAxis = GetPerpendicular(averageAxisDirection);

                rotation = RotationThatPointsUpAlong(barrierAxis);
                return true;
            }

            return false;
        }
        private static Vector2 GetPerpendicular(Vector2 direction)
        {
            // Either perpendicular is fine for a rectangle/barrier axis.
            // This returns direction rotated 90 degrees counterclockwise.
            return new Vector2(-direction.y, direction.x).normalized;
        }

        private static Quaternion RotationThatPointsUpAlong(Vector2 upAxis)
        {
            if (upAxis.sqrMagnitude < 0.0001f)
                return Quaternion.identity;

            upAxis.Normalize();

            // Produces a Z rotation where the object's local Vector3.up points along upAxis.
            float z = Mathf.Atan2(-upAxis.x, upAxis.y) * Mathf.Rad2Deg;

            return Quaternion.Euler(0f, 0f, z);
        }

        private static bool TryGetAverageAxisDirection(List<Vector2> directions, out Vector2 averageAxis)
        {
            averageAxis = Vector2.zero;

            float sumX = 0f;
            float sumY = 0f;

            foreach (Vector2 direction in directions)
            {
                if (direction.sqrMagnitude < 0.0001f)
                    continue;

                Vector2 normalized = direction.normalized;

                float angle = Mathf.Atan2(normalized.y, normalized.x);

                // Double-angle trick for averaging line axes instead of directed vectors.
                // This treats angle A and angle A + 180 as equivalent.
                sumX += Mathf.Cos(2f * angle);
                sumY += Mathf.Sin(2f * angle);
            }

            if ((sumX * sumX) + (sumY * sumY) < 0.0001f)
                return false;

            float averageAngle = 0.5f * Mathf.Atan2(sumY, sumX);

            averageAxis = new Vector2(
                Mathf.Cos(averageAngle),
                Mathf.Sin(averageAngle)
            );

            return true;
        }
        private static void UpdateExistingArrow(GameObject arrow, Vector2 from, Vector2 to)
        {
            RectTransform arrowRect = arrow.GetComponent<RectTransform>();

            Vector2 direction = to - from;
            float length = direction.magnitude;

            if (length <= 0.001f)
            {
                arrow.SetActive(false);
                return;
            }

            arrow.SetActive(true);

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            arrowRect.anchoredPosition = from;
            arrowRect.localRotation = Quaternion.Euler(0f, 0f, angle);

            Vector2 arrowSize = arrowRect.sizeDelta;
            arrowSize.x = length;
            arrowRect.sizeDelta = arrowSize;

            RectTransform shaftRect = (arrow.transform.Find("Shaft") as RectTransform)!;
            RectTransform headRect = (arrow.transform.Find("Head") as RectTransform)!;

            if (headRect is null || shaftRect is null)
                return;

            float headLength = headRect.sizeDelta.x;
            float shaftLength = Mathf.Max(0f, length - headLength);

            Vector2 shaftSize = shaftRect.sizeDelta;
            shaftSize.x = shaftLength;
            shaftRect.sizeDelta = shaftSize;

            headRect.anchoredPosition = new Vector2(shaftLength, 0f);
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
        private static bool TryGetClippedArrowPoints(PositionData from, Quaternion fromRotation, PositionData to, Quaternion toRotation, out Vector2 arrowStart, out Vector2 arrowEnd, float padding = 0f)
        {
            arrowStart = from.Position;
            arrowEnd = to.Position;

            Vector2 delta = to.Position - from.Position;

            if (delta.sqrMagnitude < 0.0001f)
                return false;

            Vector2 direction = delta.normalized;

            arrowStart = GetRotatedRectEdgePoint(from.Position, from.Size, fromRotation, direction, padding);

            arrowEnd = GetRotatedRectEdgePoint(to.Position, to.Size, toRotation, -direction, padding);

            if (Vector2.Dot(arrowEnd - arrowStart, direction) <= 0.001f)
                return false;

            return true;
        }

        private static Vector2 GetRotatedRectEdgePoint(Vector2 rectCenter, Vector2 rectSize, Quaternion rectRotation, Vector2 worldDirection, float padding = 0f)
        {
            Vector2 halfSize = new(Mathf.Abs(rectSize.x) * 0.5f, Mathf.Abs(rectSize.y) * 0.5f);

            Vector3 localDirection3 = Quaternion.Inverse(rectRotation) * new Vector3(worldDirection.x, worldDirection.y, 0f);

            Vector2 localDirection = new(localDirection3.x, localDirection3.y);

            if (localDirection.sqrMagnitude < 0.0001f)
                return rectCenter;

            localDirection.Normalize();

            float distanceToVerticalEdge =
                Mathf.Abs(localDirection.x) > 0.0001f
                    ? halfSize.x / Mathf.Abs(localDirection.x)
                    : float.PositiveInfinity;

            float distanceToHorizontalEdge =
                Mathf.Abs(localDirection.y) > 0.0001f
                    ? halfSize.y / Mathf.Abs(localDirection.y)
                    : float.PositiveInfinity;

            float distanceToEdge = Mathf.Min(distanceToVerticalEdge, distanceToHorizontalEdge);

            return rectCenter + worldDirection.normalized * (distanceToEdge + padding);
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
            internal PositionData(CampaignMapBarrier node) : this(node.Position, node.SizeDelta) { }
        }
        internal class CampaignMapNode(
            AccSaberCampaignMap map,
            CampaignProgress.CampaignProgressValue progress,
            string mapHash,
            float xOffset,
            float yOffset,
            float offsetSize, 
            AccSaberCampaignFlow flow,
            AccSaberCampaignViewController campaignViewController,
            LevelUtils levelUtils,
            SerializationHandler serialUtils
            ) : IDisposable
        {
            public readonly AccSaberCampaignMap Map = map;
            public readonly string Hash = mapHash;
            public readonly CampaignProgress.CampaignProgressValue Progress = progress;

            private readonly AccSaberCampaignFlow campaignFlow = flow;
            private readonly AccSaberCampaignViewController campaignController = campaignViewController;
            private readonly LevelUtils levelUtils = levelUtils;
            private readonly SerializationHandler serialUtils = serialUtils;

            [UIObject("container")]
            private readonly GameObject Container = null!;

            [UIComponent("borderImage")]
            private readonly ImageView BorderImage = null!;

            [UIComponent("coverImage")]
            private readonly ClickableImage CoverImage = null!;

            [UIComponent("completionImage")]
            private readonly ImageView CompletionImage = null!;


            [UIValue("NodeWidth")]
            public readonly float NodeWidth = map.Size * SCALE_FACTOR;

            [UIValue("NodeHeight")]
            public readonly float NodeHeight = map.Size * SCALE_FACTOR;

            [UIValue("NodeXPos")]
            public readonly float NodeXPos = map.PositionX * offsetSize + xOffset;

            [UIValue("NodeYPos")]
            public readonly float NodeYPos = -map.PositionY * offsetSize - yOffset;


            [UIValue("CheckmarkSrc")]
            private const string CheckmarkSrc = ResourcePaths.CHECKMARK;

            [UIValue("IsComplete")]
            private readonly bool IsComplete = progress.Completion == CampaignProgress.CompletionStatus.Complete;


            [UIAction("#post-parse")]
            private void PostParse()
            {
                NodeShape shape = Map.BorderShape switch
                {
                    "square" => NodeShape.Square,
                    "diamond" => NodeShape.Diamond,
                    "circle" => NodeShape.Circle,
                    _ => NodeShape.Hexagon
                };

                BorderImage.sprite = GetBorderSprite(shape);
                BorderImage.color = Map.BorderColor?.Color() ?? Color.white;

                CoverImage.DefaultColor = Progress.Completion == CampaignProgress.CompletionStatus.Incomplete ? new(0.25f, 0.25f, 0.25f) : Color.white;

                CoverImage.transform.localScale *= 0.9f;
                _ = CoverImage.LoadCoverImageWithMask(Hash, Map.CoverUrl, sprite => CreateMaskedCoverSprite(sprite, shape)!);

#if PRINT_DEBUG
                Plugin.Log.Info($"Pos = ({Map.PositionX}, {Map.PositionY}) Node Pos = ({NodeXPos}, {NodeYPos}), Width = {NodeWidth}, Height = {NodeHeight}");
#endif
                if (Progress.Completion == CampaignProgress.CompletionStatus.Complete)
                {
                    RectTransform transform = (CompletionImage.transform as RectTransform)!;

                    transform.sizeDelta = new(NodeWidth / 4f, NodeHeight / 4f);
                }
            }

            [UIAction("OnClicked")]
            private async void OnClicked()
            {
#if NEW_VERSION
                BeatmapLevel? level = Loader.GetLevelByHash(Hash);
#else
                IBeatmapLevel? level = (await Loader.BeatmapLevelsModelSO.GetBeatmapLevelAsync(LevelUtils.header + Hash.ToUpper(), CancellationToken.None)).beatmapLevel;
#endif

                if (level is null)
                {
                    Plugin.Log.Warn($"Cannot find level by hash \"{Hash}\", downloading...");
                    level = await levelUtils.DownloadSong(serialUtils.CachedMaps[Hash]);

                    if (level is null)
                    {
                        Plugin.Log.Critical("Level cannot be downloaded!");
                        return;
                    }
                }

#if NEW_VERSION
                IEnumerable<BeatmapKey> keys = level.GetBeatmapKeys();

                BeatmapCharacteristicSO standard = level.GetCharacteristics().FirstOrDefault(c => c.serializedName == "Standard");

                BeatmapKey key = new(level.levelID, standard, EnumUtils.ReloadedDiffToDiff(MiscUtils.ParseEnum<ReloadedDifficulty>(Map.Difficulty)));
                
                campaignFlow.ShowLeaderboard(key);

                campaignController.SetMission(Map, key, level, Progress);
#else
                BeatmapDifficulty mapDiff = EnumUtils.ReloadedDiffToDiff(MiscUtils.ParseEnum<ReloadedDifficulty>(Map.Difficulty));
                IDifficultyBeatmapSet diffSet = level.beatmapLevelData.difficultyBeatmapSets.First(set => set.beatmapCharacteristic.serializedName.Equals("Standard", StringComparison.OrdinalIgnoreCase));
                IDifficultyBeatmap diff = diffSet.difficultyBeatmaps.First(difficulty => difficulty.difficulty == mapDiff);

                campaignFlow.ShowLeaderboard(diff);

                campaignController.SetMission(Map, diff, Completed);
#endif
            }

            public void Dispose()
            {
                UnityEngine.Object.Destroy(Container);
            }
        }

        internal class CampaignMapBarrier : IDisposable
        {
            public const float WIDTH = 1f;

            public readonly AccSaberCampaignBarrier Barrier;
            public readonly CampaignProgress.CampaignProgressValue Progress;
            private readonly Transform Parent;

            private readonly GameObject obj;

            public Quaternion Rotation { get => obj.transform.rotation; set => obj.transform.rotation = value; }
            public Vector2 SizeDelta { get; private set; }
            public Vector2 Position => obj.transform.GetComponent<RectTransform>().anchoredPosition;


            public CampaignMapBarrier(AccSaberCampaignBarrier barrier, Transform parent, CampaignProgress.CampaignProgressValue progress, float xOffset, float yOffset, float offsetSize)
            {
                Barrier = barrier;
                Progress = progress;
                Parent = parent;

                SizeDelta = new(WIDTH, Barrier.Size * SCALE_FACTOR);

                obj = new("AccSaberCampaignBarrier");
                obj.transform.SetParent(parent, false);

                RectTransform transform = obj.AddComponent<RectTransform>();
                transform.anchorMin = Vector2.zero;
                transform.anchorMax = Vector2.one;
                transform.anchoredPosition = new(Barrier.PositionX * offsetSize + xOffset, -Barrier.PositionY * offsetSize - yOffset); ;
                transform.rotation = Quaternion.Euler(0f, 0f, 0f);

                LayoutElement layout = obj.AddComponent<LayoutElement>();
                layout.ignoreLayout = true;
                layout.preferredWidth = SizeDelta.x;
                layout.preferredHeight = SizeDelta.y;

                ContentSizeFitter sizeFitter = obj.AddComponent<ContentSizeFitter>();
                sizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                ImageView image = obj.AddComponent<ImageView>();
                image.sprite = Utilities.ImageResources.WhitePixel;
                image.material = Utilities.ImageResources.NoGlowMat;
                image.type = Image.Type.Simple;
                image.color = barrier.BorderColor?.Color() ?? Color.red;

#if PRINT_DEBUG
                Plugin.Log.Info($"Barrier: Pos = ({barrier.PositionX}, {barrier.PositionY}) Node Pos = ({Position.x}, {Position.y}), Width = {SizeDelta.x}, Height = {SizeDelta.y}");
#endif
            }

            public void Dispose()
            {
                UnityEngine.Object.Destroy(obj);
            }
        }
    }

    public static class NodeShapeTextures
    {
        private static readonly Dictionary<string, Sprite> _borderSpriteCache = [];
        private static readonly Dictionary<string, Sprite> _maskedCoverSpriteCache = [];

        public static Sprite GetBorderSprite(NodeShape shape, int size = 256, int borderPixels = 10)
        {
            string key = $"{shape}_{size}_{borderPixels}";

            if (_borderSpriteCache.TryGetValue(key, out Sprite cached))
                return cached;

            Texture2D texture = CreateBorderTexture(shape, size, borderPixels);
            Sprite sprite = CreateSprite(texture);

            _borderSpriteCache[key] = sprite;
            return sprite;
        }

        public static Sprite? CreateMaskedCoverSprite(Sprite sourceSprite, NodeShape shape, int size = 256)
        {
            if (sourceSprite is null)
                return null;

            Texture2D sourceTexture = sourceSprite.texture;

            string key = $"{sourceTexture.GetInstanceID()}_{sourceSprite.GetInstanceID()}_{shape}_{size}";

            if (_maskedCoverSpriteCache.TryGetValue(key, out Sprite cached))
                return cached;

            Texture2D texture = CreateMaskedCoverTexture(sourceSprite, shape, size);
            Sprite sprite = CreateSprite(texture);

            _maskedCoverSpriteCache[key] = sprite;
            return sprite;
        }

        private static Texture2D CreateBorderTexture(NodeShape shape, int size, int borderPixels)
        {
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color32[] pixels = new Color32[size * size];

            float innerScale = 1f - borderPixels * 2f / size;
            innerScale = Mathf.Clamp01(innerScale);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float outerCoverage = GetCoverage(x, y, size, shape, 1f);
                    float innerCoverage = GetCoverage(x, y, size, shape, innerScale);

                    float alpha = Mathf.Clamp01(outerCoverage - innerCoverage);

                    byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            return texture;
        }

        private static Texture2D CreateMaskedCoverTexture(Sprite sourceSprite, NodeShape shape, int size)
        {
            Texture2D sourceTexture = MakeReadableCopy(sourceSprite.texture);

            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color32[] pixels = new Color32[size * size];

            Rect sourceRect = sourceSprite.rect;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float v = (y + 0.5f) / size;

                    float coverage = GetCoverage(x, y, size, shape, 1f);

                    Color sourceColor = SampleSprite(sourceTexture, sourceRect, u, v);
                    sourceColor.a *= coverage;

                    pixels[y * size + x] = sourceColor;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            return texture;
        }

        private static Color SampleSprite(Texture2D texture, Rect spriteRect, float u, float v)
        {
            float textureU = (spriteRect.x + u * spriteRect.width) / texture.width;
            float textureV = (spriteRect.y + v * spriteRect.height) / texture.height;

            return texture.GetPixelBilinear(textureU, textureV);
        }

        private static Sprite CreateSprite(Texture2D texture)
        {
            return Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                texture.width
            );
        }

        private static float GetCoverage(int pixelX, int pixelY, int size, NodeShape shape, float scale)
        {
            if (scale <= 0f)
                return 0f;

            // Supersampling gives nicer anti-aliased edges.
            const int samples = 4;
            int insideCount = 0;
            int totalCount = samples * samples;

            for (int sy = 0; sy < samples; sy++)
            {
                for (int sx = 0; sx < samples; sx++)
                {
                    float u = (pixelX + (sx + 0.5f) / samples) / size;
                    float v = (pixelY + (sy + 0.5f) / samples) / size;

                    float px = u * 2f - 1f;
                    float py = v * 2f - 1f;

                    px /= scale;
                    py /= scale;

                    if (IsInsideShape(px, py, shape))
                        insideCount++;
                }
            }

            return insideCount / (float)totalCount;
        }

        private static bool IsInsideShape(float x, float y, NodeShape shape)
        {
            switch (shape)
            {
                case NodeShape.Square:
                    return Mathf.Abs(x) <= 1f && Mathf.Abs(y) <= 1f;

                case NodeShape.Circle:
                    return x * x + y * y <= 1f;

                case NodeShape.Diamond:
                    return Mathf.Abs(x) + Mathf.Abs(y) <= 1f;

                case NodeShape.Hexagon:
                    {
                        // Point-left/right hexagon.
                        // Swap x and y if you want the hexagon rotated 90 degrees.
                        float ax = Mathf.Abs(x);
                        float ay = Mathf.Abs(y);

                        return ax <= 1f &&
                               ay <= 0.8660254f &&
                               0.8660254f * ax + 0.5f * ay <= 0.8660254f;
                    }

                default:
                    return false;
            }
        }

        public static Texture2D MakeReadableCopy(Texture2D source)
        {
            RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);

            Graphics.Blit(source, temporary);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = temporary;

            Texture2D readable = new(source.width, source.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readable.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);

            readable.wrapMode = TextureWrapMode.Clamp;
            readable.filterMode = source.filterMode;

            return readable;
        }

        public enum NodeShape
        {
            Square,
            Circle,
            Diamond,
            Hexagon
        }
    }
}
