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

namespace AccSaber.LeaderboardSources
{
	internal sealed class CountryLeaderboardSource : ILeaderboardSource
	{
		private readonly List<List<AccSaberLeaderboardEntry>> _cachedEntries = new();
		
		private readonly WebUtils _webUtils;
		private readonly AccSaberStore _accSaberStore;
		private PluginConfig _pluginConfig = null!;

		public CountryLeaderboardSource(WebUtils webUtils, AccSaberStore accSaberStore, PluginConfig pluginConfig)
		{
			_webUtils = webUtils;
			_accSaberStore = accSaberStore;
			_pluginConfig = pluginConfig;
		}

		public string HoverHint => "Country";

		public Task<Sprite> Icon => BeatSaberMarkupLanguage.Utilities.LoadSpriteFromAssemblyAsync("AccSaber.Resources.country.png");

		public bool Scrollable => true;

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

			HttpClient client = new();
			client.DefaultRequestHeaders
			.Accept
			.Add(new MediaTypeWithQualityHeaderValue("application/json"));//ACCEPT header

			var response = await client.GetAsync($"https://api.accsaberreloaded.com/v1/maps/difficulties/leaderboard/{rankedMap.BlLeaderboardId}/scores?page={page}&country={_accSaberStore.GetCurrentCategoryUserAsync().Result.Country}&size=10");

			if (response is null)
			{
				return null;
			}

			var leaderboard = new List<AccSaberLeaderboardEntry>();

			var parsedStr = await response.Content.ReadAsStringAsync();

			if (parsedStr != null)
			{
				var parsed = JObject.Parse(parsedStr);

				if (parsed["content"] is JArray content)
				{
					var scores = JsonConvert.DeserializeObject<List<AccSaberLeaderboardEntry>>(content.ToString());

					foreach (var score in scores)
					{
						AccSaberLeaderboardEntry newScore = new()
						{
							Rank = leaderboard.Any() ? leaderboard.Count + 1 + page * 10 : 1 + page * 10,
							PlayerId = score.PlayerId,
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