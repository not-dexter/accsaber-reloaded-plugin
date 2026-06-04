using AccSaber.API;
using AccSaber.Models;
using AccSaber.Models.PlayerModels;
using AccSaber.UI.ViewControllers;
using AccSaber.Utils;
using Newtonsoft.Json;
using SiraUtil.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Zenject;

namespace AccSaber.Managers
{
	internal sealed class AccSaberStore : IInitializable, IDisposable
	{
		private readonly SiraLog _log;
		private readonly IPlatformUserModel _platformUserModel;

		public event Action<AccSaberBasicDifficulty?>? OnAccSaberRankedMapUpdated;
        public static event Action<AccSaberLeaderboardEntry>? OnScoreUpdated;
        public static event Action<AccSaberLeaderboardEntry>? OnPlayerScoreUpdated;
        public event Action? OnUpdatingFromAccSaberAPI;
		public event Action<bool>? OnUpdatedFromAccSaberAPI;

		private AccSaberPlayer? _currentUser;

        public  DateTime LastLocalUpdateTime { get; private set; } = DateTime.MinValue;
		internal static CancellationTokenSource WebsocketCanceller { get; private set; } = new();
        internal const int RecieveBufferSize = 5120;
        internal const int SendBufferSize = 16;

        private static readonly AsyncLock listenerLock = new();

        private AccSaberBasicDifficulty? _currentRankedMap;

#pragma warning disable IDE0290
		public AccSaberStore(SiraLog log, IPlatformUserModel platformUserModel)
		{
			_log = log;
			_platformUserModel = platformUserModel;
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
            await PlayerSocialLife.LoadTask;

			if (PlayerSocialLife.PlayerID is not null)
			{
				string call = string.Format(completed ? HelpfulPaths.APAPI_MILESTONE_COMPLETE : HelpfulPaths.APAPI_MILESTONE_INCOMPLETE, PlayerSocialLife.PlayerID);
                string? response = await APIHandler.CallAPI_String(call, AccsaberAPI.throttler);

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
        public async Task<List<AccSaberMission>> GetMissions(MissionPool pool = MissionPool.Daily, bool allPools = true)
        {
            await PlayerSocialLife.LoadTask;

            if (PlayerSocialLife.PlayerID is not null)
            {
                string call = allPools ? HelpfulPaths.APAPI_MISSIONS : string.Format(HelpfulPaths.APAPI_MISSIONS_POOL, pool);

                List<AccSaberMission>? outp = await APIHandler.CallAPI_Json<List<AccSaberMission>>(call, AccsaberAPI.throttler);

                if (outp is null)
                    return [];

                // This is to make sure that the missions are always in the same order (first by pool, then alphabetically by name) since the API doesn't guarantee any order and it can be a bit jarring to have them switch around every time we fetch them.
                outp.Sort((a, b) => a.MissionPool == b.MissionPool ? a.Name.CompareTo(b.Name) : a.MissionPool.CompareTo(b.MissionPool));

                return outp;
            }
            return [];
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

            AccSaberPagedContent<AccSaberNewsEntry>? content = await APIHandler.CallAPI_Json<AccSaberPagedContent<AccSaberNewsEntry>>(call, AccsaberAPI.throttler);

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

            AccSaberPagedContent<AccSaberCampaign>? content = await APIHandler.CallAPI_Json<AccSaberPagedContent<AccSaberCampaign>>(HelpfulPaths.APAPI_CAMPAIGNS_ALL, AccsaberAPI.throttler);

            if (content is null)
                return [];

            List<AccSaberCampaign> newCampaignEntries = [];

            foreach (AccSaberCampaign newsCampaign in content.Content!)
            {
                newCampaignEntries.Add(newsCampaign);
            }

            return newCampaignEntries;
        }

        public async Task<AccSaberCampaign> GetCampaign(string id)
        {
            string call = string.Format(HelpfulPaths.APAPI_CAMPAIGN, id);
            AccSaberCampaign? content = await APIHandler.CallAPI_Json<AccSaberCampaign>(call, AccsaberAPI.throttler);

            if (content is null)
            {
                Plugin.Log.Debug("Campaign not found");
                return new AccSaberCampaign();
            }

            return content;
        }

        private async Task UpdateAccSaberInfo()
		{
			OnUpdatingFromAccSaberAPI?.Invoke();

            await PlayerSocialLife.LoadTask;

            if (PlayerSocialLife.PlayerID is null)
			{
				_log.Error("PlayerID not found.");
                return;
			}

			AccSaberPlayer? newOverall = await AccsaberAPI.GetPlayerInfo(PlayerSocialLife.PlayerID, true, false);

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
            CurrentRankedMap = AccsaberAPI.GetLeaderboard(hash)?.Difficulties.FirstOrDefault(diff => diff.Difficulty == difficulty);
        }

        public async Task StartWebsocket(CancellationToken ct = default)
        {
            object locker = new();
            void WaitForHealth(string domain, bool health)
            {
                if (health && domain.Equals(HelpfulPaths.APAPI_DOMAIN))
                    lock (locker)
                        Monitor.PulseAll(locker);
            }

            try
            {
                AsyncLock.Releaser? theLock = await listenerLock.TryLockAsync();
                if (theLock is null)
                    return;
                using (theLock.Value)
                    while (true)
                    {
                        if (!await APIHandler.CheckDomain(HelpfulPaths.APAPI_DOMAIN))
                        {
                            Plugin.Log.Warn("Pausing the websocket loop until the api is found to be healthy again.");
                            lock (locker)
                            {
                                APIHandler.OnHealthUpdated += WaitForHealth;
                                Monitor.Wait(locker);
                                APIHandler.OnHealthUpdated -= WaitForHealth;
                            }
                        }

                        Plugin.Log.Info("Websocket starting.");
                        await ListenForScores(ct);
                        await Task.Delay(1000, ct);
                    }
            }
            catch (OperationCanceledException)
            {
                Plugin.Log.Info("Websocket closed.");
            }
            catch (Exception e)
            {
                Plugin.Log.Error("There was an error starting the websocket!\n" + e);
            }
        }
        private async Task ListenForScores(CancellationToken ct)
        {
            try
            {
                using ClientWebSocket webSocket = new();
                await webSocket.ConnectAsync(new(HelpfulPaths.APAPI_WEBSOCKET), ct);
                using MemoryStream ms = new();
                WebSocketReceiveResult result;
                while (webSocket.State == WebSocketState.Open)
                {
                    do
                    {
                        ArraySegment<byte> clientBuffer = WebSocket.CreateClientBuffer(RecieveBufferSize, SendBufferSize);
                        result = await webSocket.ReceiveAsync(clientBuffer, ct);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", ct);
                            return;
                        }
                        ms.Write(clientBuffer.Array, clientBuffer.Offset, result.Count);
                    }
                    while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Text)
					{
						AccSaberLeaderboardEntry? entry = JsonConvert.DeserializeObject<AccSaberLeaderboardEntry>(Encoding.UTF8.GetString(ms.ToArray()));
						if (entry is not null)
							OnScoreUpdated?.Invoke(entry);
						else
							Plugin.Log.Error("The websocket was not able to deserialize a given entry.");
                    }

                    ms.SetLength(0);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                Plugin.Log.Info("The remote party has very rudely left us hanging (closed connect without handshake).");
            }
            catch (Exception e)
            {
                Plugin.Log.Error("There was an error with the websocket!\n" + e);
            }
        }
        private void UpdatePlayerScore(AccSaberLeaderboardEntry score)
        {
            //Plugin.Log.Debug($"score name = {score.PlayerName}, score id = {score.PlayerId}, player id = {PlayerSocialLife.PlayerID}");
            if (score.PlayerId.Equals(PlayerSocialLife.PlayerID)) { 
                OnPlayerScoreUpdated?.Invoke(score);
                _ = UpdateAccSaberInfo();
            }
		}

		public async Task<UserInfo?> GetPlatformUserInfo()
		{
			// GetUserInfo caches the result, no need to do it ourselves
			return await _platformUserModel.GetUserInfo();
		}

        public void InvalidateCurrentMapCache()
        {
            if (CurrentRankedMap is not null)
                AccsaberAPI.InvalidateCache(CurrentRankedMap.DifficultyId);
        }
        private void UpdateLeaderboardOnRelationChanged()
        {
            if ((AccSaberLeaderboardViewController.Instance.DisplayType & LeaderboardDisplayType.Relations) > 0)
                _ = AccSaberLeaderboardViewController.Instance.RequestRefresh();
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
            PlayerSocialLife.OnRelationChanged += UpdateLeaderboardOnRelationChanged;

            //These are all independent tasks, so start each of them on their own thread
			Task.Run(UpdateAccSaberInfo);
			Task.Run(() => StartWebsocket(WebsocketCanceller.Token));
		}
        public void Dispose()
        {
            OnScoreUpdated -= UpdatePlayerScore;
            PlayerSocialLife.OnRelationChanged -= UpdateLeaderboardOnRelationChanged;
        }
	}
}