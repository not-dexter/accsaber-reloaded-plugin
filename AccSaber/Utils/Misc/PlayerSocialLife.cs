using AccSaber.API;
using IPA.Utilities.Async;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zenject;
using static AccSaber.API.AccsaberAPI;

namespace AccSaber.Utils.Misc
{
    public sealed class PlayerSocialLife : IInitializable//, IDisposable
    {
        [Inject] private readonly AccsaberAPI api = null!;

        public event Action? OnRelationChanged;

        private readonly AsyncLock loadLock = new();
        private readonly object loadLockObj = new();
        private readonly AsyncLock changeRelationLock = new();
        private Task? loadTask = null;
        public Task LoadTask => LoadInfo();
        private bool allowLoad = true;

        private HashSet<string>? PlayerFollowed = null;
        private HashSet<string>? PlayerRivals = null;
        private HashSet<string>? PlayerRelations = null;

        internal HashSet<string>? PlayerBlocked = null; // never expose this above internal

        private Dictionary<RelationType, Dictionary<string, Guid>> UserIdToRelationId = [];

        private bool exposeFollowed = false, exposeRivals = false;
        internal AuthInfo? AuthInfo { get; private set; }

        public string? PlayerID { get; private set; } = null;
        public IReadOnlyCollection<string>? PlayerRivalIDs => exposeRivals ? PlayerRivals : null;
        internal IReadOnlyCollection<string>? PlayerRivalIDs_Internal => PlayerRivals;
        public IReadOnlyCollection<string>? PlayerFollowedIDs => exposeFollowed ? PlayerFollowed : null;
        internal IReadOnlyCollection<string>? PlayerFollowedIDs_Internal => PlayerFollowed;
        public IReadOnlyCollection<string>? PlayerRelationIDs => PlayerRelations;

        public IReadOnlyCollection<string>? GetIds(LeaderboardDisplayType displayType) => displayType switch
        {
            LeaderboardDisplayType.Rivals => PlayerRivalIDs,
            LeaderboardDisplayType.Followed => PlayerFollowedIDs,
            LeaderboardDisplayType.Relations => PlayerRelationIDs,
            _ => null
        };
        public IReadOnlyCollection<string>? GetIds(RelationType relationType) => GetIds(relationType.Convert());
        internal HashSet<string>? GetIds_Internal(LeaderboardDisplayType displayType) => displayType switch
        {
            LeaderboardDisplayType.Rivals => PlayerRivals,
            LeaderboardDisplayType.Followed => PlayerFollowed,
            LeaderboardDisplayType.Relations => PlayerRelations,
            LeaderboardDisplayType.Blocked => PlayerBlocked,
            _ => null
        };
        internal async Task<bool> AddId(string id, LeaderboardDisplayType displayType)
        {
            AsyncLock.Releaser locker = await changeRelationLock.LockAsync();

            using (locker)
            {
                HashSet<string>? set = GetIds_Internal(displayType);

                if (set is null)
                    return false;

                set.Add(id);
                if (displayType != LeaderboardDisplayType.Relations)
                    PlayerRelations!.Add(id);

                RelationType rt = displayType.Convert();

                var (success, relationId) = await api.AddPlayerRelation(rt, id);

                if (success && UserIdToRelationId.ContainsKey(rt))
                    UserIdToRelationId[rt].Add(id, relationId!.Value);

                OnRelationChanged?.Invoke();

                return success;
            }
        }
        internal async Task<bool> RemoveId(string id, LeaderboardDisplayType displayType)
        {
            AsyncLock.Releaser locker = await changeRelationLock.LockAsync();

            using (locker)
            {
                HashSet<string>? set = GetIds_Internal(displayType);

                if (set is null)
                    return false;

                set.Remove(id);
                if (displayType != LeaderboardDisplayType.Relations)
                    PlayerRelations!.Remove(id);

                RelationType rt = displayType.Convert();

                if (!UserIdToRelationId[rt].TryGetValue(id, out Guid relationId))
                    return false;

                bool success = await api.RemovePlayerRelation(relationId);

                if (success)
                    UserIdToRelationId[rt].Remove(id);

                OnRelationChanged?.Invoke();

                return success;
            }
        }
        public async Task LoadInfo()
        {
            if (loadTask is null)
                lock (loadLockObj)
                    if (loadTask is null) // check this again after locking, in case it was set while waiting for the lock
                        Monitor.Wait(loadLockObj);

            AsyncLock.Releaser locker = await loadLock.LockAsync();
            using (locker)
                await loadTask!;
        }
        private async Task LoadInfoTask(int retries = 3)
        {
            try
            {
                AuthInfo = await api.Authenticate();

                if (AuthInfo is not null)
                {
                    (exposeFollowed, exposeRivals) = await api.ExposeRelations();
                    allowLoad = false;

                    await SetRelations(AuthInfo.UserId);
                }
                else 
                    (exposeFollowed, exposeRivals) = (false, false);
            } 
            catch (Exception e)
            {
                Plugin.Log.Error("There was an error loading player info!" + (retries > 0 ? " Retrying in 1 second." : ""));
                Plugin.Log.Debug(e);

                if (retries == 0)
                {
                    Plugin.Log.Error("Failed to load player info after 3 retries.");
                    _ = UnityMainThreadTaskScheduler.Factory.StartNew(async () =>
                    {
                        allowLoad = true;

                        await SetLoadInfoTask();
                    });
                    return;
                }

                await Task.Delay(1000);
                await LoadInfoTask(retries - 1);
            }
        }
        private async Task SetRelations(string mainUserId)
        {
            var relations = await api.GetPlayerRelations();

            UserIdToRelationId = [with(relations.Select(toConvert =>
                new KeyValuePair<RelationType, Dictionary<string, Guid>>(toConvert.Key, toConvert.Value.relations)))];

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
            _ = SetLoadInfoTask();
        }
        public async Task SetLoadInfoTask()
        {
            AsyncLock.Releaser locker = await loadLock.LockAsync();
            using (locker)
            {
                if (!allowLoad || (loadTask is not null && !loadTask.IsCompleted))
                    return;
                loadTask = LoadInfoTask();
                lock (loadLockObj)
                    Monitor.PulseAll(loadLockObj);
            }
        }
        /*public void Dispose() // This is currently unused, as it just returns a 500.
        {
            if (AuthInfo is null)
                return;

            StringContent sc = new($"\"refreshToken\": \"{AuthInfo.RefreshToken}\"", System.Text.Encoding.UTF8, "application/json");

            HttpRequestMessage request = new(HttpMethod.Post, HelpfulPaths.APAPI_AUTH_END) { Content = sc };

            APIHandler.CallAPI(request, throttler, maxRetries: 1).GetAwaiter().GetResult();
        }*/
    }
}
