using AccSaber.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace AccSaber.LeaderboardSources
{
    internal interface ILeaderboardSource
    {
        public string HoverHint { get; }
        public Task<Sprite> Icon { get; }
        public int TotalPages { get; }

        public Task<List<AccSaberLeaderboardEntry>?> GetScoresAsync(AccSaberBasicDifficulty diff, CancellationToken cancellationToken = default, int page = 0);
        public AccSaberLeaderboardEntry? GetLastCachedScore(int entryNumber);
    }
}
