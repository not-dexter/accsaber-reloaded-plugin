using System;
using System.Threading;
using System.Threading.Tasks;

namespace AccSaber.Utils.Misc
{     
    /// <summary>
    /// Lightweight asynchronous lock built on <see cref="SemaphoreSlim"/>.
    /// </summary>
    /// <remarks>
    /// Use this type to provide mutual-exclusion for asynchronous code paths. Acquire the lock
    /// by awaiting <see cref="LockAsync(CancellationToken)"/> and disposing the returned
    /// <see cref="Releaser"/> (for example via a using statement). The implementation is intentionally
    /// simple and exposes a non-blocking try-acquire method as well as small diagnostics.
    /// 
    /// Example:
    /// <code language="csharp">
    /// var releaser = await asyncLock.LockAsync(); // or: using (await asyncLock.LockAsync()) { ... }
    /// using (releaser) { /* protected section */ }
    /// </code>
    /// </remarks>
    public class AsyncLock
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private int _lineLength = 0;

        /// <summary>
        /// Acquires the lock asynchronously. The returned <see cref="Releaser"/> releases the lock when disposed.
        /// </summary>
        /// <param name="ct">An optional <see cref="CancellationToken"/> to cancel waiting for the lock.</param>
        /// <returns>
        /// A <see cref="Task{Releaser}"/> that completes when the lock is acquired. Dispose the returned
        /// <see cref="Releaser"/> to release the lock.
        /// </returns>
        /// <remarks>
        /// Callers are expected to use the releaser with a using statement, e.g.:
        /// <code language="csharp">
        /// using (await asyncLock.LockAsync()) { /* critical section */ }
        /// </code>
        /// The method uses ConfigureAwait(false) internally to avoid capturing the calling context.
        /// </remarks>
        public async Task<Releaser> LockAsync(CancellationToken ct = default)
        {
            try
            {
                ++_lineLength;
                await _semaphore.WaitAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                --_lineLength;
            }
            return new Releaser(_semaphore);
        }

        /// <summary>
        /// Attempts to acquire the lock without waiting.
        /// </summary>
        /// <returns>
        /// A <see cref="Task{Releaser}"/> that yields a <see cref="Releaser"/> if the lock was acquired,
        /// or <c>null</c> if the lock was already taken.
        /// </returns>
        /// <remarks>
        /// This method performs a non-blocking try-enter. It is useful when the caller prefers to
        /// skip the operation if the lock cannot be acquired immediately.
        /// </remarks>
        public async Task<Releaser?> TryLockAsync()
        {
            bool entered = await _semaphore.WaitAsync(0).ConfigureAwait(false);
            if (!entered) return null;
            return new Releaser(_semaphore);
        }

        /// <summary>
        /// Gets whether the lock is currently held.
        /// </summary>
        /// <remarks>
        /// This is determined from the underlying semaphore's <see cref="SemaphoreSlim.CurrentCount"/>.
        /// It is a snapshot and may change immediately after being read.
        /// </remarks>
        public bool IsLocked => _semaphore.CurrentCount == 0;

        /// <summary>
        /// Diagnostic counter indicating how many callers are currently waiting to acquire the lock.
        /// </summary>
        /// <remarks>
        /// This is an internal counter incremented when awaiting in <see cref="LockAsync(CancellationToken)"/>
        /// and decremented after the wait completes. It is intended for light diagnostics and should not be
        /// relied upon for synchronization logic.
        /// </remarks>
        public int LineLength => _lineLength;

        /// <summary>
        /// Disposable token that releases the <see cref="AsyncLock"/> when disposed.
        /// </summary>
        /// <remarks>
        /// Instances are returned from <see cref="LockAsync(CancellationToken)"/> and <see cref="TryLockAsync"/>.
        /// The struct is readonly to reduce accidental modification and to make it safe to return by value.
        /// </remarks>
        public readonly struct Releaser : IDisposable
        {
            private readonly SemaphoreSlim _semaphore;
            internal Releaser(SemaphoreSlim semaphore) => _semaphore = semaphore;

            /// <summary>
            /// Releases the underlying semaphore, allowing another waiter to acquire the lock.
            /// </summary>
            public void Dispose() => _semaphore?.Release();
        }
    }
}