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
using IPA.Utilities.Async;
using SongCore;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using static AccSaber.UI.MenuButton.Campaigns.ViewControllers.NodeShapeTextures;
using System.Runtime.CompilerServices;

#if !NEW_VERSION
using System.Threading;
#endif

namespace AccSaber.UI.MenuButton.Campaigns.ViewControllers
{
    internal class AccSaberCampaignMapViewController
    {
        public const float NODE_PADDING = 1f;

        [Inject] private readonly SerializationHandler serialHandler = null!;
        [Inject] private readonly LevelUtils levelUtils = null!;
        [Inject] private readonly AccSaberStore store = null!;
        [Inject] private readonly Utils.Safety.MainThreadDispatcher threadDispatcher = null!;
        [Inject] private readonly AccSaberCampaignFlow accCampaignFlow = null!;
        [Inject] private readonly AccSaberCampaignViewController acvc = null!;


        private bool parsed = false;
        private CampaignProgress campaignProgress;
        private AccSaberCampaign? currentCampaign;

        private readonly List<CampaignMapNode> campaignMapNodes = [];
        private readonly List<CampaignMapBarrier> campaignMapBarriers = [];
        private readonly List<(Guid fromNode, Guid toNode, GameObject go)> mapNodeArrows = [];

        public float CurrentScaleFactor { get; private set; }

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

        public async void SetCampaign(AccSaberCampaign campaign, float scaleFactor = 0.2f)
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

            CurrentScaleFactor = scaleFactor;
            currentCampaign = campaign;

            Task<CampaignProgress> campaignProgressTask = UnityMainThreadTaskScheduler.Factory.StartNew(() => store.GetCampaignProgress(campaign.Id)).Unwrap();

            int minHeight = int.MaxValue, maxHeight = int.MinValue, minWidth = int.MaxValue, maxWidth = int.MinValue;
            float minSize = float.MaxValue, maxSize = float.MinValue;
            float minHeightMaxSize = 0f, maxHeightMaxSize = 0f, minWidthMaxSize = 0f, maxWidthMaxSize = 0f;

            foreach (AccSaberCampaignPositionable map in campaign.Difficulties)
            {
                float size = map.Size * scaleFactor;


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
            PreloadStandardSprites();

            campaignProgress = await campaignProgressTask;


            Dictionary<Guid, PositionData> knownPositions = [];
            Queue<(Guid prereq, Guid toNode)> neededPositions = [];

            void HandleArrows(AccSaberCampaignPositionablePrereq node, PositionData current)
            {
                knownPositions.Add(node.Id, current);

                foreach (Guid id in node.PrerequisiteIds)
                {
                    if (knownPositions.TryGetValue(id, out PositionData from) &&
                        CreateArrow(NodeContainer.transform, from, current, campaignProgress.CompletedItems.Contains(id) ? Color.white : Color.grey, scaleFactor) 
                        is GameObject go)
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
                        scaleFactor: scaleFactor,
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
                    scaleFactor: scaleFactor,
                    xOffset: xOffset,
                    yOffset: yOffset,
                    offsetSize: offsetSize,
                    flow: accCampaignFlow,
                    campaignViewController: acvc,
                    levelUtils: levelUtils,
                    serialUtils: serialHandler,
                    threadDispatcher: threadDispatcher
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
                    CreateArrow(
                        NodeContainer.transform, from, to,
                        campaignProgress.CompletedItems.Contains(prereq) ? Color.white : Color.grey, scaleFactor)
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
        public async void UpdateCampaign(AccSaberCampaign campaign)
        {
            if (currentCampaign is null || currentCampaign.Id != campaign.Id)
                return;

            currentCampaign = campaign;
            campaignProgress = await UnityMainThreadTaskScheduler.Factory.StartNew(() => store.GetCampaignProgress(campaign.Id)).Unwrap();

            CurrentScaleFactor += 0.1f;

            UpdateScaling(CurrentScaleFactor - 0.1f);
        }
        public void UpdateScaling(float scaleFactor)
        {
            if (!parsed || currentCampaign is null || Mathf.Approximately(scaleFactor, CurrentScaleFactor))
                return;

            foreach (var (_, _, go) in mapNodeArrows)
                UnityEngine.Object.Destroy(go);

            mapNodeArrows.Clear();

            CurrentScaleFactor = scaleFactor;

            int minHeight = int.MaxValue, maxHeight = int.MinValue, minWidth = int.MaxValue, maxWidth = int.MinValue;
            float minSize = float.MaxValue, maxSize = float.MinValue;
            float minHeightMaxSize = 0f, maxHeightMaxSize = 0f, minWidthMaxSize = 0f, maxWidthMaxSize = 0f;

            foreach (AccSaberCampaignPositionable map in currentCampaign.Difficulties!)
            {
                float size = map.Size * scaleFactor;


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

            float yShift = 0;
            if (!Mathf.Approximately(minHeightMaxSize, maxHeightMaxSize))
            {
                float sign = minHeightMaxSize < maxHeightMaxSize ? -1f : 1f;
                yShift = sign * (Mathf.Abs(minHeightMaxSize - maxHeightMaxSize) / 4f);
            }

            float xOffset = -(minWidth + maxWidth) / 2f * offsetSize, yOffset = -(minHeight + maxHeight) / 2f * offsetSize + yShift;

            Utils.Safety.MainThreadDispatcher.AssertOnMainThread();

            LayoutElement scrollLayout = NodeContainer.GetComponent<LayoutElement>();

            scrollLayout.preferredWidth = width;
            scrollLayout.preferredHeight = height;

            ScrollRect scrollableContainer = ScrollContainer.transform.parent.parent.GetComponent<ScrollRect>();
            scrollableContainer.content.sizeDelta = new(width, height);
            scrollableContainer.horizontalScrollbar.value = 0;
            scrollableContainer.verticalScrollbar.value = 0;

            Dictionary<Guid, PositionData> knownPositions = [];
            Queue<(Guid prereq, Guid toNode)> neededPositions = [];

            void HandleArrows(AccSaberCampaignPositionablePrereq node, PositionData current)
            {
                knownPositions.Add(node.Id, current);

                foreach (Guid id in node.PrerequisiteIds)
                {
                    if (knownPositions.TryGetValue(id, out PositionData from) &&
                        CreateArrow(NodeContainer.transform, from, current, campaignProgress.CompletedItems.Contains(id) ? Color.white : Color.grey, scaleFactor)
                        is GameObject go)
                    {
                        go.transform.SetAsFirstSibling();
                        mapNodeArrows.Add((id, node.Id, go));
                    }
                    else
                        neededPositions.Enqueue((id, node.Id));
                }
            }

            foreach (CampaignMapBarrier barrier in campaignMapBarriers)
            {
                barrier.ScaleFactor = scaleFactor;
                barrier.OffsetSize = offsetSize;
                barrier.XOffset = xOffset;
                barrier.YOffset = yOffset;

                HandleArrows(barrier.Barrier, new(barrier));
            }

            foreach (CampaignMapNode map in campaignMapNodes)
            {
                map.ScaleFactor = scaleFactor;
                map.OffsetSize = offsetSize;
                map.XOffset = xOffset; 
                map.YOffset = yOffset;
                map.Progress = campaignProgress.PlayerValues[map.Map.Id];

                HandleArrows(map.Map, new(map));
            }

            while (neededPositions.Count > 0)
            {
                var (prereq, toNode) = neededPositions.Dequeue();

                if (knownPositions.TryGetValue(prereq, out PositionData from) &&
                    knownPositions.TryGetValue(toNode, out PositionData to) &&
                    CreateArrow(
                        NodeContainer.transform, from, to,
                        campaignProgress.CompletedItems.Contains(prereq) ? Color.white : Color.grey, scaleFactor)
                        is GameObject go)
                {
                    go.transform.SetAsFirstSibling();
                    mapNodeArrows.Add((prereq, toNode, go));
                }
                else
                    Plugin.Log.Error("There is an invalid prereq!\n" + prereq + ", " + toNode);
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


            if (arrow.transform.Find("Head") is not RectTransform headRect || arrow.transform.Find("Shaft") is not RectTransform shaftRect)
                return;

            float headLength = Mathf.Min(headRect.sizeDelta.x, length);
            float shaftLength = Mathf.Max(0f, length - headLength);

            Vector2 shaftSize = shaftRect.sizeDelta;
            shaftSize.x = shaftLength;
            shaftRect.sizeDelta = shaftSize;

            headRect.anchoredPosition = new Vector2(shaftLength, 0f);
        }
        public static GameObject? CreateArrow(
            Transform parent,
            PositionData from,
            PositionData to,
            Color color,
            float scale,
            float shaftThickness = 5f,
            float headLength = 20f,
            float headWidth = 20f,
            string name = "UI Arrow")
        {
            if (!TryGetClippedArrowPoints(from, from.Shape, to, to.Shape, out Vector2 fromPos, out Vector2 toPos))
            {
                fromPos = from.Position;
                toPos = to.Position;
            }

            Vector2 direction = toPos - fromPos;
            float length = direction.magnitude;

            if (length <= 0.001f)
                return null;

            shaftThickness *= scale;
            headLength *= scale;
            headWidth *= scale;

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

        private static bool TryGetClippedArrowPoints(
            PositionData from,
            NodeShape fromShape,
            PositionData to,
            NodeShape toShape,
            out Vector2 arrowStart,
            out Vector2 arrowEnd,
            float padding = 0f)
        {
            arrowStart = from.Position;
            arrowEnd = to.Position;

            Vector2 delta = to.Position - from.Position;

            if (delta.sqrMagnitude < 0.0001f)
                return false;

            Vector2 direction = delta.normalized;

            arrowStart = GetShapeEdgePoint(from.Position, from.Size, fromShape, direction, padding);
            arrowEnd = GetShapeEdgePoint(to.Position, to.Size, toShape, -direction, padding);

            // If the two shapes overlap, or are too close, the clipped arrow may be invalid.
            if (Vector2.Dot(arrowEnd - arrowStart, direction) <= 0.001f)
                return false;

            return true;
        }
        private static bool TryGetClippedArrowPoints(
            PositionData from,
            Quaternion fromRotation,
            PositionData to,
            Quaternion toRotation,
            out Vector2 arrowStart,
            out Vector2 arrowEnd,
            float padding = 0f)
        {
            arrowStart = from.Position;
            arrowEnd = to.Position;

            Vector2 delta = to.Position - from.Position;

            if (delta.sqrMagnitude < 0.0001f)
                return false;

            Vector2 direction = delta.normalized;

            arrowStart = GetRotatedShapeEdgePoint(from.Position, from.Size, from.Shape, fromRotation, direction, padding);

            arrowEnd = GetRotatedShapeEdgePoint(to.Position, to.Size, to.Shape, toRotation, -direction, padding);

            if (Vector2.Dot(arrowEnd - arrowStart, direction) <= 0.001f)
                return false;

            return true;
        }

        private static Vector2 GetRotatedShapeEdgePoint(Vector2 center, Vector2 size, NodeShape shape, Quaternion rotation, Vector2 worldDirection, float padding = 0f)
        {
            if (worldDirection.sqrMagnitude < 0.0001f)
                return center;

            worldDirection.Normalize();

            Vector2 halfSize = new(
                Mathf.Abs(size.x) * 0.5f,
                Mathf.Abs(size.y) * 0.5f
            );

            if (halfSize.x <= 0.0001f || halfSize.y <= 0.0001f)
                return center;

            // Convert the world-space arrow direction into the node's local rotated space.
            Vector3 localDirection3 =
                Quaternion.Inverse(rotation) *
                new Vector3(worldDirection.x, worldDirection.y, 0f);

            Vector2 localDirection = new(localDirection3.x, localDirection3.y);

            if (localDirection.sqrMagnitude < 0.0001f)
                return center;

            localDirection.Normalize();

            float distanceToEdge = GetShapeDistanceToEdge(
                halfSize,
                shape,
                localDirection
            );

            return center + worldDirection * (distanceToEdge + padding);
        }
        private static float GetShapeDistanceToEdge(
            Vector2 halfSize,
            NodeShape shape,
            Vector2 localDirection)
        {
            return shape switch
            {
                NodeShape.Square => GetRectangleDistanceToEdge(halfSize, localDirection),
                NodeShape.Circle => GetEllipseDistanceToEdge(halfSize, localDirection),
                NodeShape.Diamond => GetDiamondDistanceToEdge(halfSize, localDirection),
                NodeShape.Hexagon => GetHexagonDistanceToEdge(halfSize, localDirection),
                _ => GetRectangleDistanceToEdge(halfSize, localDirection)
            };
        }
        private static Vector2 GetShapeEdgePoint(Vector2 center, Vector2 size, NodeShape shape, Vector2 direction, float padding = 0f)
        {
            if (direction.sqrMagnitude < 0.0001f)
                return center;

            direction.Normalize();

            Vector2 halfSize = new(
                Mathf.Abs(size.x) * 0.5f,
                Mathf.Abs(size.y) * 0.5f
            );

            if (halfSize.x <= 0.0001f || halfSize.y <= 0.0001f)
                return center;

            float distance = shape switch
            {
                NodeShape.Square => GetRectangleDistanceToEdge(halfSize, direction),
                NodeShape.Circle => GetEllipseDistanceToEdge(halfSize, direction),
                NodeShape.Diamond => GetDiamondDistanceToEdge(halfSize, direction),
                NodeShape.Hexagon => GetHexagonDistanceToEdge(halfSize, direction),
                _ => GetRectangleDistanceToEdge(halfSize, direction)
            };

            return center + direction * (distance + padding);
        }
        private static float GetRectangleDistanceToEdge(Vector2 halfSize, Vector2 direction)
        {
            float distanceToVerticalEdge =
                Mathf.Abs(direction.x) > 0.0001f
                    ? halfSize.x / Mathf.Abs(direction.x)
                    : float.PositiveInfinity;

            float distanceToHorizontalEdge =
                Mathf.Abs(direction.y) > 0.0001f
                    ? halfSize.y / Mathf.Abs(direction.y)
                    : float.PositiveInfinity;

            return Mathf.Min(distanceToVerticalEdge, distanceToHorizontalEdge);
        }

        private static float GetEllipseDistanceToEdge(Vector2 halfSize, Vector2 direction)
        {
            // This treats Circle as an ellipse using the full Size.
            // If Size.x == Size.y, this is a true circle.
            float x = direction.x / halfSize.x;
            float y = direction.y / halfSize.y;

            float denominator = Mathf.Sqrt((x * x) + (y * y));

            if (denominator <= 0.0001f)
                return 0f;

            return 1f / denominator;
        }

        private static float GetDiamondDistanceToEdge(Vector2 halfSize, Vector2 direction)
        {
            // Diamond equation:
            // abs(x / halfWidth) + abs(y / halfHeight) = 1
            float denominator =
                Mathf.Abs(direction.x) / halfSize.x +
                Mathf.Abs(direction.y) / halfSize.y;

            if (denominator <= 0.0001f)
                return 0f;

            return 1f / denominator;
        }


        private static float GetHexagonDistanceToEdge(Vector2 halfSize, Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.0001f)
                return 0f;

            direction.Normalize();

            // width : height = 2 : sqrt(3)
            //
            // halfSize.x = outer radius
            // halfSize.y = apothem
            //
            // If your RectTransform is slightly non-regular, this still fits the largest
            // regular flat-top hexagon inside the given size.

            float radiusFromWidth = halfSize.x;
            float radiusFromHeight = halfSize.y * 2f / Mathf.Sqrt(3f);

            float radius = Mathf.Min(radiusFromWidth, radiusFromHeight);
            float apothem = radius * Mathf.Sqrt(3f) * 0.5f;

            // A regular flat-top hexagon can be represented by these 3 pairs of parallel edges:
            //
            // |y| <= apothem
            // |sqrt(3)/2 * x + 1/2 * y| <= apothem
            // |sqrt(3)/2 * x - 1/2 * y| <= apothem

            const float sqrt3Over2 = 0.86602540378f;

            float d1 = Mathf.Abs(direction.y);
            float d2 = Mathf.Abs((sqrt3Over2 * direction.x) + (0.5f * direction.y));
            float d3 = Mathf.Abs((sqrt3Over2 * direction.x) - (0.5f * direction.y));

            float maxProjection = Mathf.Max(d1, d2, d3);

            if (maxProjection <= 0.0001f)
                return 0f;

            return apothem / maxProjection;
        }

        private static bool TryRaySegmentIntersection(Vector2 rayOrigin, Vector2 rayDirection, Vector2 segmentA, Vector2 segmentB, out float rayDistance)
        {
            rayDistance = 0f;

            Vector2 segmentDirection = segmentB - segmentA;

            float denominator = Cross(rayDirection, segmentDirection);

            if (Mathf.Abs(denominator) <= 0.0001f)
                return false;

            Vector2 difference = segmentA - rayOrigin;

            float t = Cross(difference, segmentDirection) / denominator;
            float u = Cross(difference, rayDirection) / denominator;

            if (t >= 0f && u >= 0f && u <= 1f)
            {
                rayDistance = t;
                return true;
            }

            return false;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return (a.x * b.y) - (a.y * b.x);
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
                _triangleArrowHeadSprite ??= CreateTriangleArrowHeadSprite();

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
        public record struct PositionData(Vector2 Position, Vector2 Size, NodeShape Shape)
        {
            internal PositionData(CampaignMapNode node) : this(new(node.NodeXPos, node.NodeYPos), new(node.NodeWidth, node.NodeHeight), node.Shape) { }
            internal PositionData(CampaignMapBarrier node) : this(node.Position, node.SizeDelta, NodeShape.Square) { }
        }
        internal class CampaignMapNode : Utils.Safety.SafeNotifyPropertyChanged, IDisposable
        {
            public readonly AccSaberCampaignMap Map;
            public readonly string Hash;
            public CampaignProgress.CampaignProgressValue Progress
            {
                get;
                set
                {
                    field = value;

                    CoverImage.DefaultColor = value.Completion == CampaignProgress.CompletionStatus.Incomplete ? new(0.25f, 0.25f, 0.25f) : Color.white;

                    if (value.Completion == CampaignProgress.CompletionStatus.Complete)
                    {
                        RectTransform transform = (CompletionImage.transform as RectTransform)!;

                        transform.sizeDelta = new(NodeWidth / 4f, NodeHeight / 4f);

#if !NEW_VERSION
                        transform.anchorMin = new(0.75f, 0f);
                        transform.anchorMax = new(1f, 0.25f);
#endif
                    }
                }
            }
            public readonly NodeShape Shape;

            private bool postParse = false;

            public float ScaleFactor
            {
                get; 
                set
                {
                    field = value;

                    NotifyPropertyChanged(nameof(NodeWidth));
                    NotifyPropertyChanged(nameof(NodeHeight));
                } 
            }
            public float XOffset 
            {
                get;
                set
                {
                    field = value;

                    NotifyPropertyChanged(nameof(NodeXPos));
                }
            }
            public float YOffset 
            { 
                get;
                set
                {
                    field = value;

                    NotifyPropertyChanged(nameof(NodeYPos));
                }
            }
            public float OffsetSize
            {
                get;
                set
                {
                    field = value;

                    NotifyPropertyChanged(nameof(NodeXPos));
                    NotifyPropertyChanged(nameof(NodeYPos));
                }
            }

            private readonly AccSaberCampaignFlow campaignFlow;
            private readonly AccSaberCampaignViewController campaignController;
            private readonly LevelUtils levelUtils;
            private readonly SerializationHandler serialUtils;
            private readonly Utils.Safety.MainThreadDispatcher threadDispatcher;

            private Coroutine? imageRoutine;

            [UIObject("container")]
            private readonly GameObject Container = null!;

            [UIComponent("borderImage")]
            private readonly ImageView BorderImage = null!;

            [UIObject("coverContainer")]
            private readonly GameObject CoverContainer = null!;

            [UIComponent("coverImage")]
            private readonly ClickableImage CoverImage = null!;

            [UIComponent("completionImage")]
            private readonly ImageView CompletionImage = null!;


            [UIValue("NodeWidth")]
            public float NodeWidth => Map.Size * ScaleFactor;

            [UIValue("NodeHeight")]
            public float NodeHeight => Map.Size * ScaleFactor;

            [UIValue("NodeXPos")]
            public float NodeXPos => Map.PositionX * OffsetSize + XOffset;

            [UIValue("NodeYPos")]
            public float NodeYPos => -Map.PositionY * OffsetSize - YOffset;


            [UIValue("CheckmarkSrc")]
            private const string CheckmarkSrc = ResourcePaths.CHECKMARK;

            [UIValue("IsComplete")]
            private readonly bool IsComplete;



            public CampaignMapNode(
            AccSaberCampaignMap map,
            CampaignProgress.CampaignProgressValue progress,
            string mapHash,
            float scaleFactor,
            float xOffset,
            float yOffset,
            float offsetSize,
            AccSaberCampaignFlow flow,
            AccSaberCampaignViewController campaignViewController,
            LevelUtils levelUtils,
            SerializationHandler serialUtils,
            Utils.Safety.MainThreadDispatcher threadDispatcher
            )
            {
                Map = map;
                Progress = progress;
                Hash = mapHash;
                Shape = map.BorderShape switch
                {
                    "square" => NodeShape.Square,
                    "diamond" => NodeShape.Diamond,
                    "circle" => NodeShape.Circle,
                    _ => NodeShape.Hexagon
                };

                campaignFlow = flow;
                campaignController = campaignViewController;
                this.levelUtils = levelUtils;
                this.serialUtils = serialUtils;
                this.threadDispatcher = threadDispatcher;
                IsComplete = progress.Completion == CampaignProgress.CompletionStatus.Complete;

                XOffset = xOffset;
                YOffset = yOffset;
                ScaleFactor = scaleFactor;
                OffsetSize = offsetSize;
            }



            [UIAction("#post-parse")]
            private void PostParse()
            {
                try
                {
                    BorderImage.sprite = GetBorderSprite(Shape);
                    BorderImage.raycastTarget = false;

                    if (string.IsNullOrEmpty(Map.BorderColor))
                    {
                        APCategory category = serialUtils.CachedDifficulties[Map.MapDifficultyId].Category ?? APCategory.Overall;
                        BorderImage.color = ColorUtils.GetColor(category).Color();
                    }
                    else BorderImage.color = Map.BorderColor!.Color();

                    ImageView MaskImage = CoverContainer.AddComponent<ImageView>();
                    MaskImage.sprite = GetFillSprite(Shape);
                    MaskImage.color = Color.white;
                    MaskImage.material = Utilities.ImageResources.NoGlowMat;
                    MaskImage.raycastTarget = false;

                    Mask m = CoverContainer.AddComponent<Mask>();
                    m.showMaskGraphic = false;

                    CoverImage.DefaultColor = Progress.Completion == CampaignProgress.CompletionStatus.Incomplete ? new(0.25f, 0.25f, 0.25f) : Color.white;
                    imageRoutine = threadDispatcher.StartCoroutine(CoverImage.LoadCoverImageRoutine(Hash, Map.CoverUrl));

                    CompletionImage.raycastTarget = false;

                    LayoutElement mainLayout = CoverContainer.GetComponent<LayoutElement>();
                    mainLayout.preferredWidth = NodeWidth;
                    mainLayout.preferredHeight = NodeHeight;

#if PRINT_DEBUG
                Plugin.Log.Info($"Pos = ({Map.PositionX}, {Map.PositionY}) Node Pos = ({NodeXPos}, {NodeYPos}), Width = {NodeWidth}, Height = {NodeHeight}");
#endif
                    if (Progress.Completion == CampaignProgress.CompletionStatus.Complete)
                    {
                        RectTransform transform = (CompletionImage.transform as RectTransform)!;

                        transform.sizeDelta = new(NodeWidth / 4f, NodeHeight / 4f);

#if !NEW_VERSION
                        transform.anchorMin = new(0.75f, 0f);
                        transform.anchorMax = new(1f, 0.25f);
#endif
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.Error(e);
                }
                finally
                {
                    postParse = true;
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

                campaignController.SetMission(Map, diff, Progress);
#endif
            }

            public new void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
            {
                base.NotifyPropertyChanged(propertyName);

                if (postParse && (propertyName.Equals(nameof(NodeWidth)) || propertyName.Equals(nameof(NodeHeight))))
                {
                    (CompletionImage.transform as RectTransform)!.sizeDelta = new(NodeWidth / 4f, NodeHeight / 4f);

                    LayoutElement mainLayout = CoverContainer.GetComponent<LayoutElement>();

                    if (propertyName.Equals(nameof(NodeWidth)))
                        mainLayout.preferredWidth = NodeWidth;
                    else
                        mainLayout.preferredHeight = NodeHeight;
                }
            }

            public void Dispose()
            {
                UnityEngine.Object.Destroy(Container);

                if (imageRoutine is not null && CoverImage.sprite != Utilities.ImageResources.BlankSprite)
                    threadDispatcher.StopCoroutine(imageRoutine);
            }
        }

        internal class CampaignMapBarrier : IDisposable
        {
            public const float WIDTH = 5f;

            public readonly AccSaberCampaignBarrier Barrier;
            public readonly CampaignProgress.CampaignProgressValue Progress;
            private readonly Transform Parent;

            private readonly GameObject obj;

            public Quaternion Rotation { get => obj.transform.rotation; set => obj.transform.rotation = value; }
            public Vector2 SizeDelta 
            { 
                get; 
                set
                {
                    field = value;
                    LayoutElement le = obj.GetComponent<LayoutElement>();
                    le.preferredWidth = value.x;
                    le.preferredHeight = value.y;
                }
            }
            public Vector2 Position => obj.transform.GetComponent<RectTransform>().anchoredPosition;

            public float ScaleFactor 
            { 
                get;
                set
                {
                    field = value;

                    SizeDelta = new(WIDTH * value, Barrier.Size * value);
                }
            }
            public float XOffset
            {
                get;
                set
                {
                    field = value;

                    RectTransform transform = (obj.transform as RectTransform)!;
                    transform.anchoredPosition = new(Barrier.PositionX * OffsetSize + value, transform.anchoredPosition.y);
                }
            }
            public float YOffset
            {
                get;
                set
                {
                    field = value;

                    RectTransform transform = (obj.transform as RectTransform)!;
                    transform.anchoredPosition = new(transform.anchoredPosition.x, -Barrier.PositionY * OffsetSize - value);
                }
            }
            public float OffsetSize
            {
                get;
                set
                {
                    field = value;

                    RectTransform transform = (obj.transform as RectTransform)!;
                    transform.anchoredPosition = new(Barrier.PositionX * OffsetSize + XOffset, -Barrier.PositionY * OffsetSize - YOffset);
                }
            }



            public CampaignMapBarrier(AccSaberCampaignBarrier barrier, Transform parent, CampaignProgress.CampaignProgressValue progress, float scaleFactor, float xOffset, float yOffset, float offsetSize)
            {
                Barrier = barrier;
                Progress = progress;
                Parent = parent;

                obj = new("AccSaberCampaignBarrier");
                obj.transform.SetParent(parent, false);

                RectTransform transform = obj.AddComponent<RectTransform>();
                transform.anchorMin = Vector2.zero;
                transform.anchorMax = Vector2.one;
                transform.rotation = Quaternion.Euler(0f, 0f, 0f);

                LayoutElement layout = obj.AddComponent<LayoutElement>();
                layout.ignoreLayout = true;

                ContentSizeFitter sizeFitter = obj.AddComponent<ContentSizeFitter>();
                sizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                ImageView image = obj.AddComponent<ImageView>();
                image.sprite = Utilities.ImageResources.WhitePixel;
                image.material = Utilities.ImageResources.NoGlowMat;
                image.type = Image.Type.Simple;
                image.color = barrier.BorderColor?.Color() ?? Color.red;

                ScaleFactor = scaleFactor;
                OffsetSize = offsetSize;
                XOffset = xOffset;
                YOffset = yOffset;

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
        private static readonly ConcurrentDictionary<string, Sprite> _borderSpriteCache = [];
        private static readonly ConcurrentDictionary<string, Sprite> _fillSpriteCache = [];

        private static bool _preloadedSprites = false;

        public static void PreloadStandardSprites()
        {
            if (_preloadedSprites)
                return;
            _preloadedSprites = true;

            NodeShape[] shapes = (NodeShape[])Enum.GetValues(typeof(NodeShape));

            foreach (NodeShape shape in shapes)
            {
                GetBorderSprite(shape);
                GetFillSprite(shape);
            }
        }

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

        public static Sprite GetFillSprite(NodeShape shape, int size = 256)
        {
            string key = $"fill_{shape}_{size}";

            if (_fillSpriteCache.TryGetValue(key, out Sprite cached))
                return cached;

            Texture2D texture = CreateFillTexture(shape, size);
            Sprite sprite = CreateSprite(texture);

            _fillSpriteCache[key] = sprite;
            return sprite;
        }
        private static Texture2D CreateFillTexture(NodeShape shape, int size)
        {
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float coverage = GetCoverage(x, y, size, shape, 1f);
                    byte a = (byte)Mathf.RoundToInt(coverage * 255f);

                    pixels[y * size + x] = new Color32(255, 255, 255, a);
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