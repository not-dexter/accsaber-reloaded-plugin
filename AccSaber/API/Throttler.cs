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
        public int CallsPerCycle { get; } = callsPerCycle;

        /// <summary>
        /// Cycle length in seconds.
        /// </summary>
        public int CycleLength { get; } = cycleLength;

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
            AsyncLock.Releaser releaser = await locker.LockAsync(ct);
            using (releaser)
            {
                DateTime now = DateTime.UtcNow;
                TimeSpan diff = now - CycleStartTime;

                if (diff.TotalSeconds >= CycleLength)
                {
                    CallsThisCycle = 0;
                    CycleStartTime = now;
                }

                CallsThisCycle++;

                if (CallsThisCycle > CallsPerCycle)
                {
                    int restTime = (int)(CycleLength * 1000 - diff.TotalMilliseconds);

                    CallsThisCycle = 1;
                    CycleStartTime = now.AddMilliseconds(restTime);

                    Plugin.Log.Info("Throttling calls for " + restTime + "ms.");
                    await Task.Delay(restTime, ct);
                }

                ct.ThrowIfCancellationRequested();
            }
        }

        /// <summary>
        /// Estimates how long a caller would need to wait before it can start an operation
        /// guarded by this <see cref="Throttler"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="TimeSpan"/> representing an estimated wait time. Returns <see cref="TimeSpan.Zero"/>
        /// when there is no contention on the internal lock.
        /// </returns>
        /// <remarks>
        /// - This method inspects the internal <see cref="AsyncLock"/> state (via <c>locker</c>) and the
        ///   current cycle bookkeeping to produce a conservative estimate of the wait time.
        /// - The calculation uses <c>locker.LineLength + 1</c> to account for the queued waiters plus the
        ///   thread currently holding the lock. It then determines how many full cycles of length
        ///   <see cref="CycleLength"/> seconds are required to satisfy the outstanding callers given
        ///   the configured <see cref="CallsPerCycle"/>.
        /// - If <c>CycleStartTime</c> has been moved into the future (happens when callers were previously
        ///   throttled), the remaining time until that start is added to the estimate.
        /// - The returned value is an estimate only: it does not block, observe cancellation, nor guarantee
        ///   the exact delay that will be experienced by a real caller.
        /// - This method is safe to call concurrently but reads internal state without acquiring the lock,
        ///   so the result may be transient.
        /// </remarks>
        public TimeSpan EstimatedWaitTime()
        {
            int currentCalls = CallsThisCycle, lineLength = locker.LineLength;

            if (!locker.IsLocked && currentCalls != CallsPerCycle)
                return TimeSpan.Zero;

            DateTime now = DateTime.UtcNow, cycle = CycleStartTime;

            int fullCycles = (lineLength + currentCalls) / CallsPerCycle;
            TimeSpan currentDelay = TimeSpan.FromSeconds(CycleLength) * fullCycles;

            if (cycle > now)
                currentDelay += CycleStartTime - now;

            return currentDelay;
        }
    }
}
