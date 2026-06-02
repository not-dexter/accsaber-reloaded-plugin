using AccSaber.API;
using AccSaber.Consts;
using AccSaber.Models;
using AccSaber.Models.CacheModels;
using AccSaber.UI.MenuButton;
using AccSaber.UI.ViewControllers;
using IPA.Utilities;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Zenject;
using IPA.Loader;
using System.Collections.Generic;

#if NEW_VERSION
using System.Reflection;
#else
using BeatSaberMarkupLanguage;
#endif

namespace AccSaber.Utils
{
    internal class LevelUtils : IInitializable, IDisposable
    {
        public const string header = "custom_level_";

        private static readonly AsyncLock OpenMapLock = new();
        private static readonly object LoadWaiterLock = new();

        private static readonly Regex FilenameStarRegex = new(@"filename\*=(?<charset>[\w-]+)''(?<name>[^\s;]+)");
        private static readonly Regex FilenameRegex = new(@"(?<=filename="")[^""]+(?="")");

        [Inject] private readonly MainFlowCoordinator _mainFlowCoordinator = null!;
        [Inject] private readonly SoloFreePlayFlowCoordinator _soloCoordinator = null!;
        [Inject] private readonly AccSaberMainFlowCoordinator _parentFlowCoordinator = null!;

        public event Action<string?>? StatusTextChanged;

        public async void Initialize()
        {
            SongCore.Loader.SongsLoadedEvent += LoadWaiter;
        }
        public void Dispose()
        {
            SongCore.Loader.SongsLoadedEvent -= LoadWaiter;
        }

#if NEW_VERSION
        private static void LoadWaiter(SongCore.Loader loader, ConcurrentDictionary<string, BeatmapLevel> maps)
#else
        private static void LoadWaiter(SongCore.Loader loader, ConcurrentDictionary<string, CustomPreviewBeatmapLevel> maps)
#endif
        {
            lock (LoadWaiterLock)
                Monitor.PulseAll(LoadWaiterLock);
        }

        public async Task LoadPlaylist(string filename, string playlistName, IEnumerable<PlaylistUtils.PlaylistMapInfo> maps, Action<string?>? statEvent = null)
        {
            if (!PluginManager.EnabledPlugins.Any(plugin => plugin.Id.Equals("BeatSaberPlaylistsLib")))
            {
                Plugin.Log.Warn("PlaylistManager is not enabled, cannot load playlists!");
                return;
            }

            StatusTextChanged += statEvent;

            if (!Directory.GetFiles(ResourcePaths.CUSTOM_PLAYLISTS).Any(name => name.Contains(filename)))
                PlaylistUtils.LoadPlaylist(filename, playlistName, maps, StatusTextChanged);
            await GoToPlaylist(filename);

            StatusTextChanged -= statEvent;
        }
        public async Task LoadPlaylist(APCategory type, string playerId, float apThreshold, Action<string?>? statEvent = null)
        {
            try
            {
                StatusTextChanged += statEvent;

                StatusTextChanged?.Invoke("Loading...");

                string categoryName = EnumUtils.CategoryIdToOtherReloadedCategory(type.ToString())!.Replace('_','-');

                string filename = $"accsaber-reloaded-{categoryName}-{apThreshold}ap";
                string playlistName = $"{categoryName.Replace('-',' ').CapitializeWords()} Above {apThreshold}ap";

                IEnumerable<AccSaberPlayerScore>? scores = await AccsaberAPI.GetPlayerScores(apThreshold, type);

                if (scores is null)
                    return;

                int scoreSize = scores.Count();
                string song = scoreSize == 1 ? "map" : "maps";

                Plugin.Log.Info($"Found {scoreSize} {song}.");

                StatusTextChanged?.Invoke($"Found {scoreSize} {song}.");

                IEnumerable<string> ids = scores.Select(entry => entry.DifficultyId);

                List<PlaylistUtils.PlaylistMapInfo> maps = PlaylistUtils.GetPlaylistData(ids);

                await LoadPlaylist(filename, playlistName, maps);
            }
            catch (Exception e)
            {
                Plugin.Log.Error("There was an exception loading the threshold playlist.\n" + e);
            }
            finally
            {
                StatusTextChanged?.Invoke(null);

                StatusTextChanged -= statEvent;
            }
        }
        public async Task LoadPlaylist(APCategory type, Action<string?>? statEvent = null)
        {
            try
            {
                StatusTextChanged += statEvent;

                string categoryName = EnumUtils.CategoryIdToOtherReloadedCategory(type.ToString())!;

                string filename = $"accsaber-reloaded-{categoryName.Replace('_', '-')}.bplist";

                //Plugin.Log.Info($"{Directory.GetFiles(ResourcePaths.CUSTOM_PLAYLISTS).Print()}");

                if (!Directory.GetFiles(ResourcePaths.CUSTOM_PLAYLISTS).Any(name => name.Contains(filename))) {

                    StatusTextChanged?.Invoke("Downloading...");

                    var (data, headers) = await APIHandler.CallAPI_Bytes(string.Format(HelpfulPaths.APAPI_PLAYLIST, categoryName), AccsaberAPI.throttler);

                    filename = FilenameRegex.Match(headers!.GetValues("Content-Disposition").First()).Value;

                    StatusTextChanged?.Invoke("Saving...");

                    File.WriteAllBytes(Path.Combine(ResourcePaths.CUSTOM_PLAYLISTS, filename), data);

                    if (PluginManager.EnabledPlugins.Any(plugin => plugin.Id.Equals("BeatSaberPlaylistsLib")))
                        PlaylistUtils.RefreshPlaylist(filename);
                }

                await GoToPlaylist(filename[..filename.LastIndexOf('.')]);
            } 
            catch (Exception e)
            {
                Plugin.Log.Error($"There was an issue loading the playlist of type {type}\n{e}");
            } 
            finally
            {
                StatusTextChanged?.Invoke(null);

                StatusTextChanged -= statEvent;
            }
        }

        public async Task GoToPlaylist(string filename, Action<string?>? statEvent = null)
        {
            StatusTextChanged += statEvent;

            StatusTextChanged?.Invoke("Loading...");

            try
            {
#if NEW_VERSION
                BeatmapLevelPack? levelPack = null;
#else
                IBeatmapLevelPack? levelPack = null;
#endif

                if (PluginManager.EnabledPlugins.Any(plugin => plugin.Id.Equals("BeatSaberPlaylistsLib")))
                    levelPack = PlaylistUtils.GetPlaylistLevelpack(filename);

                //Plugin.Log.Info("levelPack null? " + (levelPack is null));
                if (levelPack is null)
                    return;

                _parentFlowCoordinator.CloseToMainMenu();

#if NEW_VERSION
                LevelSelectionFlowCoordinator.State flow = new(SelectLevelCategoryViewController.LevelCategory.CustomSongs, levelPack, default, null);
#else
                LevelSelectionFlowCoordinator.State flow = new(SelectLevelCategoryViewController.LevelCategory.CustomSongs, levelPack, new EmptyDifficultyBeatmap());
#endif

                _soloCoordinator.Setup(flow);

                _mainFlowCoordinator.YoungestChildFlowCoordinatorOrSelf().PresentFlowCoordinator(_soloCoordinator, immediately: true);
            }
            catch (Exception e)
            {
                Plugin.Log.Error("There was an error going to the map!\n" + e);
            }
            finally
            {
                StatusTextChanged?.Invoke(null);

                StatusTextChanged -= statEvent;
            }
        }

        // Interpreted from: https://github.com/kinsi55/BeatSaber_BetterSongSearch/blob/master/UI/SelectedSongView.cs#L186
        public async Task GoToSong(string diffId, string? targetPlayerId, Action<string?>? statEvent = null)
        {
            AsyncLock.Releaser? locker = await OpenMapLock.TryLockAsync();

            if (locker is null)
                return;

            using (locker.Value)
            {
                StatusTextChanged += statEvent;

                StatusTextChanged?.Invoke("Loading...");

                AccSaberBasicMap map = null!;
                AccSaberBasicDifficulty? cachedDiff = null;
                string? hash = null;

                foreach (string currentHash in SerializerHandler.CachedMaps.Keys)
                {
                    map = SerializerHandler.CachedMaps[currentHash];
                    cachedDiff = map.Difficulties.FirstOrDefault(diff => diff.DifficultyId.Equals(diffId));

                    if (cachedDiff is not null)
                    {
                        hash = currentHash;
                        break;
                    }
                }

                if (hash is null || cachedDiff is null)
                {
                    Plugin.Log.Critical("Somehow you have a mission for a level that isn't cached!! Please report this on Discord.");
                    StatusTextChanged?.Invoke(null);
                    StatusTextChanged -= statEvent;
                    return;
                }

#if NEW_VERSION
                BeatmapLevel? level = SongCore.Loader.BeatmapLevelsModelSO.GetBeatmapLevel(header + hash.ToUpper()) ?? await DownloadSong(map);
#else
                    IBeatmapLevel? level = (await SongCore.Loader.BeatmapLevelsModelSO.GetBeatmapLevelAsync(header + hash.ToUpper(), CancellationToken.None)).beatmapLevel ?? await DownloadSong(map);
#endif

                StatusTextChanged?.Invoke(null);

                if (level is null)
                {
                    Plugin.Log.Warn("Cannot open the map for this mission (" + header + hash.ToUpper() + ").");
                    StatusTextChanged -= statEvent;
                    return;
                }

                try
                {

                    _parentFlowCoordinator.CloseToMainMenu();

#if NEW_VERSION
                    BeatmapKey key = level.GetBeatmapKeys().First(k => k.difficulty == cachedDiff.Difficulty);

                    LevelSelectionFlowCoordinator.State flow = new(SelectLevelCategoryViewController.LevelCategory.CustomSongs, SongCore.Loader.CustomLevelsPack, in key, level);
#else
                        IDifficultyBeatmapSet diffSet = level.beatmapLevelData.difficultyBeatmapSets.First(set => set.beatmapCharacteristic.serializedName.Equals("Standard", StringComparison.OrdinalIgnoreCase));
                        IDifficultyBeatmap diff = diffSet.difficultyBeatmaps.First(difficulty => difficulty.difficulty == cachedDiff.Difficulty);

                        LevelSelectionFlowCoordinator.State flow = new(SelectLevelCategoryViewController.LevelCategory.CustomSongs, SongCore.Loader.CustomLevelsPack, diff);
#endif

                    _soloCoordinator.Setup(flow);

                    _mainFlowCoordinator.YoungestChildFlowCoordinatorOrSelf().PresentFlowCoordinator(_soloCoordinator, immediately: true);

                    StandardLevelDetailViewController? sldvc = UnityEngine.Object.FindObjectOfType<StandardLevelDetailViewController>();
                    StandardLevelDetailView? sldv = sldvc?.GetField<StandardLevelDetailView, StandardLevelDetailViewController>("_standardLevelDetailView");

#if NEW_VERSION
                    if (sldv is not null && sldv.beatmapKey != key)
                    {
                        sldv.SetContent(level, BeatmapDifficultyMask.All, [], key.difficulty, key.beatmapCharacteristic,
                            sldv.GetField<PlayerData, StandardLevelDetailView>("_playerData"));
                        typeof(StandardLevelDetailView).GetMethod("TriggerEvent", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).Invoke(sldv, []);
                    }
#else
                        if (sldv is not null && sldv.selectedDifficultyBeatmap.difficulty != diff.difficulty)
                        {
                            sldv.SetContent(level, diff.difficulty, diff.parentDifficultyBeatmapSet.beatmapCharacteristic, sldv.GetField<PlayerData, StandardLevelDetailView>("_playerData"));
                            sldv.GetField<Action<StandardLevelDetailView, IDifficultyBeatmap>, StandardLevelDetailView>("didChangeDifficultyBeatmapEvent").Invoke(sldv, diff);
                        }
#endif
                    if (targetPlayerId is not null)
                        AccSaberLeaderboardViewController.Instance.ShowPlayerPage(targetPlayerId);
                }
                catch (Exception e)
                {
                    Plugin.Log.Error("There was an error going to the map!\n" + e);
                }
                StatusTextChanged -= statEvent;
            }
        }
#if NEW_VERSION
        private async Task<BeatmapLevel?> DownloadSong(AccSaberBasicMap cachedMap)
#else
            private async Task<IBeatmapLevel?> DownloadSong(AccSaberBasicMap cachedMap)
#endif
        {
            try
            {
                StatusTextChanged?.Invoke("Downloading...");

                var (map, headers) = await APIHandler.CallAPI_Bytes(string.Format(HelpfulPaths.BEATSAVER_DOWNLOAD, cachedMap.Hash.ToLowerInvariant()), null);

                if (map is null)
                    return null;

                using Stream stream = new MemoryStream(map);
                using ZipArchive zip = new(stream, ZipArchiveMode.Read);

                string folderName = FilenameStarRegex.Match(headers!.GetValues("Content-Disposition").First()).Groups["name"].Value[..^4];
                folderName = Uri.UnescapeDataString(folderName);

                string folderPath = Path.Combine(ResourcePaths.CUSTOM_SONGS, folderName);

                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                zip.ExtractToDirectory(folderPath);

                SongCore.Loader.Instance.RefreshSongs(false);

                await Task.Run(() =>
                {
                    lock (LoadWaiterLock)
                        Monitor.Wait(LoadWaiterLock);
                });

#if NEW_VERSION
                return SongCore.Loader.BeatmapLevelsModelSO.GetBeatmapLevel(header + cachedMap.Hash.ToUpper());
#else
                //return SongCore.Loader.BeatmapLevelsModelSO.GetBeatmapLevelIfLoaded(header + cachedMap.Hash.ToUpper());
                return (await SongCore.Loader.BeatmapLevelsModelSO.GetBeatmapLevelAsync(header + cachedMap.Hash.ToUpper(), CancellationToken.None)).beatmapLevel;
#endif
            } catch (Exception e)
            {
                Plugin.Log.Error("There was an error downloading the map!\n" + e);
                return null;
            }
        }
    }
}
