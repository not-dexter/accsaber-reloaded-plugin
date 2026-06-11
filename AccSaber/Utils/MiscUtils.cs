using AccSaber.API;
using AccSaber.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

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


        public static string ToRelativeTime(this DateTime dateTime, int layersDeep = 2, bool formatting = true)
        {
            bool inFuture = DateTime.UtcNow < dateTime;

            TimeSpan timeSpan = inFuture ? dateTime.ToUniversalTime() - DateTime.UtcNow : DateTime.UtcNow - dateTime.ToUniversalTime();

            string outp = "";
            bool single = layersDeep == 1;

            while (timeSpan.Ticks > 0 && layersDeep-- > 0)
            {
                var (timeDiff, str) = GetMostSignificantTime(timeSpan, dateTime);
                timeSpan -= timeDiff;
                dateTime = dateTime.AddTicks(timeDiff.Ticks);
                outp += single ? str : (layersDeep == 0 || timeSpan.Ticks == 0 ? " and " : ", ") + str;
            }

            if (!single)
                outp = outp[2..];

            return formatting ? inFuture ? $"In {outp}." : $"{outp} ago." : outp;
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
                while (totalSecondsInMonths + toAdd < totalSeconds)
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
        public static void AddRange<K, V>(this IDictionary<K, V> dict, IEnumerable<KeyValuePair<K, V>> vals)
        {
            foreach (KeyValuePair<K, V> kvp in vals)
                dict.TryAdd(kvp.Key, kvp.Value);
        }
        public static string Capitialize(this string str) => char.ToUpper(str[0]) + str[1..];
        public static string CapitializeWords(this string str) => string.Join(" ", str.Split(' ').Select(word => word.Capitialize()));
        public static void CapitializeSelf(ref string str) => str = str.Capitialize();

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
}
