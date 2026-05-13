using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AccSaber.Managers;
using AccSaber.Models;
using AccSaber.Utils;
using UnityEngine;
using SiraUtil.Logging;
using AccSaber.Consts;
using AccSaber.API;

namespace AccSaber.LeaderboardSources
{
	internal sealed class CountryLeaderboardSource : LeaderboardSource, ILeaderboardSource
	{
		private readonly SiraLog _log;
		private readonly AccSaberStore _accSaberStore;

		public CountryLeaderboardSource(AccSaberStore accSaberStore, SiraLog log)
		{
			_accSaberStore = accSaberStore;
			_log = log;
		}

		public string HoverHint => "Country";

		public Task<Sprite> Icon => VersionUtils.LoadSpriteFromAssemblyAsync(ResourcePaths.COUNTRY);

		public int TotalPages { get; private set; }

		protected override async Task<IEnumerable<AccSaberLeaderboardEntry>?> LoadScoresAsync(AccSaberBasicDifficulty diff, CancellationToken cancellationToken = default, int page = 0)
		{

			var userInfo = await _accSaberStore.GetPlatformUserInfo();
			if (userInfo is null)
			{
				return null;
			}

			string? diffId = await AccsaberAPI.GetLeaderboardDifficultyId(diff);

			if (diffId is null)
				return null;

			AccSaberLeaderboardEntry[]? outp = await AccsaberAPI.GetScoreData(page, diffId, (await _accSaberStore.GetCurrentUserAsync()).Country);

            if (TotalPages < 0 && outp is not null)
                TotalPages = Mathf.CeilToInt(await AccsaberAPI.GetLength(diff) / (float)AccsaberAPI.PAGE_LENGTH);

			return outp;
        }
	}
}