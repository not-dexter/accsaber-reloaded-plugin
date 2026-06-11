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

        private readonly Dictionary<string, CacheInfo> cacheInfos;
        public IReadOnlyDictionary<string, CacheInfo> CacheInfos => cacheInfos;

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

        private AccSaberSerializedCache<AccSaberMission> _missions = null!;
        public IReadOnlyList<AccSaberMission> Missions => _missions.Content;

        public async Task RevalidateMissions(bool forceRefresh = false)
        {
            if (!forceRefresh && await ValidateMissionCache(_missions))
                return;

            AccSaberSerializedCache<AccSaberMission> newCache = ((await LoadMissionCache()) as AccSaberSerializedCache<AccSaberMission>)!;

            _missions.LastUpdated = newCache.LastUpdated;
            _missions.MaxLength = newCache.MaxLength;
            _missions.Content = newCache.Content;
        }
        public void InvalidateMissionCache() => _missions.LastUpdated = DateTime.MinValue;


        public SerializationHandler()
        {
            cacheInfos = new(3)
            {
                { ResourcePaths.MAP_CACHE_NAME, new(typeof(AccSaberSerializedCache<AccSaberBasicMap>), ValidateMapCache, LoadMapCache) },
                { ResourcePaths.PLAYER_SCORE_CACHE_NAME, new(typeof(AccSaberSerializedCache<AccSaberPlayerScore>), ValidatePlayerScoreCache, null) },
                { ResourcePaths.MISSION_CACHE_NAME, new(typeof(AccSaberSerializedCache<AccSaberMission>), ValidateMissionCache, LoadMissionCache) }
            };
        }
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

                void HandleMissionCache(AccSaberSerializedCache cache)
                {
                    if (cache is not AccSaberSerializedCache<AccSaberMission> missionCache)
                        return;

                    _missions = missionCache;
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
                        case ResourcePaths.MISSION_CACHE_NAME:
                            HandleMissionCache(cache);
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Error(e);
            }
        }

#pragma warning disable IDE0060
        public void OnPlayerScoreUpdated(AccSaberLeaderboardEntry entry)
        {
            InvalidateMissionCache();
        }
#pragma warning restore IDE0060
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

        private async Task<bool> ValidateMissionCache(AccSaberSerializedCache cache) => cache.LastUpdated > DateTime.UtcNow;
        private async Task<AccSaberSerializedCache> LoadMissionCache()
        {
            await playerInfo.LoadTask;

            List<AccSaberMission>? missions = await APIHandler.CallAPI_Json<List<AccSaberMission>>(HelpfulPaths.APAPI_MISSIONS, AccsaberAPI.Throttler);

            if (missions is null)
                return new AccSaberSerializedCache<AccSaberMission>()
                {
                    LastUpdated = DateTime.MinValue
                };

            return new AccSaberSerializedCache<AccSaberMission>()
            {
                LastUpdated = missions.Aggregate(DateTime.MaxValue, (total, current) => MiscUtils.Min(total, current.ExpiresAt)),
                MaxLength = missions.Count,
                Content = missions
            };
        }
        public record struct CacheInfo(Type CacheType,
            Func<AccSaberSerializedCache, Task<bool>> Validate,
            Func<Task<AccSaberSerializedCache>>? Load);
    }
}
