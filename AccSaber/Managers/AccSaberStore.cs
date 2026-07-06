using AccSaber.API;
using AccSaber.Models;
using AccSaber.Models.PlayerModels;
using AccSaber.UI.ViewControllers;
using AccSaber.Utils;
using AccSaber.Utils.Misc;
using Newtonsoft.Json;
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
        public static event Action<AccSaberLeaderboardEntry>? OnScoreUpdated;
        public static event Action<AccSaberLeaderboardEntry>? OnPlayerScoreUpdated;
        public event Action? OnUpdatingFromAccSaberAPI;
		public event Action<bool>? OnUpdatedFromAccSaberAPI;

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

        public async Task<List<AccSaberCampaign>> GetCampaigns(string status)
        {

            AccSaberPagedContent<AccSaberCampaign>? content = await APIHandler.CallAPI_Json<AccSaberPagedContent<AccSaberCampaign>>(string.Format(HelpfulPaths.APAPI_CAMPAIGNS_ALL, status), AccsaberAPI.Throttler);

            if (content is null)
                return [];

            List<AccSaberCampaign> newCampaignEntries = [];

            foreach (AccSaberCampaign newsCampaign in content.Content!)
            {
                newCampaignEntries.Add(newsCampaign);
            }

            return newCampaignEntries;
        }
        public async Task<List<AccSaberCampaign>> GetCampaignsPaged(string status, int page = 0, int size = 10)
        {

            string call = string.Format(HelpfulPaths.APAPI_CAMPAIGNS_STATUS, status, page, size);

            AccSaberPagedContent<AccSaberCampaign>? content = await APIHandler.CallAPI_Json<AccSaberPagedContent<AccSaberCampaign>>(call, AccsaberAPI.Throttler);

            if (content is null)
                return [];

            List<AccSaberCampaign> newCampaignEntries = [];

            foreach (AccSaberCampaign newsCampaign in content.Content!)
            {
                newCampaignEntries.Add(newsCampaign);
            }

            return newCampaignEntries;
        }

        public async Task<List<AccSaberCampaign>> GetActiveCampaigns(int page = 0, int size = 10)
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

        public async Task<AccSaberCampaign> GetCampaign(Guid id)
        {
            if (_campaignCache.TryGetCachedItem(id, out AccSaberCampaign? item))
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
            CurrentRankedMap = _api.GetLeaderboard(hash)?.Difficulties.FirstOrDefault(diff => diff.Difficulty == difficulty);
        }

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
}