using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using AccSaber.Models;
using AccSaber.Utils;
using SiraUtil.Logging;
using Zenject;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Net.WebSockets;

namespace AccSaber.Managers
{
	internal sealed class AccSaberStore : IInitializable
	{
		private readonly SiraLog _log;
		private readonly WebUtils _webUtils;
		private readonly IPlatformUserModel _platformUserModel;
		private static readonly HttpClient client = new();

		public event Action<AccSaberRankedMap?>? OnAccSaberRankedMapUpdated;
		public event Action? OnAccSaberScoreUpdated;
		public event Action? OnUpdatingFromAccSaberAPI;
		public event Action<bool>? OnUpdatedFromAccSaberAPI;

		public Dictionary<string, AccSaberRankedMap> RankedMaps = new();
		private AccSaberUser _currentUserOverall = new();
		private AccSaberUser _currentUserTrue = new();
		private AccSaberUser _currentUserStandard = new();
		private AccSaberUser _currentUserTech = new();
		public  DateTime LastLocalUpdateTime { get; private set; } = DateTime.MinValue;

		private AccSaberRankedMap? _currentRankedMap;

		public AccSaberStore(SiraLog log, WebUtils webUtils, IPlatformUserModel platformUserModel)
		{
			_log = log;
			_webUtils = webUtils;
			_platformUserModel = platformUserModel;
		}
		public enum AccSaberMapCategories
		{
			True,
			Standard,
			Tech
		}
		
		public AccSaberRankedMap? CurrentRankedMap
		{
			get => _currentRankedMap;
			set
			{
				_currentRankedMap = value;
				OnAccSaberRankedMapUpdated?.Invoke(_currentRankedMap);
			}
		}

		private async Task<Dictionary<string, AccSaberRankedMap>> GetRankedMaps()
		{
			HttpClient client = new();
			client.DefaultRequestHeaders
			.Accept
			.Add(new MediaTypeWithQualityHeaderValue("application/json"));//ACCEPT header

			var response = await client.GetAsync("https://api.accsaberreloaded.com/v1/maps?status=RANKED&size=99999");

			if (response == null)
			{
				_log.Error("Failed to get ranked maps from AccSaber API");
				return new Dictionary<string, AccSaberRankedMap>();
			}

			var rankedMaps = new Dictionary<string, AccSaberRankedMap>();

			var parsedStr = await response.Content.ReadAsStringAsync();

			if (parsedStr != null)
			{
				var parsed = JObject.Parse(parsedStr);

				if (parsed["content"] is JArray content)
				{
					var maps = JsonConvert.DeserializeObject<List<AccSaberRankedMap>>(content.ToString());

					foreach (var map in maps)
					{
						foreach (var diff in map.Difficulties)
						{
							if (diff.Difficulty == "EXPERT_PLUS")
							{
								map.Difficulty = "EXPERTPLUS";
								diff.Difficulty = "EXPERTPLUS";
							}
							else
								map.Difficulty = diff.Difficulty;


							string hash = $"{map.SongHash}/{diff.Difficulty}".ToLower();

							if (rankedMaps.ContainsKey(hash))
								continue;

							AccSaberRankedMap newMap = new()
							{
								SongName = map.SongName,
								SongSubName = map.SongSubName,
								SongAuthorName = map.SongAuthorName,
								LevelAuthorName = map.LevelAuthorName,
								BeatSaverKey = map.BeatSaverKey,
								SongHash = map.SongHash,
								Difficulties = map.Difficulties,
								Complexity = diff.Complexity,
								BlLeaderboardId = diff.BlLeaderboardId,
								LeaderboardId = map.LeaderboardId,
								Difficulty = diff.Difficulty,
								CategoryId = diff.CategoryId,
								DateRanked = map.DateRanked,
								Category = diff.CategoryId switch
								{
									"b0000000-0000-0000-0000-000000000001" => AccSaberStore.AccSaberMapCategories.True,
									"b0000000-0000-0000-0000-000000000002" => AccSaberStore.AccSaberMapCategories.Standard,
									"b0000000-0000-0000-0000-000000000003" => AccSaberStore.AccSaberMapCategories.Tech,
									_ => throw new ArgumentOutOfRangeException()
								}

							};


							if (!rankedMaps.ContainsKey(hash))
							{
								rankedMaps.Add(hash, newMap);
							}
						}
					}
				}

			}

			return rankedMaps;
		}

		private async Task UpdateAccSaberInfo(DateTime? lastAPIUpdateTime = null)
		{
			OnUpdatingFromAccSaberAPI?.Invoke();

			var platformUser = await GetPlatformUserInfo();
			if (platformUser is null)
			{
				_log.Error("platformUser is null");
				return;
			}

			var newOverall = await GetUserFromId(platformUser.platformUserId);

			// Check if the data fetched is the same as what we already have cached
			// Saves us from calling the API three more times for the True, Standard and Tech user categories.
			if (Math.Abs(newOverall.AP - _currentUserOverall.AP) < 0.01f)
			{
				OnUpdatedFromAccSaberAPI?.Invoke(false);
				return;
			}

			_currentUserOverall = newOverall;
			await Task.Delay(1000);
			_currentUserTrue = await GetUserFromId(platformUser.platformUserId, AccSaberMapCategories.True);
			await Task.Delay(1000);
			_currentUserStandard = await GetUserFromId(platformUser.platformUserId, AccSaberMapCategories.Standard);
			await Task.Delay(1000);
			_currentUserTech = await GetUserFromId(platformUser.platformUserId, AccSaberMapCategories.Tech);

			OnUpdatedFromAccSaberAPI?.Invoke(true);
		}
	
		public Task<AccSaberUser> GetCurrentUser(AccSaberMapCategories? category = null)
		{
			return Task.FromResult(category switch
			{
				AccSaberMapCategories.True => _currentUserTrue,
				AccSaberMapCategories.Standard => _currentUserStandard,
				AccSaberMapCategories.Tech => _currentUserTech,
				null => _currentUserOverall,
				_ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
			});
		}

		public async Task ListenForScores()
		{
			ClientWebSocket? webSocket = new();	
			await webSocket.ConnectAsync(new Uri("wss://accsaberreloaded.com/ws/scores"), CancellationToken.None);
			var platformUser = await GetPlatformUserInfo();
			if (platformUser is null)
			{
				_log.Error("platformUser is null");
				return;
			}
			try
			{
				using var ms = new MemoryStream();
				while (webSocket.State == WebSocketState.Open)
				{
					WebSocketReceiveResult result;
					do
					{
						var messageBuffer = WebSocket.CreateClientBuffer(1024, 16);
						result = await webSocket.ReceiveAsync(messageBuffer, CancellationToken.None);
						if (result.MessageType == WebSocketMessageType.Close)
						{
							await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
							_ = ListenForScores();
						}
						ms.Write(messageBuffer.Array, messageBuffer.Offset, result.Count);
					}
					while (!result.EndOfMessage);

					if (result.MessageType == WebSocketMessageType.Text)
					{
						var msgString = Encoding.UTF8.GetString(ms.ToArray());
						var message = JsonConvert.DeserializeObject<AccSaberScore>(msgString);

						if (message.UserId == platformUser.platformUserId)
					 	{
							OnAccSaberScoreUpdated?.Invoke();
						}
						await UpdateAccSaberInfo();
					} 
					ms.SetLength(0);
					ms.Seek(0, SeekOrigin.Begin);
					ms.Position = 0;
				}
			}
			catch (OperationCanceledException)
			{
				_log.Info("[WS] The remote party closed the WebSocket connection without completing the close handshake.");
                _ = ListenForScores();
			}
		}

		public async Task<AccSaberUser> GetUserFromId(string id, AccSaberMapCategories? category = null) // TODO: rewrite this mess
		{ 
			string wantedCategory = "";

			switch (category)
			{
				case AccSaberMapCategories.Standard:
					wantedCategory = "b0000000-0000-0000-0000-000000000002";
					break;
				case AccSaberMapCategories.True:
					wantedCategory = "b0000000-0000-0000-0000-000000000001";
					break;
				case AccSaberMapCategories.Tech:
					wantedCategory = "b0000000-0000-0000-0000-000000000003";
					break;
				case null:
					wantedCategory = "b0000000-0000-0000-0000-000000000005";
					break;
			}

			var userCall = await client.GetAsync($"https://api.accsaberreloaded.com/v1/users/{id}");

			var statisticsCall = await client.GetAsync($"https://api.accsaberreloaded.com/v1/users/{id}/statistics/all");

			if (userCall == null || statisticsCall == null)
			{
				_log.Error($"Failed to get user {id} from AccSaber API");
				return new AccSaberUser();
			}

			var userdStr = await userCall.Content.ReadAsStringAsync();
			var statisticsStr = await statisticsCall.Content.ReadAsStringAsync();

			if (userdStr != null && statisticsStr != null)
			{
				var user = JObject.Parse(userdStr);
				var statistics = JObject.Parse(statisticsStr);

				if (statistics["categories"] is JArray categories && user != null && statistics != null)
				{
					for (int i = 0; i < categories.Count; i++)
					{
                        if (categories[i]["categoryId"]!.ToString() != wantedCategory)
                            continue;

						var newUser = new AccSaberUser
						{
							PlayerName = user["name"]!.ToString(),

							Rank = categories[i]["ranking"]!.ToObject<int>(),

							CountryRank = categories[i]["countryRanking"]!.ToObject<int>(),

							PlayerId = user["id"]!.ToString(),

							LevelData = JsonConvert.DeserializeObject<LevelData>(user["levelData"]!.ToString()),

							AvatarUrl = user["avatarUrl"]!.ToString(),

							AverageAcc = categories[i]["averageAcc"]!.ToObject<float>(),

							AP = categories[i]["ap"]!.ToObject<float>(),

							Hmd = user["hmd"]?.ToString(),

							Country = user["country"]!.ToString(),

							AverageApPerMap = categories[i]["averageAp"]!.ToObject<float>(),

							RankedPlays = categories[i]["rankedPlays"]!.ToObject<int>(),

							AccChamp = true,
						};
			
						return newUser;
					}
				}
			}

			_log.Error($"Failed to get user {id} from AccSaber API");
			return new AccSaberUser();
		}


		public async Task<UserInfo?> GetPlatformUserInfo()
		{
			// GetUserInfo caches the result, no need to do it ourselves
			return await _platformUserModel.GetUserInfo(CancellationToken.None);
		}

		public async Task<AccSaberUser> GetCurrentCategoryUserAsync()
		{
			return _currentRankedMap?.Category switch
			{
				AccSaberMapCategories.True => await GetCurrentUser(AccSaberMapCategories.True),
				AccSaberMapCategories.Standard => await GetCurrentUser(AccSaberMapCategories.Standard),
				AccSaberMapCategories.Tech => await GetCurrentUser(AccSaberMapCategories.Tech),
				_ => await GetCurrentUser()
			};
		}

		public AccSaberUser GetCurrentOverallUser()
		{
			return _currentUserOverall;
		}

		public AccSaberUser GetCurrentCategoryUser()
		{
			return _currentRankedMap?.Category switch
			{
				AccSaberMapCategories.True => _currentUserTrue,
				AccSaberMapCategories.Standard => _currentUserStandard,
				AccSaberMapCategories.Tech => _currentUserTech,
				_ => _currentUserOverall
			};
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
		
		public async void Initialize()
		{
			RankedMaps = await GetRankedMaps();
			await Task.Delay(1000);
			await UpdateAccSaberInfo();
			await ListenForScores();
		}
	}
}