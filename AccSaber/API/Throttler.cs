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
            await CallInternal(releaser, ct);
        }

        /// <summary>
        /// Attempts to acquire permission to start an operation under the current throttling policy without blocking.
        /// </summary>
        /// <param name="ct">
        /// A <see cref="CancellationToken"/> forwarded to any asynchronous delay performed when throttling is required.
        /// If cancellation occurs while waiting, the returned task will complete in a canceled state and may throw
        /// an <see cref="OperationCanceledException"/>.
        /// </param>
        /// <returns>
        /// A <see cref="Task{Boolean}"/> that completes with <c>true</c> if the caller obtained permission to proceed.
        /// Returns <c>false</c> if the call would exceed the configured <see cref="CallsPerCycle"/> and the method
        /// could not obtain the internal lock immediately.
        /// </returns>
        /// <remarks>
        /// - This method performs a fast, non-blocking pre-check to avoid acquiring the lock when the current cycle
        ///   already contains exactly <see cref="CallsPerCycle"/> calls.
        /// - The method then attempts to obtain the internal <see cref="AsyncLock"/> without awaiting; if the lock is
        ///   unavailable, the method returns <c>false</c> rather than waiting for it.
        /// - When the lock is obtained the call is forwarded to <see cref="CallInternal(AsyncLock.Releaser, CancellationToken)"/>,
        ///   which performs the cycle bookkeeping and any necessary throttling delay. If that call completes successfully,
        ///   this method returns <c>true</c>.
        /// - Cancellation is only observed during any asynchronous delay performed by <see cref="CallInternal"/>.
        /// </remarks>
        public async Task<bool> TryCall(CancellationToken ct = default)
        {
            if (CallsThisCycle == CallsPerCycle && (DateTime.UtcNow - CycleStartTime).TotalSeconds < CycleLength)
                return false; // Quick check to avoid locking if we already know we're at the limit.

            AsyncLock.Releaser? releaser = await locker.TryLockAsync();

            if (releaser is null)
                return false;

            await CallInternal(releaser.Value, ct);
            return true;
        }

        /// <summary>
        /// Internal implementation that performs the cycle bookkeeping and enforces throttling while holding the provided lock.
        /// </summary>
        /// <param name="releaser">
        /// The <see cref="AsyncLock.Releaser"/> obtained from <see cref="locker"/>. This method takes ownership and will dispose it
        /// (release the lock) when finished.
        /// </param>
        /// <param name="ct">
        /// A <see cref="CancellationToken"/> forwarded to any asynchronous delay performed when throttling is required.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> that completes once the internal bookkeeping (and any throttling delay) completes.
        /// </returns>
        /// <remarks>
        /// - The method assumes the caller provided a valid <paramref name="releaser"/> that holds <see cref="locker"/>.
        /// - While holding the lock the method:
        ///     1. Computes whether the current cycle has expired and resets counters if needed.
        ///     2. Increments <see cref="CallsThisCycle"/> and, if the incremented value exceeds <see cref="CallsPerCycle"/>,
        ///        computes the remaining milliseconds in the current cycle and schedules a delay for that duration.
        /// - Before awaiting the delay the method updates cycle state so other callers see the expected start time for the next cycle.
        /// - The provided <paramref name="releaser"/> is disposed at the end of the method scope, releasing the lock.
        /// - Cancellation via <paramref name="ct"/> is observed during the asynchronous <see cref="Task.Delay"/> and will cause an
        ///   <see cref="OperationCanceledException"/> to be thrown if cancellation occurs while waiting.
        /// </remarks>
        private async Task CallInternal(AsyncLock.Releaser releaser, CancellationToken ct)
        {
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
        /// Estimates how long a caller would need to wait before their operation can start given the current
        /// throttler state and an optional number of additional "virtual" calls that are expected to be queued.
        /// </summary>
        /// <param name="extraVirtualScores">
        /// Optional count of additional pending calls to include in the estimate (defaults to 0). These are not
        ///  yet reflected in <see cref="CallsThisCycle"/> but should be considered for a projected wait time.
        /// </param>
        /// <returns>
        /// A <see cref="TimeSpan"/> representing the estimated wait time. Returns <see cref="TimeSpan.Zero"/>
        /// when the call would be allowed immediately.
        /// </returns>
        /// <remarks>
        /// - The calculation is non-destructive: it does not modify the throttler's internal state.
        /// - The estimate combines:
        ///     1) the current number of calls in the active cycle (<see cref="CallsThisCycle"/>),
        ///     2) the number of waiters currently queued on the internal <see cref="AsyncLock"/> (via <c>locker.LineLength</c>),
        ///     3) the supplied <paramref name="extraVirtualScores"/>.
        /// - If the lock is free and the projected total fits within <see cref="CallsPerCycle"/>, the method returns <see cref="TimeSpan.Zero"/>.
        /// - Otherwise the method computes how many full cycles are required to accommodate the projected calls,
        ///   adds the remaining time in the current cycle (if any), and returns the summed delay.
        /// - Use this for UI/monitoring to show expected delays; it is an approximation and may differ from the actual
        ///   delay experienced by a caller due to concurrent activity.
        /// </remarks>
        public TimeSpan EstimatedWaitTime(int extraVirtualScores = 0) 
        {
            int currentCalls = CallsThisCycle, lineLength = locker.LineLength + extraVirtualScores;
            int totalCalls = currentCalls + lineLength;

            if (!locker.IsLocked && totalCalls <= CallsPerCycle)
                return TimeSpan.Zero;

            DateTime now = DateTime.UtcNow, cycle = CycleStartTime;

            int fullCycles = (totalCalls - 1) / CallsPerCycle;
            TimeSpan currentDelay = TimeSpan.FromSeconds(CycleLength * (fullCycles - 1)); // full cycles minus one as the first cycle may not be a full cycle.

            if (cycle > now)
                currentDelay += cycle - now;
            else if (fullCycles > 0)
            {
                TimeSpan periodTimeElapsed = now - cycle;
                if (periodTimeElapsed.TotalSeconds < CycleLength)
                    currentDelay += TimeSpan.FromSeconds(CycleLength) - periodTimeElapsed;
            }

            return currentDelay;
        }
    }
}
