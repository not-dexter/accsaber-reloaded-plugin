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
using static BeatSaberMarkupLanguage.Components.KEYBOARD;

namespace AccSaber.Utils
{
    internal class SerializerUtils : IInitializable, IDisposable
    {
        [Inject] private readonly SerializationHandler handler = null!;

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
                    Directory.CreateDirectory(ResourcePaths.ACC_SABER_DATA_FOLDER);

                JsonSerializer serializer = new();

                foreach (string key in handler.CacheInfos.Keys)
                {
                    if (handler.CacheInfos.TryGetValue(key, out SerializationHandler.CacheInfo cacheInfo))
                    {
                        AccSaberSerializedCache? cache = Load(Path.Combine(ResourcePaths.ACC_SABER_DATA_FOLDER, key + ".json"), serializer, cacheInfo.CacheType);

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

                        cache.Name = key;

                        caches.Add(cache);
                    }
                }
            } 
            catch (Exception e)
            {
                Plugin.Log.Error($"There was an error loading the cache files.\n{e}");
            }
            finally
            {
                handler.SetCacheData(this);
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
