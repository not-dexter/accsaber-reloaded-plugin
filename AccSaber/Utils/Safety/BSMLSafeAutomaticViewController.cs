using BeatSaberMarkupLanguage.ViewControllers;
using System.Collections;
using System.Runtime.CompilerServices;
using UnityEngine;
using Zenject;

namespace AccSaber.Utils.Safety
{
    /// <summary>Ensures view notifications and coroutine starts follow Unity main-thread requirements.</summary>
    internal class BSMLSafeAutomaticViewController : BSMLAutomaticViewController
    {
        [Inject] protected readonly MainThreadDispatcher _mainThreadDispatcher = null!;

        /// <summary>Raises a view property notification on Unity's main thread.</summary>
        protected new void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            try
            {
                if (!IPA.Utilities.UnityGame.OnMainThread)
                {
                    Plugin.Log.Warn($"{GetType().Name} is trying to update property \"{propertyName}\" while not being on the main Unity thread! Forcing method to unity thread...");
                    _mainThreadDispatcher.EnqueueAction(() => base.NotifyPropertyChanged(propertyName));
                }
                else
                    base.NotifyPropertyChanged(propertyName);
            }
            catch (System.Exception e)
            {
                Plugin.Log.Error(e);
            }
        }

        /// <summary>Starts a coroutine and rejects calls made outside the main thread.</summary>
        public new Coroutine StartCoroutine(IEnumerator routine)
        {
            if (!IPA.Utilities.UnityGame.OnMainThread)
            {
                string error = $"{GetType().Name} is trying to start a coroutine while not being on the main Unity thread!";
                Plugin.Log.Critical(error); // in case the exceptions gets eaten.
                throw new System.Exception(error);
            }

            return base.StartCoroutine(routine);
        }

    }
}
