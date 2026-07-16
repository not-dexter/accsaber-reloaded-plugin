using AccSaber.API;
using AccSaber.Consts;
using AccSaber.Counter;
using AccSaber.Models;
using AccSaber.Models.CacheModels;
using Newtonsoft.Json.Linq;
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
        [Inject] private readonly APCalc calc = null!;

        private bool invalidateMissions = false;
        private bool invalidateEvents = false;
        private readonly SelectComparer<AccSaberPlayerScore, float> PlayerScoreSorter, APScoreSorter;

        private readonly Dictionary<string, CacheInfo> cacheInfos;
        public IReadOnlyDictionary<string, CacheInfo> CacheInfos => cacheInfos;

        public readonly Task InitTask;
        private readonly object initLock = new();

        public int TotalMaps { get; private set; } = -1;
        public Dictionary<string, AccSaberBasicMap> CachedMaps = null!;
        public Dictionary<Guid, AccSaberBasicDifficulty> CachedDifficulties = null!;

        private AccSaberSerializedCache<AccSaberPlayerScore> _playerCache = null!;
        private readonly List<AccSaberPlayerScore>[] playerCategoryScores;
        public List<AccSaberPlayerScore> PlayerScores => _playerCache.Content;
        public int PlayerScoreLength
        {
            get => _playerCache.MaxLength;
            set => _playerCache.MaxLength = value;
        }
        public List<AccSaberPlayerScore>[] CategoryPlayerScores => playerCategoryScores;
        public static DateTime LastScoreTime { get; internal set; } = DateTime.MinValue;


        private AccSaberSerializedCache<AccSaberMission>? _missions = null;
        private AccSaberSerializedCache<AccSaberEventMe>? _eventMissions = null;

        public IReadOnlyList<AccSaberMission>? Missions => _missions?.Content;
        public IReadOnlyList<AccSaberEventMe>? EventMissions => _eventMissions?.Content;

        public AccSaberEventResponse? CurrentEvent { get; private set; }

        public async Task RevalidateMissions(bool forceRefresh = false)
        {
            if (_missions is null || (!invalidateMissions && !forceRefresh && await ValidateMissionCache(_missions)))
                return;

            if (invalidateMissions)
                invalidateMissions = false;

            AccSaberSerializedCache<AccSaberMission> newCache = ((await LoadMissionCache()) as AccSaberSerializedCache<AccSaberMission>)!;

            _missions.LastUpdated = newCache.LastUpdated;
            _missions.MaxLength = newCache.MaxLength;
            _missions.Content = newCache.Content;
        }
        public void InvalidateMissionCache() => _missions?.LastUpdated = DateTime.MinValue;

        public async Task RevalidateEvents(bool forceRefresh = false)
        {
            if (_eventMissions is null || (!invalidateEvents && !forceRefresh && await ValidateEventsCache(_eventMissions)))
                return;

            if (invalidateEvents)
                invalidateEvents = false;

            AccSaberSerializedCache<AccSaberEventMe> newCache = ((await LoadEventsCache()) as AccSaberSerializedCache<AccSaberEventMe>)!;

            _eventMissions.ExtraData = newCache.ExtraData;
            _eventMissions.LastUpdated = newCache.LastUpdated;
            _eventMissions.MaxLength = newCache.MaxLength;
            _eventMissions.Content = newCache.Content;
        }
        public void InvalidatEventsCache() => _eventMissions?.LastUpdated = DateTime.MinValue;

        public SerializationHandler()
        {
            cacheInfos = new(4)
            {
                { ResourcePaths.MAP_CACHE_NAME, new(typeof(AccSaberSerializedCache<AccSaberBasicMap>), ValidateMapCache, LoadMapCache) },
                { ResourcePaths.PLAYER_SCORE_CACHE_NAME, new(typeof(AccSaberSerializedCache<AccSaberPlayerScore>), ValidatePlayerScoreCache, LoadPlayerScoreCache) },
                { ResourcePaths.MISSION_CACHE_NAME, new(typeof(AccSaberSerializedCache<AccSaberMission>), ValidateMissionCache, LoadMissionCache) },
                { ResourcePaths.EVENTS_CACHE_NAME, new(typeof(AccSaberSerializedCache<AccSaberEventMe>), ValidateEventsCache, LoadEventsCache) }
            };

            InitTask = Task.Run(() =>
            {
                lock (initLock)
                    System.Threading.Monitor.Wait(initLock, 30_000); // timeout just to make sure a deadlock will not happen even if something breaks.
            });

            APScoreSorter = new(score => score.AP, new MyFloatComparer(ComparisonType.GT));

            PlayerScoreSorter = new(score => score.WeightedAp, new MyFloatComparer(ComparisonType.GT), APScoreSorter);

            playerCategoryScores = new List<AccSaberPlayerScore>[(int)APCategory.Overall];
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
                        .Select(diff => new KeyValuePair<Guid, AccSaberBasicDifficulty>(diff.DifficultyId, diff)))];

                    if (_playerCache is not null)
                        SetPlayerScoreCache();
                }

                void HandlePlayerScoreCache(AccSaberSerializedCache cache)
                {
                    if (cache is not AccSaberSerializedCache<AccSaberPlayerScore> playerCache)
                        return;

                    _playerCache = playerCache;

                    if (CachedDifficulties is not null)
                        SetPlayerScoreCache();
                }

                void HandleMissionCache(AccSaberSerializedCache cache)
                {
                    if (cache is not AccSaberSerializedCache<AccSaberMission> missionCache)
                        return;

                    _missions = missionCache;
                }

                async Task HandleEventCache(AccSaberSerializedCache cache)
                {
                    if (cache is not AccSaberSerializedCache<AccSaberEventMe> eventCache)
                        return;

                    _eventMissions = eventCache;
                    
                    if (CurrentEvent is null)
                    {
                        if (_eventMissions.ExtraData is not null && _eventMissions.ExtraData.Count > 0 && _eventMissions.ExtraData[0] is JObject obj)
                            CurrentEvent = obj.ToObject<AccSaberEventResponse>();

                        if (CurrentEvent is null)
                        {
                            AccSaberEventResponse? maybeCurrentEvent = await LoadCurrentEvent();

                            if (maybeCurrentEvent is not null)
                            {
                                _eventMissions.ExtraData = [maybeCurrentEvent];
                                CurrentEvent = maybeCurrentEvent;
                            }
                        }
                    }
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
                        case ResourcePaths.EVENTS_CACHE_NAME:
                            await HandleEventCache(cache);
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Error(e);
            }
            finally
            {
                lock (initLock)
                    System.Threading.Monitor.Pulse(initLock);
            }
        }

        private void SetPlayerScoreCache()
        {
            for (int i = 0; i < playerCategoryScores.Length; ++i)
                playerCategoryScores[i] = [];

            float currentWeight = float.MaxValue;
            bool outOfOrder = false;

            foreach (AccSaberPlayerScore score in _playerCache.Content)
            {
                if (!outOfOrder)
                {
                    if (currentWeight > score.WeightedAp)
                        outOfOrder = true;
                    else
                        currentWeight = score.WeightedAp;
                }

                int categoryIndex;

                if (score.PersonalRank < 0)
                {
                    AccSaberBasicDifficulty diff = CachedDifficulties[score.DifficultyId];

                    categoryIndex = (int)diff.Category!.Value;
                    score.PersonalRank = playerCategoryScores[categoryIndex].Count;

                    score.SetValues(diff, calc);
                }
                else
                    categoryIndex = (int)score.Category!.Value;

                playerCategoryScores[categoryIndex].Add(score);
            }

            if (outOfOrder)
            {
                for (int i = 0; i < playerCategoryScores.Length; ++i)
                {
                    playerCategoryScores[i].Sort(APScoreSorter);

                    for (int j = 0; j < playerCategoryScores[i].Count; ++j)
                    {
                        playerCategoryScores[i][j].PersonalRank = j;
                        playerCategoryScores[i][j].SetWeight(calc);
                    }
                }

                _playerCache.Content.Clear();
                _playerCache.Content.AddRange(MiscUtils.MergeSortedLists(PlayerScoreSorter, playerCategoryScores));
            }

        }

        public async Task<AccSaberBasicDifficulty?> GetDiffById(Guid id)
        {
            if (CachedDifficulties.TryGetValue(id, out AccSaberBasicDifficulty diff))
                return diff;

            AccSaberDifficulty? fullDiff = await APIHandler.CallAPI_Json<AccSaberDifficulty>(string.Format(HelpfulPaths.APAPI_DIFF_ID, id), AccsaberAPI.Throttler);

            if (fullDiff is null)
                return null;

            CachedDifficulties.Add(id, fullDiff);
            return fullDiff;
        }
        internal void OnPlayerScoreUpdated(AccSaberLeaderboardEntry entry)
        {
            InvalidateMissionCache();
            InvalidatEventsCache();

            int categoryIndex = (int)EnumUtils.ReloadedCategoryIdToCategory(entry.CategoryId);
            List<AccSaberPlayerScore> categoryScores = playerCategoryScores[categoryIndex];

            AccSaberPlayerScore? oldScore = categoryScores.FirstOrDefault(score => score.DifficultyId.Equals(entry.DifficultyId));

            if (oldScore is not null)
            {
                categoryScores.Remove(oldScore);

                if (oldScore.AP > entry.AP)
                    return;
            }

            AccSaberPlayerScore newScore = new(entry);

            for (int i = 0; i < categoryScores.Count; ++i)
            {
                AccSaberPlayerScore score = categoryScores[i];

                if (score.AP < newScore.AP)
                {
                    newScore.PersonalRank = i;
                    break;
                }
            }

            newScore.SetValues(this, calc);

            int temp, index;

            if (newScore.PersonalRank == -1 && categoryScores.Last().AP > newScore.AP)
            {
                categoryScores.Add(newScore); // This is a very rare case, as it means the user improved their lowest score but didn't improve it past their second lowest.

                temp = PlayerScores.BinarySearch(newScore, APScoreSorter);
                PlayerScores.Insert(temp < 0 ? ~temp : temp, newScore);

                return;
            }

            if (newScore.PersonalRank == 0)
                index = 0;
            else
            {
                temp = categoryScores.BinarySearch(0, newScore.PersonalRank + 1, newScore, APScoreSorter);
                index = temp < 0 ? ~temp : temp; // We allow positive indexes to be inserted because duplicate ap values are allowed.
            }

            categoryScores.Insert(index, newScore); 

            for (int i = index + 1, rank = newScore.PersonalRank + 1; i < categoryScores.Count; ++i)
            {
                categoryScores[i].PersonalRank = rank++;
                categoryScores[i].SetWeight(calc);
            }

            PlayerScores.Clear();
            PlayerScores.AddRange(MiscUtils.MergeSortedLists(PlayerScoreSorter, playerCategoryScores)); // Since all the weights below the score have changed, resort the scores.
        }
        public async Task<(AccSaberBasicMap map, AccSaberBasicDifficulty diff)?> GetMapWithDifficulty(Guid difficultyId)
        {
            AccSaberBasicDifficulty? diff = await GetDiffById(difficultyId);

            if (diff is null)
                return null;

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

            LastScoreTime = response.Content![0].TimeSet;
            AccSaberSerializedCache<AccSaberPlayerScore>? playerCache = cache as AccSaberSerializedCache<AccSaberPlayerScore>;
            bool valid = lastUpdated >= LastScoreTime && playerCache is not null && playerCache.Content.Count == response.TotalElements;

            if (valid)
            {
                float currentWeightAP = float.MaxValue;
                foreach (AccSaberPlayerScore score in playerCache!.Content)
                {
                    if (score.WeightedAp > currentWeightAP)
                    {
                        valid = false;
                        break;
                    }
                    currentWeightAP = score.WeightedAp;
                }
            }

            invalidateMissions = !valid;
            invalidateEvents = !valid;

            return valid;
        }
        private async Task<AccSaberSerializedCache> LoadPlayerScoreCache()
        {
            List<AccSaberPlayerScore> scores = [.. (await api.LoadAllPlayerScores()).Select(score => new AccSaberPlayerScore(score))];

            return new AccSaberSerializedCache<AccSaberPlayerScore>()
            {
                LastUpdated = DateTime.UtcNow,
                MaxLength = scores.Count,
                ExtraData = [new int[3] { 0, 0, 0 }], //Note: The length is based off of the number of categories currently used.
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

            DateTime now = DateTime.UtcNow;
            for (int i = missions.Count - 1; i >= 0; --i)
                if (missions[i].ExpiresAt < now)
                {
                    Plugin.Log.Critical("There is a bug with the missions endpoint! Please report this on Discord.");
                    missions.RemoveAt(i);
                }

            return new AccSaberSerializedCache<AccSaberMission>()
            {
                LastUpdated = missions.Aggregate(DateTime.MaxValue, (total, current) => MiscUtils.Min(total, current.ExpiresAt)),
                MaxLength = missions.Count,
                Content = missions
            };
        }

        private async Task<bool> ValidateEventsCache(AccSaberSerializedCache cache) => cache.LastUpdated > DateTime.UtcNow;
        private async Task<AccSaberSerializedCache> LoadEventsCache()
        {
            AccSaberEventResponse? currentEvent = await LoadCurrentEvent();

            if (currentEvent is null)
                goto ExitBad;

            await playerInfo.LoadTask;

            string call = string.Format(HelpfulPaths.APAPI_EVENT_MISSIONS_ME, currentEvent.Event.Id);

            List<AccSaberEventMe>? eventMissions = await APIHandler.CallAPI_Json<List<AccSaberEventMe>>(call, AccsaberAPI.Throttler);

            if (eventMissions is null)
                goto ExitBad;
            
            DateTime now = DateTime.UtcNow;
            for (int i = eventMissions.Count - 1; i >= 0; --i)
                if (eventMissions[i].Mission.CompletableUntil < now)
                {
                    Plugin.Log.Critical("There is a bug with the events/missions/me endpoint! Please report this on Discord.");
                    eventMissions.RemoveAt(i);
                }

            return new AccSaberSerializedCache<AccSaberEventMe>()
            {
                ExtraData = [currentEvent],
                LastUpdated = eventMissions.Aggregate(DateTime.MaxValue, (total, current) => MiscUtils.Min(total, current.Mission.CompletableUntil)),
                MaxLength = eventMissions.Count,
                Content = eventMissions
            };
        ExitBad:
            return new AccSaberSerializedCache<AccSaberEventMe>()
            {
                LastUpdated = DateTime.MinValue
            };
        }
        private async Task<AccSaberEventResponse?> LoadCurrentEvent()
        {
            List<AccSaberEvent>? liveEvents = await APIHandler.CallAPI_Json<List<AccSaberEvent>>(HelpfulPaths.APAPI_EVENTS_LIVE, AccsaberAPI.Throttler);

            if (liveEvents is null || liveEvents.Count == 0)
            {
                Plugin.Log.Warn("There are no live events currently.");
                return null;
            }

            AccSaberEvent currentEvent = liveEvents.Aggregate(MiscUtils.Min);

            CurrentEvent = await APIHandler.CallAPI_Json<AccSaberEventResponse>(string.Format(HelpfulPaths.APAPI_EVENT, currentEvent.Id), AccsaberAPI.Throttler);

            if (CurrentEvent is null)
                Plugin.Log.Warn("There are no event missions currently.");

            return CurrentEvent;
        }

        public record struct CacheInfo(Type CacheType,
            Func<AccSaberSerializedCache, Task<bool>> Validate,
            Func<Task<AccSaberSerializedCache>>? Load);
    }
}
