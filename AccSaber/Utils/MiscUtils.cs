using AccSaber.API;
using AccSaber.Models;
using AccSaber.Utils.Misc;
using AccSaber.Utils.Safety;
using HMUI;
using IPA.Utilities.Async;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace AccSaber.Utils
{
    public static class MiscUtils
    {
        public const char STAR = (char)9733;

        public const double DAYS_YEAR = 365.2422;

        public const double SECONDS_MICRO = 1e-6; // 0.000001
        public const double SECONDS_MILLI = SECONDS_MICRO * 1000; // 0.001
        public const int SECONDS_MINUTE = 60;
        public const int SECONDS_HOUR = SECONDS_MINUTE * 60; // 3,600
        public const int SECONDS_DAY = SECONDS_HOUR * 24; // 86,400
        public const int SECONDS_WEEK = SECONDS_DAY * 7; // 604,800
        public const int SECONDS_YEAR = (int)(SECONDS_DAY * DAYS_YEAR); // 31,556,926

        private static readonly ObjectCacher<string, Texture2D> ImageCache = new(TimeSpan.FromMinutes(10));
        private const int MaxConcurrentImageDownloads = 3;
        private static int activeImageDownloads = 0;

        public static string ToRelativeTime(this DateTime dateTime, int layersDeep = 2, bool formatting = true)
        {
            try
            {
                if (layersDeep < 1)
                    throw new ArgumentOutOfRangeException(nameof(layersDeep), "layersDeep must be at least 1.");

                DateTime now = DateTime.UtcNow;
                DateTime target = dateTime.ToUniversalTime();

                bool inFuture = target > now;

                TimeSpan remaining = inFuture ? target - now : now - target;

                if (remaining <= TimeSpan.Zero)
                    return formatting ? "Now." : "now";

                // For calendar calculations, count forward from the earlier date.
                DateTime cursor = inFuture ? now : target;

                List<string> parts = [];

                while (remaining.Ticks > 0 && parts.Count < layersDeep)
                {
                    var (timeDiff, str) = GetMostSignificantTime(remaining, cursor);

                    if (timeDiff <= TimeSpan.Zero)
                        break;

                    parts.Add(str);

                    remaining -= timeDiff;
                    cursor = cursor.Add(timeDiff);
                }

                string output = parts.Count switch
                {
                    0 => "now",
                    1 => parts[0],
                    2 => $"{parts[0]} and {parts[1]}",
                    _ => string.Join(", ", parts.Take(parts.Count - 1)) + $" and {parts[^1]}"
                };

                if (!formatting)
                    return output;

                if (output.Equals("now"))
                    return "Now.";

                return inFuture ? $"In {output}." : $"{output} ago.";
            }
            catch (Exception e)
            {
                Plugin.Log.Error($"There was an error converting the dateTime given ({dateTime}) to relative time!\n{e}");
                return "";
            }
        }
        public static (TimeSpan timeDiff, string str) GetMostSignificantTime(TimeSpan timeDiff, DateTime startTime)
        {
            double totalSeconds = timeDiff.TotalSeconds;
            string outp;
            if (timeDiff.Ticks < 10)
                outp = $"{timeDiff.Ticks * 100} nanoseconds";
            else
                outp = totalSeconds switch
                {
                    < SECONDS_MILLI => $"{(int)(timeDiff.Ticks / 10)} microseconds",
                    < SECONDS_MILLI * 2 => "1 millisecond",
                    < 1 => $"{(int)timeDiff.TotalMilliseconds} milliseconds",
                    < 2 => "1 second",
                    < SECONDS_MINUTE => $"{(int)totalSeconds} seconds",
                    < SECONDS_MINUTE * 2 => "1 minute",
                    < SECONDS_HOUR => $"{(int)timeDiff.TotalMinutes} minutes",
                    < SECONDS_HOUR * 2 => "1 hour",
                    < SECONDS_DAY => $"{(int)timeDiff.TotalHours} hours",
                    < SECONDS_DAY * 2 => "1 day",
                    < SECONDS_WEEK => $"{(int)timeDiff.TotalDays} days",
                    < SECONDS_WEEK * 2 => "1 week",
                    < SECONDS_WEEK * 4 => $"{(int)(timeDiff.TotalDays / 7)} weeks",
                    < SECONDS_YEAR => "", // Handle months below
                    < SECONDS_YEAR * 2 => "1 year",
                    _ => $"{(int)(timeDiff.TotalDays / DAYS_YEAR)} years"
                };

            if (outp.Length == 0)
            {
                int months = 0;
                int totalSecondsInMonths = 0, toAdd = SECONDS_DAY * DateTime.DaysInMonth(startTime.Year, startTime.Month);
                while (totalSecondsInMonths + toAdd <= totalSeconds)
                {
                    months++;
                    startTime = startTime.AddMonths(1);
                    totalSecondsInMonths += toAdd;
                    toAdd = SECONDS_DAY * DateTime.DaysInMonth(startTime.Year, startTime.Month);
                }
                outp = months == 0 ? $"{(int)(timeDiff.TotalDays / 7)} weeks" : $"{months} month{(months == 1 ? "" : "s")}";
                return (months == 0 ? TimeSpan.FromDays((int)(timeDiff.TotalDays / 7) * 7) : TimeSpan.FromSeconds(totalSecondsInMonths), outp);
            }

            TimeSpan timeSpent = totalSeconds switch
            {
                < SECONDS_MICRO => timeDiff,
                < SECONDS_MILLI => TimeSpan.FromTicks((int)(timeDiff.Ticks / 10) * 10),
                < 1 => TimeSpan.FromMilliseconds((int)timeDiff.TotalMilliseconds),
                < SECONDS_MINUTE => TimeSpan.FromSeconds((int)totalSeconds),
                < SECONDS_HOUR => TimeSpan.FromMinutes((int)timeDiff.TotalMinutes),
                < SECONDS_DAY => TimeSpan.FromHours((int)timeDiff.TotalHours),
                < SECONDS_WEEK => TimeSpan.FromDays((int)timeDiff.TotalDays),
                < SECONDS_YEAR => TimeSpan.FromDays((int)(timeDiff.TotalDays / 7) * 7),
                _ => TimeSpan.FromSeconds((int)(timeDiff.TotalDays / DAYS_YEAR) * SECONDS_YEAR)
            };

            return (timeSpent, outp);
        }

        public static List<string> ToModCodes(this GameplayModifiers mods, bool failed)
        {
            List<string> outp = [];

            if (mods.noFailOn0Energy && failed) outp.Add("NF");
            if (mods.enabledObstacleType == GameplayModifiers.EnabledObstacleType.NoObstacles) outp.Add("NO");
            if (mods.noBombs) outp.Add("NB");
            switch (mods.songSpeed)
            {
                case GameplayModifiers.SongSpeed.Slower:
                    outp.Add("SS");
                    break;
                case GameplayModifiers.SongSpeed.Faster:
                    outp.Add("FS");
                    break;
                case GameplayModifiers.SongSpeed.SuperFast:
                    outp.Add("SF");
                    break;
            }
            if (mods.ghostNotes) outp.Add("GN");
            if (mods.disappearingArrows) outp.Add("DA");
            if (mods.proMode) outp.Add("PM");
            if (mods.smallCubes) outp.Add("SC");
            if (mods.instaFail) outp.Add("IF");
            // TODO: Add Off Platform detection (if it ever is an issue)

            return outp;
        }
        public static float ModCodesToMultiplier(this IEnumerable<string> modCodes)
        {
            if (AccsaberAPI.Modifiers is null)
                throw new Exception("Modifiers need to be loaded before getting the mod mult.");

            float mult = 1f;

            foreach (string code in modCodes)
            {
                AccSaberModifier modData = AccsaberAPI.Modifiers.FirstOrDefault(mod => mod.Code.Equals(code, StringComparison.OrdinalIgnoreCase)) 
                    ?? throw new Exception("There isn't a modifier for code \"" + code + "\"!");

                mult += modData.Multiplier - 1f;
            }

            return mult;
        }

        public static IEnumerable<T> MergeSortedLists<T>(IComparer<T> comparer, params IEnumerable<IEnumerable<T>> lists)
        {
            List<IEnumerator<T>> iterators = [with(lists.Count())];

            foreach (IEnumerable<T> list in lists)
            {
                IEnumerator<T> enumerator = list.GetEnumerator();
                enumerator.MoveNext();
                iterators.Add(enumerator);
            }

            while (iterators.Count > 0) 
            {
                T current = iterators[0].Current;
                int currentIndex = 0;

                for (int i = 1; i < iterators.Count; ++i)
                {
                    if (comparer.Compare(iterators[i].Current, current) < 0)
                    {
                        current = iterators[i].Current;
                        currentIndex = i;
                    }
                }

                yield return current;

                if (!iterators[currentIndex].MoveNext())
                    iterators.RemoveAt(currentIndex);
            }
        }
        public static IEnumerable<T> MergeSortedLists<T>(params IEnumerable<IEnumerable<T>> lists) where T : IComparable<T> =>
            MergeSortedLists(Comparer<T>.Default, lists);

        public static async Task LoadCoverImage(this Image image, string hash, string? coverUrl, CancellationToken ct = default)
        {
            try
            {
                MainThreadDispatcher.AssertOnMainThread();
                Sprite? s = null;
#if NEW_VERSION
                BeatmapLevel? level = SongCore.Loader.GetLevelByHash(hash);

                if (level is not null)
                    s = await level.previewMediaData.GetCoverSpriteAsync();
#else
                CustomPreviewBeatmapLevel? level = SongCore.Loader.GetLevelByHash(hash);

                if (level is not null)
                    s = await level.GetCoverImageAsync(ct);
#endif

                if (!image.gameObject.activeSelf)
                    return;

                if (s is not null)
                    image.sprite = s;

                else if (coverUrl is not null)
                    await LoadImage(image, coverUrl, ct);

                else
                    image.sprite = SongCore.Loader.defaultCoverImage;
            }
            catch (TaskCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception e)
            {
                Plugin.Log.Error("There was an error loading a cover image!\n" + e);
                image.sprite = SongCore.Loader.defaultCoverImage;
            }
        }
        public static IEnumerator LoadCoverImageRoutine(this Image image, string hash, string? coverUrl, CancellationToken ct = default)
        {
            MainThreadDispatcher.AssertOnMainThread();

            if (image is null)
            {
                Plugin.Log.Error("Cannot load cover image for null image.");
                yield break;
            }

            Sprite? s = null;

#if NEW_VERSION
            BeatmapLevel? level = SongCore.Loader.GetLevelByHash(hash);

            if (level is not null)
            {
                Task<Sprite> task = level.previewMediaData.GetCoverSpriteAsync();

                yield return task.WaitWithRoutine(result => s = result, e => Plugin.Log.Error(e), ct);
            }
#else
            CustomPreviewBeatmapLevel? level = SongCore.Loader.GetLevelByHash(hash);

            if (level is not null) 
            {
                Task<Sprite> task = level.GetCoverImageAsync(ct);

                yield return task.WaitWithRoutine(result => s = result, e => Plugin.Log.Error(e), ct);
            }
#endif

            if (ct.IsCancellationRequested || image is null || !image.gameObject.activeInHierarchy)
                yield break;

            if (s is not null)
                image.sprite = s;

            else if (coverUrl is not null)
                yield return LoadImageRoutine(image, coverUrl, ct);

            else
                image.sprite = SongCore.Loader.defaultCoverImage;
        }
        public static async Task LoadImage(this Image image, string url, CancellationToken ct = default)
        {
            try
            {
                Sprite? s = await GetImage(url, ct);

                if (s is not null && image.gameObject.activeSelf)
                    image.sprite = s;
            }
            catch (TaskCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception e)
            {
                Plugin.Log.Error($"There was an issue setting the image \"{url}\"!\n{e}");
            }
        }
        public static IEnumerator LoadImageRoutine(this Image image, string url, CancellationToken ct = default)
        {
            MainThreadDispatcher.AssertOnMainThread();

            if (image is null || string.IsNullOrEmpty(url))
                yield break;

            if (ImageCache.TryGetCachedItem(url, out Texture2D? val))
            {
                if (!ct.IsCancellationRequested && image is not null && image.gameObject.activeInHierarchy)
                    image.sprite = Sprite.Create(val, new(0, 0, val!.width, val.height), new Vector2(0.5f, 0.5f), val.width);

                yield break;
            }

            // Limit concurrent downloads/decodes.
            while (activeImageDownloads >= MaxConcurrentImageDownloads)
            {
                if (ct.IsCancellationRequested || image is null)
                    yield break;

                yield return null;
            }

            ++activeImageDownloads;

            UnityWebRequest request = UnityWebRequestTexture.GetTexture(url, true);

            try
            {
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();

                while (!operation.isDone)
                {
                    if (ct.IsCancellationRequested || image is null)
                    {
                        request.Abort();
                        yield break;
                    }

                    yield return null;
                }

#if NEW_VERSION
                bool failed = request.result != UnityWebRequest.Result.Success;
#else
                bool failed = request.isNetworkError || request.isHttpError;
#endif

                if (failed)
                {
                    Plugin.Log.Error($"Failed to load cover \"{url}\": {request.error}");
                    yield break;
                }

                if (ct.IsCancellationRequested || image is null)
                    yield break;

                Texture2D texture = DownloadHandlerTexture.GetContent(request);

                if (texture is null)
                {
                    Plugin.Log.Error($"Downloaded cover texture was null: {url}");
                    yield break;
                }

                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;

                Sprite sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    texture.width
                );

                ImageCache.CacheItem(url, texture);

                if (!ct.IsCancellationRequested && image is not null && image.gameObject.activeInHierarchy)
                    image.sprite = sprite;
            }
            finally
            {
                --activeImageDownloads;

                request.Dispose();
            }
        }
        public static async Task<Sprite?> GetImage(string url, CancellationToken ct = default)
        {
            try
            {
                if (ImageCache.TryGetCachedItem(url, out Texture2D? val))
                    return Sprite.Create(val, new(0, 0, val!.width, val.height), new Vector2(0.5f, 0.5f), val.width);

                Sprite? outp = null;

                IEnumerator GetImageRoutine()
                {
                    byte[]? data = null;

                    yield return Task.Run(async () => { var (bytes, _) = await APIHandler.CallAPI_Bytes(url, null, ct: ct); return bytes; }, ct).WaitWithRoutine(bytes => data = bytes);

                    if (data is null)
                        yield break;

                    Texture2D? t = null;

                    yield return VersionUtils.LoadImageAsync(data).WaitWithRoutine(tex => t = tex);

                    if (t is null)
                        yield break;

                    outp = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), t.width);

                    ImageCache.CacheItem(url, t);
                }

                await Coroutines.AsTask(GetImageRoutine());

                return outp;
            }
            catch (TaskCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception e)
            {
                Plugin.Log.Error($"There was an issue loading the image \"{url}\"!\n{e}");
            }
            return null;
        }
        public static IEnumerator WaitWithRoutine<T>(this Task<T> task, Action<T>? onSuccess = null, Action<Exception>? onError = null, CancellationToken ct = default)
        {
            while (!task.IsCompleted)
            {
                if (ct.IsCancellationRequested)
                    yield break;

                yield return null;
            }

            if (task.IsCanceled)
                yield break;

            if (task.IsFaulted)
            {
                onError?.Invoke(task.Exception);
                yield break;
            }

            onSuccess?.Invoke(task.Result);
        }
        public static Color32 GetMaxColorValues(this Texture2D tex)
        {
            // Get all pixels as a flat 1D array (byte-based 0-255)
            Color32[] pixels = tex.GetPixels32();

            byte maxR = 0;
            byte maxG = 0;
            byte maxB = 0;
            byte maxA = 0;

            // Iterate through all pixels to capture the highest channel values
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].r > maxR) maxR = pixels[i].r;
                if (pixels[i].g > maxG) maxG = pixels[i].g;
                if (pixels[i].b > maxB) maxB = pixels[i].b;
                if (pixels[i].a > maxA) maxA = pixels[i].a;
            }

            return new Color32(maxR, maxG, maxB, maxA);
        }

        public static int MaxScoreForNotes(int notes)
        {
            if (notes <= 0) return 0;
            if (notes < 6) return 115 * (notes * 2 - 1);
            if (notes < 14) return 115 * ((notes - 5) * 4 + 9);
            return 920 * (notes - 14) + 5635;
        }

        public static bool Compare<T>(this T x, T y, string comp) where T : IComparable
        {
            int compVal = x.CompareTo(y);

            return compVal switch
            {
                > 0 => comp.IndexOf('>') != -1,
                < 0 => comp.IndexOf('<') != -1,
                0 => comp.IndexOf('=') != -1
            };
        }

        public static T Max<T>(T item1, T item2) where T : IComparable<T> => item1.CompareTo(item2) <= 0 ? item2 : item1;
        public static T Min<T>(T item1, T item2) where T : IComparable<T> => item1.CompareTo(item2) <= 0 ? item1 : item2;

        public static string GenerateNonce(int byteLength = 32)
        {
            byte[] byteArray = new byte[byteLength];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(byteArray);
            }
            // Return as Base64 string for HTTP headers/tags
            return Convert.ToBase64String(byteArray);
        }
        public static int GetHashCode<T1, T2>(T1 item1, T2 item2)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + item1?.GetHashCode() ?? 0;
                hash = hash * 23 + item2?.GetHashCode() ?? 0;
                return hash;
            }
        }

        public static T? ParseEnum<T>(string value) where T : Enum => (T?)Enum.Parse(typeof(T), value);
        public static void AddRange<K, V>(this IDictionary<K, V> dict, IEnumerable<KeyValuePair<K, V>> vals)
        {
            foreach (KeyValuePair<K, V> kvp in vals)
                dict.TryAdd(kvp.Key, kvp.Value);
        }
        public static string Capitialize(this string str) => char.ToUpper(str[0]) + str[1..];
        public static string CapitializeWords(this string str) => string.Join(" ", str.Split(' ').Select(word => word.Capitialize()));
        public static void CapitializeSelf(ref string str) => str = str.Capitialize();

        public static void AttachTo(this ModalView modal, Transform parent) =>
            typeof(ModalView).GetMethod("SetupView", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).Invoke(modal, [parent]);
        public static void AttachTo(this ModalView modal, Component parent) => modal.AttachTo(parent.transform);

        #region Debug functions
        /// <summary>
        /// Produces a compact, human-readable string representation of an <see cref="IEnumerable{T}"/>.
        /// The output is formatted as: <c>[item1, item2, item3]</c>. For an empty sequence the method
        /// returns <c>[]</c>.
        /// </summary>
        /// <typeparam name="T">The element type of the sequence.</typeparam>
        /// <param name="arr">The sequence to convert to a string. The sequence must not be <c>null</c>.</param>
        /// <returns>
        /// A string containing the sequence elements separated by <c>", "</c> and wrapped in square brackets.
        /// </returns>
        /// <remarks>
        /// - Each element's <see cref="object.ToString"/> is used for representation.
        /// </remarks>
        public static string Print<T>(this IEnumerable<T> arr)
        {
            if (arr is null || !arr.Any()) return "[]";
            StringBuilder outp = new();
            foreach (T item in arr)
                outp.Append(", " + item);
            return $"[{outp.ToString()[2..]}]";
        }
        #endregion
    }

    public delegate bool SpecifiedComparer<T>(T x, T y);
    public delegate bool SpecifiedComparer(IComparable x, IComparable y);

    public class MyFloatComparer(ComparisonType compType = ComparisonType.LT) : IComparer<float> 
    {
        private readonly ComparisonType compType = compType;
        public int Compare(float x, float y)
        {
            if (Mathf.Approximately(x, y))
                return 0;

            float comp;
            if ((compType & ComparisonType.GT) != 0)
                comp = y - x;
            else if ((compType & ComparisonType.LT) != 0)
                comp = x - y;
            else return -1;

            return comp < 0 ? -1 : 1;
        }
    }
    public class SelectComparer<TBase, TComp>(Func<TBase, TComp> converter, IComparer<TComp> comparer, IComparer<TBase>? backupComparer = null) : IComparer<TBase>
    {
        private readonly Func<TBase, TComp> Converter = converter;
        private readonly IComparer<TComp> Comparer = comparer;
        private readonly IComparer<TBase>? BackupComparer = backupComparer;
        public int Compare(TBase x, TBase y)
        {
            int outp = Comparer.Compare(Converter(x), Converter(y));

            if (outp == 0 && BackupComparer is not null)
                return BackupComparer.Compare(x, y);

            return outp;
        }
    }
}
