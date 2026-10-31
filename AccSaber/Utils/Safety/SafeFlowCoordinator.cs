using HMUI;
using Zenject;

#if !NEW_VERSION
using BeatSaberMarkupLanguage;
#endif

namespace AccSaber.Utils.Safety
{
    /// <summary>Wraps flow presentation and dismissal so they are safely invoked on Unity's main thread.</summary>
    internal abstract class SafeFlowCoordinator : FlowCoordinator
    {
        [Inject] protected readonly MainThreadDispatcher _mainThreadDispatcher = null!;

        protected abstract FlowCoordinator ParentFlowCoordinator { get; set; }
        /// <summary>Presents this flow coordinator from its parent, dispatching to the main thread when needed.</summary>
        public virtual void PresentFlowCoordinator(System.Action? callback = null, bool immediately = false)
        {
            try
            {
                if (!IPA.Utilities.UnityGame.OnMainThread)
                {
                    Plugin.Log.Warn($"Cannot present {GetType().Name} when not on the main Unity thread! Forcing method to unity thread...");
                    _mainThreadDispatcher.EnqueueAction(() => ParentFlowCoordinator.PresentFlowCoordinator(this, callback, immediately: immediately));
                }
                else
                    ParentFlowCoordinator.PresentFlowCoordinator(this, callback, immediately: immediately);
            }
            catch (System.Exception e)
            {
                Plugin.Log.Error(e);
            }
        }
        /// <summary>Dismisses this flow coordinator, dispatching to the main thread when needed.</summary>
        public virtual void DismissFlowCoordinator(System.Action? callback = null, bool immediately = false)
        {
            try
            {
                if (!IPA.Utilities.UnityGame.OnMainThread)
                {
                    Plugin.Log.Warn($"Cannot close {GetType().Name} when not on the main Unity thread! Forcing method to unity thread...");
                    _mainThreadDispatcher.EnqueueAction(() => ParentFlowCoordinator.DismissFlowCoordinator(this, finishedCallback: callback, immediately: immediately));
                }
                else
                    ParentFlowCoordinator.DismissFlowCoordinator(this, finishedCallback: callback, immediately: immediately);
            }
            catch (System.Exception e)
            {
                Plugin.Log.Error(e);
            }
        }
        /// <summary>Dismisses this coordinator when the user presses the back button.</summary>
        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            DismissFlowCoordinator();
        }
    }
}
