using AccSaber.Models;
using AccSaber.Models.CacheModels;
using IPA.Loader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AccSaber.Utils
{
    internal static class PlaylistUtils
    {
        public const string PlaylistAuthor = "Accsaber Reloaded";

        // Reflection taken from: https://github.com/BeatLeader/beatleader-mod/blob/master/Source/7_Utils/Interop/Interops/PlaylistsLibInterop.cs#L10
        public static void LoadPlaylist(string filename, string playlistName, IEnumerable<PlaylistMapInfo> maps, string? syncUrl, Action<string?>? statusUpdater = null, bool endEvent = true)
        {
            try
            {
                if (!PluginManager.EnabledPlugins.Any(plugin => plugin.Id.Equals("BeatSaberPlaylistsLib")))
                {
                    Plugin.Log.Warn("BeatSaberPlaylistsLib is not installed, cannot create playlist.");
                    return;
                }

                statusUpdater?.Invoke("Creating...");

                Assembly playlistLib = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(assembly => assembly.GetName().Name.Equals("BeatSaberPlaylistsLib"));

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

                if (syncUrl is not null)
                    playlistType.GetMethod("SetCustomData", BindingFlags.Public | BindingFlags.Instance).Invoke(playlist, ["syncURL", syncUrl]);

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
        public static void RefreshPlaylist(string filename)
        {
            if (!PluginManager.EnabledPlugins.Any(plugin => plugin.Id.Equals("BeatSaberPlaylistsLib")))
            {
                Plugin.Log.Warn("BeatSaberPlaylistsLib is not installed, cannot refresh playlist.");
                return;
            }

            try
            {
                Assembly playlistLib = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(assembly => assembly.GetName().Name.Equals("BeatSaberPlaylistsLib"));

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
        public static BeatmapLevelPack? GetPlaylistLevelpack(string filename)
#else
        public static IBeatmapLevelPack? GetPlaylistLevelpack(string filename)
#endif
        {
            if (!PluginManager.EnabledPlugins.Any(plugin => plugin.Id.Equals("BeatSaberPlaylistsLib")))
            {
                Plugin.Log.Warn("BeatSaberPlaylistsLib is not installed, cannot get playlist levelpack.");
                return null;
            }

            try
            {
                Assembly playlistLib = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(assembly => assembly.GetName().Name.Equals("BeatSaberPlaylistsLib"));

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

        public static List<PlaylistMapInfo> GetPlaylistData(IEnumerable<string> mapDiffIds)
        {
            HashSet<string> idSet = [.. mapDiffIds];
            List<PlaylistMapInfo> maps = [];

            foreach (AccSaberBasicMap map in SerializerHandler.CachedMaps.Values)
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
