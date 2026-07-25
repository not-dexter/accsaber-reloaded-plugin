using AccSaber.API;
using AccSaber.Managers;
using AccSaber.Models;
using AccSaber.Patches;
using AccSaber.UI.MenuButton.Campaigns.ViewControllers;
using AccSaber.UI.ViewControllers;
using AccSaber.Utils;
using AccSaber.Utils.Misc;
using IPA.Loader;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

namespace AccSaber.ScoreTracking
{
    internal class ScoreCounter : IInitializable, IDisposable
    {
        public static event Action<AccSaberScore, bool>? OnScoreSubmit;

        [Inject] private readonly IReadonlyBeatmapData beatmapData = null!;

        [Inject] private readonly GameplayModifiers mods = null!;
        [Inject] private readonly ScoreController sc = null!;
        [Inject] private readonly BeatmapObjectManager bomb = null!;
        [Inject] private readonly PlayerHeadAndObstacleInteraction wall = null!;
        [Inject] private readonly PauseController pause = null!;
        private StandardLevelScenesTransitionSetupDataSO transition = null!;
        private GameEnergyCounter? energy = null;
        private AccSaberStore? store = null;

        private static readonly HashSet<string> AllowedModes = [ "Solo", "Multiplayer" ];

        private AccSaberScore score = null!;
        private AccSaberBasicDifficulty? currentMap;
        private AccsaberAPI api = null!;
        private AccSaberLeaderboardViewController aslvc = null!;
        private Configuration.PluginConfig config = null!;

        private int current115Streak, combo, notes, totalNotes;
        private readonly object submitLock = new();
        private bool transitionFinished, counterDisposed, failed;
        private string? gamemode = null;

        private bool AtEndsOfMap => notes == 0 || notes == totalNotes;

        public async void Initialize()
        {
            SubmissionPatch.EnableSubmissions();

            transitionFinished = false;
            counterDisposed = false;
            failed = false;

            transition = Resources.FindObjectsOfTypeAll<StandardLevelScenesTransitionSetupDataSO>().FirstOrDefault();
            store ??= Plugin.Container.TryResolve<AccSaberStore>();
            api ??= Plugin.Container.TryResolve<AccsaberAPI>();
            aslvc ??= Plugin.Container.TryResolve<AccSaberLeaderboardViewController>();
            config ??= Plugin.Container.TryResolve<Configuration.PluginConfig>();


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

            currentMap = store.CurrentRankedMap ?? await store.GetCurrentMap();

            if (currentMap is null)
            {
                Plugin.Log.Warn("Somehow the current map is null after null checks happened. This is a bug.");
                return;
            }

            totalNotes = beatmapData.GetBeatmapDataItems<NoteData>(0).Count(noteData => noteData.gameplayType != NoteData.GameplayType.Bomb);

#if NEW_VERSION
            bool IsInvalidLevel() => 
                !transition.beatmapLevel.levelID.Equals(currentMap.ParentInfo?.Hash) || transition.beatmapKey.difficulty != currentMap.Difficulty;
#else
            bool IsInvalidLevel() => 
                !transition.difficultyBeatmap.level.levelID.Equals(currentMap.ParentInfo?.Hash) || transition.difficultyBeatmap.difficulty != currentMap.Difficulty;
#endif

            if (IsInvalidLevel())
            {
                Plugin.Log.Critical("What?? The current map is not equal to the recorded map!!! Attempting to recorrect...");

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
                    }
                }

                if (currentMap is null || IsInvalidLevel())
                {
                    Plugin.Log.Critical("Current map was still not updated correctly, score submission disabled for this map.");
                    return;
                }
                else
                    Plugin.Log.Warn("Current map was fixed.");
            }

            score = new()
            {
                MapDifficultyId = currentMap.DifficultyId,
                Headset = (await store.GetCurrentUserAsync()).Headset
            };

            current115Streak = 0;
            combo = 0;
            notes = 0;

            transition.didFinishEvent += OnTransitionSetupOnDidFinishEvent;
            sc.scoringForNoteFinishedEvent += NoteScoring;
            bomb.noteWasCutEvent += OnBombHit;
            wall.headDidEnterObstacleEvent += OnWallHit;
            pause.didResumeEvent += OnUnpause;
            energy?.gameEnergyDidReach0Event += OnFail;
        }
        public void Dispose()
        {
            if (currentMap is null)
                return;

            transition.didFinishEvent -= OnTransitionSetupOnDidFinishEvent;
            sc.scoringForNoteFinishedEvent -= NoteScoring;
            bomb.noteWasCutEvent -= OnBombHit;
            wall.headDidEnterObstacleEvent -= OnWallHit;
            pause.didResumeEvent -= OnUnpause;
            energy?.gameEnergyDidReach0Event -= OnFail;

            score.ModifierCodes = mods.ToModCodes(failed);

            score.TimeSet = DateTime.UtcNow;

            score.Score = sc.multipliedScore >= 0 ? (uint)(sc.multipliedScore * score.ModifierCodes.ModCodesToMultiplier()) : 0;
            score.ScoreNoMods = sc.multipliedScore >= 0 ? (uint)sc.multipliedScore : 0;

            score.MaxCombo = Math.Max(score.MaxCombo, combo);

            lock (submitLock)
            {
                counterDisposed = true;
                if (transitionFinished)
                    SubmitScore();
            }
        }

        private void NoteScoring(ScoringElement scoringElement)
        {
            NoteData currentNote = scoringElement.noteData;

            if (currentNote.gameplayType == NoteData.GameplayType.Bomb)
                return;

            NoteData.ScoringType st = currentNote.scoringType;

            if (st == NoteData.ScoringType.Ignore)
                return;

            notes++;

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

            if (st == NoteData.ScoringType.NoScore || miss)
            {
                score.MaxCombo = Math.Max(score.MaxCombo, combo);
                combo = 0;
                current115Streak = 0;
                return;
            }
            else combo++;

            if (scoringElement.cutScore != 115)
            {
                if (current115Streak > 0)
                {
                    score.Streak115 = Math.Max(current115Streak, score.Streak115);
                    current115Streak = 0;
                }
            }
            else current115Streak++;
        }
        private void OnBombHit(NoteController nc, in NoteCutInfo nci)
        {
            if (nc.noteData.gameplayType != NoteData.GameplayType.Bomb || AtEndsOfMap)
                return;

            combo = 0;
            score.BombHits++;
        }
        private void OnWallHit(ObstacleController oc)
        {
            if (AtEndsOfMap)
                return;

            combo = 0;
            score.WallHits++;
        }
        private void OnUnpause()
        {
            if (AtEndsOfMap)
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

            lock (submitLock)
            {
                transitionFinished = true;
                if (counterDisposed)
                    SubmitScore();
            }
        }
        private async void SubmitScore()
        {
            try
            {
                const float completionPercent = 0.75f;

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

                if (totalNotes < 115 || notes > totalNotes)
                {
                    Plugin.Log.Critical("There is an issue with this map and score submission! The note amounts do not align with expected bounds.");
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
                    scoreBeaten = aslvc.LoadUntilNextRefreshIfScoreBeaten((int)score.Score, true, TimeSpan.FromSeconds(7));

                bool submitted = await api.SubmitScore(score);
                SerializationHandler.LastScoreTime = DateTime.UtcNow;

                OnScoreSubmit?.Invoke(score, scoreBeaten);

                if (!submitted && !PluginManager.EnabledPlugins.Any(plugin => plugin.Id.Equals("BeatLeader") || plugin.Id.Equals("ScoreSaber")))
                    aslvc.ForceShowLeaderboard();
            }
            catch (Exception e)
            {
                Plugin.Log.Error("There was an error submitting a score!!!\n" + e);
            }
        }
    }
}
