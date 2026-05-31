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

#if NEW_VERSION
using System.Reflection;
#else
using BeatSaberMarkupLanguage;
#endif

namespace AccSaber.Utils
{
    internal class LevelUtils : IInitializable, IDisposable
    {
        private const string header = "custom_level_";

        private static readonly AsyncLock OpenMapLock = new();
        private static readonly object LoadWaiterLock = new();

        private static readonly Regex FilenameRegex = new("(?<=filename=\")[^\"]+(?=\";)");

        [Inject] private readonly MainFlowCoordinator _mainFlowCoordinator = null!;
        [Inject] private readonly SoloFreePlayFlowCoordinator _soloCoordinator = null!;
        [Inject] private readonly MultiplayerLevelSelectionFlowCoordinator _multiCoordinator = null!;
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

                    LevelSelectionFlowCoordinator.State flow = new(SelectLevelCategoryViewController.LevelCategory.All, SongCore.Loader.CustomLevelsPack, in key, level);
#else
                        IDifficultyBeatmapSet diffSet = level.beatmapLevelData.difficultyBeatmapSets.First(set => set.beatmapCharacteristic.serializedName.Equals("Standard", StringComparison.OrdinalIgnoreCase));
                        IDifficultyBeatmap diff = diffSet.difficultyBeatmaps.First(difficulty => difficulty.difficulty == cachedDiff.Difficulty);

                        LevelSelectionFlowCoordinator.State flow = new(SelectLevelCategoryViewController.LevelCategory.All, SongCore.Loader.CustomLevelsPack, diff);
#endif

                    _multiCoordinator.Setup(flow);
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
            StatusTextChanged?.Invoke("Downloading...");

            var (map, headers) = await APIHandler.CallAPI_Bytes(string.Format(HelpfulPaths.BEATSAVER_DOWNLOAD, cachedMap.Hash.ToLowerInvariant()), null);

            if (map is null)
                return null;

            using Stream stream = new MemoryStream(map);
            using ZipArchive zip = new(stream, ZipArchiveMode.Read);

            string folderPath = Path.Combine(ResourcePaths.CUSTOM_SONGS, FilenameRegex.Match(headers!.GetValues("Content-Disposition").First()).Value[..^4]);

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
        }

        /*internal readonly struct SongPresentInfo(MainFlowCoordinator mainFlowCoordinator,
            SoloFreePlayFlowCoordinator soloCoordinator, MultiplayerLevelSelectionFlowCoordinator multiCoordinator,
            AccSaberMainFlowCoordinator parentFlowCoordinator)
        {
            public readonly MainFlowCoordinator MainFlowCoordinator = mainFlowCoordinator;
            public readonly SoloFreePlayFlowCoordinator SoloCoordinator = soloCoordinator;
            public readonly MultiplayerLevelSelectionFlowCoordinator MultiCoordinator = multiCoordinator;
            public readonly AccSaberMainFlowCoordinator ParentFlowCoordinator = parentFlowCoordinator;
        }*/
    }
}
