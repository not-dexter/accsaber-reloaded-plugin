using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace AccSaber.Utils.Safety
{
    /// <summary>Queues actions and coroutine operations to run on Unity's main thread.</summary>
    internal class MainThreadDispatcher : MonoBehaviour
    {
        private static readonly ConcurrentQueue<Action> _actionQueue = [];

        private void Update()
        {
            while (_actionQueue.TryDequeue(out Action action))
                action();
        }

        /// <summary>Schedules a coroutine to start on the main thread.</summary>
        public void EnqueueRoutine(IEnumerator routine) => _actionQueue.Enqueue(() => StartCoroutine(routine));
        /// <summary>Schedules a coroutine to stop on the main thread.</summary>
        public void EnqueueStopRoutine(IEnumerator routine) => _actionQueue.Enqueue(() => StopCoroutine(routine));
        /// <summary>Schedules a running coroutine to stop on the main thread.</summary>
        public void EnqueueStopRoutine(Coroutine routine) => _actionQueue.Enqueue(() => StopCoroutine(routine));
        /// <summary>Schedules an action to run on the main thread.</summary>
        public void EnqueueAction(Action action) => _actionQueue.Enqueue(action);

        /// <summary>Verifies the caller is on Unity's main thread and throws if it is not.</summary>
        public static void AssertOnMainThread([CallerMemberName] string callerName = "")
        {
            if (!IPA.Utilities.UnityGame.OnMainThread)
            {
                string error = $"{callerName} is not on the main thread!";
                Plugin.Log.Critical(error); // in case the error gets eaten by a async function or something.
                throw new Exception(error);
            }
        }
    }
}
