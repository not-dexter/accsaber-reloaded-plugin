using Accsaber.Utils;
using AccSaber.Managers;
using AccSaber.Models;
using AccSaber.Models.CacheModels;
using AccSaber.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

#if !NEW_VERSION
using Oculus.Platform;
#endif

using static AccSaber.API.APIHandler;
using static AccSaber.API.HelpfulPaths;
using AccSaber.Models.PlayerModels;
using AccSaber.Patches;
using BS_Utils.Gameplay;

namespace AccSaber.API
{
    /// <summary>
    /// Central API helper for interacting with the AccSaber backend.
    /// Provides synchronous cache helpers and asynchronous methods to
    /// fetch leaderboards, player info, relations and milestones. This
    /// class is internal to the plugin and is intended to centralize
    /// HTTP calls, caching behavior and basic data transformations.
    /// </summary>
    internal static class AccsaberAPI
    {
        /// <summary>
        /// Global throttler used for all API requests from this class.
        /// Configured with the plugin's desired rate limits.
        /// </summary>
        public static readonly Throttler throttler = new(400, 60);

        /// <summary>
        /// Factory producing a predicate that filters leaderboard entries by country code.
        /// Usage: <c>CountryFilterMaker("US")</c> returns a predicate that checks entry.Country == "US".
        /// </summary>
        public static readonly Func<string, Func<AccSaberLeaderboardEntry, bool>> CountryFilterMaker = country => token => token.Country.Equals(country);

        /// <summary>
        /// Cache of player information keyed by player id.
        /// </summary>
        private static readonly ObjectCacher<AccSaberPlayer> playerInfoCacher = new();

        /// <summary>
        /// Cache of leaderboard scores keyed by difficulty id.
        /// </summary>
        private static readonly ObjectCacher<ScoreCache> scoreInfoCacher = new();

        /// <summary>
        /// Number of entries per leaderboard page used by the plugin.
        /// </summary>
        public const int PAGE_LENGTH = 10;

        /// <summary>
        /// Multiplier used for filter paging logic to request larger batches from the API.
        /// </summary>
        public const int FILTER_PAGE_MULT = 10;

        public static List<AccSaberModifier>? Modifiers { get; private set; } = null;

        static AccsaberAPI()
        {
            // Listen for local score updates and invalidate caches when appropriate.
            AccSaberStore.OnScoreUpdated += token =>
            {
                string playerId = token.PlayerId, diffId = token.DifficultyId;

                playerInfoCacher.RemoveItem(playerId);

                if (scoreInfoCacher.TryGetCachedItem(diffId, out ScoreCache item) && item.UserIds.Contains(playerId))
                {
                    Plugin.Log.Notice($"Difficulty id {diffId} was removed from cache.");
                    scoreInfoCacher.RemoveItem(diffId);
                }
            };

            Task.Run(async () => Modifiers = await CallAPI_Json<List<AccSaberModifier>>(APAPI_MODS, throttler));
        }
        #region Sync Functions

        /// <summary>
        /// Returns the cached leaderboard length for a difficulty id and display type,
        /// or -1 if unknown.
        /// </summary>
        /// <param name="diffId">Leaderboard difficulty id.</param>
        /// <param name="displayType">Type of leaderboard (Global, Country, etc.).</param>
        /// <returns>Cached length or -1 when not available.</returns>
        public static int GetLength(string diffId, LeaderboardDisplayType displayType)
        {
            if (scoreInfoCacher.TryGetCachedItem(diffId, out ScoreCache info) && info.TryGetLength(displayType, out int len))
                return len;

            return -1;
        }

        /// <summary>
        /// Checks whether sufficient score data for the specified page is cached.
        /// When <paramref name="filter"/> is provided this will verify filtered counts against the cache.
        /// Page here is one-indexed.
        /// </summary>
        public static bool ScoreDataCached(string diffId, int page, Func<AccSaberLeaderboardEntry, bool>? filter = null, int setCount = -1)
        { // page is one indexed.
            if (!scoreInfoCacher.TryGetCachedItem(diffId, out ScoreCache info))
                return false;

            int count;

            if (filter is null)
            {
                page--;
                int topIdx = page * PAGE_LENGTH, bottomIdx = topIdx + PAGE_LENGTH;
                int blocked = info.BlockedUserIndexes.SkipWhile(idx => idx < topIdx).TakeWhile(idx => idx < bottomIdx).Count();
                bottomIdx += blocked;
                filter = token =>
                {
                    int rank = token.Rank;
                    return topIdx < rank && bottomIdx >= rank;
                };
                count = info.Data.Count(filter);
                //Plugin.Log.Info($"count = {count}, page = {page}");
                return PAGE_LENGTH == count;
            }

            count = info.Data.Count(filter);

            return count == setCount || count - ((page - 1) * PAGE_LENGTH) >= PAGE_LENGTH;
        }

        /// <summary>
        /// Checks whether sufficient country-filtered score data for the specified page is cached.
        /// Page here is one-indexed.
        /// </summary>
        public static bool ScoreDataCached(string diffId, int page, string country)
        { // page is one indexed
            if (!scoreInfoCacher.TryGetCachedItem(diffId, out ScoreCache info))
                return false;

            int count = info.Data.Count(CountryFilterMaker(country));

            return count >= page * PAGE_LENGTH || (info.TryGetLength(LeaderboardDisplayType.Country, out int len) && count == len - info.BlockedUserIndexes.Count);
        }

        /// <summary>
        /// Attempts to compute a player's rank within a filtered view using cached data.
        /// Returns true and sets <paramref name="rank"/> when successful; otherwise returns false.
        /// </summary>
        public static bool TryGetRankWithFilter(string diffId, string userId, Func<AccSaberLeaderboardEntry, bool>? filter, out int rank)
        {
            // init rank to -1 in case a check fails
            rank = -1;

            // check for there being a cache for this map, as well as the targeted user id is in this cache.
            if (!scoreInfoCacher.TryGetCachedItem(diffId, out ScoreCache info) || !info.UserIds.Contains(userId))
                return false;
            //Plugin.Log.Info("Passed check 1.");

            // if the user is in the cache, get their score data.
            AccSaberLeaderboardEntry score = info.Data.Find(token => token.PlayerId.Equals(userId));

            // check to make sure that all scores before the targeted one are loaded (to insure that the page number will be correct).
            int userIndex = score.Rank - 1;
            if (info.Data.Count <= userIndex || !info.Data[userIndex].PlayerId.Equals(userId))
                return false;
            //Plugin.Log.Info("Passed check 2.");

            // take all scores up to the player score, filter it using the filter, then since we know the target score in at the end, just return the length minus 1.
            rank = info.Data.Take(userIndex + 1).Where(filter).Count() - 1;

            return true;
        }

        /// <summary>
        /// Merges and caches a batch of score entries for a difficulty id.
        /// Updates known leaderboard lengths and blocked user indexes.
        /// </summary>
        private static void CacheScoreData(string diffId, IEnumerable<AccSaberLeaderboardEntry> scoreData, IEnumerable<int> BlockedUserIndexes, 
            int leaderboardSize, LeaderboardDisplayType displayType)
        {
            if (scoreInfoCacher.TryGetCachedItem(diffId, out var val))
            {
                val.UserIds.UnionWith(scoreData.Select(data => data.PlayerId));

                ref List<AccSaberLeaderboardEntry> storedData = ref val.Data;
                ref List<int> blocked = ref val.BlockedUserIndexes;

                storedData = MergeListWithEnumerable(storedData, scoreData, token => token.Rank);
                if (BlockedUserIndexes.Any())
                    blocked = MergeListWithEnumerable(blocked, BlockedUserIndexes);
                if (!val.ContainsLength(displayType) && leaderboardSize >= 0)
                    val.GetLength(displayType) = leaderboardSize;

                scoreInfoCacher.CacheItem(val, diffId);
            }
            else
            {
                (LeaderboardDisplayType, int)[] len = leaderboardSize >= 0 ? [(displayType, leaderboardSize)] : [];
                scoreInfoCacher.CacheItem(new([.. scoreData], [.. scoreData.Select(data => data.PlayerId)], [.. BlockedUserIndexes], len), diffId);
            }

            //ScoreCache c = scoreInfoCacher.diffId.CachedItem;
            //Plugin.Log.Info($"The cache now has {c.Data.Count} entries: {c.Data.Select(GetRank).Print()}");
            //Plugin.Log.Info($"There are the following sizes: { c.LeaderboardLengths.Values.Print()}");

        }

        /// <summary>
        /// Merge helper that preserves ordering by a comparable key and removes duplicates.
        /// </summary>
        private static List<T> MergeListWithEnumerable<T>(List<T> left, IEnumerable<T> right, bool reverseOrder = false) where T : IComparable
        {
            return MergeListWithEnumerable(left, right, a => a);
        }

        /// <summary>
        /// Merge helper that preserves ordering by a converted comparable key and removes duplicates.
        /// The <paramref name="converter"/> extracts the comparable key from items.
        /// </summary>
        private static List<T> MergeListWithEnumerable<T>(List<T> left, IEnumerable<T> right, Func<T, IComparable> converter, bool reverseOrder = false)
        {
            List<T> outp = new(left.Count + right.Count());
            IEnumerator<T>? rightEnum = right.GetEnumerator();

            rightEnum.MoveNext();
            int i = 0;
            while (i < left.Count)
            {
                int comp = converter(left[i]).CompareTo(converter(rightEnum.Current));
                if (reverseOrder ? comp > 0 : comp < 0)
                {
                    T toAdd = left[i++];
                    if (outp.Count == 0 || converter(outp.Last()).CompareTo(converter(toAdd)) != 0)
                        outp.Add(toAdd);
                }
                else
                {
                    outp.Add(rightEnum.Current);
                    if (!rightEnum.MoveNext())
                    {
                        rightEnum.Dispose();
                        rightEnum = null;
                        break;
                    }
                }
            }
            if (i < left.Count)
            {
                if (converter(outp.Last()).CompareTo(converter(left[i])) == 0)
                    i++;
                outp.AddRange(left.Skip(i));
            }
            if (rightEnum is not null)
                do
                    outp.Add(rightEnum.Current);
                while (rightEnum.MoveNext());

            return outp;
        }

        /// <summary>
        /// Clears all cached leaderboard score data.
        /// </summary>
        public static void InvalidateCache() => scoreInfoCacher.ClearCache();

        /// <summary>
        /// Removes cached data for a specific difficulty id.
        /// </summary>
        public static void InvalidateCache(string diffId) => scoreInfoCacher.RemoveItem(diffId);

        /// <summary>
        /// Removes all cached references to a player from all difficulty caches.
        /// Adjusts blocked user index lists accordingly so page retrieval remains consistent.
        /// </summary>
        public static void RemovePlayerFromCache(string playerId)
        {
            foreach (KeyValuePair<string, ScoreCache> diff in scoreInfoCacher)
            {
                if (!diff.Value.UserIds.Contains(playerId))
                    continue;

                diff.Value.UserIds.Remove(playerId);
                AccSaberLeaderboardEntry info = diff.Value.Data.First(token => token.PlayerId.Equals(playerId));
                diff.Value.Data.Remove(info);
                int idx = info.Rank - 1;

                if (diff.Value.BlockedUserIndexes.Count < 2 || diff.Value.BlockedUserIndexes.Last() < idx)
                    diff.Value.BlockedUserIndexes.Add(idx);
                else for (int i = diff.Value.BlockedUserIndexes.Count - 2; i >= 0; i--)
                        if (diff.Value.BlockedUserIndexes[i] < idx)
                        {
                            diff.Value.BlockedUserIndexes.Insert(i + 1, idx);
                            break;
                        }
            }
        }

        /// <summary>
        /// Attempts to satisfy a filtered score request from the in-memory cache.
        /// If successful returns the array of scores and true; otherwise returns partial results and false.
        /// </summary>
        private static (AccSaberLeaderboardEntry[] scores, bool success) SearchInCache(ScoreCache cache, ref int page, Func<AccSaberLeaderboardEntry, bool> filter, 
            int pageLength, int scoresNeeded, int pageMult)
        {
            List<AccSaberLeaderboardEntry>? currentCache = cache.Data;
            if (currentCache is not null && currentCache.Count / pageLength > page)
            {
                IEnumerable<AccSaberLeaderboardEntry> cachedScores = currentCache.Skip(page * pageLength).Where(filter)!;
                int cachedScoresLen = cachedScores.Count();
                if (currentCache.Count == cache.GetLength(LeaderboardDisplayType.Global) || cachedScoresLen >= scoresNeeded)
                {
                    cachedScores = cachedScores.Take(scoresNeeded);
                    return ([.. cachedScores], true);
                }
                if (cachedScores.Any())
                {
                    int truePage = currentCache.Count / PAGE_LENGTH;
                    page = truePage / pageMult;
                    //scoresNeeded -= cachedScores.Count();
                    return ([.. cachedScores], false);
                }
            }
            return ([], false);
        }

        #endregion
        #region Async Functions

        /// <summary>
        /// Asynchronously retrieves the cached length of the leaderboard for a hash + difficulty.
        /// Returns -1 when unknown or when the leaderboard cannot be resolved.
        /// </summary>
        public static int GetLength(string hash, BeatmapDifficulty diff, LeaderboardDisplayType displayType = LeaderboardDisplayType.Global)
        {
            string? diffId = GetLeaderboardDifficultyId(hash, diff);

            if (diffId is null)
                return -1;

            return GetLength(diffId, displayType);
        }

        /// <summary>
        /// Overload for GetLength that accepts an <see cref="AccSaberBasicDifficulty"/>.
        /// </summary>
        public static int GetLength(AccSaberBasicDifficulty diff, LeaderboardDisplayType displayType = LeaderboardDisplayType.Global) =>
            GetLength(diff.Hash, diff.Difficulty, displayType);

        /// <summary>
        /// Gets a page of scores for a specific hash & difficulty (zero-indexed page).
        /// Returns null on failure.
        /// </summary>
        public static async Task<AccSaberLeaderboardEntry[]?> GetScoreData(int page, string hash, BeatmapDifficulty diff, string? country = null)
        { // page is zero indexed.
            string? diffId = GetLeaderboardDifficultyId(hash, diff);
            if (diffId is null) return null;
            return await GetScoreData(page, diffId, country);
        }

        /// <summary>
        /// Gets a page of scores for a specific difficulty id (one-indexed page).
        /// Returns null on failure.
        /// </summary>
        public static async Task<AccSaberLeaderboardEntry[]?> GetScoreData(int page, string diffId, string? country = null)
        { // page is one indexed.
            try
            {
                --page;
                IEnumerable<AccSaberLeaderboardEntry>? scores = await (country is null ? GetLeaderboardScores(diffId, page, PAGE_LENGTH) :
                    GetLeaderboardScores(diffId, country, page, PAGE_LENGTH)).ConfigureAwait(false);

                if (scores is null) 
                    return null;

                return [.. scores];
            }
            catch (Exception e)
            {
                Plugin.Log.Error("Failure to get score data for map.\n");
                Plugin.Log.Debug(e);
                return null;
            }
        }

        /// <summary>
        /// Retrieves scores filtered by <paramref name="filter"/> while attempting to use cached batches.
        /// Returns a tuple of the found scores and the resolved true page index.
        /// </summary>
        public static async Task<(AccSaberLeaderboardEntry[] scores, int truePage)> GetScoreData(int page, string diffId, 
            Func<AccSaberLeaderboardEntry, bool> filter, LeaderboardDisplayType displayType, int scoresNeeded = PAGE_LENGTH,
            int pageMult = FILTER_PAGE_MULT, int maxCalls = 10, bool cacheBatch = true)
        { // page is one indexed.
            try
            {
                if (maxCalls <= 0)
                    throw new ArgumentException("Don't call a function then ask it to do nothing.");

                int truePage = page, pageLength = PAGE_LENGTH * pageMult;
                page = (page - 1) / pageMult;

                List<AccSaberLeaderboardEntry> outp = new(PAGE_LENGTH);

                List<AccSaberLeaderboardEntry>? toCache = null;
                ScoreCache currentCacheData = scoreInfoCacher.GetCachedItem(diffId);
                if (cacheBatch)
                {
                    toCache = new(pageLength);
                    var (scores, success) = SearchInCache(currentCacheData, ref page, filter, pageLength, scoresNeeded, pageMult);
                    if (success)
                        return (scores, currentCacheData.Data.Count / PAGE_LENGTH);
                    else
                        outp.AddRange(scores);
                }

                int leaderboardSize = -1;

                do
                {
                    string? dataStr = await CallAPI_String(string.Format(APAPI_LEADERBOARD_DIFF, diffId, page, pageLength), throttler).ConfigureAwait(false);
                    if (string.IsNullOrEmpty(dataStr))
                        throw new ArgumentNullException("The leaderboard api is not returning any data.");

                    JToken response = JToken.Parse(dataStr!);
                    if ((bool)response["empty"]!)
                        break;

                    if (leaderboardSize == -1)
                        leaderboardSize = (int)response["totalElements"]!;

                    IEnumerable<AccSaberLeaderboardEntry> tokens = response["content"]!.Children().Select(token => token.ToObject<AccSaberLeaderboardEntry>()!);

                    if (cacheBatch)
                        toCache!.AddRange(tokens);

                    IEnumerable<AccSaberLeaderboardEntry> scores = tokens.Where(filter);
                    int scoreLen = scores.Count();
                    if (scoreLen >= scoresNeeded)
                    {
                        scores = scores.Take(scoresNeeded);
                        pageMult = (int)Math.Ceiling(scores.Last().Rank / (float)PAGE_LENGTH); // This is just to update truePage correctly.
                        outp.AddRange(scores);
                        scoresNeeded = 0;
                    }
                    else
                    {
                        outp.AddRange(scores);
                        scoresNeeded -= scoreLen;
                    }
                    truePage += pageMult;

                    if ((bool)response["last"]!)
                        break;

                    page++;
                    maxCalls--;
                } while (scoresNeeded > 0 && maxCalls > 0);

                if (cacheBatch)
                    CacheScoreData(diffId, toCache!, [], leaderboardSize, displayType);

                return ([.. outp], truePage);
            }
            catch (Exception e)
            {
                Plugin.Log.Error("Issue getting filtered score data.\n" + e);
                return default;
            }
        }

        /// <summary>
        /// Gets a page of scores for a specific relation (friends/rivals/etc.).
        /// Returns null on failure.
        /// Page is one-indexed.
        /// </summary>
        public static async Task<AccSaberLeaderboardEntry[]?> GetScoreData(int page, string diffId, RelationType relation)
        { // page is one indexed
            try
            {
                --page;
                if (scoreInfoCacher.TryGetCachedItem(diffId, out ScoreCache cache) && cache.TryGetLength(relation.Convert(), out int scoreCount))
                {
                    HashSet<string> relations = PlayerSocialLife.GetIds_Internal(relation.Convert())!;
                    IEnumerable<AccSaberLeaderboardEntry> tokens = cache.Data.Where(token => relations.Contains(token.PlayerId));

                    int tokenCount = tokens.Count();
                    int pageCount = page * PAGE_LENGTH;

                    //Plugin.Log.Info($"token count = {tokenCount} || score count = {scoreCount} || page count = {pageCount}");

                    if (scoreCount == 0)
                        return [];

                    if (tokenCount == scoreCount || tokenCount > pageCount && (scoreCount < pageCount + PAGE_LENGTH || tokenCount >= pageCount + PAGE_LENGTH))
                        return [.. tokens.Skip(pageCount).Take(PAGE_LENGTH)];

                }

                AccSaberPagedContent<AccSaberLeaderboardEntry>? data = await CallAPI_Json<AccSaberPagedContent<AccSaberLeaderboardEntry>>(
                    string.Format(APAPI_LEADERBOARD_DIFF_RELATION, diffId, relation.ToString(), page, PAGE_LENGTH), throttler);

                if (data is null || data.Content is null)
                    return null;

                CacheScoreData(diffId, data.Content, [], data.TotalElements, relation.Convert());
                cache = scoreInfoCacher.GetCachedItem(diffId);
                scoreInfoCacher.CacheItem(cache, diffId);

                return [.. data.Content];

            } catch (Exception e)
            {
                Plugin.Log.Error("There was an error getting score data.");
                Plugin.Log.Debug(e);
            }
            return null;
        }

        /// <summary>
        /// Retrieves paged milestone data for a user. Optionally filters and sorts results.
        /// </summary>
        public static async Task<List<AccSaberMilestone>?> GetMilestoneData(string userId, Func<AccSaberMilestone, bool>? filter = null, Comparison<AccSaberMilestone>? sorter = null, int pageMult = FILTER_PAGE_MULT)
        {
            int page = 0;
            List<AccSaberMilestone> outp = [];
            int pageLen = PAGE_LENGTH * pageMult;
            while (true)
            {
                AccSaberPagedContent<AccSaberMilestone>? response = 
                    await CallAPI_Json<AccSaberPagedContent<AccSaberMilestone>>(string.Format(APAPI_MILESTONE, userId, page, pageLen), throttler);

                if (response is null || response.Content is null)
                    return null;

                if (response.LastPage)
                    break;

                IEnumerable<AccSaberMilestone> data = response.Content;

                if (filter is not null)
                    data = data.Where(filter);

                outp.AddRange(data);
                ++page;
            }

            if (sorter is not null)
                outp.Sort(sorter);

            return outp;
        }

        /// <summary>
        /// Gets either completed or incomplete milestones for a user.
        /// </summary>
        public static async Task<List<AccSaberMilestone>?> GetMilestoneData(string userId, bool completed, Func<AccSaberMilestone, bool>? filter = null, Comparison<AccSaberMilestone>? sorter = null)
        {
            string apapiFormat = completed ? APAPI_MILESTONE_COMPLETE : APAPI_MILESTONE_INCOMPLETE;

            AccSaberPagedContent<AccSaberMilestone>? response = await CallAPI_Json<AccSaberPagedContent<AccSaberMilestone>>(string.Format(apapiFormat, userId), throttler);

            if (response is null || response.Content is null)
                return null;

            List<AccSaberMilestone> data = response.Content;

            if (filter is not null)
                data = [.. data.Where(filter)];

            if (sorter is not null)
                data.Sort(sorter);

            return data;
        }

        /// <summary>
        /// Retrieves all relations for the authenticated user and returns a dictionary
        /// keyed by RelationType containing user ids and relation ids.
        /// </summary>
        public static async Task<Dictionary<RelationType, (HashSet<string> userIds, Dictionary<string, string> relations)>> GetPlayerRelations()
        {
            const int pageLength = PAGE_LENGTH * 10;
            int page = 0, callsLeft = -1;
            Dictionary<RelationType, (HashSet<string> userIds, Dictionary<string, string> relations)> outp = [];

            foreach (RelationType rt in Enum.GetValues(typeof(RelationType)))
                outp[rt] = ([], []);

            do
            {
                AccSaberPagedContent<AccSaberRelation>? response = await CallAPI_Json<AccSaberPagedContent<AccSaberRelation>>(string.Format(APAPI_AUTH_GET_RELATIONS_ALL, page, pageLength), throttler);

                if (response is null || response.Content is null)
                    break;

                if (callsLeft == -1)
                    callsLeft = response.TotalElements / pageLength;

                foreach (AccSaberRelation token in response.Content)
                {
                    RelationType rt = token.Relation;
                    string userId = token.TargetPlayerId, relationId = token.Id;

                    outp[rt].userIds.Add(userId);
                    outp[rt].relations.Add(userId, relationId);
                }

            } while (callsLeft-- > 0);
            return outp;
        }

        /// <summary>
        /// Retrieves relations for a specified player and relation type.
        /// Returns the set of target ids and a list of (userId, relationId) pairs.
        /// </summary>
        //public static async Task<(HashSet<string> ids, IEnumerable<(string userId, string relationId)> relations)> GetPlayerRelations(RelationType relation, string playerId)
        //{
        //    const int pageLength = PAGE_LENGTH * 10;
        //    int page = 0, callsLeft = 0;
        //    HashSet<string> userIds = [];
        //    List<(string, string)> relations = [];
        //    do
        //    {
        //        AccSaberPagedContent<AccSaberRelation>? response = await CallAPI_Json<AccSaberPagedContent<AccSaberRelation>>(
        //            string.Format(APAPI_RELATIONS, playerId, relation.ToString(), "outgoing", page, pageLength), throttler);

        //        if (response is null || response.Content is null)
        //            break;

        //        if (callsLeft == 0)
        //            callsLeft = response.TotalElements / pageLength;

        //        IEnumerable<(string userId, string relationId)> ids = response.Content.Select(token => (token.TargetPlayerId, token.Id));

        //        foreach (var (userId, _) in ids)
        //            userIds.Add(userId);

        //        relations.AddRange(ids);

        //    } while (callsLeft > 0);

        //    return (userIds, relations);
        //} // Note: Commented out as it is not used currently.

        /// <summary>
        /// Creates a relation (friend/rival/etc.) to the target player.
        /// Returns a tuple (success, relationId) where relationId is null on failure.
        /// </summary>
        public static async Task<(bool success, string? relationId)> AddPlayerRelation(RelationType relation, string targetId)
        {
            if (!long.TryParse(targetId, out long id))
            {
                Plugin.Log.Error($"The target id given to SetPlayerRelation, \"{targetId}\", is not able to be parsed!");
                return (false, null);
            }

            HttpRequestMessage request = new(HttpMethod.Post, APAPI_AUTH_SET_RELATION)
            {
                Content = new StringContent($"{{\"targetUserId\": {id}, \"type\": \"{relation}\"}}", System.Text.Encoding.UTF8, "application/json")
            };

            var (Success, Content) = await CallAPI(request, throttler, maxRetries: 1).ConfigureAwait(false);

            if (!Success)
                return (false, null);

            return (true, JToken.Parse(await Content!.ReadAsStringAsync())["id"]!.ToString());
        }

        /// <summary>
        /// Removes a relation by relation id. Returns true on success.
        /// </summary>
        public static async Task<bool> RemovePlayerRelation(string relationId)
        {
            HttpRequestMessage request = new(HttpMethod.Delete, string.Format(APAPI_AUTH_DELETE_RELATION, relationId));

            return (await CallAPI(request, throttler, maxRetries: 1).ConfigureAwait(false)).Success;
        }

        /// <summary>
        /// Gets an individual player's score entry for a specific hash and difficulty.
        /// Attempts to return from cache when possible. Cancellation supported via token.
        /// </summary>
        public static async Task<AccSaberLeaderboardEntry?> GetScoreData(string userId, string hash, BeatmapDifficulty diff, CancellationToken ct = default)
        {
            if (SerializerHandler.CachedMaps.TryGetValue(hash, out AccSaberBasicMap map))
            {
                AccSaberBasicDifficulty? selectedDiff = map.Difficulties?.FirstOrDefault(currentDiff => currentDiff.Difficulty == diff);
                if (selectedDiff is not null && scoreInfoCacher.TryGetCachedItem(selectedDiff.DifficultyId, out ScoreCache val) && val.UserIds.Contains(userId))
                    return val.Data.First(token => token.PlayerId.Equals(userId));
            }
            string reloadedDiff = EnumUtils.DiffToReloadedDiff(diff);

            string? dataStr = await CallAPI_String(string.Format(APAPI_SCORE, userId, hash.ToLower(), reloadedDiff), throttler, true, ct: ct).ConfigureAwait(false);
            if (string.IsNullOrEmpty(dataStr)) 
                return null;

            return JsonConvert.DeserializeObject<AccSaberLeaderboardEntry>(dataStr!);
        }

        /// <summary>
        /// Fetches the ranked map object for a hash. Uses an in-memory cache to avoid repeated requests.
        /// Returns null on failure or cancellation.
        /// </summary>
        public static AccSaberBasicMap? GetLeaderboard(string hash, CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested) 
                return null;

            if (SerializerHandler.CachedMaps.TryGetValue(hash.ToLower(), out AccSaberBasicMap? map))
                return map;

            // Note: This function will no longer fetch the map of a given hash. It was assume any map not loaded in the cache is unranked.
            /*try
            {
                map = await CallAPI_Json<AccSaberRankedMap>(string.Format(APAPI_HASH, hash), throttler, true, ct: ct);

                if (map is not null)
                    SerializerHandler.CachedMaps[hash] = map;

                return map;
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Issue URL: {string.Format(APAPI_HASH, hash)}");
                Plugin.Log.Error("There was an error getting map information: " + ex);
            }*/

            return null;
        }

        /// <summary>
        /// Returns the difficulty object for a hash + difficulty or null if not found.
        /// </summary>
        public static AccSaberBasicDifficulty? GetLeaderboard(string hash, BeatmapDifficulty diff, CancellationToken ct = default)
        {
            AccSaberBasicMap? map = GetLeaderboard(hash, ct);

            //Plugin.Log.Info($"Map null? {map is null}, mapDiff null? {map?.Difficulties is null}, mapDiff diff = {map?.Difficulties?.Select(diff => diff.Difficulty).Print()}");

            if (map is null) 
                return null;

            return map.Difficulties.FirstOrDefault(difficulty => difficulty.Difficulty == diff);
        }

        /// <summary>
        /// Returns the AccSaber difficulty id for a hash + difficulty, or null.
        /// </summary>
        public static string? GetLeaderboardDifficultyId(string hash, BeatmapDifficulty diff, CancellationToken ct = default) =>
            GetLeaderboard(hash, diff, ct)?.DifficultyId;

        /// <summary>
        /// Overload accepting an <see cref="AccSaberBasicDifficulty"/>.
        /// </summary>
        public static string? GetLeaderboardDifficultyId(AccSaberBasicDifficulty diff, CancellationToken ct = default) =>
            GetLeaderboardDifficultyId(diff.Hash, diff.Difficulty, ct);

        /// <summary>
        /// Retrieves all basic diffs from the API and groups them by hash.
        /// Returns an empty dictionary on failure.
        /// </summary>
        public static async Task LoadAllBasicDiffs(CancellationToken ct = default)
        {
            string? dataStr = await CallAPI_String(APAPI_DIFFS, throttler, ct: ct);

            if (string.IsNullOrEmpty(dataStr))
            {
                Plugin.Log.Error("Failed to get basic diffs, the json returned was null!");
                return;
            }

            List<AccSaberBasicDifficulty>? diffData = JsonConvert.DeserializeObject<List<AccSaberBasicDifficulty>>(dataStr!);

            if (diffData is null)
            {
                Plugin.Log.Error("Failed to get basic diffs, the json returned was not able to be deserialized!");
                return;
            }

            Dictionary<string, AccSaberBasicMap> rankedMaps = [];

            foreach (AccSaberBasicDifficulty diff in diffData)
            {
                if (rankedMaps.ContainsKey(diff.Hash))
                    rankedMaps[diff.Hash].Difficulties.Add(diff);
                else
                    rankedMaps[diff.Hash] = new()
                    {
                        Hash = diff.Hash,
                        Difficulties = [diff]
                    };
            }

            SerializerHandler.CachedMaps.AddRange(rankedMaps);
        }

        /// <summary>
        /// Loads metadata for all maps into the in-memory map cache.
        /// Uses paging to fetch the full set of maps. This can be expensive and should be called sparingly.
        /// </summary>
        public static async Task LoadAllMaps(CancellationToken ct = default)
        {
            int totalMaps = SerializerHandler.TotalMaps;

            if (totalMaps == SerializerHandler.CachedMaps.Count)
                return;

            if (totalMaps < 0)
            {
                AccSaberPagedContent? pageInfo = await CallAPI_Json<AccSaberPagedContent>(string.Format(APAPI_MAPS, 0, 1), throttler, ct: ct);

                if (pageInfo is null)
                    return;

                totalMaps = pageInfo.TotalElements;
            }

            AccSaberPagedContent<AccSaberRankedMap>? maps = await CallAPI_Json<AccSaberPagedContent<AccSaberRankedMap>>(string.Format(APAPI_MAPS, 0, totalMaps), throttler, ct: ct);

            if (maps is null || maps.Content is null)
                return;

            foreach (AccSaberRankedMap map in maps.Content)
                SerializerHandler.CachedMaps.TryAdd(map.Hash, map);
        }

        /// <summary>
        /// Fetches leaderboard scores for a difficulty id (global view). Uses cache when possible.
        /// Returns null on failure.
        /// </summary>
        public static async Task<IEnumerable<AccSaberLeaderboardEntry>?> GetLeaderboardScores(string difficulty_id, int page = 0, int count = 10, CancellationToken ct = default)
        {
            if (scoreInfoCacher.TryGetCachedItem(difficulty_id, out var data))
            {
                int minRank = page * count + 1, maxRank = minRank + count;
                int topIdx = data.Data.FindIndex(token =>
                {
                    int rank = token.Rank;
                    return rank >= minRank && rank < maxRank; 
                });

                if (topIdx >= 0 && data.Data.Count > topIdx + count - 1)
                {

                    int topRank = data.Data[topIdx].Rank;
                    int bottomRank = data.Data[topIdx + count - 1].Rank;

                    IEnumerable<int> temp = data.BlockedUserIndexes.SkipWhile(idx => idx < topRank);
                    //int blockedUserCountBefore = data.BlockedUserIndexes.Count - temp.Count(); // To use later if I decide to shift pages.
                    int blockedUserCount = temp.Count(idx => idx < bottomRank);

                    //Plugin.Log.Info($"bottom = {bottomRank}, top = {topRank}, bottom idx = {topIdx + count - 1}, top idx = {topIdx}, blocked = {blockedUserCount}");

                    if (topRank - page * count == 1 && bottomRank - (page + 1) * count == blockedUserCount)
                        return data.Data.Skip(topIdx).Take(count);
                }
            }

            string? dataStr = await CallAPI_String(string.Format(APAPI_LEADERBOARD_DIFF, difficulty_id, page, count), throttler, true, ct: ct).ConfigureAwait(false);

            if (string.IsNullOrEmpty(dataStr)) 
                return null;

            AccSaberPagedContent<AccSaberLeaderboardEntry>? dataToken = JToken.Parse(dataStr!).ToObject<AccSaberPagedContent<AccSaberLeaderboardEntry>>();

            if (dataToken is null || dataToken.Content is null)
                return null;

            List<AccSaberLeaderboardEntry>? outp = dataToken.Content;

            List<int>? blockedUserIds;
            (outp, blockedUserIds) = await HandleBlockedPlayers(outp!, data, page, count, (inPage, inCount) => GetLeaderboardScores(difficulty_id, inPage, inCount, ct));
            if (outp is null || blockedUserIds is null)
                return null;

            CacheScoreData(difficulty_id, outp, blockedUserIds, dataToken.TotalElements, LeaderboardDisplayType.Global);
            return outp.Take(count);
        }

        /// <summary>
        /// Fetches leaderboard scores for a difficulty id filtered by country. Uses cache when possible.
        /// Returns null on failure.
        /// </summary>
        public static async Task<IEnumerable<AccSaberLeaderboardEntry>?> GetLeaderboardScores(string difficulty_id, string country, int page = 0, int count = 10, CancellationToken ct = default)
        {
            if (scoreInfoCacher.TryGetCachedItem(difficulty_id, out ScoreCache data)) 
            {
                int trueCount = data.Data.Count(CountryFilterMaker(country));
                //int total = data.LeaderboardLengths.TryGetValue(LeaderboardDisplayType.Country, out int leng) ? leng : 0;
                //Plugin.Log.Info($"true count = {trueCount}, blocked = {data.BlockedUserIndexes.Count}, total = {total}");
                if (data.TryGetLength(LeaderboardDisplayType.Country, out int len) && trueCount == len - data.BlockedUserIndexes.Count || page < trueCount / count)
                    return data.Data.Where(CountryFilterMaker(country)).Skip(page * count).Take(count); 
            }

            string? dataStr = await CallAPI_String(string.Format(APAPI_LEADERBOARD_DIFF_COUNTRY, difficulty_id, country, page, count), throttler, true, ct: ct).ConfigureAwait(false);

            if (string.IsNullOrEmpty(dataStr)) 
                return null;

            AccSaberPagedContent<AccSaberLeaderboardEntry>? dataToken = JsonConvert.DeserializeObject<AccSaberPagedContent<AccSaberLeaderboardEntry>>(dataStr!);

            if (dataToken is null || dataToken.Content is null)
                return null;

            List<AccSaberLeaderboardEntry>? outp = dataToken.Content;

            List<int>? blockedUserIds;
            (outp, blockedUserIds) = await HandleBlockedPlayers(outp!, data, page, count, (inPage, inCount) => GetLeaderboardScores(difficulty_id, country, inPage, inCount, ct));
            if (outp is null || blockedUserIds is null)
                return null;

            CacheScoreData(difficulty_id, outp, blockedUserIds, dataToken.TotalElements, LeaderboardDisplayType.Country);
            return outp.Take(count);
        }

        /// <summary>
        /// Internal handler that removes blocked players from the provided score tokens and fetches additional entries if necessary.
        /// Returns the adjusted list and the list of blocked indexes to store.
        /// </summary>
        private static async Task<(List<AccSaberLeaderboardEntry>? newOutp, List<int>? blockedIds)> HandleBlockedPlayers(List<AccSaberLeaderboardEntry> scoreTokens, 
            ScoreCache data, int page, int count,
            Func<int, int, Task<IEnumerable<AccSaberLeaderboardEntry>?>> getExtraScores)
        {
            if (data.BlockedUserIndexes is null)
                return (scoreTokens, []);

            if (data.BlockedUserIndexes.Count > 0)
            {
                IEnumerable<AccSaberLeaderboardEntry> toUnblock = scoreTokens.Where(token => data.BlockedUserIndexes.Contains(token.Rank - 1) && !PlayerSocialLife.PlayerBlocked!.Contains(token.PlayerId));
                if (toUnblock.Any())
                {
                    List<int> toUnblockIdx = [.. toUnblock.Select(token => token.Rank - 1)];
                    foreach (int i in toUnblockIdx)
                        data.BlockedUserIndexes.Remove(i);
                }
            }

            int blockedUsers = 0;
            List<int> blockedUserIds = [];

            if (PlayerSocialLife.PlayerBlocked?.Count > 0)
            {
                bool addNewEntries = data.BlockedUserIndexes is null || PlayerSocialLife.PlayerBlocked.Count != data.BlockedUserIndexes.Count;

                for (int i = scoreTokens.Count - 1; i >= 0; i--)
                    if (PlayerSocialLife.PlayerBlocked.Contains(scoreTokens[i].PlayerId))
                    {
                        blockedUsers++;
                        if (addNewEntries)
                            blockedUserIds.Add(scoreTokens[i].Rank - 1);
                        scoreTokens.RemoveAt(i);
                    }

                if (blockedUsers > 0)
                {
                    int newPage = (page + 1) * count / blockedUsers;
                    IEnumerable<AccSaberLeaderboardEntry>? extras = await getExtraScores(newPage, blockedUsers);
                    if (extras is null)
                        return (null, null);
                    scoreTokens.AddRange(extras);
                }
            }

            return (scoreTokens, blockedUserIds);
        }

        /// <summary>
        /// Retrieves player information from the AccSaber API for the given <paramref name="userId"/>.
        /// </summary>
        /// <param name="userId">AccSaber user id or player identifier to retrieve.</param>
        /// <param name="stats">
        /// When <c>true</c>, requests the player's statistics from the API. If a cached value exists it will be returned
        /// only if statistics are already present on the cached object.
        /// </param>
        /// <param name="statDiff">
        /// When <c>true</c>, after the player data is loaded this method will trigger loading of stat differences
        /// (awaits <c>outp.LoadStatDiffs</c>) so the returned object contains diff information as well.
        /// </param>
        /// <param name="ct">Optional cancellation token forwarded to the underlying API call.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> that resolves to an <see cref="AccSaberPlayer"/> instance if the API call
        /// and JSON deserialization succeed; otherwise <c>null</c> when the API returns no data or deserialization fails.
        /// </returns>
        /// <remarks>
        /// - This method first checks an in-memory cache (`playerInfoCacher`). If a cached item exists and either
        ///   <paramref name="stats"/> is <c>false</c> or the cached item already contains statistics, the cached item is returned.
        /// - If not cached, the method calls the AccSaber API (via <c>CallAPI_String</c>), deserializes the JSON into
        ///   <see cref="AccSaberPlayer"/>, optionally awaits stat-diff loading, caches the result, and returns it.
        /// - Side effects: may perform network I/O, may await additional operations to populate stat diffs, and will cache the final object.
        /// - Cancellation: the provided <paramref name="ct"/> may cancel the API request (throws <see cref="OperationCanceledException"/>).
        /// </remarks>
        public static async Task<AccSaberPlayer?> GetPlayerInfo(string userId, bool stats, bool statDiff, CancellationToken ct = default)
        {
            bool playerCached = playerInfoCacher.TryGetCachedItem(userId, out AccSaberPlayer? outp);
            bool statsCached = playerCached && outp!.Statistics is not null;
            bool statDiffCached = statsCached && statDiff && outp!.LoadStatDiffs.IsCompleted;

            //Plugin.Log.Info($"cached = {playerCached}, stats cached = {statsCached}, diff cached = {statDiffCached} || want stats ? {stats}, want diff {statDiff}");

            if (playerCached && (statsCached || !stats) && (statDiffCached || !statDiff))
                return outp;

            if (statsCached && statDiff)
            {
                await outp!.LoadStatDiffs;
                return outp;
            }

            outp = await CallAPI_Json<AccSaberPlayer>(string.Format(APAPI_PLAYERID, userId, stats.ToString().ToLower()), throttler, ct: ct);

            if (outp is null)
                return null;

            if (statDiff)
                await outp.LoadStatDiffs;

            playerInfoCacher.CacheItem(outp, userId);

            return outp;
        }

        public static async Task<IEnumerable<AccSaberPlayerScore>?> GetPlayerScores(int page, int pageLength, APCategory category = APCategory.Overall, CancellationToken ct = default)
        { // page is zero indexed.
            // The cache should be sorted, so this should not be an issue.
            IEnumerable<AccSaberPlayerScore> filteredCache = SerializerHandler.CachedPlayerScores;
            if (category != APCategory.Overall)
                filteredCache = filteredCache.Where(score => score.Category == category);

            if (filteredCache.Count() >= (page + 1) * pageLength)
                return filteredCache.Skip(page * pageLength).Take(pageLength);

            await PlayerSocialLife.LoadTask;

            string url = string.Format(APAPI_SCORES, PlayerSocialLife.PlayerID, page, pageLength);
            if (category != APCategory.Overall)
                url += "&categoryId=" + EnumUtils.EnumToReloadedCategory(category);

            AccSaberPagedContent<AccSaberLeaderboardEntry>? response = await CallAPI_Json<AccSaberPagedContent<AccSaberLeaderboardEntry>>(url, throttler, ct: ct);

            if (response is null || response.Content is null)
                return null;

            IEnumerable<AccSaberPlayerScore> outp = response.Content.Select(entry => new AccSaberPlayerScore(entry));

            if (category == APCategory.Overall) // TODO: At some point, support caching for any context the score returns. As it is, it would take too much time.
            {
                SerializerHandler.CachedPlayerScores.AddRange(outp);

                //if (SerializerHandler.CachedPlayerScoreLength < 0)
                //    SerializerHandler.CachedPlayerScoreLength = response.TotalElements;
            }

            SerializerHandler.CachedPlayerScoreLength = response.TotalElements;

            return outp;
        }

        /// <summary>
        /// Returns the public visibility settings for friends/rivals from the API.
        /// </summary>
        internal static async Task<(bool friends, bool rivals)> ExposeRelations()
        {
            string? dataStr = await CallAPI_String(string.Format(APAPI_AUTH_GET_SETTINGS, "privacy"), throttler).ConfigureAwait(false);

            if (string.IsNullOrEmpty(dataStr))
                return (false, false);

            JToken privacySettings = JToken.Parse(dataStr!);
            return (privacySettings["privacy.followingVisibility"]?.ToString().Equals("public") ?? false, privacySettings["privacy.rivalsVisibility"]?.ToString().Equals("public") ?? false);
        }

        /// <summary>
        /// Authenticates the current platform user against the AccSaber API and returns an <see cref="AuthInfo"/>.
        /// Caches the returned auth info in <see cref="PlayerSocialLife.AuthInfo"/>.
        /// </summary>
        public static async Task<AuthInfo?> Authenticate()
        {
            if (PlayerSocialLife.AuthInfo is not null && PlayerSocialLife.AuthInfo.ExpirationDate > DateTime.Now)
                return PlayerSocialLife.AuthInfo;

            await GetUserInfo.GetUserAsync();
            IPlatformUserModel platformUserModel = Plugin.Container.TryResolve<IPlatformUserModel>();
            PlatformUserAuthTokenData authToken = await platformUserModel.GetUserAuthToken();
            UserInfo userInfo = await platformUserModel.GetUserInfo();
            string token = "";
            string provider = "";
            switch (userInfo.platform)
            {
                case UserInfo.Platform.Steam:
                    token = authToken.token;
                    provider = "steamTicket";
                    break;
                case UserInfo.Platform.Oculus:
#if NEW_VERSION
                    token = (await platformUserModel.RequestXPlatformAccessToken(CancellationToken.None)).token;
#else
                    token = await OculusTicket();
#endif
                    provider = "oculusTicket";
                    break;
            }

            Dictionary<string, string> jsonValues = new()
            {
                { "provider", provider },
                { "ticket", token }
            };

            StringContent sc = new(JsonConvert.SerializeObject(jsonValues), System.Text.Encoding.UTF8, "application/json");
            HttpRequestMessage request = new(HttpMethod.Post, APAPI_AUTH) { Content = sc };

            var (success, content) = await CallAPI(request, throttler, maxRetries: 1).ConfigureAwait(false);

            if (success)
            {
                string? dataStr = await content!.ReadAsStringAsync();

                if (dataStr is null)
                    return null;

                AuthInfo? outp = JsonConvert.DeserializeObject<AuthInfo>(dataStr);

                if (outp is not null)
                {
                    SetAuthForClient(outp);
                    return outp;
                }
            }

            return null;
        }
#if !NEW_VERSION
        public static async Task<string> OculusTicket()
        {
            await GetUserInfo.GetUserAsync();
            TaskCompletionSource<string> tcs = new();
#pragma warning disable CS8604 // Possible null reference argument.
            Users.GetAccessToken().OnComplete(delegate (Message<string> message) { tcs.TrySetResult(message.IsError ? null : message.Data); });
#pragma warning restore CS8604 // Possible null reference argument.
            return await tcs.Task;
        }
#endif
        internal static async Task<bool> SubmitScore(AccSaberScore score)
        {
            if (!SubmissionPatch.Submit)
                return false;

            score.Nonce = MiscUtils.GenerateNonce(64); // Regenerate this just to make sure no one can steal the nonce.

            HttpRequestMessage request = new(HttpMethod.Post, APAPI_SCORE_SUBMIT)
            {
                Content = new StringContent(JsonConvert.SerializeObject(score), System.Text.Encoding.UTF8, "application/json")
            };

            var (success, _) = await CallAPI(request, null, maxRetries: 1).ConfigureAwait(false); // No throttler because this should throw an error if it is called more than once a minute.

            Plugin.Log.Info(success ? "Score submitted!" : "Score failed to submit.");

            return success;

            //Note: Currently this will submit on party mode and probably multiplayer, which will need to be fixed
        }

        #endregion
        #region Misc structs

        /// <summary>
        /// Internal structure used to cache leaderboard data for a specific difficulty id.
        /// - Data: ordered list of leaderboard entries
        /// - UserIds: set of player ids present in the cached Data list
        /// - BlockedUserIndexes: indexes of entries that were removed due to local blocking
        /// - LeaderboardLengths: cached lengths for each <see cref="LeaderboardDisplayType"/>
        /// </summary>
        private struct ScoreCache
        {
            public List<AccSaberLeaderboardEntry> Data;
            public HashSet<string> UserIds;
            public List<int> BlockedUserIndexes;
            public readonly int[] LeaderboardLengths;

            /// <summary>
            /// Returns a ref to the cached length for a given display type.
            /// </summary>
            public ref int GetLength(LeaderboardDisplayType displayType) => ref LeaderboardLengths[BitOperations.Log2((uint)displayType)];
            public bool ContainsLength(LeaderboardDisplayType displayType) => GetLength(displayType) > -1;
            public bool TryGetLength(LeaderboardDisplayType displayType, out int length)
            {
                length = GetLength(displayType);
                return length > -1;
            }

            public ScoreCache(List<AccSaberLeaderboardEntry> data, HashSet<string> userIds, List<int> blockedUserIndexes, params (LeaderboardDisplayType displayType, int length)[] lengths)
            {
                Data = data;
                UserIds = userIds;
                BlockedUserIndexes = blockedUserIndexes;
                LeaderboardLengths = new int[Enum.GetNames(typeof(LeaderboardDisplayType)).Length];

                for (int i = 0; i < LeaderboardLengths.Length; ++i)
                    LeaderboardLengths[i] = -1;

                foreach (var (displayType, length) in lengths)
                    GetLength(displayType) = length;
            }
        }

        /// <summary>
        /// Authentication token information returned by the AccSaber API.
        /// Includes access/refresh tokens and convenience properties for expiration.
        /// </summary>
        internal sealed class AuthInfo
        {
            [JsonProperty("accessToken")]
            public string AccessToken { get; set; } = null!;

            [JsonProperty("refreshToken")]
            public string RefreshToken { get; set; } = null!;

            [JsonProperty("expiresIn")]
            public long ExpiresIn { get; set; }

            /// <summary>
            /// Expiration length as a TimeSpan computed from <see cref="ExpiresIn"/>.
            /// </summary>
            [JsonIgnore]
            public TimeSpan ValidLength => TimeSpan.FromSeconds(ExpiresIn);

            /// <summary>
            /// Absolute expiration date/time computed during deserialization.
            /// </summary>
            [JsonIgnore]
            public DateTime ExpirationDate { get; set; }

            [JsonProperty("userId")]
            public string UserId { get; set; } = null!;

            /// <summary>
            /// Optional platform user model associated with this auth info (not serialized).
            /// </summary>
            [JsonIgnore]
            public IPlatformUserModel? UserModel { get; set; }


            [OnDeserialized]
            private void OnDeserialized(StreamingContext context)
            {
                ExpirationDate = DateTime.Now + ValidLength;
            }
        }

        #endregion
    }
}