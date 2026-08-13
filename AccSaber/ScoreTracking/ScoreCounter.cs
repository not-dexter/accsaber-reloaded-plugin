using AccSaber.Managers;
using AccSaber.Models;
using AccSaber.Patches;
using AccSaber.UI.MenuButton.Campaigns.ViewControllers;
using AccSaber.UI.ViewControllers;
using AccSaber.Utils;
using AccSaber.Utils.Misc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace AccSaber.ScoreTracking
{
    internal class ScoreCounter : IInitializable, IDisposable
    {
        /// <summary>
        /// Called whenever the ScoreCounter tries to submit a score.
        /// </summary>
        /// <remarks>
        /// The variables are: <br/>
        /// 1. The score that was attempted to be submitted.<br/>
        /// 2. Whether or not the score beat the currently saved score.<br/>
        /// 3. Whether or not the submission attempt was successful.
        /// </remarks>
        public static event Action<AccSaberScore, bool, bool>? OnScoreSubmitCall;

        [Inject] private readonly IReadonlyBeatmapData beatmapData = null!;

        [Inject] private readonly GameplayModifiers mods = null!;
        [Inject] private readonly ScoreController sc = null!;
        [Inject] private readonly BeatmapObjectManager bomb = null!;
        [Inject] private readonly PlayerHeadAndObstacleInteraction wall = null!;
        [Inject] private readonly PauseController pause = null!;
        [Inject] private readonly ScoreSubmissionHandler submission = null!;
        private StandardLevelScenesTransitionSetupDataSO transition = null!;
        private GameEnergyCounter? energy = null;
        private AccSaberStore? store = null;

        private static readonly HashSet<string> AllowedModes = [with(StringComparer.Ordinal), "Solo", "Multiplayer"];

        private AccSaberScore score = null!;
        private AccSaberBasicDifficulty? currentMap;
        private AccSaberLeaderboardViewController aslvc = null!;
        private Configuration.PluginConfig config = null!;
        private Task initTask = null!;

        private int current115Streak, combo, notes, totalNotes;
        private readonly object submitLock = new();
        private bool transitionFinished, counterDisposed, scoreSubmitStarted, failed, disposing, subscribed;
        private string? gamemode = null;

        public event Action? OnMistake;

        private bool AtEndsOfMap => notes == 0 || notes == totalNotes;

        public void Initialize()
        {
            disposing = false;
            initTask = InitializeInternalSafe();
        }
        private async Task InitializeInternalSafe()
        {
            try
            {
                await InitializeInternal();
            }
            catch (Exception e)
            {
                Plugin.Log.Error("ScoreCounter initialization failed:\n" + e);
            }
        }
        private async Task InitializeInternal()
        {
            SubmissionPatch.EnableSubmissions();

            subscribed = false;
            transitionFinished = false;
            counterDisposed = false;
            scoreSubmitStarted = false;
            failed = false;
            currentMap = null;

            current115Streak = 0;
            combo = 0;
            totalNotes = 0;
            notes = 0;

            score = new();

            transition = Resources.FindObjectsOfTypeAll<StandardLevelScenesTransitionSetupDataSO>().FirstOrDefault();
            store ??= Plugin.Container.TryResolve<AccSaberStore>();
            aslvc ??= Plugin.Container.TryResolve<AccSaberLeaderboardViewController>();
            config ??= Plugin.Container.TryResolve<Configuration.PluginConfig>();

            if (store is null || aslvc is null || config is null)
            {
                Plugin.Log.Error("A built in dependency is not able to be resolved. There is a bug in the code, please report this.");
                return;
            }

            if (transition is null)
            {
                Plugin.Log.Critical("The level scenes transition is null!!! This should not be possible, stopping score submission.");
                return;
            }

            if (mods.noFailOn0Energy)
                energy = Resources.FindObjectsOfTypeAll<GameEnergyCounter>().LastOrDefault(x => x.isActiveAndEnabled);

            if (transition.practiceSettings is not null)
            {
                SubmissionPatch.SetPracticeSubmission();
                Plugin.Log.Debug($"Practice mode: start time = {transition.practiceSettings.startSongTime}, speed mult = {transition.practiceSettings.songSpeedMul}");
            }

            //Plugin.Log.Info($"current map null? {store.CurrentRankedMap is null}");
            if (store.CurrentRankedMap is null && (!Plugin.Container.TryResolve<AccSaberCampaignViewController>()?.MapStarted ?? true))
                return;

            transition.didFinishEvent += OnTransitionSetupOnDidFinishEvent;
            sc.scoringForNoteFinishedEvent += NoteScoring;
            bomb.noteWasCutEvent += OnBombHit;
            wall.headDidEnterObstacleEvent += OnWallHit;
            pause.didResumeEvent += OnUnpause;
            energy?.gameEnergyDidReach0Event += OnFail;
            subscribed = true;

            currentMap = store.CurrentRankedMap ?? await store.GetCurrentMap();

            if (disposing)
                goto onDisposed;

            if (currentMap is null)
            {
                Plugin.Log.Warn("Somehow the current map is null after null checks happened. This is a bug.");
                Unsubscribe();
                return;
            }

            totalNotes = beatmapData.cuttableNotesCount;

            if (totalNotes == 0)
                totalNotes = beatmapData.GetBeatmapDataItems<NoteData>(0).Count(noteData => noteData.gameplayType != NoteData.GameplayType.Bomb);

#if NEW_VERSION
            bool IsInvalidLevel()
            {
                if (transition.beatmapLevel is null)
                {
                    Plugin.Log.Critical("The beatmap transition beatmap level is somehow null, this should not be possible.");
                    return true;
                }

                string currentHash = transition.beatmapLevel.levelID.ToLowerInvariant();
                
                if (currentHash.StartsWith(AccSaberManager.CUSTOM_LEVEL_HASH))
                    currentHash = currentHash[AccSaberManager.CUSTOM_LEVEL_HASH.Length..];

                return !currentHash.Equals(currentMap.ParentInfo?.Hash, StringComparison.OrdinalIgnoreCase) || transition.beatmapKey.difficulty != currentMap.Difficulty;
            }
#else
            bool IsInvalidLevel()
            {
                if (transition.difficultyBeatmap is null)
                {
                    Plugin.Log.Critical("The beatmap transition beatmap level is somehow null, this should not be possible.");
                    return true;
                }

                string currentHash = transition.difficultyBeatmap.level.levelID.ToLowerInvariant();

                if (currentHash.StartsWith(AccSaberManager.CUSTOM_LEVEL_HASH))
                    currentHash = currentHash[AccSaberManager.CUSTOM_LEVEL_HASH.Length..];

                return !currentHash.Equals(currentMap.ParentInfo?.Hash, StringComparison.OrdinalIgnoreCase) || transition.difficultyBeatmap.difficulty != currentMap.Difficulty;
            }
#endif

            if (IsInvalidLevel())
            {
                Plugin.Log.Critical("What?? The current map is not equal to the recorded map!!! Attempting to recorrect...");

#if NEW_VERSION
                Plugin.Log.Info($"transition level id = {transition.beatmapLevel?.levelID}, transition difficulty = {transition.beatmapKey.difficulty}");
#else
                Plugin.Log.Info($"transition level id = {transition.difficultyBeatmap?.level?.levelID}, transition difficulty = {transition.difficultyBeatmap?.difficulty}");
#endif

                Plugin.Log.Info($"current level id = {currentMap.ParentInfo?.Hash}, current difficulty = {currentMap.Difficulty}");

#if !NEW_VERSION
                if (transition.difficultyBeatmap is null)
                {
                    Plugin.Log.Info("The transition's difficulty beatmap is null, no way to identify the map to retry. Disabling score submission.");
                    currentMap = null;
                    Unsubscribe();
                    return;
                }
#endif

                PlatformLeaderboardViewController? viewController = Plugin.Container.TryResolve<PlatformLeaderboardViewController>();

                if (viewController is not null)
                {
#if NEW_VERSION
                    BeatmapKey key = transition.beatmapKey;
                    viewController.SetData(in key);
#else
                    IDifficultyBeatmap key = transition.difficultyBeatmap;
                    viewController.SetData(key);
#endif

                    SerializationHandler? serialHandler = Plugin.Container.TryResolve<SerializationHandler>();
                    AccSaberManager manager = Plugin.Container.TryResolve<AccSaberManager>();

                    bool failed = true;

                    if (serialHandler is not null && manager is not null)
                    {
                        string? hash = manager.GetHash(key);

                        if (hash is not null)
                        {
                            Models.CacheModels.AccSaberBasicMap? map = await serialHandler.GetMapByHashAsync(hash);

                            if (disposing)
                                goto onDisposed;

                            if (map is not null)
                            {
                                BeatmapDifficulty diff = key.difficulty;
                                currentMap = map.Difficulties.FirstOrDefault(basicDiff => basicDiff.Difficulty == diff);
                                failed = false;
                            }
                        }
                    }

                    if (failed)
                    {
                        Plugin.Log.Warn("Failed to directly get map from cache, attempting to get it from the store.");

                        currentMap = store.CurrentRankedMap ?? await store.GetCurrentMap();

                        if (disposing)
                            goto onDisposed;
                    }
                }

                if (currentMap is null || IsInvalidLevel())
                {
                    Plugin.Log.Critical("Current map was still not updated correctly, score submission disabled for this map.");
                    currentMap = null;
                    Unsubscribe();
                    return;
                }
                else
                    Plugin.Log.Warn("Current map was fixed.");
            }

            score.MapDifficultyId = currentMap.DifficultyId;
            score.Headset = (await store.GetCurrentUserAsync()).Headset;

            if (!disposing)
                return;

        onDisposed:
            Plugin.Log.Warn("Disposed before init task in score submission finished!");
            currentMap = null;
            return;
        }
        public async void Dispose()
        {
            disposing = true;

            if (subscribed)
                Unsubscribe();

            if (initTask is not null)
                await initTask;

            if (subscribed)
                Unsubscribe();

            if (currentMap is null)
                return;

            score.ModifierCodes = mods.ToModCodes(failed);

            score.TimeSet = DateTime.UtcNow;

            score.Score = sc.multipliedScore >= 0 ? (uint)(sc.multipliedScore * score.ModifierCodes.ModCodesToMultiplier()) : 0;
            score.ScoreNoMods = sc.multipliedScore >= 0 ? (uint)sc.multipliedScore : 0;

            score.MaxCombo = Math.Max(score.MaxCombo, combo);
            score.Streak115 = Math.Max(score.Streak115, current115Streak);

            bool shouldSubmit;

            lock (submitLock)
            {
                counterDisposed = true;
                shouldSubmit = MarkSubmitIfReady();
            }

            if (shouldSubmit)
                _ = SubmitScore();
        }
        private void Unsubscribe()
        {
            transition?.didFinishEvent -= OnTransitionSetupOnDidFinishEvent;
            sc.scoringForNoteFinishedEvent -= NoteScoring;
            bomb.noteWasCutEvent -= OnBombHit;
            wall.headDidEnterObstacleEvent -= OnWallHit;
            pause.didResumeEvent -= OnUnpause;
            energy?.gameEnergyDidReach0Event -= OnFail;

            subscribed = false;
        }

        private void NoteScoring(ScoringElement scoringElement)
        {
            if (disposing)
                return;

            NoteData currentNote = scoringElement.noteData;

            if (currentNote.gameplayType == NoteData.GameplayType.Bomb)
                return;

            NoteData.ScoringType st = currentNote.scoringType;

            if (st == NoteData.ScoringType.Ignore)
                return;

            notes++;

            if (st == NoteData.ScoringType.NoScore)
                return; // NoScore only appears on bombs, but this check is just to be on the safe side.

            bool miss = false;

            if (scoringElement is MissScoringElement)
            {
                score.Misses++;
                miss = true;
            }
            else if (scoringElement is BadCutScoringElement)
            {
                score.BadCuts++;
                miss = true;
            }

            if (miss)
            {
                ResetCombo();

                OnMistake?.Invoke();
                return;
            }
            else 
                combo++;

            if (scoringElement.cutScore != 115)
                ResetStreak();
            else
                current115Streak++;
        }
        private void OnBombHit(NoteController nc, in NoteCutInfo nci)
        {
            if (disposing || nc.noteData.gameplayType != NoteData.GameplayType.Bomb || AtEndsOfMap)
                return;

            ResetCombo();
            score.BombHits++;

            OnMistake?.Invoke();
        }
        private void OnWallHit(ObstacleController oc)
        {
            if (disposing || AtEndsOfMap)
                return;

            ResetCombo();
            score.WallHits++;

            OnMistake?.Invoke();
        }
        private void OnUnpause()
        {
            if (disposing || AtEndsOfMap)
                return;

            score.Pauses++;
        }
        private void OnFail()
        {
            failed = true;
        }
        private void OnTransitionSetupOnDidFinishEvent(StandardLevelScenesTransitionSetupDataSO data, LevelCompletionResults results)
        {
            score.UncompletedMap = results.levelEndAction != LevelCompletionResults.LevelEndAction.None || results.levelEndStateType != LevelCompletionResults.LevelEndStateType.Cleared;

            gamemode = data.gameMode;

            bool shouldSubmit;

            lock (submitLock)
            {
                transitionFinished = true;
                shouldSubmit = MarkSubmitIfReady();
            }

            if (shouldSubmit)
                _ = SubmitScore();
        }

        private void ResetCombo()
        {
            score.MaxCombo = Math.Max(score.MaxCombo, combo);
            combo = 0;

            ResetStreak();
        }
        private void ResetStreak()
        {
            if (current115Streak > 0)
                score.Streak115 = Math.Max(current115Streak, score.Streak115);

            current115Streak = 0;
        }

        private bool MarkSubmitIfReady()
        {
            if (!scoreSubmitStarted && counterDisposed && transitionFinished)
            {
                scoreSubmitStarted = true;
                return true;
            }

            return false;
        }
        private async Task SubmitScore()
        {
            try
            {
                const float completionPercent = 0.75f;
                const int minNotesInMap = 115;

                if (totalNotes < minNotesInMap || notes > totalNotes)
                {
                    Plugin.Log.Critical("There is an issue with this map and score submission! The note amounts do not align with expected bounds.");
                    return;
                }

                float completion = (float)notes / totalNotes;

                Plugin.Log.Debug($"{notes} / {totalNotes} note(s) handled. Player completed {completion * 100f:N2}% of the map.");

                Plugin.Log.Debug(JsonConvert.SerializeObject(score));

                if (completion < completionPercent)
                {
                    Plugin.Log.Debug($"No score submit, completion did not reach the threshold of {completionPercent * 100f:N2}%.");
                    return;
                }

                if (gamemode is null || !AllowedModes.Contains(gamemode))
                {
                    Plugin.Log.Debug($"The gamemode played is not an allowed mode (mode = {gamemode ?? "null"})");
                    return;
                }

                if (!SubmissionPatch.Submit)
                {
                    Plugin.Log.Debug("No score submit: " + SubmissionPatch.GetSubmitReason());
                    return;
                }

                if (currentMap is null)
                {
                    Plugin.Log.Critical("There is an issue with this map and score submission! The current map is null.");
                    return;
                }

                if (score.Score == 0)
                {
                    Plugin.Log.Debug("No score submit: The score was 0.");
                    return;
                }

                bool mapIncomplete = score.UncompletedMap!.Value;

                if (!config.SubmitOnIncompletePlay && mapIncomplete)
                {
                    Plugin.Log.Info("No score submit: Incomplete score submission has been disabled.");
                    return;
                }

                bool scoreBeaten = false;

                if (!mapIncomplete)
                    scoreBeaten = aslvc.LoadUntilNextRefreshIfScoreBeaten((int)score.Score, overridePlayerScore: true, TimeSpan.FromSeconds(7));

                score.BeatPreviousScore = scoreBeaten;

                bool submitted = await submission.AttemptSubmitScore(score);
                SerializationHandler.LastScoreTime = DateTime.UtcNow;

                OnScoreSubmitCall?.Invoke(score, scoreBeaten, submitted);

                if (!submitted && ScoreSubmissionHandler.CheckFullSubmissionFailure(score))
                    aslvc.ForceShowLeaderboard();
            }
            catch (Exception e)
            {
                Plugin.Log.Error("There was an error submitting a score!!!\n" + e);
            }
        }
    }
}
