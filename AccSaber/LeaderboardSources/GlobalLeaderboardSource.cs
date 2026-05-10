using AccSaber.Consts;
using AccSaber.Models;
using AccSaber.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiraUtil.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace AccSaber.LeaderboardSources
{
	internal sealed class GlobalLeaderboardSource : ILeaderboardSource
	{
		private readonly List<List<AccSaberLeaderboardEntry>> _cachedEntries = new();

		private readonly WebUtils _webUtils;

		private readonly SiraLog _log;

		public GlobalLeaderboardSource(WebUtils webUtils, SiraLog log)
		{
			_webUtils = webUtils;
			_log = log;
		}

		public string HoverHint => "Global";

		public Task<Sprite> Icon => VersionUtils.LoadSpriteFromAssemblyAsync(ResourcePaths.GLOBAL_ICON);
		
		public bool Scrollable => true;

		public int TotalPages { get; set; }
		public async Task<List<AccSaberLeaderboardEntry>?> GetScoresAsync(AccSaberRankedMap rankedMap, CancellationToken cancellationToken = default, int page = 0)
		{
			if (_cachedEntries.Count >= page + 1)
			{
				return _cachedEntries[page];
			}

			var response = await Plugin.WebClient.GetAsync($"/v1/maps/difficulties/leaderboard/{rankedMap.BlLeaderboardId}/scores?page={page}&size=10");


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