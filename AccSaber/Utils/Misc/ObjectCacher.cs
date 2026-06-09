using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace AccSaber.Utils.Misc
{
    /// <summary>
    /// A simple thread-safe in-memory cache that associates keys of type <typeparamref name="K"/>
    /// with values of type <typeparamref name="V"/> and automatically expires items after a lifespan.
    /// </summary>
    /// <typeparam name="K">The cache key type. Must be non-nullable.</typeparam>
    /// <typeparam name="V">The cached value type. Must be non-nullable.</typeparam>
    /// <remarks>
    /// - The cache is safe for multi-threaded access by taking an internal lock for all public operations.
    /// - Items cached with <see cref="CacheItem(V, K, TimeSpan?)"/> receive an expiration time and are
    ///   removed when expired. Items inserted with <see cref="CacheItemPermanently(V, K)"/> are not
    ///   assigned an expiration and remain until removed explicitly or the cache is cleared.
    /// - The optional constructor parameter <c>defaultItemLifespan</c> specifies the default expiration
    ///   span for items cached without an explicit lifespan. If not provided, the default is 5 minutes.
    /// - Enumeration via <see cref="IEnumerable{KeyValuePair}"/> produces a snapshot of the cache contents
    ///   (after removing expired items); items with expiration dates are yielded in order of expiration,
    ///   followed by permanent or non-expiring items.
    /// </remarks>
    public class ObjectCacher<K, V>(TimeSpan? defaultItemLifespan = null) : IEnumerable<KeyValuePair<K,V>> where K : notnull where V : notnull
    {
        /// <summary>
        /// Primary store for cached items.
        /// </summary>
        private readonly Dictionary<K, V> cache = [];

        /// <summary>
        /// Lookup from key to its expiration entry (if any).
        /// </summary>
        private readonly Dictionary<K, ExpirationPoint> expirationDateByKey = [];

        /// <summary>
        /// Sorted set of expiration entries ordered by <see cref="ExpirationPoint.ExpirationDate"/>.
        /// Used to efficiently discover expired items.
        /// </summary>
        private readonly SortedSet<ExpirationPoint> expirationDateByDate = [];

        /// <summary>
        /// Default lifespan applied to items when no explicit lifespan is provided.
        /// </summary>
        private readonly TimeSpan itemLifespan = defaultItemLifespan ?? new(0, 5, 0);

        /// <summary>
        /// Lock object that serializes access to internal collections to provide thread-safety.
        /// </summary>
        private readonly object cacheLock = new();

        /// <summary>
        /// Add or replace an item in the cache with an optional custom lifespan.
        /// </summary>
        /// <param name="item">Value to cache.</param>
        /// <param name="key">Key under which the value will be stored.</param>
        /// <param name="lifespan">
        /// Optional lifespan for this item. If <c>null</c>, the instance's default lifespan is used.
        /// When the lifespan elapses (based on UTC time), the item becomes eligible for removal.
        /// </param>
        public void CacheItem(V item, K key, TimeSpan? lifespan = null)
        {
            lock (cacheLock)
            {
                CleanOutCache();

                cache[key] = item;

                if (expirationDateByKey.TryGetValue(key, out ExpirationPoint value))
                    expirationDateByDate.Remove(value);

                lifespan ??= itemLifespan;

                ExpirationPoint expiration = new(DateTime.UtcNow + lifespan.Value, key);

                expirationDateByKey[key] = expiration;
                if (!expirationDateByDate.Add(expiration))
                    Plugin.Log.Critical("An expiration date that should have been added failed!!!");
            }
        }

        /// <summary>
        /// Cache an item without an expiration (permanent until explicitly removed or cache cleared).
        /// </summary>
        /// <param name="item">Value to cache.</param>
        /// <param name="key">Key under which the value will be stored.</param>
        public void CacheItemPermanently(V item, K key)
        {
            lock (cacheLock)
            {
                CleanOutCache();

                if (expirationDateByKey.TryGetValue(key, out ExpirationPoint value))
                {
                    expirationDateByDate.Remove(value);
                    expirationDateByKey.Remove(key);
                }

                cache[key] = item;
            }
        }

        /// <summary>
        /// Get a cached item by key.
        /// </summary>
        /// <param name="key">Key of the item to retrieve.</param>
        /// <returns>The cached value.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the key does not exist in the cache.</exception>
        public V GetCachedItem(K key)
        {
            lock (cacheLock)
            {
                CleanOutCache();

                if (!cache.TryGetValue(key, out V val))
                    throw new InvalidOperationException("Cannot get cached item when item does not exist.");

                return val;
            }
        }

        /// <summary>
        /// Try to get a cached item by key without throwing on miss.
        /// </summary>
        /// <param name="key">Key of the item to retrieve.</param>
        /// <param name="val">When this method returns, contains the cached value if found; otherwise the default value for <typeparamref name="V"/>.</param>
        /// <returns><c>true</c> if the key was present; otherwise <c>false</c>.</returns>
        public bool TryGetCachedItem(K key, out V? val)
        {
            lock (cacheLock)
            {
                CleanOutCache();

                val = default;

                return cache.TryGetValue(key, out val);
            }
        }

        /// <summary>
        /// Returns whether the cache contains the given key.
        /// </summary>
        /// <param name="key">Key to check.</param>
        /// <returns><c>true</c> if present; otherwise <c>false</c>.</returns>
        public bool ContainsKey(K key)
        {
            lock (cacheLock)
            {
                CleanOutCache();
                return cache.ContainsKey(key);
            }
        }

        /// <summary>
        /// Remove an item from the cache and return it.
        /// </summary>
        /// <param name="key">Key of the item to remove.</param>
        /// <param name="item">When this method returns, contains the removed item if present; otherwise the default value for <typeparamref name="V"/>.</param>
        /// <returns><c>true</c> if the item was found and removed; otherwise <c>false</c>.</returns>
        public bool RemoveItem(K key, out V? item)
        {
            lock (cacheLock)
            {
                if (cache.TryGetValue(key, out item))
                {
                    cache.Remove(key);

                    if (expirationDateByKey.TryGetValue(key, out ExpirationPoint value))
                    {
                        expirationDateByDate.Remove(value);
                        expirationDateByKey.Remove(key);
                    }
                    return true;
                }

                return false;
            }
        }
        /// <summary>
        /// Remove an item from the cache without returning it.
        /// </summary>
        /// <param name="key">Key of the item to remove.</param>
        /// <returns><c>true</c> if the item was found and removed; otherwise <c>false</c>.</returns>
        public bool RemoveItem(K key)
        {
            return RemoveItem(key, out _);
        }

        /// <summary>
        /// Remove all items from the cache immediately.
        /// </summary>
        public void ClearCache()
        {
            lock (cacheLock) 
            {
                cache.Clear();
                expirationDateByKey.Clear();
                expirationDateByDate.Clear();
            }
        }

        /// <summary>
        /// Remove all expired items from the cache based on UTC time.
        /// </summary>
        private void CleanOutCache()
        {
            DateTime rn = DateTime.UtcNow;
            while (expirationDateByDate.Count > 0 && expirationDateByDate.Min.ExpirationDate <= rn)
            {
                ExpirationPoint point = expirationDateByDate.Min;

                expirationDateByDate.Remove(point);
                expirationDateByKey.Remove(point.Key);

                cache.Remove(point.Key);
            }
        }

        /// <summary>
        /// Return an enumerator that iterates over the cache snapshot (after removing expired items).
        /// The enumeration yields items with expiration dates ordered by expiration first, then the
        /// remaining (permanent or non-expiring) items in unspecified order.
        /// </summary>
        public IEnumerator<KeyValuePair<K,V>> GetEnumerator()
        {
            lock (cacheLock)
            {
                CleanOutCache();

                List<KeyValuePair<K, V>> outp = [with(cache.Count)];
                HashSet<K> unusedKeys = [.. cache.Keys]; 

                foreach (ExpirationPoint point in expirationDateByDate)
                {
                    outp.Add(new(point.Key, cache[point.Key]));
                    unusedKeys.Remove(point.Key);
                }

                foreach (K key in unusedKeys)
                    outp.Add(new(key, cache[key]));

                return outp.GetEnumerator();
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Internal struct that represents an expiration event for a specific cache key.
        /// Instances are ordered by expiration time and then by an internal unique id to ensure
        /// deterministic ordering when multiple entries share the same expiration time.
        /// </summary>
        private readonly struct ExpirationPoint(DateTime expirationDate, K key) : IEquatable<ExpirationPoint>, IComparable<ExpirationPoint>
        {
            private static long nextId;

            /// <summary>
            /// The UTC time at which the entry expires.
            /// </summary>
            public readonly DateTime ExpirationDate = expirationDate;

            /// <summary>
            /// The cache key associated with this expiration.
            /// </summary>
            public readonly K Key = key;

            /// <summary>
            /// A tiebreaker id to guarantee a stable ordering for entries with identical expiration times.
            /// </summary>
            private readonly long Id = Interlocked.Increment(ref nextId);

            public int CompareTo(ExpirationPoint other)
            {
                int cmp = ExpirationDate.CompareTo(other.ExpirationDate);

                if (cmp != 0)
                    return cmp;

                return Id.CompareTo(other.Id);
            }

            public bool Equals(ExpirationPoint other) => CompareTo(other) == 0;

            public override bool Equals(object obj) => obj is ExpirationPoint ep && Equals(ep);
            public override int GetHashCode() => MiscUtils.GetHashCode(ExpirationDate, Key.GetHashCode());
        }
    }

    /// <summary>
    /// Convenience specialization of <see cref="ObjectCacher{K,V}"/> that uses <see cref="string"/> keys.
    /// </summary>
    /// <typeparam name="T">Cached item type.</typeparam>
    public class ObjectCacher<T>(TimeSpan? defaultItemLifespan = null) : ObjectCacher<string, T>(defaultItemLifespan) where T : notnull { }
}