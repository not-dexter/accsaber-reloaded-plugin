using AccSaber.Utils.Misc;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AccSaber.API
{
    /// <summary>
    /// Simple rate limiter that restricts how many times an operation can be started within a rolling cycle.
    /// </summary>
    /// <param name="callsPerCycle">Maximum allowed calls inside a single cycle.</param>
    /// <param name="cycleLength">Length of a cycle in seconds.</param>
    /// <remarks>
    /// - This class is thread-safe for concurrent callers of <see cref="Call"/>.
    /// - It uses an internal lock to serialize access to the cycle bookkeeping.
    /// - When the number of attempts exceeds <see cref="CallsPerCycle"/> inside the current cycle,
    ///   the implementation computes a remaining time to the end of the cycle and waits.
    /// - Note: This implementation performs a blocking <see cref="Thread.Sleep(int)"/> while holding
    ///   the lock and then performs an asynchronous <see cref="Task.Delay(int)"/> of the same duration
    ///   after the lock is released. This mirrors the upstream source and results in both a synchronous
    ///   block and an awaited delay of the computed duration.
    /// </remarks>
    internal class Throttler(int callsPerCycle, int cycleLength) //Straight from here: https://github.com/IMightBeeAPerson/BLPPCounter/blob/master/BLPPCounter/Utils/API%20Handlers/Throttler.cs
    {
        /// <summary>
        /// Maximum allowed calls per cycle.
        /// </summary>
        public int CallsPerCycle { get; private set; } = callsPerCycle;

        /// <summary>
        /// Cycle length in seconds.
        /// </summary>
        public int CycleLength { get; private set; } = cycleLength;

        ///<summary>Time when the current cycle started (UTC).</summary>
        private DateTime CycleStartTime = DateTime.UtcNow;

        ///<summary>Number of calls that have occurred in the current cycle.</summary>
        private int CallsThisCycle = 0;

        ///<summary>Lock to protect cycle state for concurrent callers.</summary>
        private readonly AsyncLock locker = new();

        /// <summary>
        /// Rate-limits callers so that at most <see cref="CallsPerCycle"/> operations may start within a rolling
        /// <see cref="CycleLength"/>-second window.
        /// </summary>
        /// <param name="ct">
        /// A <see cref="CancellationToken"/> which, when cancelled, will cancel the throttling delay. If cancellation
        /// occurs during the throttling wait, the returned task will complete with an <see cref="OperationCanceledException"/>.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> that completes immediately when no throttling is required, or completes after the computed
        /// throttling delay when the rate limit has been exceeded.
        /// </returns>
        /// <remarks>
        /// - This method is safe for concurrent callers: internal cycle bookkeeping is protected by a lock.
        /// - The method increments the current-cycle call counter and, if the counter exceeds <see cref="CallsPerCycle"/>,
        ///   computes the remaining milliseconds in the cycle, updates cycle state, logs the throttling, and awaits
        ///   a <see cref="Task.Delay"/> for that duration.
        /// - Cancellation is observed only during the asynchronous delay; callers that are cancelled during the wait will
        ///   receive an <see cref="OperationCanceledException"/>.
        /// </remarks>
        /// <exception cref="TaskCanceledException">
        /// This is thrown if the <see cref="CancellationToken"/> is cancelled before the method has finished execution.
        /// </exception>
        public async Task Call(CancellationToken ct = default)
        {
            AsyncLock.Releaser releaser = await locker.LockAsync();
            using (releaser)
            {
                TimeSpan diff = DateTime.UtcNow - CycleStartTime;

                if (diff.TotalSeconds >= CycleLength)
                {
                    CallsThisCycle = 0;
                    CycleStartTime = DateTime.UtcNow;
                }

                CallsThisCycle++;

                if (CallsThisCycle > CallsPerCycle)
                {
                    int restTime = (int)(CycleLength * 1000 - diff.TotalMilliseconds);

                    Plugin.Log.Info("Throttling calls for " + restTime + "ms.");
                    await Task.Delay(restTime, ct);

                    CallsThisCycle = 1;
                    CycleStartTime = DateTime.UtcNow.AddMilliseconds(restTime);
                }

                ct.ThrowIfCancellationRequested();
            }
        }
    }
}
