using AccSaber.Consts;
using AccSaber.Utils;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Parser;
using HMUI;
using System;
using System.ComponentModel;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace AccSaber.UI.MenuButton.ViewControllers
{
    internal sealed class AccSaberNotificationModal : INotifyPropertyChanged, IDisposable, IInitializable
    {
        private bool _parsed;
        private IPopup _currentInstance = null!;
        private object _data = null!;
        private AccSaberMainFlowCoordinator _parentFlowCoordinator = null!;
        private string _targetPrompt = null!;
        private readonly AsyncLock _mainLocker = new();
        private AsyncLock.Releaser _currentLocker = default;

        public event PropertyChangedEventHandler? PropertyChanged;

        [UIComponent("modal")]
        private ModalView _modalView = null!;

        [UIParams]
        private readonly BSMLParserParams _parserParams = null!;

        [UIValue("TargetPrompt")]
        private string TargetPrompt
        {
            get => _targetPrompt;
            set
            {
                _targetPrompt = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TargetPrompt)));
            }
        }

        private void Parse(Transform parentTransform)
        {
            if (!_parsed)
            {
                VersionUtils.BSMLParser_Instance.Parse(Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), ResourcePaths.ACC_SABER_NOTIF_MODAL), parentTransform.gameObject, this);
                _modalView.name = "AccSaberNotificationModal";

                _modalView.blockerClickedEvent += () => _currentLocker.Dispose();

                _parsed = true;
            }

            _modalView.transform.SetParent(parentTransform.transform);
            Accessors.ViewValidAccessor(ref _modalView) = false;
        }
            
        public void Initialize()
        {
            _modalView.blockerClickedEvent += HideModal;
        }

        public async Task ShowModal(Transform parentTransform, IPopup instance, object data, AccSaberMainFlowCoordinator parentFlowCoordinator, string prompt)
        {
            if (_mainLocker.IsLocked)
            {
                _modalView.Hide(true, () =>
                {
                    try
                    {
                        _currentLocker.Dispose();
                    }
                    catch (ObjectDisposedException e)
                    {
                        Plugin.Log.Error("Stop hitting the button so fast.\n" + e);
                    }
                });
            }

            _currentLocker = await _mainLocker.LockAsync();

            Parse(parentTransform);

            _currentInstance = instance;
            _data = data;
            _parentFlowCoordinator = parentFlowCoordinator;
            TargetPrompt = string.IsNullOrEmpty(prompt) ? "N/A" : prompt;

            _parserParams.EmitEvent("close-modal");
            _parserParams.EmitEvent("open-modal");
        }

        [UIAction("ClickedYes")]
        public void ClickedYes()
        {
            if (_data is null || _parentFlowCoordinator is null)
                return;

            _modalView.Hide(false);

            _currentInstance.PopupSuccess(_data);
            _currentLocker.Dispose();
        }

        [UIAction("ClickedNo")]
        public void ClickedNo()
        {
            HideModal();
            _currentLocker.Dispose();
        }


        public void HideModal()
        {
            if (!_parsed)
            {
                return;
            }
            
            _parserParams.EmitEvent("close-modal");
        }
        public void Dispose()
        {
            _modalView.blockerClickedEvent -= HideModal;
        }

        public interface IPopup
        {
            void PopupSuccess(object data);
        }
    }
}