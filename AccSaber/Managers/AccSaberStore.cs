using AccSaber.API;
using AccSaber.Models;
using AccSaber.Models.PlayerModels;
using AccSaber.UI.ViewControllers;
using AccSaber.Utils;
using AccSaber.Utils.Misc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiraUtil.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Zenject;
using static AccSaber.Managers.CampaignProgress;

#if V41
using OculusStudios.Platform.Core;
#endif

namespace AccSaber.Managers
{
	internal sealed class AccSaberStore : IInitializable, IDisposable
	{
		private readonly SiraLog _log;
#if V41
        private readonly IPlatform _platformUserModel;
        private UserInfo? _userInfo = null;
#else
        private readonly IPlatformUserModel _platformUserModel;
#endif
        private readonly PlayerSocialLife _playerInfo;
        private readonly AccsaberAPI _api;
        private readonly SerializationHandler _serialHandler;
        private readonly AccSaberLeaderboardViewController _leaderboardVC;

		public event Action<AccSaberBasicDifficulty?>? OnAccSaberRankedMapUpdated;
#if NEW_VERSION
        public event Action<BeatmapKey, BeatmapLevel>? OnLeaderboardUpdated;
#else
        public event Action<IDifficultyBeatmap>? OnLeaderboardUpdated;
#endif
        public static event Action<AccSaberLeaderboardEntry>? OnScoreUpdated;
        public static event Action<AccSaberLeaderboardEntry>? OnPlayerScoreUpdated;
        public event Action? OnUpdatingFromAccSaberAPI;
		public event Action<bool>? OnUpdatedFromAccSaberAPI;

#if NEW_VERSION
        public BeatmapKey CurrentKey { get; private set; }
        public BeatmapLevel CurrentLevel { get; private set; } = null!;
#else
        public IDifficultyBeatmap CurrentLevel { get; private set; } = null!;
#endif
        public string CurrentHash { get; private set; } = null!;

        private AccSaberPlayer? _currentUser;
        private readonly ObjectCacher<Guid, AccSaberCampaign> _campaignCache = new();

        public  DateTime LastLocalUpdateTime { get; private set; } = DateTime.MinValue;
		internal static CancellationTokenSource WebsocketCanceller { get; private set; } = new();
        internal const int ReceiveBufferSize = 5120;
        internal const int SendBufferSize = 16;
        private static readonly TimeSpan WebsocketIdleTimeout = TimeSpan.FromMinutes(10);

        private static readonly AsyncLock listenerLock = new();

        private AccSaberBasicDifficulty? _currentRankedMap;

#if V41
        public AccSaberStore(SiraLog log, IPlatform platformUserModel, PlayerSocialLife playerInfo, AccsaberAPI api, SerializationHandler serialHandler, AccSaberLeaderboardViewController leaderboardVC)
#else
        public AccSaberStore(SiraLog log, IPlatformUserModel platformUserModel, PlayerSocialLife playerInfo, AccsaberAPI api, SerializationHandler serialHandler, AccSaberLeaderboardViewController leaderboardVC)
#endif
        {
			_log = log;
			_platformUserModel = platformUserModel;
            _playerInfo = playerInfo;
            _api = api;
            _serialHandler = serialHandler;
            _leaderboardVC = leaderboardVC;
        }

		public AccSaberBasicDifficulty? CurrentRankedMap
		{
			get => _currentRankedMap;
			set
			{
				_currentRankedMap = value;
				OnAccSaberRankedMapUpdated?.Invoke(_currentRankedMap);
			}
		}
		public AccSaberPlayer? CurrentUser => _currentUser;

        public async Task<AccSaberBasicDifficulty?> GetCurrentMap()
        {
            if (_currentRankedMap is not null)
                return _currentRankedMap;

            try
            {
                AccSaberRankedMap? map = await APIHandler.CallAPI_Json<AccSaberRankedMap>(string.Format(HelpfulPaths.APAPI_HASH, CurrentHash), AccsaberAPI.Throttler);

                if (map is null)
                    return null;

                _serialHandler.CachedMaps.Add(CurrentHash, map);

                AccSaberBasicDifficulty? outp = null;

                foreach (AccSaberBasicDifficulty diff in map.Difficulties)
                {
                    if (diff.Difficulty == CurrentKey.difficulty)
                        outp = diff;

                    _serialHandler.CachedDifficulties.Add(diff.DifficultyId, diff);
                }

                return outp;
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Issue URL: {string.Format(HelpfulPaths.APAPI_HASH, CurrentHash)}");
                Plugin.Log.Error("There was an error getting map information: " + ex);
                return null;
            }
        }

		public async Task<List<AccSaberMilestone>> GetUserMilestones(bool completed)
		{
            await _playerInfo.LoadTask;

			if (_playerInfo.PlayerID is not null)
			{
				string call = string.Format(completed ? HelpfulPaths.APAPI_MILESTONE_COMPLETE : HelpfulPaths.APAPI_MILESTONE_INCOMPLETE, _playerInfo.PlayerID);
                string? response = await APIHandler.CallAPI_String(call, AccsaberAPI.Throttler);

				if (response is not null)
				{
					List<AccSaberMilestone>? outp = JsonConvert.DeserializeObject<List<AccSaberMilestone>>(response);

					if (outp is null)
						return [];

                    if (completed)
						return outp;

					List<AccSaberMilestone> newMilestones = [];

					foreach (AccSaberMilestone milestone in outp)
                    {
						if (milestone.Completed)
							continue;

						newMilestones.Add(milestone);
                    }

					return completed ? newMilestones : [.. newMilestones.OrderByDescending(x => x.CalculatedProgress)];
				}
			}
			return [];
		}
        public async Task<List<AccSaberMission>> GetMissions(MissionPool pool = MissionPool.Daily, bool allPools = true, bool overrideCache = false)
        {
            await _serialHandler.RevalidateMissions(overrideCache);

            if (_serialHandler.Missions is null)
            {
                //Plugin.Log.Warn("Missions are null, waiting for init task...");
                await _serialHandler.InitTask;
                if (_serialHandler.Missions is null)
                {
                    Plugin.Log.Error("For some reason, the Missions screen is unable to load the missions correctly!");
                    return [];
                }
            }

            List<AccSaberMission> outp = [.. _serialHandler.Missions];

            if (!allPools)
                outp = [.. outp.Where(mission => mission.MissionPool == pool)];

            // This is to make sure that the missions are always in the same order (first by pool, then alphabetically by name) since the API doesn't guarantee any order and it can be a bit jarring to have them switch around every time we fetch them.
            outp.Sort((a, b) => a.MissionPool == b.MissionPool ? a.Name.CompareTo(b.Name) : a.MissionPool.CompareTo(b.MissionPool));

            return outp;
        }

        public enum NewsType
        {
            All,
            General,
            Batch,
            Milestones,
            Items,
            Plugin
        }

        public async Task<List<AccSaberNewsEntry>> GetNewsPosts(NewsType type)
        {
            var typeString = type switch
            {
                NewsType.General => "GENERAL",
                NewsType.Batch => "BATCH",
                NewsType.Milestones => "MILESTONE_SET",
                NewsType.Items => "ITEMS",
                NewsType.Plugin => "PLUGIN",
                _ => ""
            };

            string call = string.Format(type == NewsType.All ? HelpfulPaths.APAPI_NEWS : HelpfulPaths.APAPI_NEWS_TYPE, typeString);

            AccSaberPagedContent<AccSaberNewsEntry>? content = await APIHandler.CallAPI_Json<AccSaberPagedContent<AccSaberNewsEntry>>(call, AccsaberAPI.Throttler);

            if (content is null)
                return [];

            List<AccSaberNewsEntry> newNewsEntries = [];

            foreach (AccSaberNewsEntry newsEntry in content.Content!)
            {
                newNewsEntries.Add(newsEntry);
            }

            return newNewsEntries;
        }

        public async Task<List<AccSaberCampaign>> GetCampaigns()
        {
            AccSaberPagedContent<AccSaberCampaign>? content = await APIHandler.CallAPI_Json<AccSaberPagedContent<AccSaberCampaign>>(HelpfulPaths.APAPI_CAMPAIGNS_ALL, AccsaberAPI.Throttler);

            return HandlePagedCampaign(content);
        }
        public async Task<List<AccSaberCampaign>> GetCampaigns(AccSaberCampaign.CampaignStatus status)
        {
            AccSaberPagedContent<AccSaberCampaign>? content = await APIHandler.CallAPI_Json<AccSaberPagedContent<AccSaberCampaign>>(string.Format(HelpfulPaths.APAPI_CAMPAIGNS, status), AccsaberAPI.Throttler);

            return HandlePagedCampaign(content);
        }
        public async Task<List<AccSaberCampaign>> GetCampaignsPaged(string status, int page = 0, int size = 10)
        {
            string call = string.Format(HelpfulPaths.APAPI_CAMPAIGNS_STATUS, status, page, size);

            AccSaberPagedContent<AccSaberCampaign>? content = await APIHandler.CallAPI_Json<AccSaberPagedContent<AccSaberCampaign>>(call, AccsaberAPI.Throttler);

            return HandlePagedCampaign(content);
        }
        private List<AccSaberCampaign> HandlePagedCampaign(AccSaberPagedContent<AccSaberCampaign>? content)
        {
            if (content is null)
                return [];

            List<AccSaberCampaign> newCampaignEntries = [];

            foreach (AccSaberCampaign newsCampaign in content.Content!)
            {
                newCampaignEntries.Add(newsCampaign);
            }

            return newCampaignEntries;
        }
        public async Task<CampaignProgress> GetCampaignProgress(AccSaberCampaign campaign)
        {
            //Plugin.Log.Info(await APIHandler.CallAPI_String(string.Format(HelpfulPaths.APAPI_CAMPAIGN_PROGRESS, campaign.Id), AccsaberAPI.Throttler) ?? "null");

            List<JObject>? campaignList =
                await APIHandler.CallAPI_Json<List<JObject>>(
                    string.Format(HelpfulPaths.APAPI_CAMPAIGN_PROGRESS, campaign.Id), AccsaberAPI.Throttler);

            if (campaignList is null)
                return default;

            // I don't wanna make an entire new tree of models for this one function, so just using JObjects.
            // After looking at this mess, maybe I should have made the models 0.o

            JObject campaignObj = campaignList.First();

            IEnumerable<KeyValuePair<Guid, CampaignProgressValue>> diffValues = 
                campaignObj["difficulties"]
                .Select(node => new KeyValuePair<Guid, CampaignProgressValue>(
                    Guid.Parse(node["node"]?["id"]?.ToString() ?? ""),
                    new((float)(node["userValue"] ?? 0f),
                     GetCompletionStatus(
                         (bool)(node["unlocked"] ?? false),
                         (bool)(node["completed"] ?? false)
                    ))));

            IEnumerable<KeyValuePair<Guid, CampaignProgressValue>> barrierValues = 
                campaignObj["barriers"]
                .Select(barrier => new KeyValuePair<Guid, CampaignProgressValue>(
                    Guid.Parse(barrier["barrier"]?["id"]?.ToString() ?? ""),
                    new((float)(barrier["currentValue"] ?? 0f),
                     GetCompletionStatus(
                         (bool)(barrier["unlocked"] ?? false),
                         (bool)(barrier["satisfied"] ?? false)
                         ))));

            return new([with(diffValues.Concat(barrierValues))], campaign);
        }

        public async Task<List<AccSaberCampaign>> GetActiveCampaigns(int page = 0, int size = 100)
        {
            string call = string.Format(HelpfulPaths.APAPI_CAMPAIGNS_ACTIVE, page, size);
            AccSaberPagedContent<AccSaberCampaignPaged>? content = await APIHandler.CallAPI_Json<AccSaberPagedContent<AccSaberCampaignPaged>>(call, AccsaberAPI.Throttler);

            if (content is null)
                return [];

            List<AccSaberCampaign> newCampaignEntries = [];


            foreach (AccSaberCampaignPaged newCampaign in content.Content!)
            {
                newCampaign.Campaign.ProgressStatus = newCampaign.ProgressStatus;
                newCampaignEntries.Add(newCampaign.Campaign);
            }

            return newCampaignEntries;
        }

        public async Task<AccSaberCampaign> GetCampaign(Guid id, bool overrideCache = false)
        {
            if (!overrideCache && _campaignCache.TryGetCachedItem(id, out AccSaberCampaign? item))
                return item!;

            string call = string.Format(HelpfulPaths.APAPI_CAMPAIGN, id);
            AccSaberCampaign? content = await APIHandler.CallAPI_Json<AccSaberCampaign>(call, AccsaberAPI.Throttler);

            if (content is null)
            {
                Plugin.Log.Debug("Campaign not found");
                return new AccSaberCampaign();
            }

            _campaignCache.CacheItem(id, content);

            return content;
        }
        public async Task<bool> StartCampaign(Guid id)
        {
            string call = string.Format(HelpfulPaths.APAPI_START_CAMPAIGN, id);

            HttpRequestMessage request = new(HttpMethod.Post, call);

            var (Success, _) = await APIHandler.CallAPI(request, AccsaberAPI.Throttler, maxRetries: 1).ConfigureAwait(false);

            return Success;
        }

        public async Task<List<AccSaberEventMe>> GetEventMissions(int week, bool allWeeks = true, bool overrideCache = false)
        {
            await _serialHandler.RevalidateEvents(overrideCache);

            if (_serialHandler.EventMissions is null)
            {
                //Plugin.Log.Warn("Missions are null, waiting for init task...");
                await _serialHandler.InitTask;
                if (_serialHandler.EventMissions is null)
                {
                    Plugin.Log.Error("For some reason, the Missions screen is unable to load the event missions correctly!");
                    return [];
                }
            }

            List<AccSaberEventMe> outp = allWeeks ?
                [.. _serialHandler.EventMissions] :
                [.. _serialHandler.EventMissions.Where(mission => mission.Mission.Week == week)];

            return outp;
        }


        private async Task UpdateAccSaberInfo()
		{
			OnUpdatingFromAccSaberAPI?.Invoke();

            await _playerInfo.LoadTask;

            if (_playerInfo.PlayerID is null)
			{
				_log.Error("PlayerID not found.");
                return;
			}

			AccSaberPlayer? newOverall = await _api.GetPlayerInfo(_playerInfo.PlayerID, true, false);

			// Check if the data fetched is the same as what we already have cached
			// Saves us from calling the API three more times for the True, Standard and Tech user categories.
			if (UnityEngine.Mathf.Approximately(newOverall?.GetStat(APCategory.Overall)?.AP ?? -1f, _currentUser?.GetStat(APCategory.Overall)?.AP ?? -1f))
			{
				OnUpdatedFromAccSaberAPI?.Invoke(false);
				return;
			}

			_currentUser = newOverall;

			OnUpdatedFromAccSaberAPI?.Invoke(true);
		}
		public async Task<AccSaberPlayer> GetCurrentUserAsync()
		{
			if (_currentUser is not null)
				return _currentUser;

			await UpdateAccSaberInfo();

			return _currentUser!;
		}
		public void SetMapFromBasicInfo(string hash, BeatmapDifficulty difficulty)
		{
            CurrentHash = hash;
            CurrentRankedMap = _api.GetLeaderboard(hash)?.Difficulties.FirstOrDefault(diff => diff.Difficulty == difficulty);
        }
#if NEW_VERSION
        public void SetCurrentMap(BeatmapKey key, BeatmapLevel level)
        {
            CurrentKey = key;
            CurrentLevel = level;
            OnLeaderboardUpdated?.Invoke(key, level);
        }
#else
        public void SetCurrentMap(IDifficultyBeatmap beatmap)
        {
            CurrentLevel = beatmap;
            OnLeaderboardUpdated?.Invoke(beatmap);
        }
#endif

        public async Task StartWebsocket(CancellationToken ct = default)
        {
            bool started = false;

            try
            {
                AsyncLock.Releaser? theLock = await listenerLock.TryLockAsync();
                if (theLock is null)
                {
                    Plugin.Log.Warn("Cannot start websocket when it is already running!");
                    return;
                }

                Throttler throttler = new(3, 120); // 3 calls every 120 seconds
                Plugin.Log.Info("Websocket starting.");
                started = true;

                using (theLock.Value)
                    while (true)
                    {
                        if (!await APIHandler.CheckDomain(HelpfulPaths.APAPI_DOMAIN, ct))
                        {
                            Plugin.Log.Warn("Pausing the websocket loop until the api is found to be healthy again.");
                            await WaitForAPIHealth(ct);
                            Plugin.Log.Info("API is back up, restarting the websocket.");
                        }

                        await ListenForScores(ct);

                        await Task.Delay(1000, ct);
                        await throttler.Call(ct);

                        Plugin.Log.Info("Restarting the websocket.");
                    }
            }
            catch (OperationCanceledException)
            {
                Plugin.Log.Info("Websocket canceled.");
            }
            catch (Exception e)
            {
                Plugin.Log.Error("There was an error starting the websocket!\n" + e);
            }
            finally
            {
                if (started)
                    Plugin.Log.Info("The websocket has closed.");
            }
        }
        private async Task WaitForAPIHealth(CancellationToken ct)
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
               
                if (await APIHandler.CheckDomain(HelpfulPaths.APAPI_DOMAIN, ct))
                    return;

                TaskCompletionSource<object?> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

                void Handler(string domain, bool health)
                {
                    if (health && domain.Equals(HelpfulPaths.APAPI_DOMAIN))
                        tcs.TrySetResult(null);
                }

                APIHandler.OnHealthUpdated += Handler;

                try
                {
                    if (await APIHandler.CheckDomain(HelpfulPaths.APAPI_DOMAIN, ct))
                        return;

                    using var reg = ct.Register(() => tcs.TrySetCanceled(ct));
                    await tcs.Task;
                }
                finally
                {
                    APIHandler.OnHealthUpdated -= Handler;
                }
            }
        }
        private async Task ListenForScores(CancellationToken ct)
        {
            ClientWebSocket? webSocket = null;

            CancellationTokenSource? receiveCts = null;

            try
            {
                webSocket = new();
                webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

                using CancellationTokenSource connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                connectCts.CancelAfter(TimeSpan.FromSeconds(30));

                await webSocket.ConnectAsync(new(HelpfulPaths.APAPI_WEBSOCKET), connectCts.Token);

                Plugin.Log.Info("Websocket connected.");

                byte[] buffer = new byte[ReceiveBufferSize];
                ArraySegment<byte> clientBuffer = new(buffer);

                using MemoryStream ms = new();

                while (webSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    ms.SetLength(0);

                    WebSocketReceiveResult result;

                    do
                    {
                        receiveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        receiveCts.CancelAfter(WebsocketIdleTimeout);

                        result = await webSocket.ReceiveAsync(clientBuffer, receiveCts.Token);

                        receiveCts.Dispose();
                        receiveCts = null;

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            Plugin.Log.Warn($"Websocket closed by server. Status: {result.CloseStatus}, Description: {result.CloseStatusDescription}");

                            using CancellationTokenSource closeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                            closeCts.CancelAfter(TimeSpan.FromSeconds(5));

                            try
                            {
                                await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", closeCts.Token);
                            }
                            catch (OperationCanceledException) when (closeCts.IsCancellationRequested && !ct.IsCancellationRequested)
                            {
                                Plugin.Log.Warn("Timed out while responding to websocket close frame.");
                            }
                            
                            return;
                        }

                        ms.Write(clientBuffer.Array, clientBuffer.Offset, result.Count);
                    }
                    while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Text)
					{
                        AccSaberLeaderboardEntry? entry;

                        try
                        {
                            string json = Encoding.UTF8.GetString(ms.ToArray());

                            entry = JsonConvert.DeserializeObject<AccSaberLeaderboardEntry>(json);

                            if (entry is null)
                            {
                                Plugin.Log.Error("The websocket message deserialized to null.");
                                continue;
                            }
                        }
                        catch (Exception e)
                        {
                            Plugin.Log.Error("There was an error deserializing the websocket message.\n" + e);
                            continue;
                        }

                        try
                        {
                            OnScoreUpdated?.Invoke(entry);
                            Plugin.Log.Info("Websocket recieved score.");
                        }
                        catch (Exception e)
                        {
                            Plugin.Log.Error("There was an issue sending the score update out!\n" + e);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException) when (receiveCts?.IsCancellationRequested ?? false)
            {
                Plugin.Log.Info("Websocket timed out waiting for a new score.");
            }
            catch (OperationCanceledException)
            {
                Plugin.Log.Warn("Websocket timed out or was abandoned. Restarting.");
            }
            catch (Exception e)
            {
                Plugin.Log.Error("There was an error with the websocket!\n" + e);
            }
            finally
            {
                receiveCts?.Dispose();

                Plugin.Log.Warn($"ListenForScores exiting. Websocket state: {webSocket?.State}");

                webSocket?.Dispose();
            }
        }
        private void UpdatePlayerScore(AccSaberLeaderboardEntry score)
        {
            Plugin.Log.Debug($"score name = {score.PlayerName}, score id = {score.PlayerId}, player id = {_playerInfo.PlayerID}");
            if (score.PlayerId.Equals(_playerInfo.PlayerID)) { 
                OnPlayerScoreUpdated?.Invoke(score);
                _ = UpdateAccSaberInfo();
            }
		}

		public async Task<UserInfo?> GetPlatformUserInfo()
		{
#if V41
            if (_userInfo is not null)
                return _userInfo;

            _userInfo = await _platformUserModel.GetUserInfo();
            return _userInfo;
#else
            // GetUserInfo caches the result, no need to do it ourselves
            return await _platformUserModel.GetUserInfo();
#endif
		}

        public void InvalidateCurrentMapCache()
        {
            if (CurrentRankedMap is not null)
                _api.InvalidateCache(CurrentRankedMap.DifficultyId);
        }
        private void UpdateLeaderboardOnRelationChanged()
        {
            if ((_leaderboardVC.DisplayType & LeaderboardDisplayType.Relations) > 0)
                _ = _leaderboardVC.RequestRefresh();
        }

        public async Task<bool> HasAccSaberUpdated()
		{
			if (DateTime.UtcNow < LastLocalUpdateTime.AddMinutes(1))
			{
				return false;
			}

			await UpdateAccSaberInfo();
			return true;
		}
		
		public void Initialize()
		{
			OnScoreUpdated += UpdatePlayerScore;
            OnScoreUpdated += _api.OnScoreUpdated;
            OnPlayerScoreUpdated += _serialHandler.OnPlayerScoreUpdated;
            _playerInfo.OnRelationChanged += UpdateLeaderboardOnRelationChanged;

            //These are all independent tasks, so start each of them on their own thread
			Task.Run(UpdateAccSaberInfo);
			Task.Run(() => StartWebsocket(WebsocketCanceller.Token));
		}
        public void Dispose()
        {
            OnScoreUpdated -= UpdatePlayerScore;
            OnScoreUpdated -= _api.OnScoreUpdated;
            OnPlayerScoreUpdated -= _serialHandler.OnPlayerScoreUpdated;
            _playerInfo.OnRelationChanged -= UpdateLeaderboardOnRelationChanged;
        }
	}

    public readonly struct CampaignProgress
    {
        public readonly Dictionary<Guid, CampaignProgressValue> PlayerValues;
        public readonly HashSet<Guid> CompletedItems, UnlockedItems;
        public readonly AcyclicGraph<Guid> Nodes;
        public readonly HashSet<Guid> AndNodes;

        public CampaignProgress(Dictionary<Guid, CampaignProgressValue> playerValues, AcyclicGraph<Guid> nodes, IEnumerable<Guid> andNodes)
        {
            PlayerValues = playerValues;
            Nodes = nodes;
            AndNodes = [.. andNodes];

            CompletedItems = [];
            UnlockedItems = [];

            foreach (KeyValuePair<Guid, CampaignProgressValue> kvp in playerValues)
                switch (kvp.Value.Completion)
                {
                    case CompletionStatus.Incomplete:
                        continue;
                    case CompletionStatus.Unlocked:
                        UnlockedItems.Add(kvp.Key);
                        continue;
                    case CompletionStatus.Complete:
                        CompletedItems.Add(kvp.Key);
                        continue;
                }

            //Plugin.Log.Info(nodes.ToString());
        }
        internal CampaignProgress(Dictionary<Guid, CampaignProgressValue> playerValues, AccSaberCampaign campaign) :
            this(
                playerValues,
                new(campaign.Difficulties.Cast<INode<Guid>>().Concat(campaign.Barriers)),
                campaign.Difficulties.Where(map => map.PrerequisiteMode.Equals("AND")).Select(map => map.Id)
                )
        { }

        internal HashSet<Guid> MarkAsComplete(Guid id, float progess)
        {
            if (!UnlockedItems.Contains(id))
            {
                Plugin.Log.Warn($"Cannot mark node \"{id}\" as complete, it is not marked as unlocked!");
                if (PlayerValues.TryGetValue(id, out var value))
                    Plugin.Log.Debug(value.ToString());
                else
                    Plugin.Log.Warn($"The id is also not found in the playerValues dictionary!");
                return [];
            }

            if (!Nodes.NodeIdToNode.TryGetValue(id, out AcyclicGraph<Guid>.Node node))
            {
                Plugin.Log.Warn($"Cannot update id \"{id}\", it is not part of the graph!");
                return [];
            }

            PlayerValues[id] = new(progess, CompletionStatus.Complete);

            UnlockedItems.Remove(id);
            CompletedItems.Add(id);

            HashSet<Guid> outp = [];

            foreach (Guid nodeIdToUpdate in node.AffectedIdsOnUpdate)
                if (UpdateNode(nodeIdToUpdate))
                    outp.Add(nodeIdToUpdate);

            return outp;
        }

        private bool UpdateNode(Guid nodeId)
        {
            if (!Nodes.NodeIdToNode.TryGetValue(nodeId, out AcyclicGraph<Guid>.Node n))
                throw new ArgumentException("The given node id does not exist! This should not be possible.");
            return UpdateNode(n);
        }
        private bool UpdateNode(AcyclicGraph<Guid>.Node node)
        {
            Guid id = node.Current.Id;

            if (PlayerValues[id].Completion != CompletionStatus.Incomplete)
                return node.Current is AccSaberCampaignBarrier; // Always update barriers, even if not unlocked.

            bool orMode = !AndNodes.Contains(id);
            bool success = !orMode;

            foreach (Guid prereqId in node.Current.InwardArrows)
            {
                if (CompletedItems.Contains(prereqId))
                {
                    if (orMode)
                    {
                        success = true;
                        break;
                    }
                }
                else if (!orMode)
                {
                    success = false;
                    break;
                }
            }

            if (success)
            {
                PlayerValues[id] = new(PlayerValues[id].Progress, CompletionStatus.Unlocked);
                UnlockedItems.Add(id);
            }

            return success;
        }
        public IEnumerable<Guid> MostProgressedNodes(CompletionStatus status)
        {
            int maxDistance = int.MinValue;
            List<Guid> nodes = [];

            IEnumerable<Guid> ids = status switch
            {
                CompletionStatus.Incomplete => PlayerValues.Keys,
                CompletionStatus.Unlocked => UnlockedItems,
                CompletionStatus.Complete => CompletedItems,
                _ => throw new ArgumentException("Given argument is not valid!")
            };

            foreach (Guid id in ids)
            {
                if (!Nodes.NodeIdToNode.TryGetValue(id, out var value))
                    continue;

                int dist = value.DistanceToHead;

                if (dist > maxDistance)
                {
                    maxDistance = dist;
                    nodes.Clear();
                }

                if (dist >= maxDistance)
                    nodes.Add(id);
            }

            return nodes;
        }
        public IEnumerable<Guid> NodesSortedByProgression(CompletionStatus status)
        {
            ICollection<Guid> ids = status switch
            {
                CompletionStatus.Incomplete => PlayerValues.Keys,
                CompletionStatus.Unlocked => UnlockedItems,
                CompletionStatus.Complete => CompletedItems,
                _ => throw new ArgumentException("Given argument is not valid!")
            };


            List<(int distance, Guid id)> list = [with(ids.Count)];

            foreach (Guid id in ids)
                if (Nodes.NodeIdToNode.TryGetValue(id, out var value))
                    list.Add((value.DistanceToHead, id));

            list.Sort();
            list.Reverse();

            return list.Select(val => val.id);
        }

        public static CompletionStatus GetCompletionStatus(bool unlocked, bool complete)
        {
            if (unlocked)
            {
                if (complete)
                    return CompletionStatus.Complete;
                else
                    return CompletionStatus.Unlocked;
            }
            else
            {
                if (complete)
                    throw new ArgumentException("Cannot complete a node or barrier that is not unlocked.");
                else
                    return CompletionStatus.Incomplete;
            }
        }

        public enum CompletionStatus
        {
            Incomplete, Unlocked, Complete
        }

        public record struct CampaignProgressValue(float Progress, CompletionStatus Completion);

    }
}