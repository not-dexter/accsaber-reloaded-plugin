using AccSaber.Consts;
using AccSaber.Models.CacheModels;
using AccSaber.Utils.Misc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Zenject;

namespace AccSaber.Utils
{
    internal class SerializerUtils : IInitializable, IDisposable
    {
        [Inject] private readonly SerializationHandler handler = null!;

        //private static AccSaberSerializedCache<AccSaberPlayerScore> cachedPlayerScores = new();

        //public static Dictionary<string, AccSaberBasicMap> CachedMaps { get; private set; } = [];
        //public static int TotalMaps { get; private set; } = -1;
        //public static List<AccSaberPlayerScore> CachedPlayerScores => cachedPlayerScores.Content;
        //public static int CachedPlayerScoreLength
        //{
        //    get => cachedPlayerScores.MaxLength;
        //    set => cachedPlayerScores.MaxLength = value;
        //}
        //public static int[] CategoryPlayerScoreLength = [-1, -1, -1]; // A bit hardcoded, but whatever

        private readonly List<AccSaberSerializedCache> caches = [];
        public IReadOnlyList<AccSaberSerializedCache> Caches => caches;
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

        private async Task Load()
        {
            try
            {
                if (!Directory.Exists(ResourcePaths.ACC_SABER_DATA_FOLDER))
                {
                    Directory.CreateDirectory(ResourcePaths.ACC_SABER_DATA_FOLDER);
                    return;
                }

                JsonSerializer serializer = new();

                IEnumerable<string> files = handler.CacheInfos.Keys.Select(key => Path.Combine(ResourcePaths.ACC_SABER_DATA_FOLDER, key + ".json"));

                foreach (string filepath in files)
                {
                    string filename = filepath[(filepath.LastIndexOf('\\') + 1)..filepath.LastIndexOf('.')];
                    if (handler.CacheInfos.TryGetValue(filename, out SerializationHandler.CacheInfo cacheInfo))
                    {
                        AccSaberSerializedCache? cache = Load(filepath, serializer, cacheInfo.CacheType);

                        if (cache is null || !await cacheInfo.Validate(cache))
                        {
                            if (cacheInfo.Load is not null)
                                cache = await cacheInfo.Load();
                            else
                            {
                                cache = (AccSaberSerializedCache)cacheInfo.CacheType.GetConstructor([]).Invoke([]);
                                cache.LastUpdated = DateTime.UtcNow;
                            }
                                
                        }

                        cache.Name = filename;

                        caches.Add(cache);
                    }
                }

                handler.SetCacheData(this);

            } catch (Exception e)
            {
                Plugin.Log.Error($"There was an error loading the cache files.\n{e}");
            }
        }
        private AccSaberSerializedCache? Load(string file, JsonSerializer serializer, Type serializedType)
        {
            try
            {
                if (File.Exists(file))
                {
                    using StreamReader sr = new(file);
                    using JsonReader reader = new JsonTextReader(sr);

                    return (AccSaberSerializedCache?)serializer.Deserialize(reader, serializedType);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Error(e);
            }

            return null;
        }
        private void Save()
        {
            if (!Directory.Exists(ResourcePaths.ACC_SABER_DATA_FOLDER))
                Directory.CreateDirectory(ResourcePaths.ACC_SABER_DATA_FOLDER);

            JsonSerializer serializer = new();

            foreach (AccSaberSerializedCache cache in caches)
            {
                string filepath = Path.Combine(ResourcePaths.ACC_SABER_DATA_FOLDER, cache.Name + ".json");
                Save(filepath, cache, serializer);
            }
        }
        private void Save(string path, object data, JsonSerializer? serializer = null)
        {
            serializer ??= new();

            using StreamWriter sw = new(path);
            using JsonWriter writer = new JsonTextWriter(sw);

            serializer.Serialize(writer, data);
        }
    }
}
