using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace AccSaber.Utils
{
    internal static class PlaylistUtils
    {
        public const string PlaylistAuthor = "Accsaber Reloaded";

        // Reflection taken from: https://github.com/BeatLeader/beatleader-mod/blob/master/Source/7_Utils/Interop/Interops/PlaylistsLibInterop.cs#L10
        public static void LoadPlaylist(string name, IEnumerable<string> hashes, Action<string?>? statusUpdater = null)
        {
            try
            {
                statusUpdater?.Invoke("Creating...");

                Assembly? playlistLib = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(assembly => assembly.GetName().Name.Equals("BeatSaberPlaylistsLib"));

                Type playlistLibManagerType = playlistLib.GetType("BeatSaberPlaylistsLib.PlaylistManager");
                Type playlistType = playlistLib.GetType("BeatSaberPlaylistsLib.Types.IPlaylist");

                PropertyInfo playlistLibManagerProperty = playlistLibManagerType.GetProperty("DefaultManager", BindingFlags.Static | BindingFlags.Public);

                object managerInstance = playlistLibManagerProperty.GetValue(null);

                object playlist = playlistLibManagerType.GetMethod("CreatePlaylist", BindingFlags.Public | BindingFlags.Instance).Invoke(managerInstance, ["", name, PlaylistAuthor, ""]);

                statusUpdater?.Invoke("Adding...");

                MethodInfo addMap = playlistType.GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);

                foreach (string hash in hashes)
                    addMap.Invoke(playlist, [hash, null, null, null]);

                statusUpdater?.Invoke("Refreshing...");

                playlistLibManagerType.GetMethod("MarkPlaylistChanged", BindingFlags.Public | BindingFlags.Instance).Invoke(managerInstance, [playlist]);
                playlistLibManagerType.GetMethod("RequestRefresh", BindingFlags.Public | BindingFlags.Instance).Invoke(managerInstance, [PlaylistAuthor]);
                playlistLibManagerType.GetMethod("RefreshPlaylists", BindingFlags.Public | BindingFlags.Instance).Invoke(managerInstance, [false]);
            } 
            catch (Exception e)
            {
                Plugin.Log.Error("There was an error making a playlist.\n" + e);
            }
            finally
            {
                statusUpdater?.Invoke(null);
            }
        }
        public static void RefreshPlaylist(string filename)
        {
            try
            {
                Assembly? playlistLib = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(assembly => assembly.GetName().Name.Equals("BeatSaberPlaylistsLib"));

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
            try
            {
                Assembly? playlistLib = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(assembly => assembly.GetName().Name.Equals("BeatSaberPlaylistsLib"));

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
    }
}
