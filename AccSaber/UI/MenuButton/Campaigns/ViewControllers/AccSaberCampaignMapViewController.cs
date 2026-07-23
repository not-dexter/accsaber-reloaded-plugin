//#define PRINT_DEBUG

using AccSaber.Configuration;
using AccSaber.Consts;
using AccSaber.Managers;
using AccSaber.Models;
using AccSaber.Utils;
using AccSaber.Utils.Misc;
using AccsaberLeaderboard.UI.Components;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using HMUI;
using IPA.Utilities.Async;
using SongCore;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zenject;
using static AccSaber.Models.CampaignModel;
using static AccSaber.UI.MenuButton.Campaigns.ViewControllers.NodeShapeTextures;

namespace AccSaber.UI.MenuButton.Campaigns.ViewControllers
{
    internal class AccSaberCampaignMapViewController : Utils.Safety.SafeNotifyPropertyChanged, IDisposable
    {
        [Inject] private readonly SerializationHandler serialHandler = null!;
        [Inject] private readonly LevelUtils levelUtils = null!;
        [Inject] private readonly AccSaberStore store = null!;
        [Inject] private readonly PluginConfig config = null!;
        [Inject] private readonly Utils.Safety.MainThreadDispatcher threadDispatcher = null!;
        [Inject] private readonly AccSaberCampaignFlow accCampaignFlow = null!;
        [Inject] private readonly AccSaberCampaignViewController acvc = null!;

        private bool parsed = false;
        private int setCampaignVersion = 0;
        private bool disposed = false;

        private AccSaberCampaign? CurrentCampaign
        {
            get;
            set
            {
                field = value;
                OnCampaignSet();
            }
        }

        private readonly List<CampaignMapNode> campaignMapNodes = [];
        private readonly List<CampaignMapBarrier> campaignMapBarriers = [];
        private readonly List<CampaignMapText> campaignMapTexts = [];
        private readonly List<(Guid fromNode, Guid toNode, GameObject go)> mapNodeArrows = [];
        private ScrollRect scrollRect = null!;
        private Color currentBgColor, maxBgColors;

        public AccSaberCampaignOffsetData? CurrentOffsetData 
        { 
            get;
            private set
            {
                field = value;
                NotifyPropertyChanged();
            } 
        }
        public CampaignProgress CampaignProgress 
        { 
            get;
            private set
            {
                field = value;
                NotifyPropertyChanged();
            }
        }

        public bool StickScrolling
        {
            get;
            set
            {
                field = value;
                scrollRect.scrollSensitivity = value ? ScrollSpeed : 0f;
                NotifyPropertyChanged();
            }
        }
        public float ScrollSpeed
        {
            get;
            set
            {
                field = value;

                if (StickScrolling)
                    scrollRect.scrollSensitivity = value;

                NotifyPropertyChanged();
            }
        }
        public float BackgroundBrightness
        {
            get;
            set
            {
                field = Mathf.Clamp01(value);

                if (parsed && ScrollContainer.TryGetComponent(out ImageView image))
                    image.color = maxBgColors * new Color(field, field, field, BackgroundAlpha);

                NotifyPropertyChanged();
            }
        }
        public float BackgroundAlpha
        {
            get;
            set
            {
                field = Mathf.Clamp01(value);

                if (parsed && ScrollContainer.TryGetComponent(out ImageView image))
                    image.color = maxBgColors * new Color(BackgroundBrightness, BackgroundBrightness, BackgroundBrightness, field);

                NotifyPropertyChanged();
            }
        }
        public bool IsSolidBGColor { get; private set; }

        [UIObject(nameof(ScrollContainer))]
        private readonly GameObject ScrollContainer = null!;

        [UIObject(nameof(NodeContainer))]
        private readonly GameObject NodeContainer = null!;


        [UIValue(nameof(ViewportWidth))]
        public const float ViewportWidth = 100f;

        [UIValue(nameof(ViewportHeight))]
        public const float ViewportHeight = 70f;


        [UIAction("#post-parse")]
        private void PostParse()
        {
            if (!parsed)
                parsed = true;

            scrollRect = ScrollContainer.transform.parent.parent.GetComponent<ScrollRect>();

            ScrollSpeed = config.ScrollSpeed;
            StickScrolling = config.StickScrolling;

            //Plugin.Log.Info($"{Resources.FindObjectsOfTypeAll<Sprite>().Select(s => s.name).Where(n => !string.IsNullOrEmpty(n)).Print()}");
        }

        public void Dispose()
        {
            disposed = true;
            setCampaignVersion++;
            ClearDisplay();
        }

        public async Task SetCampaign(AccSaberCampaign campaign, float scaleFactor = 0.2f, bool resetScrollbars = true)
        {
            int version = Interlocked.Increment(ref setCampaignVersion);

            try
            {
                if (!parsed || campaign.Difficulties is null)
                    return;

                ClearDisplay();

                CurrentCampaign = campaign;

                Task<CampaignProgress> campaignProgressTask = UnityMainThreadTaskScheduler.Factory.StartNew(() => store.GetCampaignProgress(campaign)).Unwrap();

                CurrentOffsetData = new(scaleFactor, campaign.Difficulties.Cast<AccSaberCampaignScalable>()
                    .Concat(campaign.Barriers?.Cast<AccSaberCampaignScalable>() ?? [])
                    .Concat(campaign.Texts?.Cast<AccSaberCampaignScalable>() ?? []));

                UpdateContainerValues(resetScrollbars);

                await PreloadStandardSprites(config.CampaignMaxCoverageLoadsPerFrame);

                CampaignProgress = await campaignProgressTask;

                if (disposed || version != setCampaignVersion)
                    return;

                HandleCheckpointCollisions();

                int loads = 0;

                IEnumerator LoadSlowly()
                {
                    if (campaign.Texts is not null)
                    {
                        foreach (AccSaberCampaignText text in campaign.Texts)
                        {
                            CampaignMapText mapText = new(text, (RectTransform)NodeContainer.transform, CurrentOffsetData);

                            campaignMapTexts.Add(mapText);

                            ++loads;

                            if (loads >= config.CampaignMaxObjectLoadsPerFrame)
                            {
                                loads = 0;
                                yield return null;
                            }
                        }
                    }

                    if (campaign.Barriers is not null)
                    {
                        foreach (AccSaberCampaignBarrier barrier in campaign.Barriers)
                        {
                            CampaignMapBarrier barrierNode = new(
                                barrier: barrier,
                                parent: NodeContainer.transform,
                                parentVC: this,
                                offsetData: CurrentOffsetData
                            );

                            campaignMapBarriers.Add(barrierNode);

                            ++loads;

                            if (loads >= config.CampaignMaxObjectLoadsPerFrame)
                            {
                                loads = 0;
                                yield return null;
                            }
                        }
                    }

                    foreach (AccSaberCampaignMap map in campaign.Difficulties)
                    {
                        AccSaberBasicDifficulty? diff = null;
                        
                        yield return serialHandler.GetDiffByIdAsync(map.MapDifficultyId).WaitWithRoutine(loadedDiff => diff = loadedDiff);

                        if (diff is null)
                        {
                            Plugin.Log.Warn($"There was an error loading map with id \"{map.MapDifficultyId}\"");
                            continue;
                        }

                        CampaignMapNode node = new(
                            map: map,
                            parent: this,
                            mapHash: diff.Hash,
                            offsetData: CurrentOffsetData,
                            flow: accCampaignFlow,
                            campaignViewController: acvc,
                            levelUtils: levelUtils,
                            serialUtils: serialHandler,
                            threadDispatcher: threadDispatcher,
                            config: config
                        );

                        campaignMapNodes.Add(node);

                        VersionUtils.Parse(ResourcePaths.ACC_SABER_CAMPAIGN_MAP_CELL, NodeContainer, node);

                        loads += 2;

                        if (loads >= config.CampaignMaxObjectLoadsPerFrame)
                        {
                            loads = 0;
                            yield return null;
                        }
                    }

                        CurrentOffsetData.RecalculateValues();
                        UpdateContainerValues(resetScrollbars);
                }

                await Coroutines.AsTask(LoadSlowly());

                RebuildArrows(loads);

                ScrollToNode(CampaignProgress.Nodes.Heads.First().Current.Id);
            }
            catch (Exception e)
            {
                Plugin.Log.Error(e);
            }
        }
        private void HandleCheckpointCollisions()
        {
            if (CurrentCampaign?.Difficulties is null)
                return;

            Dictionary<string, List<Guid>> checkpointNames = [];
            Dictionary<Guid, AccSaberCampaignMap> usedMaps = [];

            foreach (AccSaberCampaignMap map in CurrentCampaign.Difficulties)
            {
                if (!string.IsNullOrEmpty(map.CheckpointLabel))
                {
                    if (!checkpointNames.TryGetValue(map.CheckpointLabel!, out List<Guid> val))
                        val = [];

                    val.Add(map.Id);
                    
                    if (val.Count == 1)
                        checkpointNames[map.CheckpointLabel!] = val;

                    usedMaps.Add(map.Id, map);
                }
            }

            foreach (string key in checkpointNames.Keys)
            {
                List<Guid> ids = checkpointNames[key];

                if (ids.Count <= 1)
                    continue;

                int xPos = 0, yPos = 0;

                foreach (Guid id in ids)
                {
                    AccSaberCampaignMap map = usedMaps[id];

                    xPos += map.PositionX;
                    yPos += map.PositionY;
                }

                float averageXPos = xPos / (float)ids.Count, averageYPos = yPos / (float)ids.Count;
                float minDistance = float.PositiveInfinity;
                int minDistIndex = -1;

                for (int i = 0; i < ids.Count; ++i)
                {
                    AccSaberCampaignMap map = usedMaps[ids[i]];
                    float dist = Mathf.Abs((map.PositionX - averageXPos + map.PositionY - averageYPos) / 2f);

                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        minDistIndex = i;
                    }
                }

                for (int i = 0; i < ids.Count; ++i)
                {
                    AccSaberCampaignMap map = usedMaps[ids[i]];

                    if (minDistIndex == i)
                        map.CheckpointSize *= 2;
                    else
                        map.CheckpointLabel = null;
                }
            }
        }
        private void OnCampaignSet()
        {
            if (!parsed)
                return;

            Utils.Safety.MainThreadDispatcher.AssertOnMainThread();

            CustomBackground customBg;

            if (CurrentCampaign is null || (CurrentCampaign.BackgroundColor is null && CurrentCampaign.BackgroundUrl is null))
            {
                customBg = ScrollContainer.GetComponent<CustomBackground>();

                customBg.Apply(ResourcePaths.PIXEL);

                currentBgColor = Color.white;
                maxBgColors = Color.white;

                IsSolidBGColor = true;

                BackgroundAlpha = config.CampaignColorBackgroundAlpha;
                BackgroundBrightness = config.CampaignColorBackgroundBrightness;

                return;
            }

            customBg = ScrollContainer.GetComponent<CustomBackground>();

            UnityEngine.Object.DestroyImmediate(ScrollContainer.GetComponent<ImageView>());

            bool bgColorExists = CurrentCampaign.BackgroundColor is not null;

            if (CurrentCampaign.BackgroundUrl is not null)
            {
                void AfterImageLoaded(Task task)
                {
                    try
                    {
                        if (!task.IsCompletedSuccessfully)
                            return;

                        currentBgColor = CurrentCampaign.BackgroundColor?.Color() ?? Color.white;
                        maxBgColors = customBg.Background!.sprite.texture.GetMaxColorValues();

                        IsSolidBGColor = false;

                        BackgroundAlpha = config.CampaignImageBackgroundAlpha;
                        BackgroundBrightness = config.CampaignImageBackgroundBrightness;
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.Error(e);
                    }
                }

                Task imgTask = customBg.ApplyUrl(CurrentCampaign.BackgroundUrl, new(1f, 1f, 1f, 0f));

                imgTask.ContinueWith(AfterImageLoaded, UnityMainThreadTaskScheduler.Default);
            }
            else if (bgColorExists)
            {
                customBg.Apply(ResourcePaths.PIXEL);

                currentBgColor = CurrentCampaign.BackgroundColor!.Color();
                maxBgColors = currentBgColor;

                IsSolidBGColor = true;

                BackgroundAlpha = config.CampaignColorBackgroundAlpha;
                BackgroundBrightness = config.CampaignColorBackgroundBrightness;
            }
        }
        private void ClearDisplay()
        {
            foreach (IDisposable node in campaignMapNodes.Cast<IDisposable>().Concat(campaignMapBarriers).Concat(campaignMapTexts))
                node.Dispose();

            foreach (var (_, _, go) in mapNodeArrows)
                UnityEngine.Object.Destroy(go);

            campaignMapNodes.Clear();
            campaignMapBarriers.Clear();
            campaignMapTexts.Clear();
            mapNodeArrows.Clear();
        }
        private void UpdateContainerValues(bool resetScrollbars)
        {
            if (CurrentOffsetData is null)
                return;

            Utils.Safety.MainThreadDispatcher.AssertOnMainThread();

            LayoutElement scrollLayout = NodeContainer.GetComponent<LayoutElement>();

            scrollLayout.preferredWidth = CurrentOffsetData.ContainerSize.x;
            scrollLayout.preferredHeight = CurrentOffsetData.ContainerSize.y;

            scrollRect.content.sizeDelta = CurrentOffsetData.ContainerSize;

            if (resetScrollbars)
            {
                scrollRect.horizontalScrollbar.value = 0;
                scrollRect.verticalScrollbar.value = 0;
            }
        }
        public async Task UpdateCampaign()
        {
            if (CurrentCampaign is null)
                return;

            async Task<(AccSaberCampaign campaign, CampaignProgress progress)> GetData()
            {
                AccSaberCampaign campaign = await store.GetCampaign(CurrentCampaign.Id, true);
                return (campaign, await store.GetCampaignProgress(CurrentCampaign));
            }

            var data = await UnityMainThreadTaskScheduler.Factory.StartNew(GetData).Unwrap();

            CurrentCampaign = data.campaign;
            CampaignProgress = data.progress;

            UpdateDisplay();
        }
        public void UpdateScaling(float scaleFactor)
        {
            if (!parsed || CurrentCampaign is null || CurrentOffsetData is null || Mathf.Approximately(CurrentOffsetData.ScaleFactor, scaleFactor))
                return;

            CurrentOffsetData.RecalculateValuesWithScale(scaleFactor);
            UpdateDisplay();
        }
        public void UpdateScalingDelta(float deltaScale)
        {
            if (!parsed || CurrentCampaign is null || CurrentOffsetData is null)
                return;

            CurrentOffsetData.RecalculateValuesWithScale(CurrentOffsetData.ScaleFactor + deltaScale);
            UpdateDisplay();
        }
        public void UpdateDisplay()
        {
            if (!parsed || CurrentOffsetData is null)
                return;

            UpdateContainerValues(false);

            RebuildArrows();
        }
        public void ScrollToNode(Guid nodeId)
        {
            if (!parsed)
            {
                Plugin.Log.Warn("Cannot scroll to node before the map is loaded!");
                return;
            }

            CampaignMapNode? node = campaignMapNodes.FirstOrDefault(node => node.Map.Id == nodeId);

            if (node is null)
            {
                Plugin.Log.Warn($"No node of id \"{nodeId}\" found.");
                return;
            }

            Vector2 viewSize = new(ViewportWidth, ViewportHeight);
            Vector2 actualSize = scrollRect.content.sizeDelta;
            Vector2 trueNodePos = new(node.NodeXPos + actualSize.x / 2f, node.NodeYPos + actualSize.y / 2f);

#if PRINT_DEBUG
            Plugin.Log.Info($"viewSize = {viewSize}, actualSize = {actualSize}, node size = {trueNodePos}");
#endif

            if (trueNodePos.x >= actualSize.x - viewSize.x / 2f)
                scrollRect.horizontalScrollbar.value = 1f;
            else if (trueNodePos.x <= viewSize.x / 2f)
                scrollRect.horizontalScrollbar.value = 0f;
            else
                scrollRect.horizontalScrollbar.value =
                    (trueNodePos.x - viewSize.x / 2f) / (actualSize.x - viewSize.x);

            if (trueNodePos.y >= actualSize.y - viewSize.y / 2f)
                scrollRect.verticalScrollbar.value = 1f;
            else if (trueNodePos.y <= viewSize.y / 2f)
                scrollRect.verticalScrollbar.value = 0f;
            else
                scrollRect.verticalScrollbar.value =
                    (trueNodePos.y - viewSize.y / 2f) / (actualSize.y - viewSize.y);
#if PRINT_DEBUG
            Plugin.Log.Info($"Final scroll percent = ({scrollableContainer.horizontalScrollbar.value * 100f:N2}%, {scrollableContainer.verticalScrollbar.value * 100f:N2}%)");
#endif
        }
        public void ClickNode(Guid nodeId)
        {
            CampaignMapNode? node = campaignMapNodes.FirstOrDefault(node => node.Map.Id == nodeId);

            if (node is null)
            {
                Plugin.Log.Warn($"No node of id \"{nodeId}\" found.");
                return;
            }

            node.OnClick();
        }
        private async void RebuildArrows(int loads = 0)
        {
            foreach (var (_, _, go) in mapNodeArrows)
                UnityEngine.Object.Destroy(go);

            mapNodeArrows.Clear();

            Dictionary<Guid, PositionData> knownPositions = [];
            Queue<(AccSaberCampaignPrereqInfo prereq, Guid toNode)> neededPositions = [];

            IEnumerator LoadSlowly()
            {
                IEnumerator HandleArrows(IAccSaberCampaignPrereq node, PositionData current)
                {
                    if (!knownPositions.TryAdd(node.Id, current))
                    {
                        Plugin.Log.Warn($"Duplicate campaign item id: {node.Id}");
                        yield break;
                    }

                    foreach (AccSaberCampaignPrereqInfo prereq in node.PrerequisiteInfos)
                    {
                        if (knownPositions.TryGetValue(prereq.Id, out PositionData from) &&
                            CreateArrow(
                                NodeContainer.transform,
                                from,
                                current,
                                (CampaignProgress.CompletedItems.Contains(prereq.Id) ? prereq.Color : prereq.DimmedColor).Color(),
                                CurrentOffsetData!.ScaleFactor)
                            is GameObject go)
                        {
                            go.transform.SetAsFirstSibling();
                            mapNodeArrows.Add((prereq.Id, node.Id, go));
                        }
                        else
                        {
                            neededPositions.Enqueue((prereq, node.Id));
                        }

                        ++loads;

                        if (loads >= config.CampaignMaxObjectLoadsPerFrame)
                        {
                            loads = 0;
                            yield return null;
                        }
                    }
                }

                foreach (CampaignMapBarrier barrier in campaignMapBarriers)
                    yield return HandleArrows(barrier.Barrier, new PositionData(barrier));

                foreach (CampaignMapNode node in campaignMapNodes)
                    yield return HandleArrows(node.Map, new PositionData(node));

                while (neededPositions.Count > 0)
                {
                    var (prereq, toNode) = neededPositions.Dequeue();

                    if (knownPositions.TryGetValue(prereq.Id, out PositionData from) &&
                        knownPositions.TryGetValue(toNode, out PositionData to) &&
                        CreateArrow(
                            NodeContainer.transform,
                            from,
                            to,
                            (CampaignProgress.CompletedItems.Contains(prereq.Id) ? prereq.Color : prereq.DimmedColor).Color(),
                            CurrentOffsetData!.ScaleFactor)
                        is GameObject go)
                    {
                        go.transform.SetAsFirstSibling();
                        mapNodeArrows.Add((prereq.Id, toNode, go));
                    }
                    else
                    {
                        Plugin.Log.Error("There is an invalid prereq!\n" + prereq + ", " + toNode);
                    }

                    ++loads;

                    if (loads >= config.CampaignMaxObjectLoadsPerFrame)
                    {
                        loads = 0;
                        yield return null;
                    }
                }
            }

            await Coroutines.AsTask(LoadSlowly());

            UpdateBarrierRotationsAndArrowClipping(knownPositions);
        }
        private void UpdateBarrierRotationsAndArrowClipping(Dictionary<Guid, PositionData> knownPositions)
        {
            Dictionary<Guid, CampaignMapBarrier> barrierNodeIds =
                campaignMapBarriers.ToDictionary(barrier => barrier.Barrier.Id, barrier => barrier);

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

                barrier.Rotation = desiredRotation;
                finalBarrierRotations[barrierNodeId] = desiredRotation;
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
        public async Task<CampaignProgress.CampaignProgressValue?> MarkNodeAsComplete(Guid id, float progress)
        {
            if (CurrentCampaign is null || CurrentCampaign.Difficulties is null)
            {
                Plugin.Log.Warn($"Cannot mark node \"{id}\" as complete as the current campaign is null!");
                Plugin.Log.Debug($"CurrentCampaign null? {CurrentCampaign is null}, CurrentCampaign.Difficulties null? {CurrentCampaign?.Difficulties is null}");
                return null;
            }

            HashSet<Guid> mapsToUpdate = [.. CampaignProgress.MarkAsComplete(id, progress), id];

            if (campaignMapBarriers.Any(node => mapsToUpdate.Contains(node.Barrier.Id)))
                await FetchProgress();

            foreach (CampaignMapBarrier node in campaignMapBarriers)
                if (mapsToUpdate.Contains(node.Barrier.Id))
                    node.UpdateProgress();

            foreach (CampaignMapNode node in campaignMapNodes)
                if (mapsToUpdate.Contains(node.Map.Id))
                    node.UpdateProgress();

            return CampaignProgress.PlayerValues[id];
        }
        private async Task FetchProgress()
        {
            AccSaberCampaign? current = CurrentCampaign;

            await acvc.WaitForServerUpdate();

            if (CurrentCampaign is null || !CurrentCampaign.Equals(current) || !acvc.InCampaign)
                return;

            CampaignProgress = await store.GetCampaignProgress(CurrentCampaign);
        }

        private static bool TryGetPerpendicularBarrierRotation(List<Vector2> arrowDirections, out Quaternion rotation)
        {
            rotation = Quaternion.identity;

            if (arrowDirections is null || arrowDirections.Count == 0)
                return false;

            if (!TryGetAverageAxisDirection(arrowDirections, out Vector2 averageAxisDirection))
                return false;

            Vector2 barrierAxis = GetPerpendicular(averageAxisDirection);
            rotation = RotationThatPointsUpAlong(barrierAxis);
            return true;
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
            arrowRect.sizeDelta = new Vector2(length, arrowRect.sizeDelta.y);


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
            private static event Action? UpdateMapCovers;

            private bool postParse = false;
            private CancellationTokenSource? imageTaskToken;
            private readonly AsyncLock onClickLock = new();

            private readonly AccSaberCampaignFlow campaignFlow;
            private readonly AccSaberCampaignMapViewController parent;
            private readonly AccSaberCampaignViewController campaignController;
            private readonly LevelUtils levelUtils;
            private readonly SerializationHandler serialUtils;
            private readonly Utils.Safety.MainThreadDispatcher threadDispatcher;
            private readonly PluginConfig config;


            public readonly AccSaberCampaignMap Map;
            public readonly string Hash;
            public readonly AccSaberCampaignOffsetData OffsetData;
            public readonly NodeShape Shape;
            private readonly bool requiresAllPrereqs;

            public CampaignProgress.CampaignProgressValue Progress => parent.CampaignProgress.PlayerValues[Map.Id];


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

            [UIComponent("requiresAllImage")]
            private readonly ImageView RequiresAllImage = null!;

            [UIComponent("checkpointText")]
            private readonly TextMeshProUGUI CheckpointText = null!;


            [UIValue("NodeWidth")]
            public float NodeWidth => Map.Scale * OffsetData.ScaleFactor;

            [UIValue("NodeHeight")]
            public float NodeHeight => Map.Scale * OffsetData.ScaleFactor;

            [UIValue("NodeXPos")]
            public float NodeXPos => Map.PositionX * OffsetData.OffsetSize + OffsetData.Offset.x;

            [UIValue("NodeYPos")]
            public float NodeYPos => -Map.PositionY * OffsetData.OffsetSize - OffsetData.Offset.y;


            [UIValue("CheckmarkSrc")]
            private const string CheckmarkSrc = ResourcePaths.CHECKMARK;

            [UIValue("RequiresAllSrc")]
            private const string RequiresAllSrc = ResourcePaths.CAMPAIGN_ALL;

            [UIValue("IsComplete")]
            private bool IsComplete 
            {
                get;
                set
                {
                    field = value;
                    NotifyPropertyChanged();
                }

            }

            [UIValue("ShowPrereqIndicator")]
            private bool ShowPrereqIndicator
            {
                get;
                set
                {
                    if (field == value)
                        return;

                    field = value;
                    NotifyPropertyChanged();
                }
            }

            public CampaignMapNode(
            AccSaberCampaignMap map,
            AccSaberCampaignMapViewController parent,
            string mapHash,
            AccSaberCampaignOffsetData offsetData,
            AccSaberCampaignFlow flow,
            AccSaberCampaignViewController campaignViewController,
            LevelUtils levelUtils,
            SerializationHandler serialUtils,
            Utils.Safety.MainThreadDispatcher threadDispatcher,
            PluginConfig config
            )
            {
                Map = map;
                this.parent = parent;
                OffsetData = offsetData;
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
                this.config = config;

                IsComplete = Progress.Completion == CampaignProgress.CompletionStatus.Complete;
                requiresAllPrereqs = map.PrerequisiteMode == CampaignPrerequisiteMode.AND;
                ShowPrereqIndicator = config.ShowPrereqIndicator && requiresAllPrereqs;

                offsetData.OnScaleChanging += UpdateCheckpointLabel;
                offsetData.OnScaleChanged += OnOffsetDataChanged;
                UpdateMapCovers += UpdateCover;
                config.PropertyChanged += OnPluginUpdate;
            }


            [UIAction("#post-parse")]
            private async void PostParse()
            {
                try
                {
                    BorderImage.sprite = await GetBorderSprite(Shape, config.CampaignMaxCoverageLoadsPerFrame);
                    BorderImage.raycastTarget = false;

                    if (string.IsNullOrEmpty(Map.BorderColor))
                        BorderImage.color = ColorUtils.GetColor(Map.Category).Color();
                    else 
                        BorderImage.color = Map.BorderColor!.Color();

                    ImageView MaskImage = CoverContainer.AddComponent<ImageView>();
                    MaskImage.sprite = await GetFillSprite(Shape, config.CampaignMaxCoverageLoadsPerFrame);
                    MaskImage.color = Color.white;
                    MaskImage.material = Utilities.ImageResources.NoGlowMat;
                    MaskImage.raycastTarget = false;

                    Mask m = CoverContainer.AddComponent<Mask>();
                    m.showMaskGraphic = false;

                    CompletionImage.raycastTarget = false;

                    LayoutElement mainLayout = CoverContainer.GetComponent<LayoutElement>();
                    mainLayout.preferredWidth = NodeWidth;
                    mainLayout.preferredHeight = NodeHeight;

                    RectTransform transform = (RectTransform)RequiresAllImage.transform;
                    transform.sizeDelta = new(NodeWidth / 4f, NodeHeight / 4f);

                    postParse = true;
                    SetupCheckpointLabel();
                    UpdateCover();
                    UpdateProgress();
#if PRINT_DEBUG
                Plugin.Log.Info($"Pos = ({Map.PositionX}, {Map.PositionY}) Node Pos = ({NodeXPos}, {NodeYPos}), Width = {NodeWidth}, Height = {NodeHeight}");
#endif
                }
                catch (Exception e)
                {
                    Plugin.Log.Error(e);
                }
            }

            [UIAction("OnClick")]
            internal async void OnClick()
            {
                AsyncLock.Releaser? locker = await onClickLock.TryLockAsync();

                if (locker is null)
                    return;

                using (locker.Value)
                {
#if NEW_VERSION
                    BeatmapLevel? level = Loader.GetLevelByHash(Hash);
#else
                    IBeatmapLevel? level = (await Loader.BeatmapLevelsModelSO.GetBeatmapLevelAsync(LevelUtils.header + Hash.ToUpper(), CancellationToken.None)).beatmapLevel;
#endif

                    if (level is null)
                    {
                        Plugin.Log.Warn($"Cannot find level by hash \"{Hash}\", downloading...");

                        // Map should be loaded at this point.
                        level = await levelUtils.DownloadSong(serialUtils.GetMapByHash(Hash)!);

                        UpdateMapCovers?.Invoke();

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
            }

            private void SetupCheckpointLabel()
            {
                if (!postParse)
                    return;

                if (string.IsNullOrWhiteSpace(Map.CheckpointLabel))
                {
                    CheckpointText.gameObject.SetActive(false);
                    return;
                }

                CheckpointText.gameObject.SetActive(true);

                // Make sure the text does not affect the stack layout.
                LayoutElement layoutElement = CheckpointText.GetComponent<LayoutElement>();
                layoutElement ??= CheckpointText.gameObject.AddComponent<LayoutElement>();

                layoutElement.ignoreLayout = true;

                CheckpointText.text = Map.CheckpointLabel;
                CheckpointText.color = string.IsNullOrEmpty(Map.CheckpointColor) ? new Color32(99, 102, 241, 255) : Map.CheckpointColor!.Color();

                CheckpointText.alignment = TextAlignmentOptions.Center;
                CheckpointText.enableWordWrapping = false;
                CheckpointText.overflowMode = TextOverflowModes.Overflow;
                CheckpointText.raycastTarget = false;

                RectTransform rectTransform = (RectTransform)CheckpointText.transform;
                rectTransform.SetAsLastSibling();

                rectTransform.localRotation = Quaternion.identity;
                rectTransform.localScale = Vector3.one;

                UpdateCheckpointLabel();
            }
            private void UpdateCheckpointLabel()
            {
                CheckpointText.fontSize = Math.Max(1, Map.CheckpointSize * OffsetData.ScaleFactor);

                RectTransform rectTransform = (RectTransform)CheckpointText.transform;

                float padding = Mathf.Max(2f, NodeWidth * 0.05f);

                switch (Map.CheckpointLabelPosition)
                {
                    case CampaignLabelPosition.UP:
                        rectTransform.anchorMin = new Vector2(0.5f, 1f);
                        rectTransform.anchorMax = new Vector2(0.5f, 1f);
                        rectTransform.pivot = new Vector2(0.5f, 0f);
                        rectTransform.anchoredPosition = new Vector2(0f, padding);
                        break;

                    case CampaignLabelPosition.DOWN:
                        rectTransform.anchorMin = new Vector2(0.5f, 0f);
                        rectTransform.anchorMax = new Vector2(0.5f, 0f);
                        rectTransform.pivot = new Vector2(0.5f, 1f);
                        rectTransform.anchoredPosition = new Vector2(0f, -padding);
                        break;

                    case CampaignLabelPosition.LEFT:
                        rectTransform.anchorMin = new Vector2(0f, 0.5f);
                        rectTransform.anchorMax = new Vector2(0f, 0.5f);
                        rectTransform.pivot = new Vector2(1f, 0.5f);
                        rectTransform.anchoredPosition = new Vector2(-padding, 0f);
                        break;

                    case CampaignLabelPosition.RIGHT:
                        rectTransform.anchorMin = new Vector2(1f, 0.5f);
                        rectTransform.anchorMax = new Vector2(1f, 0.5f);
                        rectTransform.pivot = new Vector2(0f, 0.5f);
                        rectTransform.anchoredPosition = new Vector2(padding, 0f);
                        break;

                    default:
                        CheckpointText.gameObject.SetActive(false);
                        return;
                }

                CheckpointText.ForceMeshUpdate();

                rectTransform.sizeDelta = new Vector2(
                    Mathf.Max(CheckpointText.preferredWidth, CheckpointText.fontSize),
                    Mathf.Max(CheckpointText.preferredHeight, CheckpointText.fontSize)
                );

                Map.Size = rectTransform.rect.size;
            }

            private void OnPluginUpdate(object sender, PropertyChangedEventArgs args)
            {
                if (args.PropertyName.Equals(nameof(PluginConfig.ShowPrereqIndicator)))
                    ShowPrereqIndicator = requiresAllPrereqs && config.ShowPrereqIndicator;
            }
            private void UpdateCover()
            {
                if (!postParse)
                    return;

                if (imageTaskToken is not null)
                {
                    imageTaskToken.Cancel();
                    imageTaskToken.Dispose();
                }

                imageTaskToken = new();

                _ = Map.CheckpointAvatarUrl is null ?
                    CoverImage.LoadCoverImage(Hash, Map.CoverUrl, imageTaskToken.Token) :
                    CoverImage.LoadImage(Map.CheckpointAvatarUrl, imageTaskToken.Token);
            }

            public void UpdateProgress()
            {
                if (!postParse)
                    return;

                CoverImage.DefaultColor = Progress.Completion == CampaignProgress.CompletionStatus.Incomplete ? new(0.25f, 0.25f, 0.25f) : Color.white;

                IsComplete = Progress.Completion == CampaignProgress.CompletionStatus.Complete;

                if (IsComplete)
                    ((RectTransform)CompletionImage.transform).sizeDelta = new(NodeWidth / 4f, NodeHeight / 4f);
            }
            private void OnOffsetDataChanged()
            {
                ((RectTransform)CompletionImage.transform).sizeDelta = new(NodeWidth / 4f, NodeHeight / 4f);
                ((RectTransform)RequiresAllImage.transform).sizeDelta = new(NodeWidth / 4f, NodeHeight / 4f);

                LayoutElement mainLayout = CoverContainer.GetComponent<LayoutElement>();
                mainLayout.preferredWidth = NodeWidth;
                mainLayout.preferredHeight = NodeHeight;

                NotifyPropertyChanged(nameof(NodeWidth));
                NotifyPropertyChanged(nameof(NodeHeight));
                NotifyPropertyChanged(nameof(NodeXPos));
                NotifyPropertyChanged(nameof(NodeYPos));
            }

            public void Dispose()
            {
                OffsetData.OnScaleChanging -= UpdateCheckpointLabel;
                OffsetData.OnScaleChanged -= OnOffsetDataChanged;
                UpdateMapCovers -= UpdateCover;
                config.PropertyChanged -= OnPluginUpdate;

                if (imageTaskToken is not null)
                {
                    imageTaskToken.Cancel();
                    imageTaskToken.Dispose();
                    imageTaskToken = null;
                }

                UnityEngine.Object.Destroy(Container);
            }
        }

        internal class CampaignMapBarrier : IDisposable
        {
            public const float WIDTH = 5f;
            public const float FONT_SIZE = 15f;

            private const float TEXT_MARGIN = 4f;

            // Extra padding around text collision boxes.
            // Increase this if labels are still visually too close.
            private const float TEXT_COLLISION_PADDING = 2f;

            private static readonly List<CampaignMapBarrier> ActiveBarriers = [];
            private static bool resolvingTextCollisions;

            public readonly AccSaberCampaignBarrier Barrier;
            public readonly AccSaberCampaignOffsetData OffsetData;

            private readonly AccSaberCampaignMapViewController parentVC;

            private readonly GameObject obj;
            private readonly GameObject textObj;

            private readonly RectTransform barrierRt;
            private readonly RectTransform textRt;

            private readonly LayoutElement barrierLayout;
            private readonly LayoutElement textLayout;

            private readonly TextMeshProUGUI text;

            // false = Vector3.down end
            // true  = Vector3.up end, aka flipped 180 degrees
            private bool textOnOppositeEnd;

            public string ProgressText => text.text;

            public CampaignProgress.CampaignProgressValue Progress => parentVC.CampaignProgress.PlayerValues[Barrier.Id];

            public Quaternion Rotation
            {
                get => barrierRt.localRotation;
                set
                {
                    barrierRt.localRotation = value;
                    UpdateTextPos();
                }
            }

            public Vector2 SizeDelta
            {
                get => barrierRt.sizeDelta;
                set
                {
                    barrierRt.sizeDelta = value;

                    barrierLayout.preferredWidth = value.x;
                    barrierLayout.preferredHeight = value.y;

                    UpdateTextPos();
                }
            }

            public Vector2 Position => barrierRt.anchoredPosition;

            public CampaignMapBarrier(AccSaberCampaignBarrier barrier, Transform parent, AccSaberCampaignMapViewController parentVC, AccSaberCampaignOffsetData offsetData)
            {
                Barrier = barrier;
                OffsetData = offsetData;
                this.parentVC = parentVC;

                obj = new GameObject("AccSaberCampaignBarrier", typeof(RectTransform));
                obj.transform.SetParent(parent, false);

                barrierRt = (RectTransform)obj.transform;
                SetupManualRectTransform(barrierRt);
                barrierRt.localRotation = Quaternion.identity;

                barrierLayout = obj.AddComponent<LayoutElement>();
                barrierLayout.ignoreLayout = true;

                ClickableImage image = obj.AddComponent<ClickableImage>();
                image.sprite = Utilities.ImageResources.WhitePixel;
                image.material = Utilities.ImageResources.NoGlowMat;
                image.type = Image.Type.Simple;
                image.DefaultColor = barrier.BorderColor?.Color() ?? Color.red;
                image.HighlightColor = (barrier.BorderColor ?? "#F00").BrightenColor(5).Color();
                image.OnClickEvent += OnClick;


#if V41
                text = BeatSaberUI.CreateCurvedUIText(parent as RectTransform, "Hello");
                text.alignment = TextAlignmentOptions.Center;
                text.textWrappingMode = TextWrappingModes.NoWrap;
#else
                text = BeatSaberUI.CreateText(parent as RectTransform, "Hello", Vector2.zero);
                text.alignment = TextAlignmentOptions.Center;
                text.enableWordWrapping = false;
#endif

                textObj = text.gameObject;
                textObj.name = "AccSaberCampaignBarrierText";

                textRt = (RectTransform)textObj.transform;
                SetupManualRectTransform(textRt);
                textRt.localRotation = Quaternion.identity;

                textLayout = textObj.AddComponent<LayoutElement>();
                textLayout.ignoreLayout = true;

                // This is done so the text does not block clicking the barrier/map.
                text.raycastTarget = false;

                // Keep the label visually above the barrier.
                textRt.SetAsLastSibling();

                ActiveBarriers.Add(this);

                OffsetData.OnScaleChanged += OnOffsetDataUpdate;
                OnOffsetDataUpdate();

#if PRINT_DEBUG
        Plugin.Log.Info($"Barrier: Pos = ({barrier.PositionX}, {barrier.PositionY}) Node Pos = ({Position.x}, {Position.y}), Width = {SizeDelta.x}, Height = {SizeDelta.y}");
#endif
            }

            public void UpdateProgress()
            {
                RecalculateFCProgress();
                UpdateText();
                UpdateTextSize();
                UpdateTextPos();
            }
            private void RecalculateFCProgress()
            {
                if (Barrier.ConditionType != BarrierConditionType.FC)
                    return;

                List<float> values = [.. Barrier.AffectedCampaignDifficultyIds
                    .Select(id => parentVC.CampaignProgress.PlayerValues[id])
                    .Where(val => val.Completion == CampaignProgress.CompletionStatus.Complete)
                    .Select(val => val.Progress)];

                CampaignProgress.CampaignProgressValue progess = parentVC.CampaignProgress.PlayerValues[Barrier.Id];
                parentVC.CampaignProgress.PlayerValues[Barrier.Id] = new(values.Count, progess.Completion);
            }

            private static void SetupManualRectTransform(RectTransform rt)
            {
                rt.anchorMin = new(0.5f, 0.5f);
                rt.anchorMax = new(0.5f, 0.5f);
                rt.pivot = new(0.5f, 0.5f);
                rt.localScale = Vector3.one;
            }

            private void OnOffsetDataUpdate()
            {
                barrierRt.anchoredPosition = new Vector2(
                    Barrier.PositionX * OffsetData.OffsetSize + OffsetData.Offset.x,
                    -Barrier.PositionY * OffsetData.OffsetSize - OffsetData.Offset.y
                );

                SizeDelta = new Vector2(
                    WIDTH * OffsetData.ScaleFactor,
                    Barrier.Scale * OffsetData.ScaleFactor
                );

                text.fontSize = FONT_SIZE * OffsetData.ScaleFactor;

                UpdateProgress();
            }

            private void UpdateTextSize()
            {
                text.ForceMeshUpdate();

                // Use a large available area instead of infinity because some TMP/Unity versions
                // behave oddly with Mathf.Infinity.
                Vector2 preferredSize = text.GetPreferredValues(text.text, 10000f, 10000f);

                float padding = 2f * OffsetData.ScaleFactor;
                preferredSize.x += padding;
                preferredSize.y += padding;

                textLayout.preferredWidth = preferredSize.x;
                textLayout.preferredHeight = preferredSize.y;

                textRt.sizeDelta = preferredSize;
            }

            private void UpdateTextPos() => UpdateTextPos(resolveCollisions: true);

            private void UpdateTextPos(bool resolveCollisions)
            {
                if (textRt is null || barrierRt is null)
                    return;

                ApplyTextPosition();

                if (resolveCollisions && !resolvingTextCollisions)
                    ResolveTextCollisions();
            }

            private void ApplyTextPosition()
            {
                Vector2 textSize = textRt.sizeDelta;

                // Default end is down. Opposite end is up.
                // This flips the label placement direction 180 degrees without rotating the text itself.
                Vector3 localEndDirection = textOnOppositeEnd ? Vector3.up : Vector3.down;

                Vector3 endDirection3D = barrierRt.localRotation * localEndDirection;
                Vector2 endDirection = new(endDirection3D.x, endDirection3D.y);

                if (endDirection.sqrMagnitude < 0.0001f)
                {
                    endDirection = textOnOppositeEnd ? Vector2.up : Vector2.down;
                }
                else
                {
                    endDirection.Normalize();
                }

                float barrierHalfLength = SizeDelta.y * 0.5f;

                // The text remains unrotated/upright.
                // Because of that, we calculate the axis-aligned half extent of the text
                // in the direction we are moving it.
                float textHalfExtentInDirection =
                    Mathf.Abs(endDirection.x) * textSize.x * 0.5f +
                    Mathf.Abs(endDirection.y) * textSize.y * 0.5f;

                float margin = TEXT_MARGIN * OffsetData.ScaleFactor;

                textRt.anchoredPosition =
                    barrierRt.anchoredPosition +
                    endDirection * (barrierHalfLength + textHalfExtentInDirection + margin);

                // Keep the text readable.
                textRt.localRotation = Quaternion.identity;

                // Keep it above the barrier in the hierarchy.
                textRt.SetAsLastSibling();
            }

            private static void ResolveTextCollisions()
            {
                if (resolvingTextCollisions)
                    return;

                resolvingTextCollisions = true;

                try
                {
                    ActiveBarriers.RemoveAll(barrier => !IsValidBarrier(barrier));

                    if (ActiveBarriers.Count <= 1)
                        return;

                    // Start from a deterministic layout:
                    // every label goes back to its default end first.
                    foreach (CampaignMapBarrier barrier in ActiveBarriers)
                    {
                        barrier.textOnOppositeEnd = false;
                        barrier.UpdateTextPos(resolveCollisions: false);
                    }

                    // Iteratively resolve because flipping one label can create a new collision
                    // with another label that was checked earlier.
                    int maxIterations = ActiveBarriers.Count + 1;

                    for (int iteration = 0; iteration < maxIterations; iteration++)
                    {
                        bool changedSomething = false;

                        for (int i = 0; i < ActiveBarriers.Count; i++)
                        {
                            CampaignMapBarrier a = ActiveBarriers[i];

                            for (int j = i + 1; j < ActiveBarriers.Count; j++)
                            {
                                CampaignMapBarrier b = ActiveBarriers[j];

                                if (!TextRectsOverlap(a, b))
                                    continue;

                                // Collision found.
                                // Move both colliding labels to the other end of their barriers.
                                if (!a.textOnOppositeEnd)
                                {
                                    a.textOnOppositeEnd = true;
                                    a.UpdateTextPos(resolveCollisions: false);
                                    changedSomething = true;
                                }

                                if (!b.textOnOppositeEnd)
                                {
                                    b.textOnOppositeEnd = true;
                                    b.UpdateTextPos(resolveCollisions: false);
                                    changedSomething = true;
                                }
                            }
                        }

                        if (!changedSomething)
                            break;
                    }

                    foreach (CampaignMapBarrier barrier in ActiveBarriers)
                        barrier.textRt.SetAsLastSibling();
                }
                finally
                {
                    resolvingTextCollisions = false;
                }
            }

            private static bool IsValidBarrier(CampaignMapBarrier barrier)
            {
                return barrier is not null &&
                       barrier.obj is not null &&
                       barrier.textObj is not null &&
                       barrier.barrierRt is not null &&
                       barrier.textRt is not null;
            }

            private static bool TextRectsOverlap(CampaignMapBarrier a, CampaignMapBarrier b)
            {
                Rect rectA = GetTextRect(a);
                Rect rectB = GetTextRect(b);

                return rectA.Overlaps(rectB);
            }

            private static Rect GetTextRect(CampaignMapBarrier barrier)
            {
                Vector2 pos = barrier.textRt.anchoredPosition;
                Vector2 size = barrier.textRt.sizeDelta;

                float padding = TEXT_COLLISION_PADDING * barrier.OffsetData.ScaleFactor;

                return new Rect(
                    pos.x - size.x * 0.5f - padding,
                    pos.y - size.y * 0.5f - padding,
                    size.x + padding * 2f,
                    size.y + padding * 2f
                );
            }

            private void UpdateText()
            {
                string value = Barrier.ConditionType switch
                {
                    BarrierConditionType.AVERAGE_ACC =>
                        $"Average Accuracy\n<color={ColorUtils.LEVEL}>{Progress.Progress * 100f:N2}%</color> / <color={ColorUtils.LEVEL}>{Barrier.ConditionValue * 100f:N2}%</color>",

                    BarrierConditionType.AVERAGE_AP =>
                        $"Average Ap\n<color={ColorUtils.AP}>{Progress.Progress:0.##} ap</color> / <color={ColorUtils.AP}>{Barrier.ConditionValue:0.##} ap</color>",

                    BarrierConditionType.AP_MAX =>
                        $"Max Ap\n<color={ColorUtils.AP}>{Progress.Progress:0.##} ap</color> / <color={ColorUtils.AP}>{Barrier.ConditionValue:0.##} ap</color>",

                    BarrierConditionType.ACC_MAX =>
                        $"Max Accuracy\n<color={ColorUtils.LEVEL}>{Progress.Progress * 100f:N2}%</color> / <color={ColorUtils.LEVEL}>{Barrier.ConditionValue * 100f:N2}%</color>",

                    BarrierConditionType.STREAK_115_AVERAGE =>
                        $"Average streak\n<color={ColorUtils.TECH}>{Progress.Progress:N0}x</color> / <color={ColorUtils.TECH}>{Barrier.ConditionValue:N0}x streak</color>",

                    BarrierConditionType.STREAK_115_MAX =>
                        $"Max streak\n<color={ColorUtils.TECH}>{Progress.Progress:N0}x</color> / <color={ColorUtils.TECH}>{Barrier.ConditionValue:N0}x streak</color>",

                    BarrierConditionType.FC =>
                        $"FC maps\n<color={ColorUtils.RELOADED}>{Progress.Progress:N0}</color> / <color={ColorUtils.RELOADED}>{Barrier.AffectedCampaignDifficultyIds.Count:N0}</color>",

                    BarrierConditionType.AVERAGE_RANK =>
                        $"Average Rank\n<color={ColorUtils.RANK}>#{Progress.Progress:0.##}</color> / <color={ColorUtils.RANK}>#{Barrier.ConditionValue:0.##}</color>",

                    BarrierConditionType.MAX_RANK =>
                        $"Max Rank\n<color={ColorUtils.RANK}>#{Progress.Progress:N0}</color> / <color={ColorUtils.RANK}>#{Barrier.ConditionValue:N0}</color>",

                    BarrierConditionType.COMPLETION_COUNT =>
                        $"Nodes Completed\n<color={ColorUtils.GLOBAL}>{Progress.Progress:N0}</color> / <color={ColorUtils.GLOBAL}>{Barrier.ConditionValue:N0}</color>",

                    BarrierConditionType.PASS =>
                        $"Nodes Passed\n<color={ColorUtils.RELOADED}>{Progress.Progress:N0}</color> / <color={ColorUtils.RELOADED}>{Barrier.AffectedCampaignDifficultyIds.Count:N0}</color>",

                    _ => "Unknown type"
                };

                text.text = value;
            }

            private void OnClick(PointerEventData data)
            {
                parentVC.acvc.SetBarrierInfo(this, Progress);
            }

            public void Dispose()
            {
                OffsetData.OnScaleChanged -= OnOffsetDataUpdate;

                ActiveBarriers.Remove(this);

                UnityEngine.Object.Destroy(obj);
                UnityEngine.Object.Destroy(textObj);
            }
        }

        internal class CampaignMapText : Utils.Safety.SafeNotifyPropertyChanged, IDisposable
        {
            public readonly AccSaberCampaignText Text;
            public readonly AccSaberCampaignOffsetData OffsetData;

            private readonly TextMeshProUGUI TextObj;

            public Vector2 Position { get; private set; }
            public Vector2 RenderedSize
            {
                get
                {
#if NEW_VERSION
                    TextObj.ForceMeshUpdate(true, true);
#else
                    TextObj.ForceMeshUpdate(true);
#endif
                    return TextObj.GetRenderedValues(false);
                }
            }
            public Vector2 Size
            {
                get
                {
                    Rect rect = TextObj.rectTransform.rect;
                    return rect.size;
                }
            }

            public Rect GetRect()
            {
                Vector2 size = Size;

                Vector2 min = Position - size * 0.5f;
                Vector2 max = Position + size * 0.5f;

                return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }

            public CampaignMapText(AccSaberCampaignText text, RectTransform parent, AccSaberCampaignOffsetData offsetData)
            {
                Text = text;
                OffsetData = offsetData;

                TextObj = BeatSaberUI.CreateText(parent, ParseGivenContent(text.Content), Vector2.zero);

                RectTransform rt = TextObj.rectTransform;

                // Since map coordinates are centered on the parent plane, use center anchors.
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);

                // This makes anchoredPosition refer to the center of the text rect.
                rt.pivot = new Vector2(0.5f, 0.5f);

                // Center the actual text inside the RectTransform.
                TextObj.alignment = TextAlignmentOptions.Center;

                TextObj.raycastTarget = false;

                if (!TextObj.TryGetComponent(out LayoutElement le))
                    le = TextObj.gameObject.AddComponent<LayoutElement>();

                le.ignoreLayout = true;

                ApplyOffsetData();

                OffsetData.OnScaleChanging += ApplyOffsetData;
            }

            private void ApplyOffsetData()
            {
                UpdateTextSize();

                Position = new(Text.PositionX * OffsetData.OffsetSize + OffsetData.Offset.x, -Text.PositionY * OffsetData.OffsetSize - OffsetData.Offset.y);
                //Position = new Vector2(Text.PositionX * OffsetData.OffsetSize, -Text.PositionY * OffsetData.OffsetSize) + OffsetData.Offset;

                TextObj.rectTransform.anchoredPosition = Position;
            }
            private void UpdateTextSize()
            {
                TextObj.fontSize = Text.Scale * 15f * OffsetData.ScaleFactor;

                ResizeTextToFit(240f * Text.Scale * OffsetData.ScaleFactor);

                Canvas.ForceUpdateCanvases();
#if NEW_VERSION
                TextObj.ForceMeshUpdate(true, true);
#else
                TextObj.ForceMeshUpdate(true);
#endif

                Vector2 rectSize = TextObj.rectTransform.rect.size;

                // Extra safety padding. TMP glyphs can slightly exceed calculated bounds.
                rectSize += new Vector2(4f, 4f);

                Text.Size = rectSize;
            }

            private void ResizeTextToFit(float maxWidth = 0f)
            {
                RectTransform rt = TextObj.rectTransform;

                bool useWrapping = maxWidth > 0f;

                TextObj.enableWordWrapping = useWrapping;
                TextObj.overflowMode = TextOverflowModes.Overflow;

#if NEW_VERSION
                TextObj.ForceMeshUpdate(true, true);
#else
                TextObj.ForceMeshUpdate(true);
#endif

                Vector2 preferredSize;

                if (useWrapping)
                {
                    preferredSize = TextObj.GetPreferredValues(TextObj.text, maxWidth, Mathf.Infinity);

                    preferredSize.x = Mathf.Min(preferredSize.x, maxWidth);
                }
                else
                {
                    preferredSize = TextObj.GetPreferredValues(TextObj.text, Mathf.Infinity, Mathf.Infinity);
                }

                const float padding = 4f;

                preferredSize.x = Mathf.Ceil(preferredSize.x + padding);
                preferredSize.y = Mathf.Ceil(preferredSize.y + padding);

                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, preferredSize.x);
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, preferredSize.y);

#if NEW_VERSION
                TextObj.ForceMeshUpdate(true, true);
#else
                TextObj.ForceMeshUpdate(true);
#endif
            }

            public void Dispose()
            {
                OffsetData.OnScaleChanging -= ApplyOffsetData;

                if (TextObj is not null)
                    UnityEngine.Object.Destroy(TextObj.gameObject);
            }

            private static readonly Regex TagMatcher = new(@"</?(?'name'[\w_]+) ?(?'content'[^>]+)?>");
            private static readonly Regex ContentMatcher = new(@"(?'tag'[\w_]+)=(?'value'\d+|\w+|""[^""]+"")");

            private static string ParseGivenContent(string givenStr)
            {
                MatchCollection mc = TagMatcher.Matches(givenStr);
                Queue<(string oldStr, string newStr)> toReplace = [];
                Stack<(string oldStr, string newStr)> closingTagReplacement = [];

                foreach (Match m in mc)
                {
                    string tag = m.Groups["name"].Value;

                    if (m.Value[1] == '/')
                    {
                        Stack<(string oldStr, string newStr)> temp = [];

                        while (closingTagReplacement.Count > 0)
                        {
                            if (closingTagReplacement.Peek().oldStr.Equals(m.Value))
                                break;

                            temp.Push(closingTagReplacement.Pop());
                        }

                        if (closingTagReplacement.Count == 0)
                            Plugin.Log.Warn("There was an error parsing the text content!\n" + givenStr);
                        else
                            toReplace.Enqueue(closingTagReplacement.Pop());

                        while (temp.Count > 0)
                            closingTagReplacement.Push(temp.Pop());

                        continue;
                    }

                    Dictionary<string, string> values = m.Groups["content"].Success
                        ? [with(ContentMatcher.Matches(m.Groups["content"].Value)
#if !NEW_VERSION
                        .Cast<Match>()
#endif
                        .Select(m => new KeyValuePair<string, string>(
                            m.Groups["tag"].Value,
                            m.Groups["value"].Value)))]
                            : [];

                    if (tag.Length < 2)
                        continue;

                    bool failed = true;

                    switch (tag)
                    {
                        case "span":
                            if (values.TryGetValue("style", out string content))
                            {
                                failed = false;

                                if (content[1..].StartsWith("color:rgb("))
                                {
                                    int current = 11, set = 0;
                                    byte[] rgbVals = new byte[3];

                                    for (int i = 0; i < 3; ++i, ++set)
                                    {
                                        while (current < content.Length && !char.IsDigit(content[current]))
                                            ++current;

                                        if (current >= content.Length)
                                        {
                                            Plugin.Log.Warn("Cannot parse the given content: " + content);
                                            break;
                                        }

                                        int len = 0;

                                        while (current < content.Length && char.IsDigit(content[current]))
                                        {
                                            ++len;
                                            ++current;
                                        }

                                        rgbVals[i] = byte.Parse(content[(current - len)..current]);
                                    }

                                    if (set < 3)
                                        break;

                                    toReplace.Enqueue((m.Value, $"<color={new Color32(rgbVals[0], rgbVals[1], rgbVals[2], 255).Color()}>"));
                                    closingTagReplacement.Push(("</span>", "</color>"));
                                }
                            }

                            break;
                    }

                    if (failed)
                    {
                        toReplace.Enqueue((m.Value, ""));
                        closingTagReplacement.Push(($"</{tag}>", ""));
                    }
                }

                while (closingTagReplacement.Count > 0)
                    toReplace.Enqueue(closingTagReplacement.Pop());

                while (toReplace.Count > 0)
                {
                    var (oldStr, newStr) = toReplace.Dequeue();
                    givenStr = givenStr.ReplaceFirst(oldStr, newStr);
                }

                return givenStr;
            }
        }
    }

    public static class NodeShapeTextures
    {
        private static readonly ConcurrentDictionary<string, Sprite> _borderSpriteCache = [];
        private static readonly ConcurrentDictionary<string, Sprite> _fillSpriteCache = [];

        private static bool _preloadedSprites = false;

        public static async Task PreloadStandardSprites(int maxCoverageLoads)
        {
            if (_preloadedSprites)
                return;
            _preloadedSprites = true;

            NodeShape[] shapes = (NodeShape[])Enum.GetValues(typeof(NodeShape));

            IEnumerator LoadSlowly()
            {
                foreach (NodeShape shape in shapes)
                {
                    yield return GetBorderSprite(shape, maxCoverageLoads).WaitWithRoutine();
                    yield return null;

                    yield return GetFillSprite(shape, maxCoverageLoads).WaitWithRoutine();
                    yield return null;
                }
            }

            await Coroutines.AsTask(LoadSlowly());
        }

        public static async Task<Sprite> GetBorderSprite(NodeShape shape, int maxCoverageLoads, int size = 256, int borderPixels = 10)
        {
            string key = $"{shape}_{size}_{borderPixels}";

            if (_borderSpriteCache.TryGetValue(key, out Sprite cached))
                return cached;

            Texture2D texture = await CreateBorderTexture(shape, size, borderPixels, maxCoverageLoads);
            Sprite sprite = CreateSprite(texture);

            _borderSpriteCache[key] = sprite;
            return sprite;
        }

        private static async Task<Texture2D> CreateBorderTexture(NodeShape shape, int size, int borderPixels, int maxCoverageLoads)
        {
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color32[] pixels = new Color32[size * size];

            float innerScale = 1f - borderPixels * 2f / size;
            innerScale = Mathf.Clamp01(innerScale);

            IEnumerator LoadSlowly() 
            {
                int coverages = 0;

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float outerCoverage = GetCoverage(x, y, size, shape, 1f);
                        float innerCoverage = GetCoverage(x, y, size, shape, innerScale);

                        float alpha = Mathf.Clamp01(outerCoverage - innerCoverage);

                        byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                        pixels[y * size + x] = new Color32(255, 255, 255, a);

                        coverages += 2;

                        if (coverages >= maxCoverageLoads)
                        {
                            coverages = 0;
                            yield return null;
                        }
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, true);
            }

            await Coroutines.AsTask(LoadSlowly());

            return texture;
        }

        public static async Task<Sprite> GetFillSprite(NodeShape shape, int maxCoverageLoads, int size = 256)
        {
            string key = $"fill_{shape}_{size}";

            if (_fillSpriteCache.TryGetValue(key, out Sprite cached))
                return cached;

            Texture2D texture = await CreateFillTexture(shape, size, maxCoverageLoads);
            Sprite sprite = CreateSprite(texture);

            _fillSpriteCache[key] = sprite;
            return sprite;
        }
        private static async Task<Texture2D> CreateFillTexture(NodeShape shape, int size, int maxCoverageLoads)
        {
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color32[] pixels = new Color32[size * size];

            IEnumerator LoadSlowly()
            {
                int coverages = 0;

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float coverage = GetCoverage(x, y, size, shape, 1f);
                        byte a = (byte)Mathf.RoundToInt(coverage * 255f);

                        pixels[y * size + x] = new Color32(255, 255, 255, a);

                        ++coverages;

                        if (coverages >= maxCoverageLoads)
                        {
                            coverages = 0;
                            yield return null;
                        }
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, true);
            }

            await Coroutines.AsTask(LoadSlowly());

            return texture;
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

        public enum NodeShape
        {
            Square,
            Circle,
            Diamond,
            Hexagon
        }
    }
}