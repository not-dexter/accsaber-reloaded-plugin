using AccSaber.API;
using AccSaber.Consts;
using AccSaber.Models;
using AccSaber.Models.CacheModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Zenject;

namespace AccSaber.Utils.Misc
{
    internal sealed class SerializationHandler
    {
        [Inject] private readonly AccsaberAPI api = null!;
        [Inject] private readonly PlayerSocialLife playerInfo = null!;

        public readonly Dictionary<string, CacheInfo> CacheInfos;

        public int TotalMaps { get; private set; } = -1;
        public Dictionary<string, AccSaberBasicMap> CachedMaps = null!;
        public Dictionary<string, AccSaberBasicDifficulty> CachedDifficulties = null!;

        private AccSaberSerializedCache<AccSaberPlayerScore> _playerCache = null!;
        public List<AccSaberPlayerScore> PlayerScores => _playerCache.Content;
        public int PlayerScoreLength
        {
            get => _playerCache.MaxLength;
            set => _playerCache.MaxLength = value;
        }
        public int[] CategoryPlayerScoreLength
        {
            get
            {
                _playerCache.ExtraData ??= [new int[3] { -1, -1, -1 }];

                return (int[])_playerCache.ExtraData[0];
            }
        }

        public SerializationHandler()
        {
            CacheInfos = new(2)
            {
                { ResourcePaths.MAP_CACHE_NAME, new(typeof(AccSaberSerializedCache<AccSaberBasicMap>), ValidateMapCache, LoadMapCache) },
                { ResourcePaths.PLAYER_SCORE_CACHE_NAME, new(typeof(AccSaberSerializedCache<AccSaberPlayerScore>), ValidatePlayerScoreCache, null) }
            };
        }
        /*And btw, if you would prefer not to do the maps, I do have an idea for a compromise. If instead you add 2 properties to the score json object that is returned in normal endpoints, it help speed up the leaderboard loading the score menu.  

The properties are playerScoreRank and playerCategoryScoreRank. playerScoreRank is what index the score is on the list of all player scores (sorted by ap, not weighted), playerCategoryScoreRank is the same thing but for the given category.  */

        internal async void SetCacheData(SerializerUtils serializerUtils)
        {
            try
            {
                void HandleMapCache(AccSaberSerializedCache cache)
                {
                    if (cache is not AccSaberSerializedCache<AccSaberBasicMap> mapCache)
                        return;

                    CachedMaps = [with(mapCache.Content.Select(map => new KeyValuePair<string, AccSaberBasicMap>(map.Hash, map)))];
                    CachedDifficulties = [with(mapCache.Content.SelectMany(map => map.Difficulties)
                        .Select(diff => new KeyValuePair<string, AccSaberBasicDifficulty>(diff.DifficultyId, diff)))];
                }

                void HandlePlayerScoreCache(AccSaberSerializedCache cache)
                {
                    if (cache is not AccSaberSerializedCache<AccSaberPlayerScore> playerCache)
                        return;

                    _playerCache = playerCache;
                }

                foreach (AccSaberSerializedCache cache in serializerUtils.Caches)
                {
                    switch (cache.Name)
                    {
                        case ResourcePaths.MAP_CACHE_NAME:
                            HandleMapCache(cache);
                            break;
                        case ResourcePaths.PLAYER_SCORE_CACHE_NAME:
                            HandlePlayerScoreCache(cache);
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Error(e);
            }
        }

        public (AccSaberBasicMap map, AccSaberBasicDifficulty diff)? GetMapWithDifficulty(string difficultyId)
        {
            AccSaberBasicDifficulty diff = CachedDifficulties[difficultyId];

            return (CachedMaps[diff.Hash], diff);
        }

        private async Task<bool> ValidateMapCache(AccSaberSerializedCache cache)
        {
            int mapCount = cache.MaxLength;

            if (TotalMaps > -1)
                return TotalMaps == mapCount;

            AccSaberPagedContent? response = await APIHandler.CallAPI_Json<AccSaberPagedContent>(string.Format(HelpfulPaths.APAPI_DIFF, "RANKED", 0, 1), AccsaberAPI.Throttler);

            if (response is null)
                return true; // If we don't get a good response from the API, then we can't invalidate it, so might as well use what we have.

            TotalMaps = response.TotalElements;

            return TotalMaps == mapCount;
        }
        private async Task<AccSaberSerializedCache> LoadMapCache()
        {
            List<AccSaberBasicMap> maps = await api.LoadAllBasicDiffs();

            return new AccSaberSerializedCache<AccSaberBasicMap>()
            {
                MaxLength = TotalMaps == -1 ? maps.Sum(map => map.Difficulties.Count) : TotalMaps,
                Content = maps
            };
        }
        private async Task<AccSaberSerializedCache> LoadPlayerScoreCache()
        {
            List<AccSaberPlayerScore> scores = [.. (await api.LoadAllPlayerScores()).Select(score => new AccSaberPlayerScore(score))];

            return new AccSaberSerializedCache<AccSaberPlayerScore>()
            {
                LastUpdated = DateTime.UtcNow,
                MaxLength = TotalMaps,
                ExtraData = [new int[3] { -1, -1, -1 }],
                Content = scores
            };
        }

        private async Task<bool> ValidatePlayerScoreCache(AccSaberSerializedCache cache)
        {
            DateTime lastUpdated = cache.LastUpdated;

            await playerInfo.LoadTask;

            AccSaberPagedContent<AccSaberLeaderboardEntry>? response = await APIHandler.CallAPI_Json<AccSaberPagedContent<AccSaberLeaderboardEntry>>(
                string.Format(HelpfulPaths.APAPI_SCORES, playerInfo.PlayerID!, 0, 1) + "&sort=timeSet,desc", AccsaberAPI.Throttler);

            if (response is null)
                return true; // If we don't get a good response from the API, then we can't invalidate it, so might as well use what we have.

            bool valid = lastUpdated >= response.Content![0].TimeSet;

            return valid;
        }

        public record struct CacheInfo(Type CacheType,
            Func<AccSaberSerializedCache, Task<bool>> Validate,
            Func<Task<AccSaberSerializedCache>>? Load);
    }
}
