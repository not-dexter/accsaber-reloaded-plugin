using AccSaber.API;
using AccSaber.Consts;
using AccSaber.Models;
using AccSaber.Utils;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace AccSaber.LeaderboardSources
{
	internal sealed class GlobalLeaderboardSource : LeaderboardSource, ILeaderboardSource
	{
		public string HoverHint => "Global";
		public Task<Sprite> Icon => VersionUtils.LoadSpriteFromAssemblyAsync(ResourcePaths.GLOBAL_ICON);
		public int TotalPages { get; private set; } = -1;

		protected override async Task<IEnumerable<AccSaberLeaderboardEntry>?> LoadScoresAsync(AccSaberBasicDifficulty diff, CancellationToken cancellationToken = default, int page = 0)
		{
            AccSaberLeaderboardEntry[]? entries = await AccsaberAPI.GetScoreData(page, diff.Hash!, diff.Difficulty);

			if (TotalPages < 0 && entries is not null)
				TotalPages = Mathf.CeilToInt(await AccsaberAPI.GetLength(diff) / (float)AccsaberAPI.PAGE_LENGTH);

			return entries;
			
		}
	}
}