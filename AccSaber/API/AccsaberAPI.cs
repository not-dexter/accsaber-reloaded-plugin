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
using AccSaber.Utils.Misc;
using Zenject;

namespace AccSaber.API
{
    internal class AccsaberAPI : IInitializable
    {
        [Inject] private readonly PlayerSocialLife playerInfo = null!;
        [Inject] private readonly SerializationHandler serialHandler = null!;

        public static readonly Throttler Throttler = new(400, 60);

        public readonly Func<string, Func<AccSaberLeaderboardEntry, bool>> CountryFilterMaker = country => token => token.Country.Equals(country);

        private readonly ObjectCacher<AccSaberPlayer> playerInfoCacher = new();

        private readonly ObjectCacher<ScoreCache> scoreInfoCacher = new();

        public const int PAGE_LENGTH = 10;

        public const int FILTER_PAGE_MULT = 10;

        public static List<AccSaberModifier>? Modifiers { get; private set; } = null;
        private readonly AsyncLock PlayerLoadLock = new();

        public enum LoginState
        {
            InProgress,
            Success,
            Failed
        };

        public LoginState CurrentLoginState { get; private set; }

        public event Action<LoginState, string>? OnLoginUpdated;

        public void Initialize()
        {
            Task.Run(async () => Modifiers = await CallAPI_Json<List<AccSaberModifier>>(APAPI_MODS, Throttler));
        }
        #region Sync Functions

        public void OnScoreUpdated(AccSaberLeaderboardEntry token)
        {
            string playerId = token.PlayerId, diffId = token.DifficultyId;

            playerInfoCacher.RemoveItem(playerId);

            if (scoreInfoCacher.TryGetCachedItem(diffId, out ScoreCache item) && item.UserIds.Contains(playerId))
            {
                Plugin.Log.Notice($"Difficulty id {diffId} was removed from cache.");
                scoreInfoCacher.RemoveItem(diffId);
            }
        }
        public int GetLength(string diffId, LeaderboardDisplayType displayType)
        {
            if (scoreInfoCacher.TryGetCachedItem(diffId, out ScoreCache info) && info.TryGetLength(displayType, out int len))
                return len;

            return -1;
        }

        public bool ScoreDataCached(string diffId, int page, Func<AccSaberLeaderboardEntry, bool>? filter = null, int setCount = -1)
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

        public bool ScoreDataCached(string diffId, int page, string country)
        { // page is one indexed
            if (!scoreInfoCacher.TryGetCachedItem(diffId, out ScoreCache info))
                return false;

            int count = info.Data.Count(CountryFilterMaker(country));

            return count >= page * PAGE_LENGTH || (info.TryGetLength(LeaderboardDisplayType.Country, out int len) && count == len - info.BlockedUserIndexes.Count);
        }

        public bool TryGetRankWithFilter(string diffId, string userId, Func<AccSaberLeaderboardEntry, bool>? filter, out int rank)
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
            rank = info.Data.Take(userIndex + 1).Count(filter) - 1;

            return true;
        }

        private void CacheScoreData(string diffId, IEnumerable<AccSaberLeaderboardEntry> scoreData, IEnumerable<int> blockedUserIndexes, 
            IEnumerable<(LeaderboardDisplayType displayType, int leaderboardSize)>? sizeData)
        {
            if (scoreInfoCacher.TryGetCachedItem(diffId, out var val))
            {
                val.UserIds.UnionWith(scoreData.Select(data => data.PlayerId));

                ref List<AccSaberLeaderboardEntry> storedData = ref val.Data;
                ref List<int> blocked = ref val.BlockedUserIndexes;

                storedData = MergeListWithEnumerable(storedData, scoreData, token => token.Rank);
                if (blockedUserIndexes.Any())
                    blocked = MergeListWithEnumerable(blocked, blockedUserIndexes);

                if (sizeData is not null)
                    foreach (var (displayType, leaderboardSize) in sizeData)
                        if (!val.ContainsLength(displayType) && leaderboardSize >= 0)
                            val.GetLength(displayType) = leaderboardSize;

                scoreInfoCacher.CacheItem(val, diffId);
            }
            else
                scoreInfoCacher.CacheItem(new([.. scoreData], [.. scoreData.Select(data => data.PlayerId)], [.. blockedUserIndexes], sizeData ?? []), diffId);

            //ScoreCache c = scoreInfoCacher.diffId.CachedItem;
            //Plugin.Log.Info($"The cache now has {c.Data.Count} entries: {c.Data.Select(GetRank).Print()}");
            //Plugin.Log.Info($"There are the following sizes: { c.LeaderboardLengths.Values.Print()}");

        }
        private void CacheScoreData(string diffId, IEnumerable<AccSaberLeaderboardEntry> scoreData, IEnumerable<int> blockedUserIndexes,
            int leaderboardSize, LeaderboardDisplayType displayType)
        {
            CacheScoreData(diffId, scoreData, blockedUserIndexes, [(displayType, leaderboardSize)]);
        }

        private List<T> MergeListWithEnumerable<T>(List<T> left, IEnumerable<T> right, bool reverseOrder = false) where T : IComparable
        {
            return MergeListWithEnumerable(left, right, a => a);
        }

        private List<T> MergeListWithEnumerable<T>(List<T> left, IEnumerable<T> right, Func<T, IComparable> converter, bool reverseOrder = false)
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

        public void InvalidateCache() => scoreInfoCacher.ClearCache();

        public void InvalidateCache(string diffId) => scoreInfoCacher.RemoveItem(diffId);

        public void RemovePlayerFromCache(string playerId)
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

        private (AccSaberLeaderboardEntry[] scores, bool success) SearchInCache(ScoreCache cache, ref int page, Func<AccSaberLeaderboardEntry, bool> filter, 
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

        public int GetLength(string hash, BeatmapDifficulty diff, LeaderboardDisplayType displayType = LeaderboardDisplayType.Global)
        {
            string? diffId = GetLeaderboardDifficultyId(hash, diff);

            if (diffId is null)
                return -1;

            return GetLength(diffId, displayType);
        }

        public int GetLength(AccSaberBasicDifficulty diff, LeaderboardDisplayType displayType = LeaderboardDisplayType.Global) =>
            GetLength(diff.Hash, diff.Difficulty, displayType);

        public async Task<AccSaberLeaderboardEntry[]?> GetScoreData(int page, string hash, BeatmapDifficulty diff, string? country = null)
        { // page is zero indexed.
            string? diffId = GetLeaderboardDifficultyId(hash, diff);
            if (diffId is null) return null;
            return await GetScoreData(page, diffId, country);
        }

        public async Task<AccSaberLeaderboardEntry[]?> GetScoreData(int page, string diffId, string? country = null)
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

        public async Task<(AccSaberLeaderboardEntry[] scores, int truePage)> GetScoreData(int page, string diffId, 
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
                    string? dataStr = await CallAPI_String(string.Format(APAPI_LEADERBOARD_DIFF, diffId, page, pageLength), Throttler).ConfigureAwait(false);
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

        public async Task<AccSaberLeaderboardEntry[]?> GetScoreData(int page, string diffId, RelationType relation)
        { // page is one indexed
            try
            {
                --page;
                if (scoreInfoCacher.TryGetCachedItem(diffId, out ScoreCache cache) && cache.TryGetLength(relation.Convert(), out int scoreCount))
                {
                    HashSet<string> relations = playerInfo.GetIds_Internal(relation.Convert())!;
                    IEnumerable<AccSaberLeaderboardEntry> tokens = cache.Data.Where(token => relations.Contains(token.PlayerId));

                    int tokenCount = tokens.Count();
                    int pageCount = page * PAGE_LENGTH;

                    //Plugin.Log.Info($"token count = {tokenCount} || score count = {scoreCount} || page count = {pageCount}");

                    if (scoreCount == 0)
                        return [];

                    if (scoreCount != relations.Count)
                    {
                        scoreCount = relations.Count;
                        cache.GetLength(relation.Convert()) = scoreCount;
                    }

                    if (tokenCount == scoreCount || tokenCount > pageCount && (scoreCount < pageCount + PAGE_LENGTH || tokenCount >= pageCount + PAGE_LENGTH))
                        return [.. tokens.Skip(pageCount).Take(PAGE_LENGTH)];

                }

                AccSaberPagedContent<AccSaberLeaderboardEntry>? data = await CallAPI_Json<AccSaberPagedContent<AccSaberLeaderboardEntry>>(
                    string.Format(APAPI_LEADERBOARD_DIFF_RELATION, diffId, relation.ToString(), page, PAGE_LENGTH), Throttler);

                if (data is null || data.Content is null)
                    return null;

                CacheScoreData(diffId, data.Content, [], data.TotalElements, relation.Convert());

                return [.. data.Content];

            } catch (Exception e)
            {
                Plugin.Log.Error("There was an error getting score data.\n" + e);
            }
            return null;
        }

        public async Task<AccSaberLeaderboardEntry[]?> GetScoreData(int page, string diffId, params IEnumerable<RelationType> relations)
        { // page is one indexed
            if (relations is null || !relations.Any())
                return null;

            try
            {
                --page;

                int pageSize = PAGE_LENGTH;
                IEnumerable<AccSaberLeaderboardEntry> outp = [];

                if (scoreInfoCacher.TryGetCachedItem(diffId, out ScoreCache cache))
                {
                    int scoreCount = 0;
                    HashSet<string> relationIds = [];

                    foreach (RelationType type in relations)
                    {
                        HashSet<string> currentRelationIds = playerInfo.GetIds_Internal(type.Convert())!;

                        relationIds.UnionWith(currentRelationIds);

                        if (cache.TryGetLength(type.Convert(), out int count))
                        {
                            if (count != currentRelationIds.Count)
                            {
                                count = currentRelationIds.Count;
                                cache.GetLength(type.Convert()) = count;
                            }

                            scoreCount = Math.Max(scoreCount, count);
                        }
                    }

                    if (scoreCount > 0)
                    {
                        IEnumerable<AccSaberLeaderboardEntry> tokens = cache.Data.Where(token => relationIds.Contains(token.PlayerId));

                        int tokenCount = tokens.Count();
                        int pageCount = page * PAGE_LENGTH;

                        //Plugin.Log.Info($"token count = {tokenCount} || score count = {scoreCount} || page count = {pageCount}");

                        if (scoreCount == 0)
                            return [];

                        if (tokenCount == scoreCount || tokenCount > pageCount && (scoreCount < pageCount + PAGE_LENGTH || tokenCount >= pageCount + PAGE_LENGTH))
                            return [.. tokens.Skip(pageCount).Take(PAGE_LENGTH)];

                        if (tokenCount > pageCount)
                        {
                            outp = tokens.Skip(pageCount);
                            pageSize -= tokenCount - pageCount;
                        }
                    }
                }

                List<(LeaderboardDisplayType, int)> sizes = [];

                foreach (RelationType type in relations)
                {
                    AccSaberPagedContent<AccSaberLeaderboardEntry>? data = await CallAPI_Json<AccSaberPagedContent<AccSaberLeaderboardEntry>>(
                    string.Format(APAPI_LEADERBOARD_DIFF_RELATION, diffId, type.ToString(), page, PAGE_LENGTH), Throttler);

                    if (data is null || data.Content is null)
                        return null;

                    sizes.Add((type.Convert(), data.TotalElements));

                    outp = outp.Union(data.Content);
                }

                CacheScoreData(diffId, outp, [], sizes);

                return [.. outp.Take(PAGE_LENGTH)];
            }
            catch (Exception e)
            {
                Plugin.Log.Error("There was an exception getting score data.\n" + e);
                return null;
            }
        }

        public async Task<List<AccSaberMilestone>?> GetMilestoneData(string userId, Func<AccSaberMilestone, bool>? filter = null, Comparison<AccSaberMilestone>? sorter = null, int pageMult = FILTER_PAGE_MULT)
        {
            int page = 0;
            List<AccSaberMilestone> outp = [];
            int pageLen = PAGE_LENGTH * pageMult;
            while (true)
            {
                AccSaberPagedContent<AccSaberMilestone>? response = 
                    await CallAPI_Json<AccSaberPagedContent<AccSaberMilestone>>(string.Format(APAPI_MILESTONE, userId, page, pageLen), Throttler);

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

        public async Task<List<AccSaberMilestone>?> GetMilestoneData(string userId, bool completed, Func<AccSaberMilestone, bool>? filter = null, Comparison<AccSaberMilestone>? sorter = null)
        {
            string apapiFormat = completed ? APAPI_MILESTONE_COMPLETE : APAPI_MILESTONE_INCOMPLETE;

            AccSaberPagedContent<AccSaberMilestone>? response = await CallAPI_Json<AccSaberPagedContent<AccSaberMilestone>>(string.Format(apapiFormat, userId), Throttler);

            if (response is null || response.Content is null)
                return null;

            List<AccSaberMilestone> data = response.Content;

            if (filter is not null)
                data = [.. data.Where(filter)];

            if (sorter is not null)
                data.Sort(sorter);

            return data;
        }

        public async Task<Dictionary<RelationType, (HashSet<string> userIds, Dictionary<string, string> relations)>> GetPlayerRelations()
        {
            const int pageLength = PAGE_LENGTH * 10;
            int page = 0, callsLeft = -1;
            Dictionary<RelationType, (HashSet<string> userIds, Dictionary<string, string> relations)> outp = [];

            foreach (RelationType rt in Enum.GetValues(typeof(RelationType)))
                outp[rt] = ([], []);

            do
            {
                AccSaberPagedContent<AccSaberRelation>? response = await CallAPI_Json<AccSaberPagedContent<AccSaberRelation>>(string.Format(APAPI_AUTH_GET_RELATIONS_ALL, page, pageLength), Throttler);

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

        //public async Task<(HashSet<string> ids, IEnumerable<(string userId, string relationId)> relations)> GetPlayerRelations(RelationType relation, string playerId)
        //{
        //    const int pageLength = PAGE_LENGTH * 10;
        //    int page = 0, callsLeft = 0;
        //    HashSet<string> userIds = [];
        //    List<(string, string)> relations = [];
        //    do
        //    {
        //        AccSaberPagedContent<AccSaberRelation>? response = await CallAPI_Json<AccSaberPagedContent<AccSaberRelation>>(
        //            string.Format(APAPI_RELATIONS, playerId, relation.ToString(), "outgoing", page, pageLength), Throttler);

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

        public async Task<(bool success, string? relationId)> AddPlayerRelation(RelationType relation, string targetId)
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

            var (Success, Content) = await CallAPI(request, Throttler, maxRetries: 1).ConfigureAwait(false);

            if (!Success)
                return (false, null);

            return (true, JToken.Parse(await Content!.ReadAsStringAsync())["id"]!.ToString());
        }

        public async Task<bool> RemovePlayerRelation(string relationId)
        {
            HttpRequestMessage request = new(HttpMethod.Delete, string.Format(APAPI_AUTH_DELETE_RELATION, relationId));

            return (await CallAPI(request, Throttler, maxRetries: 1).ConfigureAwait(false)).Success;
        }

        public async Task<AccSaberLeaderboardEntry?> GetScoreData(string userId, string hash, BeatmapDifficulty diff, CancellationToken ct = default)
        {
            if (serialHandler.CachedMaps.TryGetValue(hash, out AccSaberBasicMap map))
            {
                AccSaberBasicDifficulty? selectedDiff = map.Difficulties?.FirstOrDefault(currentDiff => currentDiff.Difficulty == diff);
                if (selectedDiff is not null && scoreInfoCacher.TryGetCachedItem(selectedDiff.DifficultyId, out ScoreCache val) && val.UserIds.Contains(userId))
                    return val.Data.First(token => token.PlayerId.Equals(userId));
            }
            string reloadedDiff = EnumUtils.DiffToReloadedDiff(diff);

            string? dataStr = await CallAPI_String(string.Format(APAPI_SCORE, userId, hash.ToLower(), reloadedDiff), Throttler, true, ct: ct).ConfigureAwait(false);
            if (string.IsNullOrEmpty(dataStr)) 
                return null;

            return JsonConvert.DeserializeObject<AccSaberLeaderboardEntry>(dataStr!);
        }

        public AccSaberBasicMap? GetLeaderboard(string hash, CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested) 
                return null;

            if (serialHandler.CachedMaps.TryGetValue(hash.ToLower(), out AccSaberBasicMap? map))
                return map;

            // Note: This function will no longer fetch the map of a given hash. It will assume any map not loaded in the cache is unranked.
            /*try
            {
                map = await CallAPI_Json<AccSaberRankedMap>(string.Format(APAPI_HASH, hash), Throttler, true, ct: ct);

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

        public AccSaberBasicDifficulty? GetLeaderboard(string hash, BeatmapDifficulty diff, CancellationToken ct = default)
        {
            AccSaberBasicMap? map = GetLeaderboard(hash, ct);

            //Plugin.Log.Info($"Map null? {map is null}, mapDiff null? {map?.Difficulties is null}, mapDiff diff = {map?.Difficulties?.Select(diff => diff.Difficulty).Print()}");

            if (map is null) 
                return null;

            return map.Difficulties.FirstOrDefault(difficulty => difficulty.Difficulty == diff);
        }

        public string? GetLeaderboardDifficultyId(string hash, BeatmapDifficulty diff, CancellationToken ct = default) =>
            GetLeaderboard(hash, diff, ct)?.DifficultyId;

        public string? GetLeaderboardDifficultyId(AccSaberBasicDifficulty diff, CancellationToken ct = default) =>
            GetLeaderboardDifficultyId(diff.Hash, diff.Difficulty, ct);

        public async Task<List<AccSaberBasicMap>> LoadAllBasicDiffs(CancellationToken ct = default)
        {
            string? dataStr = await CallAPI_String(APAPI_DIFFS, Throttler, ct: ct);

            if (string.IsNullOrEmpty(dataStr))
            {
                Plugin.Log.Error("Failed to get basic diffs, the json returned was null!");
                return [];
            }

            List<AccSaberBasicDifficulty>? diffData = JsonConvert.DeserializeObject<List<AccSaberBasicDifficulty>>(dataStr!);

            if (diffData is null)
            {
                Plugin.Log.Error("Failed to get basic diffs, the json returned was not able to be deserialized!");
                return [];
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

            return [.. rankedMaps.Values];
        }

        public async Task<List<AccSaberLeaderboardEntry>> LoadAllPlayerScores(CancellationToken ct = default)
        {
            await playerInfo.LoadTask;

            return (await CallAPI_Json<List<AccSaberLeaderboardEntry>>(string.Format(APAPI_SCORES_ALL, playerInfo.PlayerID!), Throttler, ct: ct)) ?? [];
        }

        public async Task<IEnumerable<AccSaberLeaderboardEntry>?> GetLeaderboardScores(string difficulty_id, int page = 0, int count = 10, CancellationToken ct = default)
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

            string? dataStr = await CallAPI_String(string.Format(APAPI_LEADERBOARD_DIFF, difficulty_id, page, count), Throttler, true, ct: ct).ConfigureAwait(false);

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

        public async Task<IEnumerable<AccSaberLeaderboardEntry>?> GetLeaderboardScores(string difficulty_id, string country, int page = 0, int count = 10, CancellationToken ct = default)
        {
            if (scoreInfoCacher.TryGetCachedItem(difficulty_id, out ScoreCache data)) 
            {
                int trueCount = data.Data.Count(CountryFilterMaker(country));
                //int total = data.LeaderboardLengths.TryGetValue(LeaderboardDisplayType.Country, out int leng) ? leng : 0;
                //Plugin.Log.Info($"true count = {trueCount}, blocked = {data.BlockedUserIndexes.Count}, total = {total}");
                if (data.TryGetLength(LeaderboardDisplayType.Country, out int len) && trueCount == len - data.BlockedUserIndexes.Count || page < trueCount / count)
                    return data.Data.Where(CountryFilterMaker(country)).Skip(page * count).Take(count); 
            }

            string? dataStr = await CallAPI_String(string.Format(APAPI_LEADERBOARD_DIFF_COUNTRY, difficulty_id, country, page, count), Throttler, true, ct: ct).ConfigureAwait(false);

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

        private async Task<(List<AccSaberLeaderboardEntry>? newOutp, List<int>? blockedIds)> HandleBlockedPlayers(List<AccSaberLeaderboardEntry> scoreTokens, 
            ScoreCache data, int page, int count,
            Func<int, int, Task<IEnumerable<AccSaberLeaderboardEntry>?>> getExtraScores)
        {
            if (data.BlockedUserIndexes is null)
                return (scoreTokens, []);

            if (data.BlockedUserIndexes.Count > 0)
            {
                IEnumerable<AccSaberLeaderboardEntry> toUnblock = scoreTokens.Where(token => data.BlockedUserIndexes.Contains(token.Rank - 1) && !playerInfo.PlayerBlocked!.Contains(token.PlayerId));
                if (toUnblock.Any())
                {
                    List<int> toUnblockIdx = [.. toUnblock.Select(token => token.Rank - 1)];
                    foreach (int i in toUnblockIdx)
                        data.BlockedUserIndexes.Remove(i);
                }
            }

            int blockedUsers = 0;
            List<int> blockedUserIds = [];

            if (playerInfo.PlayerBlocked?.Count > 0)
            {
                bool addNewEntries = data.BlockedUserIndexes is null || playerInfo.PlayerBlocked.Count != data.BlockedUserIndexes.Count;

                for (int i = scoreTokens.Count - 1; i >= 0; i--)
                    if (playerInfo.PlayerBlocked.Contains(scoreTokens[i].PlayerId))
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

        public async Task<AccSaberPlayer?> GetPlayerInfo(string userId, bool stats, bool statDiff, CancellationToken ct = default)
        {
            AsyncLock.Releaser locker = await PlayerLoadLock.LockAsync();

            using (locker)
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

                outp = await CallAPI_Json<AccSaberPlayer>(string.Format(APAPI_PLAYERID, userId, stats.ToString().ToLower()), Throttler, ct: ct);

                if (outp is null)
                    return null;

                if (statDiff)
                    await outp.LoadStatDiffs;

                playerInfoCacher.CacheItem(outp, userId);

                return outp;
            }
        }

        public async Task<IEnumerable<AccSaberPlayerScore>?> GetPlayerScores(int page, int pageLength, APCategory category = APCategory.Overall, CancellationToken ct = default)
        { // page is zero indexed.
            // The cache should be sorted, so this should not be an issue.
            IEnumerable<AccSaberPlayerScore> filteredCache = serialHandler.PlayerScores;
            if (category == APCategory.Overall || serialHandler.CategoryPlayerScoreLength[(int)category] >= 0)
            {
                if (category != APCategory.Overall)
                    filteredCache = filteredCache.Where(score => score.Category == category);

                if (filteredCache.Count() >= (page + 1) * pageLength)
                    return filteredCache.Skip(page * pageLength).Take(pageLength);
            }

            await playerInfo.LoadTask;

            string url = string.Format(APAPI_SCORES, playerInfo.PlayerID, page, pageLength) + "&sort=weightedAp,desc";
            if (category != APCategory.Overall)
                url += "&categoryId=" + EnumUtils.EnumToReloadedCategory(category);

            AccSaberPagedContent<AccSaberLeaderboardEntry>? response = await CallAPI_Json<AccSaberPagedContent<AccSaberLeaderboardEntry>>(url, Throttler, ct: ct);

            if (response is null || response.Content is null)
                return null;

            IEnumerable<AccSaberPlayerScore> outp = response.Content.Select(entry => new AccSaberPlayerScore(entry));

            if (category == APCategory.Overall) // TODO: At some point, support caching for any context the score returns. As it is, it would take too much time.
            {
                serialHandler.PlayerScores.AddRange(outp);

                if (serialHandler.PlayerScoreLength < 0)
                    serialHandler.PlayerScoreLength = response.TotalElements;
            } else
            {
                if (serialHandler.CategoryPlayerScoreLength[(int)category] < 0)
                    serialHandler.CategoryPlayerScoreLength[(int)category] = response.TotalElements;
            }

            return outp;
        }
        public async Task<IEnumerable<AccSaberDifficulty>?> GetMapsAboveThreshold(string playerId, float apThreshold, APCategory category = APCategory.Overall, CancellationToken ct = default)
        {
            string url = string.Format(APAPI_PLAYLIST_THRESHOLD, playerId, apThreshold);

            if (category != APCategory.Overall)
                url += "&categoryId=" + EnumUtils.EnumToReloadedCategory(category);

            return await CallAPI_Json<List<AccSaberDifficulty>>(url, Throttler, ct: ct);
        }

        internal async Task<(bool friends, bool rivals)> ExposeRelations()
        {
            string? dataStr = await CallAPI_String(string.Format(APAPI_AUTH_GET_SETTINGS, "privacy"), Throttler).ConfigureAwait(false);

            if (string.IsNullOrEmpty(dataStr))
                return (false, false);

            JToken privacySettings = JToken.Parse(dataStr!);
            return (privacySettings["privacy.followingVisibility"]?.ToString().Equals("public") ?? false, privacySettings["privacy.rivalsVisibility"]?.ToString().Equals("public") ?? false);
        }
        private void SetLoginState(LoginState loginState)
        {
            CurrentLoginState = loginState;

            string content = loginState switch
            {
                LoginState.InProgress => "Logging in to AccSaber Reloaded...",
                LoginState.Success => "Logged in to AccSaber Reloaded!",
                LoginState.Failed => "Failed to Log in to AccSaber Reloaded",
                _ => ""
            };

            if (loginState != LoginState.InProgress)
                Plugin.Log.Info(content);

            OnLoginUpdated?.Invoke(loginState, content);
        }

        public async Task<AuthInfo?> Authenticate()
        {
            try
            {
                if (playerInfo.AuthInfo is not null && playerInfo.AuthInfo.ExpirationDate > DateTime.Now)
                    return playerInfo.AuthInfo;

                SetLoginState(LoginState.InProgress);

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

                var (success, content) = await CallAPI(request, Throttler, maxRetries: 1).ConfigureAwait(false);

                if (success)
                {
                    string? dataStr = await content!.ReadAsStringAsync();

                    if (dataStr is null)
                        return null;

                    AuthInfo? outp = JsonConvert.DeserializeObject<AuthInfo>(dataStr);

                    if (outp is not null)
                    {
                        SetAuthForClient(outp);
                        SetLoginState(LoginState.Success);
                        return outp;
                    }
                }
                else
                    SetLoginState(LoginState.Failed);

                return null;
            }
            catch (Exception e)
            {
                Plugin.Log.Error("There was an error doing auth!\n" + e);
                SetLoginState(LoginState.Failed);
                return null;
            }
        }
#if !NEW_VERSION
        public async Task<string> OculusTicket()
        {
            TaskCompletionSource<string> tcs = new();
#pragma warning disable CS8604 // Possible null reference argument.
            Users.GetAccessToken().OnComplete(delegate (Message<string> message) { tcs.TrySetResult(message.IsError ? null : message.Data); });
#pragma warning restore CS8604 // Possible null reference argument.
            return await tcs.Task;
        }
#endif
        internal async Task<bool> SubmitScore(AccSaberScore score)
        {
            if (!SubmissionPatch.Submit)
                return false;

            score.Nonce = MiscUtils.GenerateNonce(64); // Regenerate this just to make sure no one can steal the nonce.

            HttpRequestMessage request = new(HttpMethod.Post, APAPI_SCORE_SUBMIT)
            {
                Content = new StringContent(JsonConvert.SerializeObject(score), System.Text.Encoding.UTF8, "application/json")
            };

            var (success, _) = await CallAPI(request, null, maxRetries: 1).ConfigureAwait(false); // No Throttler because this should throw an error if it is called more than once a minute.

            Plugin.Log.Info(success ? "Score submitted!" : "Score failed to submit.");

            return success;

            //Note: Currently this will submit on party mode and probably multiplayer, which will need to be fixed
        }

        #endregion
        #region Misc structs

        private struct ScoreCache
        {
            public List<AccSaberLeaderboardEntry> Data;
            public HashSet<string> UserIds;
            public List<int> BlockedUserIndexes;
            public readonly int[] LeaderboardLengths;

            public readonly ref int GetLength(LeaderboardDisplayType displayType) => ref LeaderboardLengths[BitOperations.Log2((uint)displayType)];
            public readonly bool ContainsLength(LeaderboardDisplayType displayType) => GetLength(displayType) > -1;
            public readonly bool TryGetLength(LeaderboardDisplayType displayType, out int length)
            {
                length = GetLength(displayType);
                return length > -1;
            }

            public ScoreCache(List<AccSaberLeaderboardEntry> data, HashSet<string> userIds, List<int> blockedUserIndexes, params IEnumerable<(LeaderboardDisplayType displayType, int length)> lengths)
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

        internal sealed class AuthInfo
        {
            [JsonProperty("accessToken")]
            public string AccessToken { get; set; } = null!;

            [JsonProperty("refreshToken")]
            public string RefreshToken { get; set; } = null!;

            [JsonProperty("expiresIn")]
            public long ExpiresIn { get; set; }

            [JsonIgnore]
            public TimeSpan ValidLength => TimeSpan.FromSeconds(ExpiresIn);

            [JsonIgnore]
            public DateTime ExpirationDate { get; set; }

            [JsonProperty("userId")]
            public string UserId { get; set; } = null!;

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