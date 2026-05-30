using AccSaber.Consts;
using AccSaber.Models;
using AccSaber.Utils;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Parser;
using HMUI;
using IPA.Utilities;
using System;
using System.Collections;
using System.ComponentModel;
using TMPro;
using UnityEngine;

namespace AccSaber.UI.MenuButton.ViewControllers
{
    internal sealed class AccSaberNewsModal : INotifyPropertyChanged, IDisposable
    {
#pragma warning disable IDE0051
        private bool _parsed;
        private string _title = null!;
        private string _content = null!;
        public event PropertyChangedEventHandler? PropertyChanged;
        private MonoBehaviour? _host;

        [UIComponent("content-page")]
        private TextPageScrollView _scrollView = null!;

        [UIComponent("modal")]
        private ModalView _modalView = null!;

        [UIParams]
        private readonly BSMLParserParams _parserParams = null!;


        [UIValue("news-title")]
        private string Title
        {
            get => _title;
            set
            {
                _title = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
            }
        }


        [UIValue("news-content")]
        private string Content
        {
            get => _content;
            set
            {
                _content = MarkdownParser.ParseMarkdown(value);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Content)));
            }
        }

        private void Parse(Transform parentTransform)
        {
            if (!_parsed)
            {
                VersionUtils.Parse(ResourcePaths.ACC_SABER_NEWS_MODAL, parentTransform, this);
                _modalView.name = "AccSaberNewsModal";
                _modalView.blockerClickedEvent += OnModalClosed;
                _scrollView.gameObject.AddComponent<Hyperlink>().pTextMeshPro = _scrollView.GetField<TextMeshProUGUI, TextPageScrollView>("_text");
                _parsed = true;
            }

            _modalView.transform.SetParent(parentTransform.transform);
            Accessors.ViewValidAccessor(ref _modalView) = false;
        }
        public void ShowModal(Transform parentTransform, MonoBehaviour host, AccSaberNewsEntry post)
        {
            Parse(parentTransform);
            _host = host;
            IEnumerator Show()
            {
                yield return new WaitForEndOfFrame();

                Title = post.Title;
                Content = post.Content;

                _parserParams.EmitEvent("close-modal");
                _parserParams.EmitEvent("open-modal");
            }
            _host.StartCoroutine(Show());
        }
        [UIAction("hide-modal")]
        private void HideModal()
        {
            if (!_parsed)
            {
                return;
            }

            _parserParams.EmitEvent("close-modal");
            OnModalClosed();
        }
        public void ForceCloseModal() 
        {
            if (!_parsed || !_modalView.isActiveAndEnabled)
                return;

            _modalView.Hide(false);
            OnModalClosed();
        }

        private void OnModalClosed()
        {
            _scrollView.ScrollTo(0, false);
        }

        public void Dispose()
        {
            _modalView.blockerClickedEvent -= OnModalClosed;
        }
    }
}
