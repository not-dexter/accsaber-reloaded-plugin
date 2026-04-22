using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AccSaber.Managers;
using AccSaber.Models;
using AccSaber.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using UnityEngine;

namespace AccSaber.LeaderboardSources
{
	internal sealed class AroundMeLeaderboardSource : ILeaderboardSource
	{
		private readonly List<List<AccSaberLeaderboardEntry>> _cachedEntries = new();
		
		private readonly WebUtils _webUtils;
		private readonly AccSaberStore _accSaberStore;

		public AroundMeLeaderboardSource(WebUtils webUtils, AccSaberStore accSaberStore)
		{
			_webUtils = webUtils;
			_accSaberStore = accSaberStore;
		}
		
		public string HoverHint => "Around Me";

		public Task<Sprite> Icon => BeatSaberMarkupLanguage.Utilities.LoadSpriteFromAssemblyAsync("AccSaber.Resources.PlayerIcon.png");
		
		public bool Scrollable => false;
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

			if (_accSaberStore.CurrentRankedMap is null)
				return null;

			var response = await client.GetAsync($"https://api.accsaberreloaded.com/v1/maps/difficulties/leaderboard/{_accSaberStore.CurrentRankedMap.BlLeaderboardId}/scores-around/{userInfo.platformUserId}?above=4&below=5");

			if (response is null)
			{
				return null;
			}

			var leaderboard = new List<AccSaberLeaderboardEntry>();

			var parsedStr = await response.Content.ReadAsStringAsync();

			if (parsedStr != null)
			{
				var parsed = JObject.Parse(parsedStr);

				if (parsed["playerScore"] is JToken playerScore && parsed["scoresAbove"] is JArray scoresAbove && parsed["scoresBelow"] is JArray scoresBelow)
				{

					scoresAbove.Add(playerScore);
					scoresAbove.Merge(scoresBelow);

					var scores = JsonConvert.DeserializeObject<List<AccSaberLeaderboardEntry>>(scoresAbove.ToString());

					foreach (var score in scores)
					{
						leaderboard.Add(score);
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