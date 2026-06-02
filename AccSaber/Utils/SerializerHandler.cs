using AccSaber.API;
using AccSaber.Consts;
using AccSaber.Models;
using AccSaber.Models.Base;
using AccSaber.Models.CacheModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Zenject;

namespace AccSaber.Utils
{
    internal class SerializerHandler : IInitializable, IDisposable
    {
        private static AccSaberSerializedCache<AccSaberPlayerScore> cachedPlayerScores = new();

        public static Dictionary<string, AccSaberBasicMap> CachedMaps { get; private set; } = [];
        public static int TotalMaps { get; private set; } = -1;
        public static List<AccSaberPlayerScore> CachedPlayerScores => cachedPlayerScores.Content;
        public static int CachedPlayerScoreLength
        {
            get => cachedPlayerScores.MaxLength;
            set => cachedPlayerScores.MaxLength = value;
        }
        public static int[] CategoryPlayerScoreLength = [-1, -1, -1]; // A bit hardcoded, but whatever

        public void Initialize()
        {
            Task.Run(Load);
        }
        public void Dispose()
        {
            try
            {
                Save();
            } catch (Exception e)
            {
                Plugin.Log.Error($"There was an error when saving the caches!\n{e}");
            }
        }

        public static (AccSaberBasicMap map, AccSaberBasicDifficulty diff)? GetMapWithDifficulty(string difficultyId)
        {
            foreach (AccSaberBasicMap map in CachedMaps.Values)
            {
                AccSaberBasicDifficulty? diff = map.Difficulties.FirstOrDefault(diff => diff.DifficultyId == difficultyId);
                if (diff is not null)
                    return (map, diff);
            }
            return null;
        }

        private async Task Load()
        {
            try
            {
                if (!Directory.Exists(ResourcePaths.ACC_SABER_DATA_FOLDER))
                    Directory.CreateDirectory(ResourcePaths.ACC_SABER_DATA_FOLDER);

                JsonSerializer serializer = new();

                AccSaberSerializedCache<AccSaberBasicMap>? maps = null;
                AccSaberSerializedCache<AccSaberPlayerScore>? playerScores = null;

                Load(ResourcePaths.ACC_SABER_MAP_CACHE, ref maps, serializer);
                Load(ResourcePaths.ACC_SABER_PLAYER_SCORE_CACHE, ref playerScores, serializer);

                if (maps is not null && (await ValidateMapCache(maps.Content.Select(map => map.Difficulties.Count).Aggregate(0, (total, current) => total + current))))
                    CachedMaps.AddRange(maps.Content.Select(map => new KeyValuePair<string, AccSaberBasicMap>(map.Hash, map)));
                else
                    await AccsaberAPI.LoadAllBasicDiffs();

                if (playerScores is not null && (await ValidatePlayerScoreCache(playerScores.LastUpdated)))
                    cachedPlayerScores = playerScores;
            } catch (Exception e)
            {
                Plugin.Log.Error($"There was an error loading the cache files.\n{e}");
            }
        }
        private void Load<T>(string file, ref AccSaberSerializedCache<T>? cache, JsonSerializer serializer) where T : Model
        {
            if (File.Exists(file))
            {
                using StreamReader sr = new(file);
                using JsonReader reader = new JsonTextReader(sr);

                cache = serializer.Deserialize<AccSaberSerializedCache<T>>(reader);
            }
        }
        private void Save()
        {
            if (!Directory.Exists(ResourcePaths.ACC_SABER_DATA_FOLDER))
                Directory.CreateDirectory(ResourcePaths.ACC_SABER_DATA_FOLDER);

            JsonSerializer serializer = new();

            AccSaberSerializedCache<AccSaberBasicMap> maps = new()
            {
                MaxLength = TotalMaps,
                Content = [.. CachedMaps.Values]
            };

            foreach (AccSaberBasicMap map in maps.Content)
                foreach (AccSaberBasicDifficulty mapDiff in map.Difficulties)
                    mapDiff.Hash = null!; // this saves around 20 KBs off the cache file, worth it lol.

            Save(ResourcePaths.ACC_SABER_MAP_CACHE, maps, serializer);
            Save(ResourcePaths.ACC_SABER_PLAYER_SCORE_CACHE, cachedPlayerScores, serializer);
        }
        private void Save(string path, object data, JsonSerializer? serializer = null)
        {
            serializer ??= new();

            using StreamWriter sw = new(path);
            using JsonWriter writer = new JsonTextWriter(sw);

            serializer.Serialize(writer, data);
        }


        private async Task<bool> ValidateMapCache(int mapCount)
        {
            AccSaberPagedContent? response = await APIHandler.CallAPI_Json<AccSaberPagedContent>(string.Format(HelpfulPaths.APAPI_DIFF, "RANKED", 0, 1), AccsaberAPI.throttler);

            if (response is null)
                return true; // If we don't get a good response from the API, then we can't invalidate it, so might as well use what we have.

            TotalMaps = response.TotalElements;

            return TotalMaps == mapCount;
        }
        private async Task<bool> ValidatePlayerScoreCache(DateTime lastUpdated)
        {
            await PlayerSocialLife.LoadTask;

            AccSaberPagedContent<AccSaberLeaderboardEntry>? response = await APIHandler.CallAPI_Json<AccSaberPagedContent<AccSaberLeaderboardEntry>>(
                string.Format(HelpfulPaths.APAPI_SCORES, PlayerSocialLife.PlayerID!, 0, 1) + "&sort=timeSet,desc", AccsaberAPI.throttler);

            if (response is null)
                return true; // If we don't get a good response from the API, then we can't invalidate it, so might as well use what we have.

            bool valid = lastUpdated >= response.Content![0].TimeSet;

            if (!valid)
                cachedPlayerScores.LastUpdated = response.Content![0].TimeSet;

            return valid;
        }
    }
}
