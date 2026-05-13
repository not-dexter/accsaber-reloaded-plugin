using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AccSaber.Models;
using UnityEngine;

namespace AccSaber.LeaderboardSources
{
	internal abstract class LeaderboardSource
	{
		private List<AccSaberLeaderboardEntry> lastLoadedEntries = [];
        protected abstract Task<IEnumerable<AccSaberLeaderboardEntry>?> LoadScoresAsync(AccSaberBasicDifficulty diff, CancellationToken cancellationToken = default, int page = 0);
        public async Task<List<AccSaberLeaderboardEntry>?> GetScoresAsync(AccSaberBasicDifficulty diff, CancellationToken cancellationToken = default, int page = 0)
		{
			IEnumerable<AccSaberLeaderboardEntry>? vals = await LoadScoresAsync(diff, cancellationToken, page);

			if (vals is null)
				return null;

			List<AccSaberLeaderboardEntry> outp = [.. vals];

			lastLoadedEntries = outp;

			return outp;
        }
		public AccSaberLeaderboardEntry? GetLastCachedScore(int entryNumber) => lastLoadedEntries.Count > entryNumber ? lastLoadedEntries[entryNumber] : null;
	}
}