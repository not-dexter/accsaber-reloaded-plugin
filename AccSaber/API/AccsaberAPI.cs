using Accsaber.Utils;
using AccSaber.Managers;
using AccSaber.Models;
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

namespace AccSaber.API
{
#nullable enable
    internal static class AccsaberAPI
    {
        public static readonly Throttler throttler = new(400, 60);
        public static readonly Func<string, Func<AccSaberLeaderboardEntry, bool>> CountryFilterMaker = country => token => token.Country.Equals(country);

        private static readonly ObjectCacher<AccSaberUser> playerInfoCacher = new();
        private static readonly ObjectCacher<ScoreCache> scoreInfoCacher = new();

        private static readonly Dictionary<string, AccSaberRankedMap> mapCache = [];

        public const int PAGE_LENGTH = 10;
        public const int FILTER_PAGE_MULT = 10;

        static AccsaberAPI()
        {
            AccSaberStore.OnScoreUpdated += token =>
            {
                string playerId = token.PlayerId, diffId = token.DifficultyId;

                playerInfoCacher.RemoveItem(playerId);

                if (scoreInfoCacher.TryGetCachedItem(diffId, out var item) && item.UserIds.Contains(playerId))
                {
                    Plugin.Log.Notice($"Difficulty id {diffId} was removed from cache.");
                    scoreInfoCacher.RemoveItem(diffId);
                }
            };
        }
        
        /*#region Diff Info Getters
        public static float GetComplexity(DifficultyInfoToken diffData) => (float)(diffData["complexity"] ?? 0f);
        public static string GetSongName(DifficultyInfoToken diffData) => diffData["songName"]!.ToString();
        public static string GetDiffName(DifficultyInfoToken diffData) => diffData["difficulty"]!.ToString();
        public static string GetLeaderboardId(DifficultyInfoToken diffData) => diffData["leaderboardId"]!.ToString();
        public static string GetDifficultyId(DifficultyInfoToken diffData) => diffData["id"]!.ToString();
        public static string GetHash(DifficultyInfoToken diffData) => diffData["songHash"]!.ToString();
        public static bool MapIsUsable(DifficultyInfoToken diffData) => diffData is not null && diffData.Complexity > 0;
        public static bool AreRatingsNull(DifficultyInfoToken diffData) => diffData["complexity"] is null;
        public static int GetMaxScore(DifficultyInfoToken diffData) => (int)(diffData["maxScore"] ?? 0);
        public static string GetCategoryId(DifficultyInfoToken diffData) => diffData["categoryId"]!.ToString();

#endregion
        #region Player Info Getters

        public static string GetPlayerAvatar(PlayerInfoToken playerData) => playerData["avatarUrl"]!.ToString();
        public static LevelInfoToken GetPlayerLevelData(PlayerInfoToken playerData) => new((JObject)playerData["levelData"]!);
        public static string GetPlayerName(PlayerInfoToken playerData) => playerData["name"]!.ToString();
        public static string GetPlayerId(PlayerInfoToken playerData) => playerData["id"]!.ToString();
        public static bool CheckPlayerForStats(PlayerInfoToken playerData) => playerData["statistics"] is not null;
        public static StatsInfoToken? GetPlayerStats(PlayerInfoToken playerData, AccSaberStore.APCategory category)
        {
            string? id = CategoryIdToReloadedCategory(category.ToString());
            return playerData["statistics"]?.Children().FirstOrDefault(token => id.Equals(token["categoryId"]?.ToString())) is not JObject obj ? null : new(obj);
        }

        #endregion
        #region Level Info Getters

        public static int GetLevel(LevelInfoToken levelData) => (int)levelData["level"]!;
        public static string GetTitle(LevelInfoToken levelData) => levelData["title"]!.ToString();
        public static float GetCurrentLevelXp(LevelInfoToken levelData) => (float)levelData["xpForCurrentLevel"]!;
        public static float GetNextLevelXp(LevelInfoToken levelData) => (float)levelData["xpForNextLevel"]!;
        public static float GetProgress(LevelInfoToken levelData) => (float)levelData["progressPercent"]!;

        #endregion
        #region Stat Info Getters

        public static float GetAP(StatsInfoToken statsData) => (float)statsData["ap"]!;
        public static int GetGlobalRank(StatsInfoToken statsData) => (int)statsData["ranking"]!;
        public static int GetCountryRank(StatsInfoToken statsData) => (int)statsData["countryRanking"]!;

        #endregion
        #region Milestone Info Getters

        public static float GetProgress(MilestoneInfoToken milestoneData) => (float)milestoneData["normalizedProgress"]!;
        public static float GetCalculatedProgress(MilestoneInfoToken milestoneData) => 
            AccsaberMilestoneData.AccsaberMilestoneDataInfo.CalcProgress(milestoneData.Target, milestoneData.ProgressValue);
        public static float GetTarget(MilestoneInfoToken milestoneData) => (float)milestoneData["targetValue"]!;
        public static float GetProgressValue(MilestoneInfoToken milestoneData) => (float)(milestoneData["progress"] ?? 0f);
        public static string GetTier(MilestoneInfoToken milestoneData) => milestoneData["tier"]!.ToString();
        public static string GetTitle(MilestoneInfoToken milestoneData) => milestoneData["title"]!.ToString();
        public static string GetDescription(MilestoneInfoToken milestoneData) => milestoneData["description"]!.ToString();
        public static string GetId(MilestoneInfoToken milestoneData) => milestoneData["milestoneId"]!.ToString();
        public static AccsaberMilestoneData WrapData(MilestoneInfoToken milestoneData) => new(milestoneData.Target, milestoneData.ProgressValue,
            milestoneData.Tier, milestoneData.Title, milestoneData.Description, milestoneData.Id);

        #endregion*/
        #region Sync Functions
        public static int GetLength(string diffId, LeaderboardDisplayType displayType)
        {
            if (scoreInfoCacher.TryGetCachedItem(diffId, out ScoreCache info) && info.TryGetLength(displayType, out int len))
                return len;

            return -1;
        }
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
        public static bool ScoreDataCached(string diffId, int page, string country)
        { // page is one indexed
            if (!scoreInfoCacher.TryGetCachedItem(diffId, out ScoreCache info))
                return false;

            int count = info.Data.Count(CountryFilterMaker(country));

            return count >= page * PAGE_LENGTH || (info.TryGetLength(LeaderboardDisplayType.Country, out int len) && count == len - info.BlockedUserIndexes.Count);
        }
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
        private static List<T> MergeListWithEnumerable<T>(List<T> left, IEnumerable<T> right) where T : IComparable
        {
            return MergeListWithEnumerable(left, right, a => a);
        }
        private static List<T> MergeListWithEnumerable<T>(List<T> left, IEnumerable<T> right, Func<T, IComparable> converter)
        {
            List<T> outp = new(left.Count + right.Count());
            IEnumerator<T>? rightEnum = right.GetEnumerator();

            rightEnum.MoveNext();
            int i = 0;
            while (i < left.Count)
            {
                if (converter(left[i]).CompareTo(converter(rightEnum.Current)) < 0)
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

        public static void InvalidateCache() => scoreInfoCacher.ClearCache();
        public static void InvalidateCache(string diffId) => scoreInfoCacher.RemoveItem(diffId);
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

        public static async Task<int> GetLength(string hash, BeatmapDifficulty diff, LeaderboardDisplayType displayType = LeaderboardDisplayType.Global)
        {
            string? diffId = await GetLeaderboardDifficultyId(hash, diff);

            if (diffId is null)
                return -1;

            return GetLength(diffId, displayType);
        }
        public static async Task<int> GetLength(AccSaberBasicDifficulty diff, LeaderboardDisplayType displayType = LeaderboardDisplayType.Global) =>
            await GetLength(diff.Hash, diff.Difficulty, displayType);
        public static async Task<AccSaberLeaderboardEntry[]?> GetScoreData(int page, string hash, BeatmapDifficulty diff, string? country = null)
        { // page is zero indexed.
            string? diffId = await GetLeaderboardDifficultyId(hash, diff);
            if (diffId is null) return null;
            return await GetScoreData(page, diffId, country);
        }
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
        public static async Task<AccSaberLeaderboardEntry[]?> GetScoreData(int page, string diffId, RelationType relation)
        { // page is one indexed
            try
            {
                --page;
                if (scoreInfoCacher.TryGetCachedItem(diffId, out ScoreCache cache))
                {
                    HashSet<string> relations = PlayerSocialLife.GetIds_Internal(relation.Convert())!;
                    IEnumerable<AccSaberLeaderboardEntry> tokens = cache.Data.Where(token => relations.Contains(token.PlayerId));

                    int tokenCount = tokens.Count();
                    int pageCount = page * PAGE_LENGTH;

                    if (!cache.TryGetLength(relation.Convert(), out int scoreCount))
                        scoreCount = -1;

                    //Plugin.Log.Info($"token count = {tokenCount} || score count = {scoreCount} || page count = {pageCount}");

                    if (scoreCount == 0)
                        return [];

                    if (tokenCount == scoreCount || tokenCount > pageCount && (scoreCount < pageCount + PAGE_LENGTH || tokenCount >= pageCount + PAGE_LENGTH))
                        return [.. tokens.Skip(pageCount).Take(PAGE_LENGTH)];

                }

                string? dataStr = await CallAPI_String(string.Format(APAPI_LEADERBOARD_DIFF_RELATION, diffId, relation.ToString(), page, PAGE_LENGTH));

                if (string.IsNullOrEmpty(dataStr))
                    return null;

                AccSaberPagedContent<AccSaberLeaderboardEntry>? data = JsonConvert.DeserializeObject<AccSaberPagedContent<AccSaberLeaderboardEntry>>(dataStr!);

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
        public static async Task<List<AccSaberMilestone>?> GetMilestoneData(string userId, Func<AccSaberMilestone, bool>? filter = null, Comparison<AccSaberMilestone>? sorter = null, int pageMult = FILTER_PAGE_MULT)
        {
            int page = 0;
            List<AccSaberMilestone> outp = [];
            int pageLen = PAGE_LENGTH * pageMult;
            while (true)
            {
                string? dataStr = await CallAPI_String(string.Format(APAPI_MILESTONE, userId, page, pageLen)).ConfigureAwait(false);

                if (string.IsNullOrEmpty(dataStr)) 
                    return null;

                AccSaberPagedContent<AccSaberMilestone>? response = JsonConvert.DeserializeObject<AccSaberPagedContent<AccSaberMilestone>>(dataStr!);

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
        public static async Task<List<AccSaberMilestone>?> GetMilestoneData(string userId, bool completed, Func<AccSaberMilestone, bool>? filter = null, Comparison<AccSaberMilestone>? sorter = null)
        {
            string apapiFormat = completed ? APAPI_MILESTONE_COMPLETE : APAPI_MILESTONE_INCOMPLETE;

            string? dataStr = await CallAPI_String(string.Format(apapiFormat, userId)).ConfigureAwait(false);

            if (string.IsNullOrEmpty(dataStr))
                return null;

            AccSaberPagedContent<AccSaberMilestone>? response = JsonConvert.DeserializeObject<AccSaberPagedContent<AccSaberMilestone>>(dataStr!);

            if (response is null || response.Content is null)
                return null;

            List<AccSaberMilestone> data = response.Content;

            if (filter is not null)
                data = [.. data.Where(filter)];

            if (sorter is not null)
                data.Sort(sorter);

            return data;
        }
        public static async Task<Dictionary<RelationType, (HashSet<string> userIds, Dictionary<string, string> relations)>> GetPlayerRelations()
        {
            const int pageLength = PAGE_LENGTH * 10;
            int page = 0, callsLeft = -1;
            Dictionary<RelationType, (HashSet<string> userIds, Dictionary<string, string> relations)> outp = [];

            foreach (RelationType rt in Enum.GetValues(typeof(RelationType)))
                outp[rt] = ([], []);

            do
            {
                string? dataStr = await CallAPI_String(string.Format(APAPI_AUTH_GET_RELATIONS_ALL, page, pageLength));

                if (string.IsNullOrEmpty(dataStr))
                    break;

                AccSaberPagedContent<AccSaberRelation>? response = JsonConvert.DeserializeObject<AccSaberPagedContent<AccSaberRelation>>(dataStr!);

                if (response is null || response.Content is null)
                    break;

                if (callsLeft == -1)
                    callsLeft = response.TotalElements / pageLength;

                foreach (AccSaberRelation token in response.Content)
                {
                    RelationType rt = token.Relation;
                    string userId = token.TargetPlayerId, relationId = token.ID;

                    outp[rt].userIds.Add(userId);
                    outp[rt].relations.Add(userId, relationId);
                }

            } while (callsLeft-- > 0);
            return outp;
        }
        public static async Task<(HashSet<string> ids, IEnumerable<(string userId, string relationId)> relations)> GetPlayerRelations(RelationType relation, string playerId)
        {
            const int pageLength = PAGE_LENGTH * 10;
            int page = 0, callsLeft = 0;
            HashSet<string> userIds = [];
            List<(string, string)> relations = [];
            do
            {
                string? dataStr = await CallAPI_String(string.Format(APAPI_RELATIONS, playerId, relation.ToString(), "outgoing", page, pageLength));
                if (string.IsNullOrEmpty(dataStr))
                    break;
                JToken response = JToken.Parse(dataStr!);

                if (callsLeft == 0)
                    callsLeft = (int)response["totalElements"]! / pageLength;

                IEnumerable<(string userId, string relationId)> ids = response["content"]!.Children().Select(token => (token["targetUserId"]!.ToString(), token["id"]!.ToString()));
                foreach (var (userId, _) in ids)
                    userIds.Add(userId);
                relations.AddRange(ids);

            } while (callsLeft > 0);
            return (userIds, relations);
        }
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
        public static async Task<bool> RemovePlayerRelation(string relationId)
        {
            HttpRequestMessage request = new(HttpMethod.Delete, string.Format(APAPI_AUTH_DELETE_RELATION, relationId));

            return (await CallAPI(request, throttler, maxRetries: 1).ConfigureAwait(false)).Success;
        }
        public static async Task<AccSaberLeaderboardEntry?> GetScoreData(string userId, string hash, BeatmapDifficulty diff, CancellationToken ct = default)
        {
            if (mapCache.TryGetValue(hash, out AccSaberRankedMap map))
            {
                AccSaberDifficulty? selectedDiff = map.Difficulties?.FirstOrDefault(currentDiff => currentDiff.Difficulty == diff);
                if (selectedDiff is not null && scoreInfoCacher.TryGetCachedItem(selectedDiff.DifficultyId, out ScoreCache val) && val.UserIds.Contains(userId))
                    return val.Data.First(token => token.PlayerId.Equals(userId));
            }
            string reloadedDiff = EnumUtils.DiffToReloadedDiff(diff);

            string? dataStr = await CallAPI_String(string.Format(APAPI_SCORE, userId, hash.ToLower(), reloadedDiff), throttler, true, ct: ct).ConfigureAwait(false);
            if (string.IsNullOrEmpty(dataStr)) 
                return null;

            return JsonConvert.DeserializeObject<AccSaberLeaderboardEntry>(dataStr!);
        }
        public static async Task<AccSaberRankedMap?> GetLeaderboard(string hash, CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested) 
                return null;

            if (mapCache.TryGetValue(hash, out AccSaberRankedMap? map))
                return map;

            try
            {
                string? dataStr = await CallAPI_String(string.Format(APAPI_HASH, hash), throttler, true, ct: ct).ConfigureAwait(false);

                if (string.IsNullOrEmpty(dataStr)) 
                    return null;

                map = JsonConvert.DeserializeObject<AccSaberRankedMap>(dataStr!);

                if (map is not null)
                    mapCache[hash] = map;

                return map;
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Issue URL: {string.Format(APAPI_HASH, hash)}");
                Plugin.Log.Error("There was an error getting map information: " + ex);
                return null;
            }
        }
        public static async Task<AccSaberDifficulty?> GetLeaderboard(string hash, BeatmapDifficulty diff, CancellationToken ct = default)
        {
            AccSaberRankedMap? map = await GetLeaderboard(hash, ct);

            if (map is null) 
                return null;

            return map.Difficulties.FirstOrDefault(difficulty => difficulty.Difficulty == diff);
        }
        public static async Task<string?> GetLeaderboardDifficultyId(string hash, BeatmapDifficulty diff, CancellationToken ct = default) =>
            (await GetLeaderboard(hash, diff, ct))?.DifficultyId;
        public static async Task<string?> GetLeaderboardDifficultyId(AccSaberBasicDifficulty diff, CancellationToken ct = default) =>
            await GetLeaderboardDifficultyId(diff.Hash, diff.Difficulty, ct);
        public static async Task<Dictionary<string, AccSaberBasicDifficulty[]>> GetAllBasicDiffs(CancellationToken ct = default)
        {
            string? dataStr = await CallAPI_String(APAPI_DIFFS, throttler, ct: ct);

            if (string.IsNullOrEmpty(dataStr))
            {
                Plugin.Log.Error("Failed to get basic diffs, the json returned was null!");
                return [];
            }

            AccSaberPagedContent<AccSaberBasicDifficulty>? diffData = JsonConvert.DeserializeObject<AccSaberPagedContent<AccSaberBasicDifficulty>>(dataStr!);

            if (diffData is null || diffData.Content is null)
            {
                Plugin.Log.Error("Failed to get basic diffs, the json returned was not able to be deserialized!");
                return [];
            }

            Dictionary<string, List<AccSaberBasicDifficulty>> rankedMaps = [];

            foreach (AccSaberBasicDifficulty diff in diffData.Content)
            {
                if (rankedMaps.ContainsKey(diff.Hash))
                    rankedMaps[diff.Hash].Add(diff);
                else
                    rankedMaps[diff.Hash] = [diff];
            }

            return new(rankedMaps.Select(pair => new KeyValuePair<string, AccSaberBasicDifficulty[]>(pair.Key, [.. pair.Value])));
        }
        public static async Task LoadAllMaps(CancellationToken ct = default)
        {
            string? dataStr = await CallAPI_String(string.Format(APAPI_MAPS, 0, 1), throttler, ct: ct);

            if (string.IsNullOrEmpty(dataStr)) 
                return;

            AccSaberPagedContent? pageInfo = JsonConvert.DeserializeObject<AccSaberPagedContent>(dataStr!);

            if (pageInfo is null)
                return;

            int totalMaps = pageInfo.TotalElements;

            dataStr = await CallAPI_String(string.Format(APAPI_MAPS, 0, totalMaps), throttler, ct: ct);

            if (string.IsNullOrEmpty(dataStr)) 
                return;

            AccSaberPagedContent<AccSaberRankedMap>? maps = JsonConvert.DeserializeObject<AccSaberPagedContent<AccSaberRankedMap>>(dataStr!);

            if (maps is null || maps.Content is null)
                return;

            foreach (AccSaberRankedMap map in maps.Content)
                mapCache.TryAdd(map.Hash, map);
        }
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
        public static async Task<AccSaberUser?> GetPlayerInfo(string userId, bool stats, CancellationToken ct = default)
        {
            if (playerInfoCacher.TryGetCachedItem(userId, out AccSaberUser? outp) && (!stats || outp!.Statistics is not null))
                return outp;

            string? dataStr = await CallAPI_String(string.Format(APAPI_PLAYERID, userId, stats.ToString().ToLower()), throttler, false, ct: ct).ConfigureAwait(false);

            if (string.IsNullOrEmpty(dataStr)) 
                return null;

            outp = JsonConvert.DeserializeObject<AccSaberUser>(dataStr!);

            if (outp is null)
                return null;

            if (stats)
                await outp.LoadStatDiffs;

            playerInfoCacher.CacheItem(outp, userId);

            return outp;
        }

        internal static async Task<(bool friends, bool rivals)> ExposeRelations()
        {
            string? dataStr = await CallAPI_String(string.Format(APAPI_AUTH_GET_SETTINGS, "privacy"), throttler).ConfigureAwait(false);

            if (string.IsNullOrEmpty(dataStr))
                return (false, false);

            JToken privacySettings = JToken.Parse(dataStr!);
            return (privacySettings["privacy.followingVisibility"]?.ToString().Equals("public") ?? false, privacySettings["privacy.rivalsVisibility"]?.ToString().Equals("public") ?? false);
        }
        public static async Task<AuthInfo?> Authenticate()
        {
            if (PlayerSocialLife.AuthInfo is not null && PlayerSocialLife.AuthInfo.ExpirationDate > DateTime.Now)
                return PlayerSocialLife.AuthInfo;

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
                    token = authToken.token + "," + platformUserModel.RequestXPlatformAccessToken(CancellationToken.None).GetAwaiter().GetResult().token;
#else
                    token = GetOculusToken();
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
        private static string GetOculusToken()
        {

            string token = "";
            Users.GetLoggedInUser().OnComplete(delegate (Message<Oculus.Platform.Models.User> loggedInMessage) {
                if (!loggedInMessage.IsError)
                {
                    Users.GetUserProof().OnComplete(delegate (Message<Oculus.Platform.Models.UserProof> userProofMessage) {
                        if (!userProofMessage.IsError)
                        {
                            Users.GetAccessToken().OnComplete(delegate (Message<string> authTokenMessage)
                            {
                                token = userProofMessage.Data.Value + "," + authTokenMessage.Data;
                            });

                        }
                    });
                }
            });
            return token;
        }
#endif

        #endregion
        #region Misc structs

        private struct ScoreCache
        {
            public List<AccSaberLeaderboardEntry> Data;
            public HashSet<string> UserIds;
            public List<int> BlockedUserIndexes;
            public readonly int[] LeaderboardLengths;

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
