using Accsaber.Utils;
using AccSaber.API;
using AccSaber.Models;
using AccSaber.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiraUtil.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
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

		public event Action<AccSaberDifficulty?>? OnAccSaberRankedMapUpdated;
		public event Action? OnAccSaberScoreUpdated;
		public event Action? OnUpdatingFromAccSaberAPI;
		public event Action<bool>? OnUpdatedFromAccSaberAPI;

		private Dictionary<string, AccSaberBasicDifficulty[]>? _rankedMaps = [];
		private AccSaberUser? _currentUser;

		public IReadOnlyDictionary<string, AccSaberBasicDifficulty[]>? RankedMaps => _rankedMaps;
        public  DateTime LastLocalUpdateTime { get; private set; } = DateTime.MinValue;
		public List<AccSaberMilestone>_currentUserMilestones = [];

        public static event Action<AccSaberLeaderboardEntry>? OnScoreUpdated;
        public static event Action<AccSaberLeaderboardEntry>? OnPlayerScoreUpdated;
		internal static CancellationTokenSource WebsocketCanceller { get; private set; } = new();
        internal const int RecieveBufferSize = 1024;
        internal const int SendBufferSize = 16;

        private static readonly ClientWebSocket webSocket = new();
        private static readonly AsyncLock listenerLock = new();

        private AccSaberDifficulty? _currentRankedMap;

#pragma warning disable IDE0290
		public AccSaberStore(SiraLog log, IPlatformUserModel platformUserModel)
		{
			_log = log;
			_platformUserModel = platformUserModel;
		}

		public AccSaberDifficulty? CurrentRankedMap
		{
			get => _currentRankedMap;
			set
			{
				_currentRankedMap = value;
				OnAccSaberRankedMapUpdated?.Invoke(_currentRankedMap);
			}
		}
		public AccSaberUser? CurrentUser => _currentUser;


        private async Task SetRankedMaps(bool fullMaps)
		{
			
			if (fullMaps)
			{
				await AccsaberAPI.LoadAllMaps();
				_rankedMaps = null;
            }
			else
			{
				_rankedMaps = await AccsaberAPI.GetAllBasicDiffs();
			}
        }

		public async Task<List<AccSaberMilestone>> GetUserMilestones(bool completed)
		{
			var platformUser = await GetPlatformUserInfo();

			if (platformUser is not null)
			{
				//var response = await Plugin.WebClient.GetAsync(completed ? $"v1/milestones/completion-stats?userId={platformUser.platformUserId}&sort=completedAt" : $"v1/milestones/completion-stats?userId={platformUser.platformUserId}&sort=progress");
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
					return newMilestones;
				}
			}
			return [];
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

			AccSaberUser? newOverall = await AccsaberAPI.GetPlayerInfo(platformUser.platformUserId, true);

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
		public async Task<AccSaberUser> GetCurrentUserAsync()
		{
			if (_currentUser is not null)
				return _currentUser;

			await UpdateAccSaberInfo();

			return _currentUser!;
		}
		public async void SetMapFromBasicInfo(string hash, BeatmapDifficulty difficulty)
		{
            CurrentRankedMap = (await AccsaberAPI.GetLeaderboard(hash))?.Difficulties.FirstOrDefault(diff => diff.Difficulty == difficulty);
        }
		public async void SetMapFromBasicDifficulty(AccSaberBasicDifficulty? difficulty)
		{
			CurrentRankedMap = difficulty is null ? null : (await AccsaberAPI.GetLeaderboard(difficulty.Hash))?.Difficulties.FirstOrDefault(diff => diff.Difficulty == difficulty.Difficulty);
        }


        public async Task StartWebsocket(CancellationToken ct = default)
        {
            while (!ct.IsCancellationRequested)
                await ListenForScores(ct);
        }
        private async Task ListenForScores(CancellationToken ct)
        {
            AsyncLock.Releaser? theLock = await listenerLock.TryLockAsync();
            if (theLock is null)
                return;
            using (theLock.Value)
            {
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
            //These are all independent tasks, so start each of them on their own thread
            Task.Run(async () => await SetRankedMaps(true));
			Task.Run(async () => _currentUserMilestones = await GetUserMilestones(true));
			Task.Run(UpdateAccSaberInfo);
			Task.Run(() => StartWebsocket(WebsocketCanceller.Token));
		}
	}
}