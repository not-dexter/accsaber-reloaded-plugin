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
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zenject;
using static AccSaber.Models.CampaignModel;
using static AccSaber.UI.MenuButton.Campaigns.ViewControllers.AccSaberCampaignMapViewController;
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
        private bool disposed = false;

        private readonly API.Throttler fetchThrottler = new(1, 60);
        private readonly AsyncLock setCampaignLock = new();

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
        private CampaignMapBackground? campaignMapBackground;
        private readonly List<(Guid fromNode, Guid toNode, UIArrow arrow)> mapNodeArrows = [];
        private ScrollRect scrollRect = null!;
        private Color currentBgColor, maxBgColors;
        private Action? UpdateArrowClipping;
        private Task setCampaignTask = Task.CompletedTask;
        private CancellationTokenSource? setCampaignCts;

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
            setCampaignCts?.Cancel();
            ClearDisplay();
        }
        public async Task Cleanup()
        {
            setCampaignCts?.Cancel();

            await setCampaignTask;

            ClearDisplay();
        }

        public async Task SetCampaign(AccSaberCampaign campaign, float scaleFactor = -1f, bool resetScrollbars = true)
        {
            setCampaignCts?.Cancel();

            await setCampaignTask;

            setCampaignCts = new();

            AsyncLock.Releaser locker = await setCampaignLock.LockAsync(setCampaignCts.Token);

            setCampaignTask = UnityMainThreadTaskScheduler.Factory.StartNew(async () =>
            {
                try
                {
                    await SetCampaignInternal(campaign, scaleFactor, resetScrollbars, setCampaignCts.Token);
                }
                catch (Exception e)
                {
                    Plugin.Log.Error(e);
                }
                finally
                {
                    setCampaignCts?.Dispose();
                    setCampaignCts = null;

                    locker.Dispose();
                }
            }, setCampaignCts.Token).Unwrap();
        }
        private async Task SetCampaignInternal(AccSaberCampaign campaign, float scaleFactor, bool resetScrollbars, CancellationToken ct)
        {
            try
            {
                if (!parsed || campaign.Difficulties is null)
                    return;

                if (scaleFactor <= 0f)
                    scaleFactor = config.CampaignDefaultZoomValue;

                ClearDisplay();

                CurrentCampaign = campaign;

                Task<CampaignProgress> campaignProgressTask = UnityMainThreadTaskScheduler.Factory.StartNew(() => store.GetCampaignProgress(campaign), ct).Unwrap();
                Task preloadSpritesTask = PreloadStandardSprites(config.CampaignMaxCoverageLoadsPerFrame);

                List<IAccSaberCampaignScalable> scalableObjs = [.. MiscUtils.CombineAllAsType<IAccSaberCampaignScalable>(campaign.Difficulties, campaign.Barriers ?? [], campaign.Texts ?? [])];

                if (campaign.BackgroundSizeInfo is not null)
                    scalableObjs.Add(campaign.BackgroundSizeInfo);

                CurrentOffsetData = new(scaleFactor, scalableObjs);

                UpdateContainerValues(resetScrollbars);

                Task bgUrlTask = Task.CompletedTask;
                if (campaign.BackgroundUrl is not null)
                    bgUrlTask = MiscUtils.GetImage(campaign.BackgroundUrl); // this will allow for the bg image to be cached.

                CampaignProgress = await campaignProgressTask;

                HandleCheckpointCollisions();

                int loads = 0;

                IEnumerator LoadSlowly()
                {
#if PRINT_DEBUG && DEBUG
                    int expectedLoads = (campaign.Texts?.Count ?? 0) + (campaign.Barriers?.Count ?? 0) + campaign.Difficulties.Count;
                    Plugin.Log.Info($"There are {expectedLoads} loads expected that will take a total of {expectedLoads / config.CampaignMaxObjectLoadsPerFrame} frames.");
                    int frames = 0;
#endif

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

#if PRINT_DEBUG && DEBUG
                                ++frames;
#endif

                                if (ct.IsCancellationRequested)
                                    yield break;
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

#if PRINT_DEBUG && DEBUG
                                ++frames;
#endif

                                if (ct.IsCancellationRequested)
                                    yield break;
                            }
                        }
                    }

                    foreach (AccSaberCampaignMap map in campaign.Difficulties)
                    {
                        AccSaberBasicDifficulty? diff = null;
                        
                        yield return serialHandler.GetDiffByIdAsync(map.MapDifficultyId, loads, true).WaitWithRoutine(info => { diff = info.Diff; loads = info.Loads; });

                        if (loads == -1)
                        {
                            loads = 0;

                            if (ct.IsCancellationRequested)
                                yield break;
                        }

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

                        ++loads;

                        if (loads >= config.CampaignMaxObjectLoadsPerFrame)
                        {
                            loads = 0;
                            yield return null;

#if PRINT_DEBUG && DEBUG
                            ++frames;
#endif

                            if (ct.IsCancellationRequested)
                                yield break;
                        }
                    }

#if PRINT_DEBUG && DEBUG
                    Plugin.Log.Info($"There was a total of {frames} frames awaited.");
#endif
                }

                await preloadSpritesTask;

                if (disposed || ct.IsCancellationRequested) // Need to check after every await as a decent amount of time can pass in each Task.
                    return;

                await Coroutines.AsTask(LoadSlowly());

                if (disposed || ct.IsCancellationRequested) 
                    return;

                await RebuildArrows(loads, ct);

                if (disposed || ct.IsCancellationRequested)
                    return;

                await bgUrlTask;

                if (disposed || ct.IsCancellationRequested)
                    return;

                if (campaign.BackgroundSizeInfo is not null && campaign.BackgroundUrl is not null)
                    campaignMapBackground = new(NodeContainer.transform, campaign.BackgroundSizeInfo, CurrentOffsetData, campaign.BackgroundUrl);

                CurrentOffsetData.RecalculateValues();
                UpdateContainerValues(resetScrollbars);

                if (ct.IsCancellationRequested)
                    return;

                if (!ScrollToFirstValidNode(CampaignProgress.Nodes.Heads.Select(node => node.Current.Id)))
                {
                    Plugin.Log.Warn("There are no valid starting nodes, this is a wacky campaign.");
                    Plugin.Log.Debug($"Heads: {CampaignProgress.Nodes.Heads.Print()}");
                }
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

#if PRINT_DEBUG && DEBUG
                Plugin.Log.Info($"There are {ids.Count} milestones with that name \"{key}\".");
#endif

                float xPos = 0, yPos = 0;

                foreach (Guid id in ids)
                {
                    AccSaberCampaignMap map = usedMaps[id];

                    xPos += map.PositionX;
                    yPos += map.PositionY;
                }

                float averageXPos = xPos / ids.Count, averageYPos = yPos / ids.Count;
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

            if (CurrentCampaign is null || (CurrentCampaign.BackgroundColor is null && CurrentCampaign.BackgroundUrl is null) || CurrentCampaign.BackgroundSizeInfo is not null)
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

            foreach (var (_, _, arrow) in mapNodeArrows)
                arrow.Dispose();

            if (CurrentOffsetData is not null && UpdateArrowClipping is not null)
            {
                CurrentOffsetData.OnScaleChanged -= UpdateArrowClipping;
                UpdateArrowClipping = null;
            }

            campaignMapBackground?.Dispose();
            campaignMapBackground = null;

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

            SetArrowColors();
        }
        public bool ScrollToNode(Guid nodeId, bool printWarning = true)
        {
            if (!parsed)
            {
                if (printWarning)
                    Plugin.Log.Warn("Cannot scroll to node before the map is loaded!");
                return false;
            }

            CampaignMapNode? node = campaignMapNodes.FirstOrDefault(node => node.Map.Id == nodeId);

            if (node is null)
            {
                if (printWarning)
                    Plugin.Log.Warn($"No node of id \"{nodeId}\" found.");
                return false;
            }

            Vector2 viewSize = new(ViewportWidth, ViewportHeight);
            Vector2 actualSize = scrollRect.content.sizeDelta;
            Vector2 trueNodePos = new(node.NodeXPos + actualSize.x / 2f, node.NodeYPos + actualSize.y / 2f);

#if PRINT_DEBUG && DEBUG
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
#if PRINT_DEBUG && DEBUG
            Plugin.Log.Info($"Final scroll percent = ({scrollRect.horizontalScrollbar.value * 100f:N2}%, {scrollRect.verticalScrollbar.value * 100f:N2}%)");
#endif

            return true;
        }
        public bool ScrollToFirstValidNode(IEnumerable<Guid> ids)
        {
            foreach (Guid id in ids)
                if (ScrollToNode(id, false))
                    return true;

            return false;
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
        private void SetArrowColors()
        {
            List<(Guid fromNode, Guid toNode, UIArrow arrow)> listCopy = [with(mapNodeArrows)];

            foreach (IAccSaberCampaignPrereq node in 
                campaignMapNodes.Select(node => (IAccSaberCampaignPrereq)node.Map).Concat(campaignMapBarriers.Select(node => (IAccSaberCampaignPrereq)node.Barrier)))
            {
                int searchAmount = node.PrerequisiteInfos.Count;

                for (int i = listCopy.Count - 1; i >= 0; --i)
                {
                    var (fromNode, toNode, arrow) = listCopy[i];

                    if (toNode != node.Id)
                        continue;

                    arrow.Color = GetPrereqColor(node.PrerequisiteInfos.First(prereq => prereq.Id == fromNode));
                    
                    --searchAmount;
                    listCopy.RemoveAt(i);

                    if (searchAmount <= 0)
                        break;
                }
            }
        }
        private async Task RebuildArrows(int loads = 0, CancellationToken ct = default)
        {
            foreach (var (_, _, arrow) in mapNodeArrows)
                arrow.Dispose();

            mapNodeArrows.Clear();

            Dictionary<Guid, IndependentPositionData> knownPositions = [];
            Queue<(AccSaberCampaignPrereqInfo prereq, Guid toNode)> neededPositions = [];

            if (ct.IsCancellationRequested)
                return;

            IEnumerator LoadSlowly()
            {
                IEnumerator HandleArrows(IAccSaberCampaignPrereq node, IndependentPositionData current)
                {
                    if (!knownPositions.TryAdd(node.Id, current))
                    {
                        Plugin.Log.Warn($"Duplicate campaign item id: {node.Id}");
                        yield break;
                    }

                    foreach (AccSaberCampaignPrereqInfo prereq in node.PrerequisiteInfos)
                    {
                        if (knownPositions.TryGetValue(prereq.Id, out IndependentPositionData from))
                        {
                            UIArrow arrow = new(NodeContainer.transform, from, current, GetPrereqColor(prereq));

                            arrow.CreateOrGetArrow()?.transform.SetAsFirstSibling();
                            mapNodeArrows.Add((prereq.Id, node.Id, arrow));
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

                            if (ct.IsCancellationRequested)
                                yield break;
                        }
                    }
                }

                foreach (CampaignMapBarrier barrier in campaignMapBarriers)
                    yield return HandleArrows(barrier.Barrier, new IndependentPositionData(barrier));

                foreach (CampaignMapNode node in campaignMapNodes)
                    yield return HandleArrows(node.Map, new IndependentPositionData(node));

                while (neededPositions.Count > 0)
                {
                    var (prereq, toNode) = neededPositions.Dequeue();

                    if (knownPositions.TryGetValue(prereq.Id, out IndependentPositionData from) &&
                        knownPositions.TryGetValue(toNode, out IndependentPositionData to))
                    {
                        UIArrow arrow = new(NodeContainer.transform, from, to, GetPrereqColor(prereq));

                        arrow.CreateOrGetArrow()?.transform.SetAsFirstSibling();
                        mapNodeArrows.Add((prereq.Id, toNode, arrow));
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

                        if (ct.IsCancellationRequested)
                            yield break;
                    }
                }
            }

            await Coroutines.AsTask(LoadSlowly());

            if (ct.IsCancellationRequested)
                return;

            UpdateArrowClipping = () => UpdateBarrierRotationsAndArrowClipping([with(knownPositions.Select(kvp => new KeyValuePair<Guid, PositionData>(kvp.Key, kvp.Value)))]);

            CurrentOffsetData?.OnScaleChanged += UpdateArrowClipping;

            UpdateArrowClipping();
        }
        private Color GetPrereqColor(AccSaberCampaignPrereqInfo prereq) =>
            (CampaignProgress.CompletedItems.Contains(prereq.Id) ? prereq.Color : prereq.DimmedColor).Color();
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

            foreach (var (fromNode, toNode, arrow) in mapNodeArrows)
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

                if (UIArrow.TryGetClippedArrowPoints(
                        fromPositionData,
                        fromRotation,
                        toPositionData,
                        toRotation,
                        out Vector2 newStart,
                        out Vector2 newEnd,
                        padding: 0f))
                {
                    UpdateExistingArrow(arrow!, newStart, newEnd);
                }
            }
        }
        public async Task<CampaignProgress.CampaignProgressValue?> MarkNodeAsComplete(Guid id, CampaignProgress.CampaignTargetProgess[] progress)
        {
            if (CurrentCampaign is null || CurrentCampaign.Difficulties is null)
            {
                Plugin.Log.Warn($"Cannot mark node \"{id}\" as complete as the current campaign is null!");
                Plugin.Log.Debug($"CurrentCampaign null? {CurrentCampaign is null}, CurrentCampaign.Difficulties null? {CurrentCampaign?.Difficulties is null}");
                return null;
            }

            HashSet<Guid> mapsToUpdate = [.. CampaignProgress.MarkStatusAndUpdateNode(id, progress, CampaignProgress.CompletionStatus.Complete), id];

            if (campaignMapBarriers.Any(node => mapsToUpdate.Contains(node.Barrier.Id)))
                await FetchProgress();

            UpdateNodes(mapsToUpdate);

            return CampaignProgress.PlayerValues[id];
        }
        public void UpdateNodes(HashSet<Guid> mapsToUpdate)
        {
            foreach (CampaignMapBarrier node in campaignMapBarriers)
                if (mapsToUpdate.Contains(node.Barrier.Id))
                    node.UpdateProgress();

            foreach (CampaignMapNode node in campaignMapNodes)
                if (mapsToUpdate.Contains(node.Map.Id))
                    node.UpdateProgress();
        }
        private async Task FetchProgress()
        {
            AccSaberCampaign? current = CurrentCampaign;

            await acvc.WaitForServerUpdate();

            if (CurrentCampaign is null || !CurrentCampaign.Equals(current) || !acvc.InCampaign)
                return;

            if (await fetchThrottler.TryCall())
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
        
        public record struct PositionData(Vector2 Position, Vector2 Size, NodeShape Shape)
        {
            internal PositionData(CampaignMapNode node) : this(new(node.NodeXPos, node.NodeYPos), new(node.NodeWidth, node.NodeHeight), node.Shape) { }
            internal PositionData(CampaignMapBarrier node) : this(node.Position, node.SizeDelta, NodeShape.Square) { }
        }
        public record class IndependentPositionData
        {
            public Func<AccSaberCampaignOffsetData, Vector2> PositionFunc { get; init; }
            public Func<AccSaberCampaignOffsetData, Vector2> SizeFunc { get; init; }
            public NodeShape Shape { get; init; }
            public AccSaberCampaignOffsetData PositionOffset { get; init; }

            internal IndependentPositionData(CampaignMapNode node)
            {
                //PositionFunc = offset => new(node.Map.PositionX * offset.OffsetSize + offset.Offset.x, -node.Map.PositionY * offset.OffsetSize - offset.Offset.y);
                //SizeFunc = offset => new(node.Map.Scale * offset.ScaleFactor, node.Map.Scale * offset.ScaleFactor);
                PositionFunc = offset => new(node.NodeXPos, node.NodeYPos);
                SizeFunc = offset => new(node.NodeWidth, node.NodeHeight);
                Shape = node.Shape;
                PositionOffset = node.OffsetData;
            }
            internal IndependentPositionData(CampaignMapBarrier node)
            {
                //PositionFunc = offset => new(node.Barrier.PositionX * offset.OffsetSize + offset.Offset.x, -node.Barrier.PositionY * offset.OffsetSize - offset.Offset.y);
                //SizeFunc = offset => new(CampaignMapBarrier.WIDTH * offset.ScaleFactor, node.Barrier.Scale * offset.ScaleFactor);
                PositionFunc = offset => node.Position;
                SizeFunc = offset => node.SizeDelta;
                Shape = NodeShape.Square;
                PositionOffset = node.OffsetData;
            }

            public DependentPositionData ToDependentPositionData() => new(this);
            public PositionData ToPositionData() =>
                new(PositionFunc(PositionOffset), SizeFunc(PositionOffset), Shape);

            public static implicit operator DependentPositionData(IndependentPositionData posData) => posData.ToDependentPositionData();
            public static implicit operator PositionData(IndependentPositionData posData) => posData.ToPositionData();
        }
        public class DependentPositionData
        {
            private readonly IndependentPositionData parent;

            private Vector2 position, size;
            
            public float Scale { get; private set; }

            public readonly NodeShape Shape;

            public event Action<PositionData>? OnPositionDataUpdate;
            public event Action OnParentUpdate
            {
                add => parent.PositionOffset.OnScaleChanged += value;
                remove => parent.PositionOffset.OnScaleChanged -= value;
            }

            public DependentPositionData(IndependentPositionData parent)
            {
                this.parent = parent;
                Shape = parent.Shape;

                parent.PositionOffset.OnScaleChanged += UpdateData;
                UpdateData();
            }

            private void UpdateData()
            {
                position = parent.PositionFunc(parent.PositionOffset);
                size = parent.SizeFunc(parent.PositionOffset);
                Scale = parent.PositionOffset.ScaleFactor;

                OnPositionDataUpdate?.Invoke(ToPositionData());
            }

            public PositionData ToPositionData() =>
                new(position, size, Shape);

            public static implicit operator PositionData(DependentPositionData d) => d.ToPositionData();
        }
        internal class CampaignMapBackground : IDisposable
        {
            private readonly Transform parent;
            private readonly AccSaberCampaignBackgroundSizeInfo sizeInfo;

            public readonly AccSaberCampaignOffsetData OffsetData;

            private readonly GameObject bg;
            private readonly CancellationTokenSource imageTokenSource;
            private readonly float widthToHeight;

            public CampaignMapBackground(Transform parent, AccSaberCampaignBackgroundSizeInfo bgSize, AccSaberCampaignOffsetData offsetData, string bgUrl)
            {
                this.parent = parent;
                sizeInfo = bgSize;
                OffsetData = offsetData;

                bg = new("CampaignMapBackground");

                RectTransform rt = bg.AddComponent<RectTransform>();
                bg.transform.SetParent(parent, false);

                rt.anchorMin = new(0.5f, 0.5f);
                rt.anchorMax = new(0.5f, 0.5f);
                rt.pivot = new(0.5f, 0.5f);
                rt.localScale = Vector3.one;

                bg.AddComponent<LayoutElement>().ignoreLayout = true;

                ImageView image = bg.AddComponent<ImageView>();
                image.material = Utilities.ImageResources.NoGlowMat;
                image.type = Image.Type.Simple;

                imageTokenSource = new();
                image.LoadImage(bgUrl, imageTokenSource.Token).GetAwaiter().GetResult(); // This has to be awaited for.

                widthToHeight = image.sprite.textureRect.height / image.sprite.textureRect.width;

                bg.transform.SetAsFirstSibling();

                offsetData.OnScaleChanging += OnOffsetDataUpdating;
                offsetData.OnScaleChanged += OnOffsetDataUpdate;
            }

            private void OnOffsetDataUpdating()
            {
                float normalSizeX = OffsetData.OffsetSize * 20f;
                float normalSizeY = normalSizeX * widthToHeight;

                RectTransform rt = bg.GetComponent<RectTransform>();

                Vector2 size = new(normalSizeX * sizeInfo.Scale, normalSizeY * sizeInfo.Scale);
                rt.sizeDelta = size;
                sizeInfo.Size = size;

                LayoutElement le = bg.GetComponent<LayoutElement>();

                le.preferredWidth = size.x;
                le.preferredHeight = size.y;
            }
            private void OnOffsetDataUpdate()
            {
                RectTransform rt = bg.GetComponent<RectTransform>();

                rt.anchoredPosition = new(
                    sizeInfo.PositionX * OffsetData.OffsetSize + OffsetData.Offset.x,
                    -sizeInfo.PositionY * OffsetData.OffsetSize - OffsetData.Offset.y
                );
            }

            public void Dispose()
            {
                OffsetData.OnScaleChanged -= OnOffsetDataUpdate;
                OffsetData.OnScaleChanging -= OnOffsetDataUpdating;

                imageTokenSource.Cancel();
                imageTokenSource.Dispose();

                UnityEngine.Object.Destroy(bg);
            }
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

                if (!string.IsNullOrWhiteSpace(Map.CheckpointLabel) && !(Map.CheckpointLabelPosition == CampaignLabelPosition.NONE))
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
#if PRINT_DEBUG && DEBUG
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
                    BeatmapKey key = new(level.levelID, standard, EnumUtils.ReloadedDiffToDiff(Map.Difficulty));

                    campaignFlow.ShowLeaderboard(key);

                    await campaignController.SetMission(Map, key, level, Progress);
#else
                    BeatmapDifficulty mapDiff = EnumUtils.ReloadedDiffToDiff(Map.Difficulty);
                    IDifficultyBeatmapSet diffSet = level.beatmapLevelData.difficultyBeatmapSets.First(set => set.beatmapCharacteristic.serializedName.Equals("Standard", StringComparison.OrdinalIgnoreCase));
                    IDifficultyBeatmap diff = diffSet.difficultyBeatmaps.First(difficulty => difficulty.difficulty == mapDiff);

                    campaignFlow.ShowLeaderboard(diff);

                    await campaignController.SetMission(Map, diff, Progress);
#endif
                }
            }

            private void SetupCheckpointLabel()
            {
                if (!postParse)
                    return;

                if (string.IsNullOrWhiteSpace(Map.CheckpointLabel) || Map.CheckpointLabelPosition == CampaignLabelPosition.NONE)
                {
                    CheckpointText.gameObject.SetActive(false);

                    Map.Size = new(NodeWidth, NodeHeight);

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
                CheckpointText.overflowMode = TextOverflowModes.Overflow;
                CheckpointText.raycastTarget = false;

#if V41
                CheckpointText.textWrappingMode = TextWrappingModes.NoWrap;
#else
                CheckpointText.enableWordWrapping = false;
#endif

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

                float padding = NodeWidth * 0.05f;

                Vector2 nodeSize;

                switch (Map.CheckpointLabelPosition)
                {
                    case CampaignLabelPosition.UP:
                        rectTransform.anchorMin = new Vector2(0.5f, 1f);
                        rectTransform.anchorMax = new Vector2(0.5f, 1f);
                        rectTransform.pivot = new Vector2(0.5f, 0f);
                        rectTransform.anchoredPosition = new Vector2(0f, padding);
                        nodeSize = new Vector2(0f, NodeHeight + padding * 2);
                        break;

                    case CampaignLabelPosition.DOWN:
                        rectTransform.anchorMin = new Vector2(0.5f, 0f);
                        rectTransform.anchorMax = new Vector2(0.5f, 0f);
                        rectTransform.pivot = new Vector2(0.5f, 1f);
                        rectTransform.anchoredPosition = new Vector2(0f, -padding);
                        nodeSize = new Vector2(0f, NodeHeight + padding * 2);
                        break;

                    case CampaignLabelPosition.LEFT:
                        rectTransform.anchorMin = new Vector2(0f, 0.5f);
                        rectTransform.anchorMax = new Vector2(0f, 0.5f);
                        rectTransform.pivot = new Vector2(1f, 0.5f);
                        rectTransform.anchoredPosition = new Vector2(-padding, 0f);
                        nodeSize = new Vector2(NodeWidth + padding * 2, 0f);
                        break;

                    case CampaignLabelPosition.RIGHT:
                        rectTransform.anchorMin = new Vector2(1f, 0.5f);
                        rectTransform.anchorMax = new Vector2(1f, 0.5f);
                        rectTransform.pivot = new Vector2(0f, 0.5f);
                        rectTransform.anchoredPosition = new Vector2(padding, 0f);
                        nodeSize = new Vector2(NodeWidth + padding * 2, 0f);
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

                Map.Size = rectTransform.rect.size + nodeSize;
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

                _ = string.IsNullOrEmpty(Map.CheckpointAvatarUrl) ?
                    (Map.NodeBorderUrl is null ? CoverImage.LoadCoverImage(Hash, Map.CoverUrl, imageTaskToken.Token) : CoverImage.LoadImage(Map.NodeBorderUrl!, imageTaskToken.Token)) :
                    CoverImage.LoadImage(Map.CheckpointAvatarUrl!, imageTaskToken.Token);
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

                    UpdateBarrierSizeBounds();
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

                    UpdateBarrierSizeBounds();
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

                OffsetData.OnScaleChanging += OnOffsetDataUpdateStarted;
                OffsetData.OnScaleChanged += OnOffsetDataUpdateFinished;
                OnOffsetDataUpdateFinished();

#if PRINT_DEBUG && DEBUG
        Plugin.Log.Info($"Barrier: Pos = ({barrier.PositionX}, {barrier.PositionY}) Node Pos = ({Position.x}, {Position.y}), Width = {SizeDelta.x}, Height = {SizeDelta.y}");
#endif
            }

            public void UpdateProgress()
            {
                UpdateText();
                UpdateTextSize();
                UpdateTextPos();
            }

            private static void SetupManualRectTransform(RectTransform rt)
            {
                rt.anchorMin = new(0.5f, 0.5f);
                rt.anchorMax = new(0.5f, 0.5f);
                rt.pivot = new(0.5f, 0.5f);
                rt.localScale = Vector3.one;
            }

            private void OnOffsetDataUpdateStarted()
            {
                SizeDelta = new(
                    WIDTH * OffsetData.ScaleFactor,
                    Barrier.Scale * OffsetData.ScaleFactor
                );

                text.fontSize = FONT_SIZE * OffsetData.ScaleFactor;

                UpdateText();
                UpdateTextSize();

                // This updates Barrier.Size to include:
                // - the rotated barrier rectangle
                // - the label on the default end
                // - the label on the opposite end
                //
                // Including both ends is important because text collision resolution
                // may flip the label after the parent has already calculated bounds.
                UpdateBarrierSizeBounds();

                // Apply current visual position using the current/old offset.
                // Do not force collision resolution here; final positions are not ready yet.
                UpdateTextPos(resolveCollisions: false);
            }

            private void OnOffsetDataUpdateFinished()
            {
                barrierRt.anchoredPosition = new(
                    Barrier.PositionX * OffsetData.OffsetSize + OffsetData.Offset.x,
                    -Barrier.PositionY * OffsetData.OffsetSize - OffsetData.Offset.y
                );

                // Text is a separate object under the same parent, so it must be moved
                // after the barrier anchored position changes.
                UpdateTextPos();
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

                UpdateBarrierSizeBounds();
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

                Vector2 endDirection = GetTextEndDirection(textOnOppositeEnd);

                float barrierHalfLength = SizeDelta.y * 0.5f;

                // The text remains unrotated/upright.
                // Because of that, calculate the axis-aligned half-extent of the text
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
                    {
                        barrier.UpdateBarrierSizeBounds();
                        barrier.textRt.SetAsLastSibling();
                    }
                }
                finally
                {
                    resolvingTextCollisions = false;
                }
            }

            private void UpdateBarrierSizeBounds()
            {
                if (barrierRt is null || textRt is null)
                    return;

                Barrier.Size = CalculateRequiredBarrierSize();
            }

            private Vector2 CalculateRequiredBarrierSize()
            {
                Vector2 totalHalfExtents = GetRotatedBarrierHalfExtents();

                // Include the text on the default end.
                ExpandHalfExtentsToIncludeTextEnd(ref totalHalfExtents, oppositeEnd: false);

                // Also include the text on the opposite end.
                //
                // This is intentionally conservative. It prevents the parent view from
                // calculating bounds that are too small before collision resolution flips
                // the text to the other side.
                ExpandHalfExtentsToIncludeTextEnd(ref totalHalfExtents, oppositeEnd: true);

                // Barrier.Size is the full bounding-box size, not half-extents.
                return totalHalfExtents * 2f;
            }

            private Vector2 GetRotatedBarrierHalfExtents()
            {
                Vector2 size = SizeDelta;

                float halfWidth = size.x * 0.5f;
                float halfHeight = size.y * 0.5f;

                Vector3 right3D = barrierRt.localRotation * Vector3.right;
                Vector3 up3D = barrierRt.localRotation * Vector3.up;

                Vector2 right = new(right3D.x, right3D.y);
                Vector2 up = new(up3D.x, up3D.y);

                // Axis-aligned half extents of a rotated rectangle.
                return new Vector2(
                    Mathf.Abs(right.x) * halfWidth + Mathf.Abs(up.x) * halfHeight,
                    Mathf.Abs(right.y) * halfWidth + Mathf.Abs(up.y) * halfHeight
                );
            }

            private void ExpandHalfExtentsToIncludeTextEnd(ref Vector2 totalHalfExtents, bool oppositeEnd)
            {
                Vector2 textSize = textRt.sizeDelta;
                Vector2 endDirection = GetTextEndDirection(oppositeEnd);

                float barrierHalfLength = SizeDelta.y * 0.5f;

                float textHalfExtentInDirection =
                    Mathf.Abs(endDirection.x) * textSize.x * 0.5f +
                    Mathf.Abs(endDirection.y) * textSize.y * 0.5f;

                float margin = TEXT_MARGIN * OffsetData.ScaleFactor;

                Vector2 textCenterRelativeToBarrier =
                    endDirection * (barrierHalfLength + textHalfExtentInDirection + margin);

                float collisionPadding = TEXT_COLLISION_PADDING * OffsetData.ScaleFactor;

                Vector2 textHalfExtents = new(
                    textSize.x * 0.5f + collisionPadding,
                    textSize.y * 0.5f + collisionPadding
                );

                // Because Barrier.Size is only a Vector2, it cannot represent an offset center.
                // So we calculate a symmetric bounding box around the barrier center.
                //
                // This means:
                // required half extent X = abs(text center X) + text half width
                // required half extent Y = abs(text center Y) + text half height
                totalHalfExtents.x = Mathf.Max(
                    totalHalfExtents.x,
                    Mathf.Abs(textCenterRelativeToBarrier.x) + textHalfExtents.x
                );

                totalHalfExtents.y = Mathf.Max(
                    totalHalfExtents.y,
                    Mathf.Abs(textCenterRelativeToBarrier.y) + textHalfExtents.y
                );
            }

            private Vector2 GetTextEndDirection(bool oppositeEnd)
            {
                // Default end is down. Opposite end is up.
                Vector3 localEndDirection = oppositeEnd ? Vector3.up : Vector3.down;

                Vector3 endDirection3D = barrierRt.localRotation * localEndDirection;
                Vector2 endDirection = new(endDirection3D.x, endDirection3D.y);

                if (endDirection.sqrMagnitude < 0.0001f)
                    return oppositeEnd ? Vector2.up : Vector2.down;

                endDirection.Normalize();
                return endDirection;
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
                float progress = Progress.Progress[0].CurrentValue;

                string value = Barrier.ConditionType switch
                {
                    BarrierConditionType.AVERAGE_ACC =>
                        $"Average Accuracy\n<color={ColorUtils.LEVEL}>{progress * 100f:N2}%</color> / <color={ColorUtils.LEVEL}>{Barrier.ConditionValue * 100f:N2}%</color>",

                    BarrierConditionType.AVERAGE_AP =>
                        $"Average Ap\n<color={ColorUtils.AP}>{progress:0.##} ap</color> / <color={ColorUtils.AP}>{Barrier.ConditionValue:0.##} ap</color>",

                    BarrierConditionType.AP_MAX =>
                        $"Max Ap\n<color={ColorUtils.AP}>{progress:0.##} ap</color> / <color={ColorUtils.AP}>{Barrier.ConditionValue:0.##} ap</color>",

                    BarrierConditionType.ACC_MAX =>
                        $"Max Accuracy\n<color={ColorUtils.LEVEL}>{progress * 100f:N2}%</color> / <color={ColorUtils.LEVEL}>{Barrier.ConditionValue * 100f:N2}%</color>",

                    BarrierConditionType.STREAK_115_AVERAGE =>
                        $"Average streak\n<color={ColorUtils.TECH}>{progress:N0}x</color> / <color={ColorUtils.TECH}>{Barrier.ConditionValue:N0}x streak</color>",

                    BarrierConditionType.STREAK_115_MAX =>
                        $"Max streak\n<color={ColorUtils.TECH}>{progress:N0}x</color> / <color={ColorUtils.TECH}>{Barrier.ConditionValue:N0}x streak</color>",

                    BarrierConditionType.FC =>
                        $"FC maps\n<color={ColorUtils.RELOADED}>{progress:N0}</color> / <color={ColorUtils.RELOADED}>{Barrier.AffectedCampaignDifficultyIds.Count:N0}</color>",

                    BarrierConditionType.AVERAGE_RANK =>
                        $"Average Rank\n<color={ColorUtils.RANK}>#{progress:0.##}</color> / <color={ColorUtils.RANK}>#{Barrier.ConditionValue:0.##}</color>",

                    BarrierConditionType.MAX_RANK =>
                        $"Max Rank\n<color={ColorUtils.RANK}>#{progress:N0}</color> / <color={ColorUtils.RANK}>#{Barrier.ConditionValue:N0}</color>",

                    BarrierConditionType.COMPLETION_COUNT =>
                        $"Nodes Completed\n<color={ColorUtils.GLOBAL}>{progress:N0}</color> / <color={ColorUtils.GLOBAL}>{Barrier.ConditionValue:N0}</color>",

                    BarrierConditionType.PASS =>
                        $"Nodes Passed\n<color={ColorUtils.RELOADED}>{progress:N0}</color> / <color={ColorUtils.RELOADED}>{Barrier.AffectedCampaignDifficultyIds.Count:N0}</color>",

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
                OffsetData.OnScaleChanging -= OnOffsetDataUpdateStarted;
                OffsetData.OnScaleChanged -= OnOffsetDataUpdateFinished;

                ActiveBarriers.Remove(this);

                UnityEngine.Object.Destroy(obj);
                UnityEngine.Object.Destroy(textObj);
            }
        }

        internal class CampaignMapText : Utils.Safety.SafeNotifyPropertyChanged, IDisposable
        {
            private const float padding = 1f;

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

                string content = ParseGivenContent(text.Content);

                if (!string.IsNullOrEmpty(text.Color))
                    content = $"<color={text.Color}>{content}</color>";

#if V41
                TextObj = BeatSaberUI.CreateCurvedUIText(parent, content);
#else
                TextObj = BeatSaberUI.CreateText(parent, content, Vector2.zero);
#endif

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

                OffsetData.OnScaleChanging += UpdateTextSize;
                OffsetData.OnScaleChanged += ApplyOffsetData;
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
                const float FONT_SCALE = 12f;
                const float BOUNDS_SCALE = 12f; // originally = 16
                const float TRUE_BOUNDS_SCALE = FONT_SCALE * BOUNDS_SCALE;

                TextObj.fontSize = Text.Scale * FONT_SCALE * OffsetData.ScaleFactor;

                ResizeTextToFit(TRUE_BOUNDS_SCALE * Text.Scale * OffsetData.ScaleFactor);

                Canvas.ForceUpdateCanvases();
#if NEW_VERSION
                TextObj.ForceMeshUpdate(true, true);
#else
                TextObj.ForceMeshUpdate(true);
#endif

                Vector2 rectSize = TextObj.rectTransform.rect.size;


                if (padding != 0f)
                    rectSize += new Vector2(padding, padding) * OffsetData.ScaleFactor;

                Text.Size = rectSize;
            }

            private void ResizeTextToFit(float maxWidth = 0f)
            {
                RectTransform rt = TextObj.rectTransform;

                bool useWrapping = maxWidth > 0f;

                TextObj.overflowMode = TextOverflowModes.Overflow;

#if V41
                TextObj.textWrappingMode = useWrapping ? TextWrappingModes.PreserveWhitespace : TextWrappingModes.NoWrap;
#else
                TextObj.enableWordWrapping = useWrapping;
#endif

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

                preferredSize.x = Mathf.Ceil(preferredSize.x + padding * OffsetData.ScaleFactor);
                preferredSize.y = Mathf.Ceil(preferredSize.y + padding * OffsetData.ScaleFactor);

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
                OffsetData.OnScaleChanging -= UpdateTextSize;
                OffsetData.OnScaleChanged -= ApplyOffsetData;

                if (TextObj is not null)
                    UnityEngine.Object.Destroy(TextObj.gameObject);
            }

            private static readonly Regex TagMatcher = new(@"</?(?'name'[\w_]+) ?(?'content'[^>]+)?>");
            private static readonly Regex ContentMatcher = new(@"(?'tag'[\w_]+)=(?'value'\d+|\w+|""[^""]+"")");

            private static string ParseGivenContent(string givenStr)
            {
                givenStr = givenStr.Replace("\n", "");

                MatchCollection mc = TagMatcher.Matches(givenStr);
                Queue<(string oldStr, string newStr)> toReplace = [];
                Stack<(string oldStr, string newStr)> closingTagReplacement = [];

                foreach (Match m in mc)
                {
                    string tag = m.Groups["name"].Value;

                    if (tag.Length < 2)
                        continue;

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

                    bool failed = true;
                    bool endTagInstant = m.Value[^2] == '/';

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

                                    toReplace.Enqueue((m.Value, $"<color={new Color32(rgbVals[0], rgbVals[1], rgbVals[2], 255).Color()}{(endTagInstant ? "/" : "")}>"));

                                    if (!endTagInstant)
                                        closingTagReplacement.Push(("</span>", "</color>"));

                                    break;
                                }

                                if (content[1..].StartsWith("font-size:"))
                                {
                                    toReplace.Enqueue((m.Value, $"<size={content[11..content.Length].Replace("\"", "")}{(endTagInstant ? "/" : "")}>"));

                                    if (!endTagInstant)
                                        closingTagReplacement.Push(("</span>", "</size>"));

                                    break;
                                }

                                failed = true;
                            }

                            break;

                        case "div" or "br":
                            failed = false;

                            toReplace.Enqueue((m.Value, endTagInstant ? "\n" : ""));

                            if (!endTagInstant)
                                closingTagReplacement.Push(($"</{tag}>", "\n"));
                            break;
                    }

                    if (failed)
                    {
                        toReplace.Enqueue((m.Value, ""));

                        if (!endTagInstant)
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

                string outp = WebUtility.HtmlDecode(givenStr);

#if PRINT_DEBUG && DEBUG
                Plugin.Log.Info("Text: " + outp);
#endif

                return outp;
            }
        }
    }

    internal class UIArrow : IDisposable
    {
        private readonly Transform parent;
        private readonly DependentPositionData StartPoint, EndPoint;
        private readonly float shaftThickness, headLength, headWidth;
        private readonly string name;

        private GameObject? arrowObj;
        private ImageView? headImg, shaftImg;

        public Color Color
        {
            get;
            set
            {
                if (field == value)
                    return;

                field = value;

                if (headImg is not null && shaftImg is not null)
                {
                    headImg.color = value;
                    shaftImg.color = value;
                }
            }
        }
        public GameObject? Arrow => arrowObj;

        public UIArrow(Transform parent, DependentPositionData from, DependentPositionData to, Color color) :
            this(parent, from, to, color, 5f, 20f, 20f, "UI Arrow") { }
        public UIArrow(Transform parent, DependentPositionData from, DependentPositionData to, Color color, float shaftThickness, float headLength, float headWidth, string name)
        {
            this.parent = parent;
            StartPoint = from; 
            EndPoint = to;
            Color = color;
            this.shaftThickness = shaftThickness;
            this.headLength = headLength;
            this.headWidth = headWidth;
            this.name = name;

            from.OnParentUpdate += ModifyArrow;
        }

        public GameObject? CreateOrGetArrow()
        {
            if (arrowObj is null)
                return CreateArrow();

            return arrowObj;
        }
        public GameObject? CreateArrow()
        {
            PositionData from = StartPoint;
            PositionData to = EndPoint;

            float scale = StartPoint.Scale;

            if (!TryGetClippedArrowPoints(from, to, out Vector2 fromPos, out Vector2 toPos))
            {
                fromPos = from.Position;
                toPos = to.Position;
            }

            Vector2 direction = toPos - fromPos;
            float length = direction.magnitude;

            if (length <= 0.001f)
                return null;

            float shaftThickness = this.shaftThickness * scale;
            float headLength = this.headLength * scale;
            float headWidth = this.headWidth * scale;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            headLength = Mathf.Min(headLength, length);
            float shaftLength = Mathf.Max(0f, length - headLength);

            if (arrowObj is not null)
                UnityEngine.Object.Destroy(arrowObj);

            arrowObj = new(name, typeof(RectTransform));
            arrowObj.transform.SetParent(parent, false);

            arrowObj.AddComponent<LayoutElement>().ignoreLayout = true;

            RectTransform arrowRect = arrowObj.GetComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
            arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
            arrowRect.pivot = new Vector2(0f, 0.5f);
            arrowRect.anchoredPosition = fromPos;
            arrowRect.sizeDelta = new Vector2(length, headWidth);
            arrowRect.localRotation = Quaternion.Euler(0f, 0f, angle);

            // Shaft
            GameObject shaft = new("Shaft", typeof(RectTransform));
            shaft.transform.SetParent(arrowObj.transform, false);

            RectTransform shaftRect = shaft.GetComponent<RectTransform>();
            shaftRect.anchorMin = new Vector2(0f, 0.5f);
            shaftRect.anchorMax = new Vector2(0f, 0.5f);
            shaftRect.pivot = new Vector2(0f, 0.5f);
            shaftRect.anchoredPosition = Vector2.zero;
            shaftRect.sizeDelta = new Vector2(shaftLength, shaftThickness);

            shaftImg = shaft.AddComponent<ImageView>();
            shaftImg.sprite = Utilities.ImageResources.WhitePixel;
            shaftImg.material = Utilities.ImageResources.NoGlowMat;
            shaftImg.color = Color;
            shaftImg.raycastTarget = false;

            // Arrow head
            GameObject head = new("Head", typeof(RectTransform));
            head.transform.SetParent(arrowObj.transform, false);

            RectTransform headRect = head.GetComponent<RectTransform>();
            headRect.anchorMin = new Vector2(0f, 0.5f);
            headRect.anchorMax = new Vector2(0f, 0.5f);
            headRect.pivot = new Vector2(0f, 0.5f);
            headRect.anchoredPosition = new Vector2(shaftLength, 0f);
            headRect.sizeDelta = new Vector2(headLength, headWidth);

            headImg = head.AddComponent<ImageView>();
            headImg.sprite = TriangleArrowHeadSprite;
            headImg.material = Utilities.ImageResources.NoGlowMat;
            headImg.type = Image.Type.Simple;
            headImg.color = Color;
            headImg.raycastTarget = false;

            return arrowObj;
        }
        private void ModifyArrow()
        {
            if (arrowObj is null)
                return;

            PositionData from = StartPoint;
            PositionData to = EndPoint;

            float scale = StartPoint.Scale;

            if (!TryGetClippedArrowPoints(from, to, out Vector2 fromPos, out Vector2 toPos))
            {
                fromPos = from.Position;
                toPos = to.Position;
            }

            Vector2 direction = toPos - fromPos;
            float length = direction.magnitude;

            if (length <= 0.001f)
                return;

            float shaftThickness = this.shaftThickness * scale;
            float headLength = this.headLength * scale;
            float headWidth = this.headWidth * scale;

            headLength = Mathf.Min(headLength, length);
            float shaftLength = Mathf.Max(0f, length - headLength);

            RectTransform arrow = (RectTransform)arrowObj.transform;
            RectTransform shaft = (RectTransform)arrowObj.transform.Find("Shaft");
            RectTransform head = (RectTransform)arrowObj.transform.Find("Head");

            arrow.anchoredPosition = fromPos;
            arrow.sizeDelta = new Vector2(length, headWidth);

            shaft.sizeDelta = new(shaftLength, shaftThickness);

            head.anchoredPosition = new(shaftLength, 0f);
            head.sizeDelta = new(headLength, headWidth);
        }

        private static bool TryGetClippedArrowPoints(PositionData from, PositionData to, out Vector2 arrowStart, out Vector2 arrowEnd, float padding = 0f)
        {
            NodeShape fromShape = from.Shape;
            NodeShape toShape = to.Shape;

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
        public static bool TryGetClippedArrowPoints(
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

            arrowStart = GetRotatedShapeEdgePoint(from, fromRotation, direction, padding);

            arrowEnd = GetRotatedShapeEdgePoint(to, toRotation, -direction, padding);

            if (Vector2.Dot(arrowEnd - arrowStart, direction) <= 0.001f)
                return false;

            return true;
        }

        private static Vector2 GetRotatedShapeEdgePoint(PositionData data, Quaternion rotation, Vector2 worldDirection, float padding = 0f) =>
            GetRotatedShapeEdgePoint(data.Position, data.Size, data.Shape, rotation, worldDirection, padding);
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

            float distanceToEdge = GetShapeDistanceToEdge(halfSize, shape, localDirection);

            return center + worldDirection * (distanceToEdge + padding);
        }
        private static float GetShapeDistanceToEdge(Vector2 halfSize, NodeShape shape, Vector2 localDirection)
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

        public void Dispose()
        {
            StartPoint.OnParentUpdate -= ModifyArrow;

            if (arrowObj is not null)
                UnityEngine.Object.Destroy(arrowObj);
        }

        public static implicit operator GameObject?(UIArrow arrow) => arrow.CreateOrGetArrow();
    }

    public static class NodeShapeTextures
    {
        private static readonly ConcurrentDictionary<string, Sprite> _borderSpriteCache = [];
        private static readonly ConcurrentDictionary<string, Sprite> _fillSpriteCache = [];

        private static bool _preloadedSprites = false;
#if PRINT_DEBUG && DEBUG
        private static int _extraFrames;
#endif

        public static async Task PreloadStandardSprites(int maxCoverageLoads)
        {
            if (_preloadedSprites)
                return;
            _preloadedSprites = true;
#if PRINT_DEBUG && DEBUG
            _extraFrames = 0;
#endif

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

#if PRINT_DEBUG && DEBUG
            Plugin.Log.Info($"When preloading sprites, there were {_extraFrames + 2} frames of delay.");
#endif
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

#if PRINT_DEBUG && DEBUG
                            ++_extraFrames;
#endif
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

#if PRINT_DEBUG && DEBUG
                            ++_extraFrames;
#endif
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