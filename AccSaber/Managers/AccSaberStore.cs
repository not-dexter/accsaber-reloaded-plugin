using AccSaber.API;
using AccSaber.Models;
using AccSaber.Models.PlayerModels;
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
	internal sealed class AccSaberStore : IInitializable
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
		public List<AccSaberMilestone> _currentUserMilestones = [];
        
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
			var platformUser = await GetPlatformUserInfo();

			if (platformUser is not null)
			{
				string call = string.Format(completed ? HelpfulPaths.APAPI_MILESTONE_COMPLETE : HelpfulPaths.APAPI_MILESTONE_INCOMPLETE, platformUser.platformUserId);
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
            var platformUser = await GetPlatformUserInfo();

            if (platformUser is not null)
            {
                string call = string.Format(allPools ? HelpfulPaths.APAPI_MISSIONS : HelpfulPaths.APAPI_MISSIONS_POOL, platformUser.platformUserId, nameof(pool).ToLower());

                List<AccSaberMission>? outp = await APIHandler.CallAPI_Json<List<AccSaberMission>>(call, AccsaberAPI.throttler);

                if (outp is null)
                    return [];

                List<AccSaberMission> newMissions = [];

                foreach (AccSaberMission mission in outp)
                {
                    newMissions.Add(mission);
                }

                return newMissions;
            }
            return [];
        }

        public enum NewsType
        {
            All,
            General,
            Batch,
            Campaign,
            Curve,
            Milestones
        }

        public async Task<List<AccSaberNewsEntry>> GetNewsPosts(NewsType type)
        {
            var typeString = type switch
            {
                NewsType.General => "GENERAL",
                NewsType.Batch => "BATCH",
                NewsType.Campaign => "CAMPAIGN",
                NewsType.Curve => "CURVE",
                NewsType.Milestones => "MILESTONE_SET",
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

        private async Task UpdateAccSaberInfo()
		{
			OnUpdatingFromAccSaberAPI?.Invoke();

			UserInfo? platformUser = await GetPlatformUserInfo();

			if (platformUser is null)
			{
				_log.Error("platformUser is null");
				return;
			}

			AccSaberPlayer? newOverall = await AccsaberAPI.GetPlayerInfo(platformUser.platformUserId, true, false);

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
            AsyncLock.Releaser? theLock = await listenerLock.TryLockAsync();
            if (theLock is null)
                return;
            using (theLock.Value)
                while (!ct.IsCancellationRequested)
					await ListenForScores(ct);
        }
        private async Task ListenForScores(CancellationToken ct)
        {
			using ClientWebSocket webSocket = new();
            await webSocket.ConnectAsync(new(HelpfulPaths.APAPI_WEBSOCKET), ct);
            try
            {
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
                    ms.Seek(0, SeekOrigin.Begin);
                    ms.Position = 0;
                }
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
			if (score.PlayerId.Equals(PlayerSocialLife.PlayerID))
				OnPlayerScoreUpdated?.Invoke(score);
		}

		public async Task<UserInfo?> GetPlatformUserInfo()
		{
			// GetUserInfo caches the result, no need to do it ourselves
			return await _platformUserModel.GetUserInfo();
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

            //These are all independent tasks, so start each of them on their own thread
			Task.Run(async () => _currentUserMilestones = await GetUserMilestones(true));
			Task.Run(UpdateAccSaberInfo);
			Task.Run(() => StartWebsocket(WebsocketCanceller.Token));
		}
	}
}