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
using AccSaber.API;

namespace AccSaber.LeaderboardSources
{
	internal sealed class FriendsLeaderboardSource : LeaderboardSource, ILeaderboardSource
	{
		private readonly SiraLog _log;

		public FriendsLeaderboardSource(SiraLog log)
		{
			_log = log;
		}
		
		public string HoverHint => "Friends";
		public Task<Sprite> Icon => VersionUtils.LoadSpriteFromAssemblyAsync(ResourcePaths.FRIEND);
		public int TotalPages { get; private set; }

		protected override async Task<IEnumerable<AccSaberLeaderboardEntry>?> LoadScoresAsync(AccSaberBasicDifficulty diff, CancellationToken cancellationToken = default, int page = 0)
		{
			string? diffId = await AccsaberAPI.GetLeaderboardDifficultyId(diff);

			if (diffId is null)
				return null;

			AccSaberLeaderboardEntry[]? outp = await AccsaberAPI.GetScoreData(page, diffId, RelationType.follower);

			if (TotalPages < 0 && outp is not null)
                TotalPages = Mathf.CeilToInt(await AccsaberAPI.GetLength(diff) / (float)AccsaberAPI.PAGE_LENGTH);

			return outp;
        }
	}
}