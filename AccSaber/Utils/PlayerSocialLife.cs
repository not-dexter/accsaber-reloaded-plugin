using AccSaber.API;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Zenject;
using static AccSaber.API.AccsaberAPI;

namespace AccSaber.Utils
{
    public sealed class PlayerSocialLife : IInitializable, IDisposable
    {
        public static event Action? OnRelationChanged;

        private static readonly AsyncLock loadLock = new();
        private static Task? loadTask = null;
        public static Task LoadTask => LoadInfo();

        private static HashSet<string>? PlayerFollowed = null;
        private static HashSet<string>? PlayerRivals = null;
        private static HashSet<string>? PlayerRelations = null;

        internal static HashSet<string>? PlayerBlocked = null; // never expose this above internal

        private static Dictionary<RelationType, Dictionary<string, string>> UserIdToRelationId = [];

        private static bool exposeFollowed = false, exposeRivals = false;
        internal static AuthInfo? AuthInfo { get; private set; }

        public static string? PlayerID { get; private set; } = null;
        public static IReadOnlyCollection<string>? PlayerRivalIDs => exposeRivals ? PlayerRivals : null;
        internal static IReadOnlyCollection<string>? PlayerRivalIDs_Internal => PlayerRivals;
        public static IReadOnlyCollection<string>? PlayerFollowedIDs => exposeFollowed ? PlayerFollowed : null;
        internal static IReadOnlyCollection<string>? PlayerFollowedIDs_Internal => PlayerFollowed;
        public static IReadOnlyCollection<string>? PlayerRelationIDs => PlayerRelations;

        public static IReadOnlyCollection<string>? GetIds(LeaderboardDisplayType displayType) => displayType switch
        {
            LeaderboardDisplayType.Rivals => PlayerRivalIDs,
            LeaderboardDisplayType.Followed => PlayerFollowedIDs,
            LeaderboardDisplayType.Relations => PlayerRelationIDs,
            _ => null
        };
        public static IReadOnlyCollection<string>? GetIds(RelationType relationType) => GetIds(relationType.Convert());
        internal static HashSet<string>? GetIds_Internal(LeaderboardDisplayType displayType) => displayType switch
        {
            LeaderboardDisplayType.Rivals => PlayerRivals,
            LeaderboardDisplayType.Followed => PlayerFollowed,
            LeaderboardDisplayType.Relations => PlayerRelations,
            LeaderboardDisplayType.Blocked => PlayerBlocked,
            _ => null
        };
        internal static async Task<bool> AddId(string id, LeaderboardDisplayType displayType)
        {
            HashSet<string>? set = GetIds_Internal(displayType);

            if (set is null)
                return false;

            set.Add(id);
            if (displayType != LeaderboardDisplayType.Relations)
                PlayerRelations!.Add(id);

            RelationType rt = displayType.Convert();

            var (success, relationId) = await AddPlayerRelation(rt, id);

            if (success && UserIdToRelationId.ContainsKey(rt))
                UserIdToRelationId[rt].Add(id, relationId!);

            OnRelationChanged?.Invoke();

            return success;
        }
        internal static async Task<bool> RemoveId(string id, LeaderboardDisplayType displayType)
        {
            HashSet<string>? set = GetIds_Internal(displayType);

            if (set is null)
                return false;

            set.Remove(id);
            if (displayType != LeaderboardDisplayType.Relations)
                PlayerRelations!.Remove(id);

            if (!UserIdToRelationId[displayType.Convert()].TryGetValue(id, out string relationId))
                return false;

            bool success = await RemovePlayerRelation(relationId);

            OnRelationChanged?.Invoke();

            return success;
        }
        public static async Task LoadInfo()
        {
            if (loadTask is not null)
            {
                await loadTask;
                return;
            }
            lock (loadLock)
                Monitor.Wait(loadLock);
            await loadTask!;
        }
        private static async Task LoadInfoTask(int retries = 3)
        {
            try
            {
                AuthInfo = await Authenticate();

                (exposeFollowed, exposeRivals) = await ExposeRelations();

                if (AuthInfo is not null)
                    await SetRelations(AuthInfo.UserId);

                Plugin.Log.Info("Logged into accsaber!");
            } catch (Exception e)
            {
                Plugin.Log.Error("There was an error loading player info!" + (retries > 0 ? " Retrying in 1 second." : ""));
                Plugin.Log.Debug(e);
                if (retries == 0)
                    return;
                await Task.Delay(1000);
                await LoadInfoTask(retries - 1);
            }
        }
        private static async Task SetRelations(string mainUserId)
        {
            var relations = await GetPlayerRelations();

            UserIdToRelationId = new(relations.Select(toConvert =>
                new KeyValuePair<RelationType, Dictionary<string, string>>(toConvert.Key, toConvert.Value.relations)));

            HashSet<string> followed = relations[RelationType.follower].userIds;
            HashSet<string> rivals = relations[RelationType.rival].userIds;
            HashSet<string> blocked = relations[RelationType.blocked].userIds;
            HashSet<string> playerRelations = [.. followed, .. rivals];

            followed.Add(mainUserId);
            rivals.Add(mainUserId);
            playerRelations.Add(mainUserId);

            PlayerFollowed = followed;
            PlayerRivals = rivals;
            PlayerBlocked = blocked;
            PlayerRelations = playerRelations;
            PlayerID = mainUserId;

            OnRelationChanged?.Invoke();
        }

        public void Initialize()
        {
            if (loadTask is not null)
                return;
            loadTask = LoadInfoTask();
            lock (loadLock)
                Monitor.PulseAll(loadLock);
        }
        public void Dispose() // This is currently unused, as it just returns a 500.
        {
            if (AuthInfo is null)
                return;

            StringContent sc = new($"\"refreshToken\": \"{AuthInfo.RefreshToken}\"", System.Text.Encoding.UTF8, "application/json");

            HttpRequestMessage request = new(HttpMethod.Post, HelpfulPaths.APAPI_AUTH_END) { Content = sc };

            APIHandler.CallAPI(request, throttler, maxRetries: 1).GetAwaiter().GetResult();
        }
    }
}
