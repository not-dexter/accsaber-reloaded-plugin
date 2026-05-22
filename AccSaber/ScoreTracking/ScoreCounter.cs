using AccSaber.API;
using AccSaber.Managers;
using AccSaber.Models;
using AccSaber.Patches;
using AccSaber.Utils;
using System;
using System.Linq;
using UnityEngine;
using Zenject;

namespace AccSaber.ScoreTracking
{
    internal class ScoreCounter : IInitializable, IDisposable
    {
        [Inject] private readonly IReadonlyBeatmapData beatmapData = null!;

        [Inject] private readonly GameplayModifiers mods = null!;
        [Inject] private readonly ScoreController sc = null!;
        [Inject] private readonly BeatmapObjectManager bomb = null!;
        [Inject] private readonly PlayerHeadAndObstacleInteraction wall = null!;
        [Inject] private readonly PauseController pause = null!;
        private StandardLevelScenesTransitionSetupDataSO transition = null!;
        private GameEnergyCounter? energy = null;
        private AccSaberStore? store = null;


        private AccSaberScore score = null!;

        private int current115Streak, combo, notes, totalNotes;
        private readonly object submitLock = new();
        private bool transitionFinished, counterDisposed, failed;

        public void Initialize()
        {
            SubmissionPatch.EnableSubmissions();

            transitionFinished = false;
            counterDisposed = false;
            failed = false;

            transition = Resources.FindObjectsOfTypeAll<StandardLevelScenesTransitionSetupDataSO>().FirstOrDefault();
            store ??= Plugin.Container.TryResolve<AccSaberStore>();

            if (mods.noFailOn0Energy)
                energy = Resources.FindObjectsOfTypeAll<GameEnergyCounter>().LastOrDefault(x => x.isActiveAndEnabled);

            if (transition.practiceSettings is not null){
                SubmissionPatch.SetPracticeSubmission();
                Plugin.Log.Info($"Practice mode: start time = {transition.practiceSettings.startSongTime}, speed mult = {transition.practiceSettings.songSpeedMul}");
            }

            //Plugin.Log.Info($"current map null? {store.CurrentRankedMap is null}");
            if (store.CurrentRankedMap is null)
                return;

            score = new()
            {
                MapDifficultyId = store.CurrentRankedMap!.DifficultyId,
                Headset = store.GetCurrentUserAsync().GetAwaiter().GetResult().Headset,
            };

            current115Streak = 0;
            combo = 0;
            notes = 0;

            transition.didFinishEvent += OnTransitionSetupOnDidFinishEvent;
            sc.scoringForNoteFinishedEvent += NoteScoring;
            bomb.noteWasCutEvent += OnBombHit;
            wall.headDidEnterObstacleEvent += OnWallHit;
            pause.didPauseEvent += OnPause;
            energy?.gameEnergyDidReach0Event += OnFail;
        }
        public void Dispose()
        {
            if (store?.CurrentRankedMap is null)
                return;

            transition.didFinishEvent -= OnTransitionSetupOnDidFinishEvent;
            sc.scoringForNoteFinishedEvent -= NoteScoring;
            bomb.noteWasCutEvent -= OnBombHit;
            wall.headDidEnterObstacleEvent -= OnWallHit;
            pause.didPauseEvent -= OnPause;
            energy?.gameEnergyDidReach0Event -= OnFail;

            score.ModifierCodes = mods.ToModCodes(failed);

            totalNotes = beatmapData.GetBeatmapDataItems<NoteData>(0).Count(noteData => noteData.gameplayType != NoteData.GameplayType.Bomb);

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
            if (nc.noteData.gameplayType != NoteData.GameplayType.Bomb)
                return;

            combo = 0;
            score.BombHits++;
        }
        private void OnWallHit(ObstacleController oc)
        {
            combo = 0;
            score.WallHits++;
        }
        private void OnPause()
        {
            score.Pauses++;
        }
        private void OnFail()
        {
            failed = true;
        }
        private void OnTransitionSetupOnDidFinishEvent(StandardLevelScenesTransitionSetupDataSO data, LevelCompletionResults results)
        {
            score.UncompletedMap = results.levelEndAction != LevelCompletionResults.LevelEndAction.None || results.levelEndStateType != LevelCompletionResults.LevelEndStateType.Cleared;

            lock (submitLock)
            {
                transitionFinished = true;
                if (counterDisposed)
                    SubmitScore();
            }
        }
        private async void SubmitScore()
        {
            float completion = (float)notes / totalNotes;

            Plugin.Log.Info($"{notes} / {totalNotes} note(s) handled. Player completed {completion * 100f:N2}% of the map.");

            if (completion >= 0.75f && SubmissionPatch.Submit)
                await AccsaberAPI.SubmitScore(score);
            else
                Plugin.Log.Info("No score submit: " + SubmissionPatch.GetSubmitReason());
        }
    }
}
