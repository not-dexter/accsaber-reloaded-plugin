using AccSaber.Models;
using AccSaber.Models.CacheModels;
using AccSaber.Utils.Misc;
using HarmonyLib;
using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Zenject;

namespace AccSaber.Utils
{
    internal class PlaylistUtils : IInitializable, IDisposable
    {
#pragma warning disable IDE0051
        public const string PlaylistAuthor = "Accsaber Reloaded";
        public const string CustomDataKey = "customSync";

        [Inject] private readonly LevelPackDetailViewController lpdvc = null!;
        [Inject] private readonly LevelUtils levelUtils = null!;
        [Inject] private readonly SerializationHandler serialHandler = null!;

        [BeatSaberMarkupLanguage.Attributes.UIComponent("customSyncButton")] private readonly UnityEngine.UI.Button customSyncButton = null!;

        private bool _init = false;

        private Assembly? playlistManager = null, playlistLib = null;
        private static object? pdvbcInstance = null;

        private Delegate? handler;

        private object? currentManagerInstance;
        private object? currentPlaylist;
        private string? currentCustomData;

        public void Initialize()
        {
            if (_init)
                return;

            _init = true;

            bool playlistManagerExists = false;
            bool playlistLibExists = false;

            foreach (Assembly assemb in AppDomain.CurrentDomain.GetAssemblies()) 
            {
                string assembName = assemb.GetName().Name;

                if (!playlistManagerExists && assembName.Equals("PlaylistManager"))
                {
                    playlistManagerExists = true;
                    playlistManager = assemb;
                }
                else if (!playlistLibExists && assembName.Equals("BeatSaberPlaylistsLib"))
                {
                    playlistLibExists = true;
                    playlistLib = assemb;
                }
                else continue;

                if (playlistManagerExists && playlistLibExists)
                    break;
            }

            if (playlistManager is null)
                return;

            try
            {
                const string bsmlContent = "<icon-button id='customSyncButton' icon='PlaylistManager.Icons.Sync.png' anchor-pos-x='51' anchor-pos-y='-3' hover-hint='Sync Playlist' on-click='syncPlaylist' active='false'/>";
                VersionUtils.BSMLParser_Instance.Parse(bsmlContent, lpdvc.GetField<UnityEngine.GameObject, LevelPackDetailViewController>("_detailWrapper"), this);
                customSyncButton.transform.localScale *= 0.6f;
            }
            catch (Exception e)
            {
                Plugin.Log.Error("There was an exception parsing the custom sync button!\n" + e);
            }

            try
            {
                Type pdvbc = playlistManager.GetType("PlaylistManager.UI.PlaylistDetailViewButtonsController");
                MethodInfo meth = AccessTools.Method(pdvbc, "Initialize");
                Plugin.harmony.Patch(meth, new(typeof(PlaylistUtils).GetMethod("SetPdvbc", BindingFlags.NonPublic | BindingFlags.Static)));
            }
            catch (Exception e)
            {
                Plugin.Log.Error("There was an error getting PlaylistManager class instances\n" + e);
            }

            try
            {
                if (handler is null)
                {
                    Type eventType = playlistManager.GetType("PlaylistManager.Utilities.Events");
                    EventInfo eventInfo = eventType.GetEvent("playlistSelected");
                    MethodInfo methodInfo = typeof(PlaylistUtils).GetMethod("OnPlaylistSelected", BindingFlags.NonPublic | BindingFlags.Instance);

                    handler = Delegate.CreateDelegate(eventInfo.EventHandlerType, this, methodInfo);
                    eventInfo.AddEventHandler(null, handler);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Error("There was an issue with binding OnPlaylistSelected!\n" + e);
            }
        }
        public void Dispose()
        {
            try
            {
                if (handler is null || playlistManager is null)
                    return;

                playlistManager.GetType("PlaylistManager.Utilities.Events").GetEvent("playlistSelected").RemoveEventHandler(null, handler);
            }
            catch (Exception e)
            {
                Plugin.Log.Error(e);
            }
        }

        private static void SetPdvbc(object __instance) => pdvbcInstance = __instance;
        private void OnPlaylistSelected(object playlist, object playlistManager)
        {
            //Plugin.Log.Info("This worked: " + playlist.ToString());
            currentPlaylist = playlist;
            currentManagerInstance = playlistManager;
            try
            {
                Type playlistType = playlistLib!.GetType("BeatSaberPlaylistsLib.Types.IPlaylist");
                object?[] customDataParams = [CustomDataKey, null];

                if ((bool)playlistType.GetMethod("TryGetCustomData").Invoke(playlist, customDataParams) && customDataParams[1] is string str)
                    currentCustomData = str;
                else
                    currentCustomData = null;

                customSyncButton.gameObject.SetActive(currentCustomData is not null);
            }
            catch (Exception e)
            {
                Plugin.Log.Error("There was an error trying to handle the playlist event.\n" + e);
            }
        }

        [BeatSaberMarkupLanguage.Attributes.UIAction("syncPlaylist")]
        private async void SyncPlaylist()
        {
            if (currentCustomData is null)
                throw new Exception("This button shouldn't be able to be pressed if there is not custom data.");

            customSyncButton.interactable = false;

            try
            {
                string[] args = currentCustomData.Split(',');
                string comp = args[0];
                float threshold = float.Parse(args[1]);

                // Just in case localization using commas as decimals
                int offset = 0;
                if (args.Length > 5)
                {
                    threshold = float.Parse(args[1] + ',' + args[2]);
                    offset = 1;
                }

                string playerId = args[2 + offset];
                APCategory category = (APCategory)Enum.Parse(typeof(APCategory), args[3 + offset]);
                string type = args[4 + offset];

                IEnumerable<PlaylistMapInfo>? maps = null;
                switch (type)
                {
                    case "ap":
                        maps = await levelUtils.GetMapsAp(category, playerId, threshold, comp.FromComparisonString());
                        break;
                    case "accuracy":
                        maps = await levelUtils.GetMapsAcc(category, playerId, threshold, comp.FromComparisonString());
                        break;
                }

                if (maps is null)
                    return;

                Type playlistLibManagerType = playlistLib!.GetType("BeatSaberPlaylistsLib.PlaylistManager");
                Type playlistType = playlistLib.GetType("BeatSaberPlaylistsLib.Types.Playlist");
                Type playlistSongType = playlistLib.GetType("BeatSaberPlaylistsLib.Types.PlaylistSong");

                playlistType.GetMethod("Clear").Invoke(currentPlaylist, null);

                MethodInfo addMap = playlistType.GetMethod("Add", BindingFlags.Public | BindingFlags.Instance, null, [typeof(string), typeof(string), typeof(string), typeof(string)], null);
                MethodInfo addDifficulty = playlistSongType.GetMethod("AddDifficulty", BindingFlags.Public | BindingFlags.Instance, null, [typeof(string), typeof(string)], null);

                foreach (var (hash, diffInfo) in maps)
                {
                    object playlistSong = addMap.Invoke(currentPlaylist, [hash, null, null, null]);
                    foreach (var (characteristic, diff) in diffInfo)
                        addDifficulty.Invoke(playlistSong, [characteristic, diff.ToString()]);
                }

                playlistType.GetMethod("RaisePlaylistChanged", BindingFlags.Public | BindingFlags.Instance).Invoke(currentPlaylist, null);
                playlistType.GetMethod("RaiseCoverImageChanged", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(currentPlaylist, null);
                playlistLibManagerType.GetMethod("StorePlaylist", BindingFlags.Public | BindingFlags.Instance, null, [playlistType, typeof(bool)], null).Invoke(currentManagerInstance, [currentPlaylist, true]);

                Type pluginConfig = playlistManager!.GetType("PlaylistManager.Configuration.PluginConfig");
                object pcInstance = pluginConfig.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);

                if (pdvbcInstance is null)
                    return;

                Type pdvbc = playlistManager.GetType("PlaylistManager.UI.PlaylistDetailViewButtonsController");
                int syncOption = (int)pluginConfig.GetProperty("SyncOption", BindingFlags.Public | BindingFlags.Instance).GetValue(pcInstance);

                if (syncOption == 1)
                    pdvbc.GetMethod("DownloadAccepted", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(pdvbcInstance, null);
                else
                    pdvbc.GetMethod("DownloadRejected", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(pdvbcInstance, null);
            }
            catch (Exception e)
            {
                Plugin.Log.Error("There was an error syncing the playlist.\n" + e);
            }
            finally
            {
                customSyncButton.interactable = true;
            }
        }

        // Reflection taken from: https://github.com/BeatLeader/beatleader-mod/blob/master/Source/7_Utils/Interop/Interops/PlaylistsLibInterop.cs#L10
        public void LoadPlaylist(string filename, string playlistName, IEnumerable<PlaylistMapInfo> maps, string? customSyncData, Action<string?>? statusUpdater = null, bool endEvent = true)
        {
            try
            {
                if (playlistLib is null)
                {
                    Plugin.Log.Warn("BeatSaberPlaylistsLib is not installed, cannot create playlist.");
                    return;
                }

                statusUpdater?.Invoke("Creating...");

                Type playlistLibManagerType = playlistLib.GetType("BeatSaberPlaylistsLib.PlaylistManager");
                Type playlistType = playlistLib.GetType("BeatSaberPlaylistsLib.Types.IPlaylist");
                Type playlistSongType = playlistLib.GetType("BeatSaberPlaylistsLib.Types.PlaylistSong");

                PropertyInfo playlistLibManagerProperty = playlistLibManagerType.GetProperty("DefaultManager", BindingFlags.Static | BindingFlags.Public);

                object managerInstance = playlistLibManagerProperty.GetValue(null);

                object playlist = playlistLibManagerType.GetMethod("CreatePlaylist", BindingFlags.Public | BindingFlags.Instance, null, [typeof(string), typeof(string), typeof(string), typeof(string), typeof(string)], null)
                    .Invoke(managerInstance, [filename, playlistName, PlaylistAuthor, "", null]);

                statusUpdater?.Invoke("Adding...");

                MethodInfo addMap = playlistType.GetMethod("Add", BindingFlags.Public | BindingFlags.Instance, null, [typeof(string), typeof(string), typeof(string), typeof(string)], null);
                MethodInfo addDifficulty = playlistSongType.GetMethod("AddDifficulty", BindingFlags.Public | BindingFlags.Instance, null, [typeof(string), typeof(string)], null);

                foreach (var (hash, diffInfo) in maps)
                {
                    object playlistSong = addMap.Invoke(playlist, [hash, null, null, null]);
                    foreach (var (characteristic, diff) in diffInfo)
                        addDifficulty.Invoke(playlistSong, [characteristic, diff.ToString()]);
                }

                if (customSyncData is not null)
                    playlistType.GetMethod("SetCustomData", BindingFlags.Public | BindingFlags.Instance).Invoke(playlist, [CustomDataKey, customSyncData]);

                statusUpdater?.Invoke("Refreshing...");

                playlistLibManagerType.GetMethod("StorePlaylist", BindingFlags.Public | BindingFlags.Instance, null, [playlistType, typeof(bool)], null).Invoke(managerInstance, [playlist, true]);
                playlistLibManagerType.GetMethod("RefreshPlaylists", BindingFlags.Public | BindingFlags.Instance).Invoke(managerInstance, [true]);

            }
            catch (Exception e)
            {
                Plugin.Log.Error("There was an error making a playlist.\n" + e);
            }
            finally
            {
                if (endEvent)
                    statusUpdater?.Invoke(null);
            }
        }
        public void RefreshPlaylist(string filename)
        {
            if (playlistLib is null)
            {
                Plugin.Log.Warn("BeatSaberPlaylistsLib is not installed, cannot refresh playlist.");
                return;
            }

            try
            {
                Type playlistLibManagerType = playlistLib.GetType("BeatSaberPlaylistsLib.PlaylistManager");
                Type playlistType = playlistLib.GetType("BeatSaberPlaylistsLib.Types.IPlaylist");

                PropertyInfo playlistLibManagerProperty = playlistLibManagerType.GetProperty("DefaultManager", BindingFlags.Static | BindingFlags.Public);

                object managerInstance = playlistLibManagerProperty.GetValue(null);
                object? playlist = playlistLibManagerType.GetMethod("GetPlaylist", BindingFlags.Public | BindingFlags.Instance).Invoke(managerInstance, [filename, true, null]);

                if (playlist is null)
                {
                    Plugin.Log.Warn($"Playlist \"{filename}\" cannot be found.");
                    return;
                }

                playlistLibManagerType.GetMethod("MarkPlaylistChanged", BindingFlags.Public | BindingFlags.Instance).Invoke(managerInstance, [playlist]);
                playlistLibManagerType.GetMethod("RefreshPlaylists", BindingFlags.Public | BindingFlags.Instance).Invoke(managerInstance, [false]);
            }
            catch (Exception e)
            {
                Plugin.Log.Error("Issue refreshing the playlists.\n" + e);
            }
        }
#if NEW_VERSION
        public BeatmapLevelPack? GetPlaylistLevelpack(string filename)
#else
        public IBeatmapLevelPack? GetPlaylistLevelpack(string filename)
#endif
        {
            if (playlistLib is null)
            {
                Plugin.Log.Warn("BeatSaberPlaylistsLib is not installed, cannot get playlist levelpack.");
                return null;
            }

            try
            {
                Type playlistLibManagerType = playlistLib.GetType("BeatSaberPlaylistsLib.PlaylistManager");
                Type playlistType = playlistLib.GetType("BeatSaberPlaylistsLib.Types.IPlaylist");

                PropertyInfo playlistLibManagerProperty = playlistLibManagerType.GetProperty("DefaultManager", BindingFlags.Static | BindingFlags.Public);

                object managerInstance = playlistLibManagerProperty.GetValue(null);

                object[] playlists = (object[])playlistLibManagerType.GetMethod("GetAllPlaylists", BindingFlags.Public | BindingFlags.Instance, null, [], null).Invoke(managerInstance, []);
                PropertyInfo filenameProperty = playlistType.GetProperty("Filename", BindingFlags.Public | BindingFlags.Instance);

                object? playlistWrapper = playlists.FirstOrDefault(list => filename.Equals((string)filenameProperty.GetValue(list)));

                if (playlistWrapper is null)
                    return null;

#if NEW_VERSION
                return (BeatmapLevelPack)playlistType.GetProperty("PlaylistLevelPack", BindingFlags.Public | BindingFlags.Instance).GetValue(playlistWrapper);
#else
                return (IBeatmapLevelPack)playlistWrapper;
#endif
            }
            catch (Exception e)
            {
                Plugin.Log.Error("There was an error in trying to get the playlist level pack\n" + e);
                return null;
            }
        }

        public List<PlaylistMapInfo> GetPlaylistData(IEnumerable<string> mapDiffIds)
        {
            HashSet<string> idSet = [.. mapDiffIds];
            List<PlaylistMapInfo> maps = [];

            foreach (AccSaberBasicMap map in serialHandler.CachedMaps.Values)
            {
                AccSaberBasicDifficulty? basicDiff = map.Difficulties.FirstOrDefault(diff => idSet.Contains(diff.DifficultyId));

                if (basicDiff is null)
                    continue;

                string hash = basicDiff.Hash;
                List<PlaylistDiffInfo> diffInfo = [];

                do
                {
                    diffInfo.Add(new("Standard", basicDiff.Difficulty)); // characteristic is always standard for now, but this is where we'd add it if we added more characteristics to the API
                    idSet.Remove(basicDiff.DifficultyId);
                    basicDiff = map.Difficulties.FirstOrDefault(diff => idSet.Contains(diff.DifficultyId));
                } while (basicDiff is not null);

                maps.Add(new(hash, diffInfo));

                if (idSet.Count == 0)
                    break;
            }

            return maps;
        }

        public readonly record struct PlaylistMapInfo(string Hash, IEnumerable<PlaylistDiffInfo> DiffInfo);
        public readonly record struct PlaylistDiffInfo(string Characteristic, BeatmapDifficulty Difficulty);
    }
}
