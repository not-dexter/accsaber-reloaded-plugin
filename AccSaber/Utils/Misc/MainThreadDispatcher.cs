using System;
using System.Collections;
using System.Collections.Concurrent;
using UnityEngine;

namespace AccSaber.Utils.Misc
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
    }
}
