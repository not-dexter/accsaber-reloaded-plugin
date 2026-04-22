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

		public Task<Sprite> Icon => BeatSaberMarkupLanguage.Utilities.LoadSpriteFromAssemblyAsync("AccSaber.Resources.friend.png");

		public bool Scrollable => true;

		// This is more of a placeholder while we wait on the full friends system for reloaded but it works for now
		public async Task<List<AccSaberLeaderboardEntry>?> GetScoresAsync(AccSaberRankedMap rankedMap, CancellationToken cancellationToken = default, int page = 0)
		{
			if (_cachedEntries.Count >= page + 1)
			{
				return _cachedEntries[page];
			}

			var userInfo = await _accSaberStore.GetPlatformUserInfo();
			if (userInfo is null)
			{
				return null;
			}

			var diff = (rankedMap.Difficulty == "EXPERTPLUS") ? "EXPERT_PLUS" : rankedMap.Difficulty;

			HttpClient client = new();
			client.DefaultRequestHeaders
			.Accept
			.Add(new MediaTypeWithQualityHeaderValue("application/json"));//ACCEPT header

			var scoresList = new List<AccSaberLeaderboardEntry>();

			var leaderboard = new List<AccSaberLeaderboardEntry>();

			var ownScore = await client.GetAsync($"https://api.accsaberreloaded.com/v1/users/{_accSaberStore.GetCurrentUser().Result.PlayerId}/scores/by-hash/{rankedMap.SongHash}?difficulty={diff}&characteristic=Standard");

			if (ownScore is null)
			{
				return null;
			}

			if (ownScore.StatusCode != System.Net.HttpStatusCode.NotFound)
			{
				var parsedStr = await ownScore.Content.ReadAsStringAsync();

				if (parsedStr != null)
				{
					var parsed = JObject.Parse(parsedStr);

					var score = JsonConvert.DeserializeObject<AccSaberLeaderboardEntry>(parsed.ToString());

					scoresList.Add(score);
				}
			}

			foreach (var friend in _pluginConfig.Friends)
			{
				var response = await client.GetAsync($"https://api.accsaberreloaded.com/v1/users/{friend}/scores/by-hash/{rankedMap.SongHash}?difficulty={diff}&characteristic=Standard");

				if (response is null)
				{
					return null;
				}

				if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
					continue;

				var parsedStr = await response.Content.ReadAsStringAsync();

				if (parsedStr != null)
				{
					var parsed = JObject.Parse(parsedStr);

					var score = JsonConvert.DeserializeObject<AccSaberLeaderboardEntry>(parsed.ToString());

					scoresList.Add(score);
										
				}
				scoresList.OrderByDescending(o => o.Score);
			}
			 
			var newscoresList = scoresList.OrderByDescending(x => x.Score)
				  .ThenBy(x => x.TimeSet)
				  .ToList();

			var scorepage = (page == 0) ? newscoresList.Take(10).ToList() : newscoresList.Skip(10 * page).Take(10).ToList();

			foreach (var score in scorepage)
			{
				var index = scorepage.FindIndex(a => a == score);
				score.Rank = scoresList.Any() ? index + 1 + page * 10 : 1 + page * 10;
				leaderboard.Add(score);
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