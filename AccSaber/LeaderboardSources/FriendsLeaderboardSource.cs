using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AccSaber.Managers;
using AccSaber.Models;
using AccSaber.Utils;
using AccSaber.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using SiraUtil.Logging;
using AccSaber.Consts;

namespace AccSaber.LeaderboardSources
{
	internal sealed class FriendsLeaderboardSource : ILeaderboardSource
	{
		private readonly List<List<AccSaberLeaderboardEntry>> _cachedEntries = new();
		
		private readonly WebUtils _webUtils;
		private readonly AccSaberStore _accSaberStore;
		private PluginConfig _pluginConfig = null!;
		private readonly SiraLog _log;

		public FriendsLeaderboardSource(WebUtils webUtils, AccSaberStore accSaberStore, PluginConfig pluginConfig, SiraLog log)
		{
			_webUtils = webUtils;
			_accSaberStore = accSaberStore;
			_pluginConfig = pluginConfig;
			_log = log;
		}
		
		public string HoverHint => "Friends";

		public Task<Sprite> Icon => VersionUtils.LoadSpriteFromAssemblyAsync(ResourcePaths.FRIEND);

		public int TotalPages { get; set; }
		public bool Scrollable => true;

		public async Task<List<AccSaberLeaderboardEntry>?> GetScoresAsync(AccSaberRankedMap rankedMap, CancellationToken cancellationToken = default, int page = 0)
		{
			if (_cachedEntries.Count >= page + 1)
			{
				return _cachedEntries[page];
			}

			var response = await Plugin.WebClient.GetAsync($"/v1/maps/difficulties/leaderboard/{rankedMap.BlLeaderboardId}/scores?page={page}&size=10&relation=follower");


			if (response is null)
			{
				return null;
			}

			var leaderboard = new List<AccSaberLeaderboardEntry>();

			var parsedStr = await response.Content.ReadAsStringAsync();

			if (parsedStr != null)
			{
				var parsed = JObject.Parse(parsedStr);

				TotalPages = parsed["totalPages"]!.ToObject<int>();

				if (parsed["content"] is JArray content)
				{
					var scores = JsonConvert.DeserializeObject<List<AccSaberLeaderboardEntry>>(content.ToString());

					foreach (var score in scores)
					{
						AccSaberLeaderboardEntry newScore = new()
						{
							Rank = leaderboard.Any() ? leaderboard.Count + 1 + page * 10 : 1 + page * 10,
							PlayerId = score.PlayerId,
							AvatarURL = score.AvatarURL,
							PlayerName = score.PlayerName,
							Accuracy = score.Accuracy,
							Score = score.Score,
							AP = score.AP,
							Modifiers = score.Modifiers,
							AccChamp = score.AccChamp,
							TimeSet = score.TimeSet
						};

						leaderboard.Add(newScore);
					}
				}
			}

			_cachedEntries.Add(leaderboard);
			return leaderboard;
		}
		public List<AccSaberLeaderboardEntry>? GetCachedScore(int page)
		{
			return _cachedEntries[page];
		}

		public List<AccSaberLeaderboardEntry>? GetLatestCachedScore()
		{
			return _cachedEntries.LastOrDefault();
		}

		public void ClearCache()
		{
			_cachedEntries.Clear();
		}
	}
}