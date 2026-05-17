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
        private readonly object locker = new();

        /// <summary>
        /// Enforces the throttling policy for a single call.
        /// </summary>
        /// <returns>
        /// A <see cref="Task"/> that completes when any necessary throttling delay has finished.
        /// Callers should await this task before performing the rate-limited operation.
        /// </returns>
        /// <remarks>
        /// - If the current cycle has expired (elapsed >= <see cref="CycleLength"/> seconds),
        ///   the cycle counters are reset immediately.
        /// - If the call would exceed the allowed calls in the current cycle, the method computes
        ///   the remaining milliseconds in the cycle, performs a blocking wait while holding the
        ///   internal lock to update cycle state, then performs an equivalent asynchronous delay
        ///   after releasing the lock and logs the throttling duration via <c>Plugin.Log.Info</c>.
        /// - Because of the synchronous sleep inside the lock followed by an awaiting <see cref="Task.Delay(int)"/>,
        ///   the caller will experience both a blocking and an asynchronous wait equal to the computed rest time.
        /// </remarks>
        public async Task Call()
        {
            int restTime = 0;
            lock (locker)
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
                    restTime = (int)(CycleLength * 1000 - diff.TotalMilliseconds);
                    Thread.Sleep(restTime);
                    CallsThisCycle = 1;
                    CycleStartTime = DateTime.UtcNow.AddMilliseconds(restTime);
                }
            }
            if (restTime > 0)
            {
                Plugin.Log.Info("Throttling calls for " + restTime + "ms.");
                await Task.Delay(restTime);
            }
        }
    }
}
