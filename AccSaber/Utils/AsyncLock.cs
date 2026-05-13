using System;
using System.Threading;
using System.Threading.Tasks;

namespace AccSaber.Utils
{     
    public class AsyncLock
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        /// <summary>
        /// Acquires the lock asynchronously. Use 'using' to release it automatically.
        /// </summary>
        public async Task<Releaser> LockAsync()
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);
            return new Releaser(_semaphore);
        }
        /// <summary>
        /// Attempts to acquire the lock asynchronously. Returns null if lock is already taken.
        /// </summary>
        public async Task<Releaser?> TryLockAsync()
        {
            bool entered = await _semaphore.WaitAsync(0).ConfigureAwait(false);
            if (!entered) return null;
            return new Releaser(_semaphore);
        }

        public readonly struct Releaser : IDisposable
        {
            private readonly SemaphoreSlim _semaphore;
            internal Releaser(SemaphoreSlim semaphore) => _semaphore = semaphore;
            public void Dispose() => _semaphore?.Release();
        }
    }
}
