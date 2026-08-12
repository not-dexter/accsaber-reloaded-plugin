//#define TEST_SUBMISSION

using AccSaber.API;
using AccSaber.Models;
using AccSaber.Utils;
using IPA.Loader;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Zenject;

namespace AccSaber.ScoreTracking
{
    using System.Collections.Concurrent;

    internal class ScoreSubmissionHandler : IInitializable, IDisposable
    {
        private static readonly TimeSpan WaitDelayThreshold = TimeSpan.FromMilliseconds(500);

#if TEST_SUBMISSION
        // Fake values for faster testing.
        private static readonly Throttler SubmitThrottler = new(1, 8);
        private static readonly TimeSpan LoopDelayThreshold = TimeSpan.FromMinutes(0.1);


        [Inject] private readonly Utils.Misc.SerializationHandler handler = null!;
#else
        private static readonly Throttler SubmitThrottler = new(1, 60);
        private static readonly TimeSpan LoopDelayThreshold = TimeSpan.FromMinutes(5);
#endif

        private readonly ConcurrentQueue<AccSaberScore> ScoresToSubmit = new();

        private readonly object activeSubmissionTasksLock = new();
        private readonly HashSet<Task> activeSubmissionTasks = [];

        private DateTime lastChecked = DateTime.UtcNow;

        private CancellationTokenSource? loopCancelToken;
        private Task submissionsLoopTask = Task.CompletedTask;

        private volatile bool isDisposingOrDisposed;

        public void Initialize()
        {
            loopCancelToken = new CancellationTokenSource();

            submissionsLoopTask = ClearFailedSubmissionsLoop(loopCancelToken.Token);

#if TEST_SUBMISSION
            TestSubmission();
#endif
        }

        public void Dispose()
        {
            isDisposingOrDisposed = true;

            CancellationTokenSource? cts = loopCancelToken;

            if (cts is not null)
            {
                try
                {
                    cts.Cancel();

                    try
                    {
                        submissionsLoopTask.GetAwaiter().GetResult();
                    }
                    catch (OperationCanceledException) when (cts.IsCancellationRequested)
                    {
                        // Expected during disposal.
                    }

                    WaitForActiveSubmissions();
                }
                finally
                {
                    cts.Dispose();
                }
            }

            LogRemainingQueuedScores();
        }

        private void WaitForActiveSubmissions()
        {
            while (true)
            {
                Task[] tasks;

                lock (activeSubmissionTasksLock)
                    tasks = [.. activeSubmissionTasks];

                if (tasks.Length == 0)
                    return;

                try
                {
                    Task.WhenAll(tasks).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    // Expected if a submission was cancelled during disposal.
                }
                catch (Exception e)
                {
                    Plugin.Log.Error("There was an error while waiting for active score submissions to finish!\n" + e);
                }
            }
        }

        private async Task<bool> TrackSubmissionTask(Task<bool> task)
        {
            lock (activeSubmissionTasksLock)
                activeSubmissionTasks.Add(task);

            try
            {
                return await task.ConfigureAwait(false);
            }
            finally
            {
                lock (activeSubmissionTasksLock)
                    activeSubmissionTasks.Remove(task);
            }
        }

        private void RequeueScore(AccSaberScore score)
        {
            ScoresToSubmit.Enqueue(score);
        }

        private void LogRemainingQueuedScores()
        {
            if (ScoresToSubmit.IsEmpty)
                return;

            Plugin.Log.Warn(
                "There are still failed submissions left!!!\n" +
                "Please send this log to a developer to get these scores submitted.");

            while (ScoresToSubmit.TryDequeue(out AccSaberScore score))
                Plugin.Log.Debug(JsonConvert.SerializeObject(score));
        }

#if TEST_SUBMISSION
        private async void TestSubmission()
        {
            await handler.InitTask.ConfigureAwait(false);

            AccSaberScore[] scores = new AccSaberScore[10];

            Random r = new();

            Guid[] ids = [.. handler.CachedDifficulties.Select(diff => diff.DifficultyId)];

            for (int i = 0; i < scores.Length; i++)
            {
                scores[i] = new AccSaberScore
                {
                    MapDifficultyId = ids[r.Next(0, ids.Length)],
                    Headset = "Unknown"
                };
            }

            foreach (AccSaberScore score in scores)
                await AttemptSubmitScore(score).ConfigureAwait(false);
        }
#endif

        public static bool CheckFullSubmissionFailure(AccSaberScore score) =>
            (score.UncompletedMap ?? true) ||
            !score.BeatPreviousScore ||
            !PluginManager.EnabledPlugins.Any(plugin =>
                plugin.Id.Equals("BeatLeader") || plugin.Id.Equals("ScoreSaber"));

        public async Task<bool> AttemptSubmitScore(AccSaberScore score)
        {
            if (isDisposingOrDisposed)
            {
                RequeueScore(score);
                return false;
            }

            CancellationToken ct = loopCancelToken?.Token ?? CancellationToken.None;

            TimeSpan wait = SubmitThrottler.EstimatedWaitTime(extraVirtualScores: 1);

#if TEST_SUBMISSION
            Plugin.Log.Info($"Current wait: {wait}");
#endif

            if (wait < WaitDelayThreshold)
                return await TrackSubmissionTask(SubmitScore(score, false, ct)).ConfigureAwait(false);

            Plugin.Log.Warn(
                $"The current score could not be submitted as the wait was longer than {WaitDelayThreshold.TotalMilliseconds:N0}ms.\n" +
                "Estimated wait time: " + wait);

            RequeueScore(score);

            // Not actually a submission failure, but the caller needs to treat it as not submitted.
            return false;
        }

        private async Task<bool> SubmitScore(AccSaberScore score, bool silent = true, CancellationToken ct = default)
        {
            try
            {
                ct.ThrowIfCancellationRequested();

                score.Nonce = MiscUtils.GenerateNonce(64);

                using HttpRequestMessage request = new(HttpMethod.Post, HelpfulPaths.APAPI_SCORE_SUBMIT)
                {
                    Content = new StringContent(
                        JsonConvert.SerializeObject(score),
                        System.Text.Encoding.UTF8,
                        "application/json")
                };

#if TEST_SUBMISSION
                await SubmitThrottler.Call(ct).ConfigureAwait(false);
                bool success = true;
#else
            var (success, _) = await APIHandler.CallAPI(request, SubmitThrottler, maxRetries: 1, ct: ct).ConfigureAwait(false);
#endif

                if (success)
                {
                    if (!silent)
                        Plugin.Log.Info("Score submitted!");

                    return true;
                }

                if (!silent)
                    Plugin.Log.Info("Score failed to submit.");

                // If the API/throttler returned failure because disposal cancellation happened,
                // preserve the score so Dispose() can print it.
                if (ct.IsCancellationRequested)
                {
                    RequeueScore(score);
                    return false;
                }

                if (CheckFullSubmissionFailure(score))
                {
                    Plugin.Log.Warn("The score that failed to submit will not be submitted any other way. Will wait a bit before trying again.");

                    RequeueScore(score);
                }

                return false;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Important: requeue on cancellation so Dispose() can print the score.
                RequeueScore(score);
                return false;
            }
            catch (Exception e)
            {
                Plugin.Log.Error("There was an error on score submission!\n" + e);
                return false;
            }
        }

        private async Task ClearFailedSubmissions(CancellationToken ct)
        {
            lastChecked = DateTime.UtcNow;

            List<Task<bool>> tasks = [with(ScoresToSubmit.Count)];

            Plugin.Log.Info("Attempting to submit a previously failed score(s)...");

            while (!ct.IsCancellationRequested && ScoresToSubmit.TryDequeue(out AccSaberScore score))
            {
                Plugin.Log.Debug(JsonConvert.SerializeObject(score));

                tasks.Add(TrackSubmissionTask(SubmitScore(score, false, ct)));
            }

            if (tasks.Count == 0)
                return;

            Plugin.Log.Info($"Current wait time to submit: {SubmitThrottler.EstimatedWaitTime()}");

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private async Task ClearFailedSubmissionsLoop(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    TimeSpan timeSinceUpdate = DateTime.UtcNow - lastChecked;

                    if (timeSinceUpdate < LoopDelayThreshold)
                    {
                        TimeSpan delay = LoopDelayThreshold - timeSinceUpdate;
                        await Task.Delay(delay, ct).ConfigureAwait(false);
                    }

                    if (!ct.IsCancellationRequested)
                        await ClearFailedSubmissions(ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Expected during disposal.
            }
            catch (Exception e)
            {
                Plugin.Log.Error("There was an error with the failed submissions loop!\n" + e);
            }
        }
    }
}
