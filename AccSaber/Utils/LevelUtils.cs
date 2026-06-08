using AccSaber.API;
using AccSaber.Consts;
using AccSaber.Models;
using AccSaber.Models.CacheModels;
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
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

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
        [Inject] private readonly PlaylistUtils _playlistUtils = null!;

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

        internal async Task<IEnumerable<PlaylistUtils.PlaylistMapInfo>?> GetMapsAp(APCategory type, string playerId, float apThreshold, string comp)
        {
            IEnumerable<AccSaberBasicDifficulty>? scores = (await AccsaberAPI.GetMapsAboveThreshold(playerId, apThreshold, type))?.Cast<AccSaberBasicDifficulty>();

            if (scores is null)
                return null;

            int scoreSize = scores.Count();

            StatusTextChanged?.Invoke($"Found {scoreSize} {(scoreSize == 1 ? "map" : "maps")}.");

            IEnumerable<string> ids = scores.Select(entry => entry.DifficultyId);

            if (comp.Contains('<'))
            {
                HashSet<string> idSet = [.. ids];
                ids = SerializerHandler.CachedMaps.Values.SelectMany(map => map.Difficulties.Where(diff => diff.Category == type && !idSet.Contains(diff.DifficultyId)).Select(diff => diff.DifficultyId));
            }

            return _playlistUtils.GetPlaylistData(ids);
        }
        internal async Task<IEnumerable<PlaylistUtils.PlaylistMapInfo>?> GetMapsAcc(APCategory type, string playerId, float accThreshold, string comp)
        {
            string sort = comp.Contains('<') ? "&sort=accuracy,desc" : "&sort=accuracy,asc";
            AccSaberPagedContent<AccSaberLeaderboardEntry>? scores = await APIHandler.CallAPI_Json<AccSaberPagedContent<AccSaberLeaderboardEntry>>(string.Format(HelpfulPaths.APAPI_CATEGORY_SCORES, playerId, EnumUtils.EnumToReloadedCategory(type), 0, 50) + sort, AccsaberAPI.throttler);

            if (scores is null || scores.Content is null)
                return null;

            int scoreSize = scores.Content.Count;

            StatusTextChanged?.Invoke($"Found {scoreSize} {(scoreSize == 1 ? "map" : "maps")}.");

            IEnumerable<string> ids = scores.Content.Where(entry => entry.Accuracy.Compare(accThreshold, comp)).Select(entry => entry.DifficultyId);

            return _playlistUtils.GetPlaylistData(ids);
        }

        public async Task LoadPlaylist(string filename, string playlistName, IEnumerable<PlaylistUtils.PlaylistMapInfo> maps, string? customSyncData, Action? closeMenu, Action<string?>? statEvent = null, bool endEvent = true)
        {
            StatusTextChanged += statEvent;

            if (!Directory.GetFiles(ResourcePaths.CUSTOM_PLAYLISTS).Any(name => name.Contains(filename)))
                _playlistUtils.LoadPlaylist(filename, playlistName, maps, customSyncData, StatusTextChanged, endEvent);

            if (closeMenu is not null)
                await GoToPlaylist(filename, closeMenu);
            
            StatusTextChanged -= statEvent;
        }
        public async Task LoadPlaylistAp(APCategory type, string playerId, float apThreshold, string comp, Action? closeMenu, Action<string?>? statEvent = null, bool endEvent = true)
        {
            try
            {
                StatusTextChanged += statEvent;

                StatusTextChanged?.Invoke("Loading...");

                string categoryName = EnumUtils.CategoryIdToOtherReloadedCategory(type.ToString())!.Replace('_','-');
                string thresholdDirection = comp.Contains('>') ? "above" : "below";

                string filename = $"accsaber-reloaded-{categoryName}-{thresholdDirection}-{apThreshold:0.##}ap";
                string playlistName = $"{categoryName.Replace('-',' ').CapitializeWords()} {thresholdDirection.Capitialize()} {apThreshold:0.##}ap";

                IEnumerable<PlaylistUtils.PlaylistMapInfo>? maps = await GetMapsAp(type, playerId, apThreshold, comp);

                if (maps is null)
                    return;

                await LoadPlaylist(filename, playlistName, maps, $"{comp},{apThreshold},{playerId},{type},ap", closeMenu);
            }
            catch (Exception e)
            {
                Plugin.Log.Error("There was an exception loading the threshold playlist.\n" + e);
            }
            finally
            {
                if (endEvent)
                    StatusTextChanged?.Invoke(null);

                StatusTextChanged -= statEvent;
            }
        }
        public async Task LoadPlaylistAcc(APCategory type, string playerId, float accThreshold, string comp, Action? closeMenu, Action<string?>? statEvent = null, bool endEvent = true)
        {
            try
            {
                StatusTextChanged += statEvent;

                StatusTextChanged?.Invoke("Loading...");

                string categoryName = EnumUtils.CategoryIdToOtherReloadedCategory(type.ToString())!.Replace('_','-');
                string thresholdDirection = comp.Contains('>') ? "above" : "below";

                string filename = $"accsaber-reloaded-{categoryName}-{thresholdDirection}-{accThreshold * 100f:0.##}%";
                string playlistName = $"{categoryName.Replace('-',' ').CapitializeWords()} {thresholdDirection.Capitialize()} {accThreshold * 100f:0.##}%";

                IEnumerable<PlaylistUtils.PlaylistMapInfo>? maps = await GetMapsAcc(type, playerId, accThreshold, comp);

                if (maps is null)
                    return;

                await LoadPlaylist(filename, playlistName, maps, $"{comp},{accThreshold},{playerId},{type},accuracy", closeMenu);
            }
            catch (Exception e)
            {
                Plugin.Log.Error("There was an exception loading the threshold playlist.\n" + e);
            }
            finally
            {
                if (endEvent)
                    StatusTextChanged?.Invoke(null);

                StatusTextChanged -= statEvent;
            }
        }
        public async Task LoadPlaylist(APCategory type, string userId, Action? closeMenu, Action<string?>? statEvent = null, bool endEvent = true)
        {
            try
            {
                StatusTextChanged += statEvent;

                string categoryName = EnumUtils.CategoryIdToOtherReloadedCategory(type.ToString())!;

                string filename = $"accsaber-reloaded-missing-{userId}-{categoryName.Replace('_', '-')}.bplist";

                if (!Directory.GetFiles(ResourcePaths.CUSTOM_PLAYLISTS).Any(name => name.Contains(filename)))
                {

                    StatusTextChanged?.Invoke("Downloading...");

                    var (data, headers) = await APIHandler.CallAPI_Bytes(string.Format(HelpfulPaths.APAPI_PLAYLIST_MISSING, userId, categoryName), AccsaberAPI.throttler);

                    filename = FilenameRegex.Match(headers!.GetValues("Content-Disposition").First()).Value;

                    StatusTextChanged?.Invoke("Saving...");

                    File.WriteAllBytes(Path.Combine(ResourcePaths.CUSTOM_PLAYLISTS, filename), data);

                    _playlistUtils.RefreshPlaylist(filename);
                }

                if (closeMenu is not null)
                    await GoToPlaylist(filename[..filename.LastIndexOf('.')], closeMenu);
            }
            catch (Exception e)
            {
                Plugin.Log.Error($"There was an error loading the missing playlist for userid {userId}!\n{e}");
            }
            finally
            {
                if (closeMenu is null)
                {
                    if (endEvent)
                        StatusTextChanged?.Invoke(null);

                    StatusTextChanged -= statEvent;
                }
            }
        }
        public async Task LoadPlaylist(APCategory type, Action? closeMenu, Action<string?>? statEvent = null, bool endEvent = true)
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

                    _playlistUtils.RefreshPlaylist(filename);
                }

                if (closeMenu is not null)
                    await GoToPlaylist(filename[..filename.LastIndexOf('.')], closeMenu);
            } 
            catch (Exception e)
            {
                Plugin.Log.Error($"There was an issue loading the playlist of type {type}\n{e}");
            } 
            finally
            {
                if (closeMenu is null)
                {
                    if (endEvent)
                        StatusTextChanged?.Invoke(null);

                    StatusTextChanged -= statEvent;
                }
            }
        }
        public async Task LoadPlaylist(string sniperId, string targetId, APCategory category, Action? closeMenu, Action<string?>? statEvent = null, bool endEvent = true)
        {
            try
            {
                StatusTextChanged += statEvent;

                string filename = $"accsaber-snipe-{sniperId}-{targetId}-{EnumUtils.CategoryIdToOtherReloadedCategory(category.ToString())?.Replace('_','-')}.bplist";

                if (!Directory.GetFiles(ResourcePaths.CUSTOM_PLAYLISTS).Any(name => name.Contains(filename)))
                {
                    StatusTextChanged?.Invoke("Downloading...");

                    var (data, headers) = await APIHandler.CallAPI_Bytes(string.Format(HelpfulPaths.APAPI_PLAYLIST_SNIPE, sniperId, targetId, 0, EnumUtils.CategoryIdToOtherReloadedCategory(category.ToString())), AccsaberAPI.throttler);
                    filename = FilenameRegex.Match(headers!.GetValues("Content-Disposition").First()).Value;

                    StatusTextChanged?.Invoke("Saving...");

                    File.WriteAllBytes(Path.Combine(ResourcePaths.CUSTOM_PLAYLISTS, filename), data);
                        
                    _playlistUtils.RefreshPlaylist(filename);
                }

                if (closeMenu is not null)
                    await GoToPlaylist(filename[..filename.LastIndexOf('.')], closeMenu);
            }
            catch (Exception e)
            {
                Plugin.Log.Error($"There was an issue loading the sniper playlist for sniper {sniperId} and target {targetId} in category {category}\n{e}");
            }
            finally
            {
                if (closeMenu is null)
                {
                    if (endEvent)
                        StatusTextChanged?.Invoke(null);

                    StatusTextChanged -= statEvent;
                }
            }
        }

        public async Task GoToPlaylist(string filename, Action closeMenu, Action<string?>? statEvent = null, bool endEvent = true)
        {
            StatusTextChanged += statEvent;

            StatusTextChanged?.Invoke("Loading...");

            IEnumerator PresentCoroutine()
            {
                yield return new WaitForEndOfFrame();

                try
                {
#if NEW_VERSION
                    BeatmapLevelPack? levelPack = null;
#else
                    IBeatmapLevelPack? levelPack = null;
#endif

                    levelPack = _playlistUtils.GetPlaylistLevelpack(filename);

                    //Plugin.Log.Info("levelPack null? " + (levelPack is null));
                    if (levelPack is null)
                        yield break;

                    closeMenu();

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
                    if (endEvent)
                        StatusTextChanged?.Invoke(null);

                    StatusTextChanged -= statEvent;
                }
            }

            _mainFlowCoordinator.StartCoroutine(PresentCoroutine());
        }

        // Interpreted from: https://github.com/kinsi55/BeatSaber_BetterSongSearch/blob/master/UI/SelectedSongView.cs#L186
        public async Task GoToSong(string diffId, string? targetPlayerId, Action? closeMenu, Action<string?>? statEvent = null, bool endEvent = true)
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
                    if (endEvent)
                        StatusTextChanged?.Invoke(null);
                    StatusTextChanged -= statEvent;
                    return;
                }

#if NEW_VERSION
                BeatmapLevel? level = SongCore.Loader.BeatmapLevelsModelSO.GetBeatmapLevel(header + hash.ToUpper()) ?? await DownloadSong(map);
#else
                    IBeatmapLevel? level = (await SongCore.Loader.BeatmapLevelsModelSO.GetBeatmapLevelAsync(header + hash.ToUpper(), CancellationToken.None)).beatmapLevel ?? await DownloadSong(map);
#endif
                if (endEvent)
                    StatusTextChanged?.Invoke(null);

                if (level is null)
                {
                    Plugin.Log.Warn("Cannot open the map for this mission (" + header + hash.ToUpper() + ").");
                    StatusTextChanged -= statEvent;
                    return;
                }

                IEnumerator PresentRoutine()
                {
                    yield return new WaitForEndOfFrame();

                    try
                    {

                        closeMenu?.Invoke();

#if NEW_VERSION
                    BeatmapKey key = level.GetBeatmapKeys().First(k => k.difficulty == cachedDiff.Difficulty);

                    LevelSelectionFlowCoordinator.State flow = new(SelectLevelCategoryViewController.LevelCategory.All, SongCore.Loader.CustomLevelsPack, in key, level);
#else
                        IDifficultyBeatmapSet diffSet = level.beatmapLevelData.difficultyBeatmapSets.First(set => set.beatmapCharacteristic.serializedName.Equals("Standard", StringComparison.OrdinalIgnoreCase));
                        IDifficultyBeatmap diff = diffSet.difficultyBeatmaps.First(difficulty => difficulty.difficulty == cachedDiff.Difficulty);

                        LevelSelectionFlowCoordinator.State flow = new(SelectLevelCategoryViewController.LevelCategory.All, SongCore.Loader.CustomLevelsPack, diff);
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
                    finally
                    {
                        StatusTextChanged -= statEvent;
                    }
                }

                _mainFlowCoordinator.StartCoroutine(PresentRoutine());
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
