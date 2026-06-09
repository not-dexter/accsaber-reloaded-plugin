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

namespace AccSaber.UI.MenuButton.ViewControllers
{
    internal sealed class AccSaberNotificationModal : INotifyPropertyChanged, IDisposable
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
                VersionUtils.Parse(ResourcePaths.ACC_SABER_NOTIF_MODAL, parentTransform.gameObject, this);
                _modalView.name = "AccSaberNotificationModal";

                _modalView.blockerClickedEvent += KillLock;

                _parsed = true;
            }

            _modalView.transform.SetParent(parentTransform.transform);
            Accessors.ViewValidAccessor(ref _modalView) = false;
        }
            
        public async Task ShowModal(Transform parentTransform, IPopup instance, object data, AccSaberMainFlowCoordinator parentFlowCoordinator, string prompt)
        {
            if (_mainLocker.IsLocked)
            {
                HideModal();
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

            _modalView.Hide(false, _currentLocker.Dispose);

            _currentInstance.PopupSuccess(_data);
        }

        [UIAction("ClickedNo")]
        public void ClickedNo()
        {
            HideModal();
        }


        public void HideModal()
        {
            if (!_parsed)
            {
                return;
            }

            _modalView.Hide(true, _currentLocker.Dispose);
        }

        private void KillLock() => _currentLocker.Dispose();

        public void Dispose()
        {
            _modalView.blockerClickedEvent -= KillLock;
        }

        public interface IPopup
        {
            void PopupSuccess(object data);
        }
    }
}