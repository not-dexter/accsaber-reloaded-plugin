using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Accsaber.Utils
{
    public class ObjectCacher<K, V>(TimeSpan defaultItemLifespan = default) : IEnumerable<KeyValuePair<K,V>>
    {
        private readonly Dictionary<K, V> cache = [];
        private readonly SortedSet<(DateTime expiration, K key)> expirationDates = [];
        private readonly TimeSpan itemLifespan = defaultItemLifespan == default ? new(0, 5, 0) : defaultItemLifespan;

        private readonly object cacheLock = new();

        public void CacheItem(V item, TimeSpan lifespan = default)
        {
            CacheItem(item, GetKey(item), lifespan);
        }
        public void CacheItem(V item, K key, TimeSpan lifespan = default)
        {
            lock (cacheLock)
            {
                CleanOutCache();
                cache[key] = item;
                if (lifespan == default)
                    lifespan = itemLifespan;
                expirationDates.Add((DateTime.Now + lifespan, key));
            }
        }

        public void CacheItemPermanently(V item) => CacheItemPermanently(item, GetKey(item));
        public void CacheItemPermanently(V item, K key)
        {
            lock (cacheLock)
            {
                CleanOutCache();
                cache[key] = item;
            }
        }

        public V? GetCachedItem(K key)
        {
            lock (cacheLock)
            {
                CleanOutCache();
                if (key is null || !cache.TryGetValue(key, out V val))
                    return default;
                return val;
            }
        }
        public bool TryGetCachedItem(K key, out V? val)
        {
            lock (cacheLock)
            {
                CleanOutCache();
                val = default;
                return key is not null && cache.TryGetValue(key, out val);
            }
        }
        public bool ContainsKey(K key)
        {
            lock (cacheLock)
            {
                CleanOutCache();
                return cache.ContainsKey(key);
            }
        }

        public void RemoveItem(K key)
        {
            if (key is null)
                return;

            lock (cacheLock)
            {
                if (cache.TryGetValue(key, out V item))
                {
                    cache.Remove(key);
                    var expirationDate = expirationDates.FirstOrDefault(token => key.Equals(token.key));
                    if (expirationDate.expiration != default)
                        expirationDates.Remove(expirationDate);
                }
            }
        }
        public void ClearCache()
        {
            lock (cacheLock) 
            {
                cache.Clear();
                expirationDates.Clear(); 
            }
        }

        private void CleanOutCache()
        {
            DateTime rn = DateTime.Now;
            while (expirationDates.Count > 0 && expirationDates.Min.expiration < rn)
            {
                cache.Remove(expirationDates.Min.key);
                expirationDates.Remove(expirationDates.Min);
            }
        }
        private K GetKey(V item)
        {
            if (item is ICacheKey keyHolder)
                return keyHolder.Key;
            throw new Exception("Cannot get the key of a type that doesn't implement ICacheKey.");
        }

        public IEnumerator<KeyValuePair<K,V>> GetEnumerator()
        {
            lock (cacheLock)
            {
                List<KeyValuePair<K, V>> outp = new(cache.Count);
                HashSet<K> unusedKeys = [.. cache.Keys]; 

                foreach (var (expiration, key) in expirationDates)
                {
                    outp.Add(new(key, cache[key]));
                    unusedKeys.Remove(key);
                }

                foreach (K key in unusedKeys)
                    outp.Add(new(key, cache[key]));

                return outp.GetEnumerator();
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public interface ICacheKey
        {
            public K Key { get; }
        }
    }

    public class ObjectCacher<T>(TimeSpan defaultItemLifespan = default) : ObjectCacher<string, T>(defaultItemLifespan) { }
}
