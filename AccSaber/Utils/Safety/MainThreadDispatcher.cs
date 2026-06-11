using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace AccSaber.Utils.Safety
{
    internal class MainThreadDispatcher : MonoBehaviour
    {
#pragma warning disable IDE0051
        private static readonly ConcurrentQueue<Action> _actionQueue = [];

        private void Update()
        {
            while (_actionQueue.TryDequeue(out Action action))
                action();
        }

        public void EnqueueRoutine(IEnumerator routine) => _actionQueue.Enqueue(() => StartCoroutine(routine));
        public void EnqueueStopRoutine(IEnumerator routine) => _actionQueue.Enqueue(() => StopCoroutine(routine));
        public void EnqueueStopRoutine(Coroutine routine) => _actionQueue.Enqueue(() => StopCoroutine(routine));
        public void EnqueueAction(Action action) => _actionQueue.Enqueue(action);

        public void AssertOnMainThread([CallerMemberName] string callerName = "")
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
