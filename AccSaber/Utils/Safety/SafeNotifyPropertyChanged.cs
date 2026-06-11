using System.ComponentModel;
using System.Runtime.CompilerServices;
using Zenject;

namespace AccSaber.Utils.Safety
{
    internal abstract class SafeNotifyPropertyChanged : INotifyPropertyChanged
    {
        [Inject] protected readonly MainThreadDispatcher _mainThreadDispatcher = null!;
        // PropertyChanged\??\.Invoke.+(nameof\(.+?\)).+?;
        // NotifyPropertyChanged($1);

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            try
            {
                if (!IPA.Utilities.UnityGame.OnMainThread)
                {
                    Plugin.Log.Warn($"{GetType().Name} is trying to update property \"{propertyName}\" while not being on the main Unity thread! Forcing method to unity thread...");
                    _mainThreadDispatcher.EnqueueAction(() => PropertyChanged?.Invoke(this, new(propertyName)));
                }
                else
                    PropertyChanged?.Invoke(this, new(propertyName));
            }
            catch (System.Exception e)
            {
                Plugin.Log.Error(e);
            }
        }
    }
}
