using System;
using System.Collections;
using UnityEngine;

namespace AccSaber.Utils.Misc
{
    public sealed class Requester(Func<bool> fulfiller, MonoBehaviour host)
    {
        private readonly Func<bool> fulfiller = fulfiller;
        private readonly MonoBehaviour host = host;

        private bool requestActive;
        private Coroutine? requestRoutine;
        private int requestVersion;

        public Requester(Action fulfiller, MonoBehaviour host) : this(() => { fulfiller(); return true; }, host) 
        { }

        public void Request(bool attemptFulfill = true)
        {
            Safety.MainThreadDispatcher.AssertOnMainThread();

            if (requestActive)
                return;

            requestActive = true;
            requestVersion++;

            StopRequestRoutine();

            if (attemptFulfill)
                TryFulfillRequest();
        }

        public void CancelRequest()
        {
            Safety.MainThreadDispatcher.AssertOnMainThread();

            if (!requestActive)
                return;

            requestActive = false;
            requestVersion++;

            StopRequestRoutine();
        }

        public void RequestIn(TimeSpan time, bool attemptFulfill = true)
        {
            Safety.MainThreadDispatcher.AssertOnMainThread();

            if (requestActive)
                return;

            requestVersion++;
            StopRequestRoutine();

            if (time <= TimeSpan.Zero)
                throw new ArgumentException("The given timespan must be greater than 0.", nameof(time));

            int version = requestVersion;
            requestRoutine = host.StartCoroutine(WaitThenRequest(version, time, attemptFulfill));
        }

        public void TryFulfillRequest()
        {
            Safety.MainThreadDispatcher.AssertOnMainThread();

            if (!requestActive)
                return;

            bool success = fulfiller();

            if (success)
            {
                requestActive = false;
                requestVersion++;
                StopRequestRoutine();
            }
        }

        private IEnumerator WaitThenRequest(int version, TimeSpan length, bool attemptFulfill)
        {
            yield return new WaitForSecondsRealtime((float)length.TotalSeconds);

            if (version != requestVersion)
                yield break;

            requestRoutine = null;

            Request(attemptFulfill);
        }

        private void StopRequestRoutine()
        {
            if (requestRoutine is not null)
            {
                host.StopCoroutine(requestRoutine);
                requestRoutine = null;
            }
        }
    }
}
