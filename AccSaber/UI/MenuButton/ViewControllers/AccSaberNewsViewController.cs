using AccSaber.Managers;
using AccSaber.Models;
using AccSaber.Utils;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace AccSaber.UI.MenuButton.ViewControllers
{
    [ViewDefinition("AccSaber.UI.MenuButton.Views.AccSaberNewsView.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\AccSaberNewsView.bsml")]
    internal class AccSaberNewsViewController : BSMLAutomaticViewController, INotifyPropertyChanged
    {
#pragma warning disable IDE0051
        public new event PropertyChangedEventHandler? PropertyChanged;

        private bool _parsed;
        private bool _isLoading;
        private AccSaberStore.NewsType _currentTab;

        private AccSaberStore _accSaberStore = null!;

        private List<AccSaberNewsEntry> _news = null!;

        [Inject] private readonly AccSaberNewsModal nmc = null!;

        [UIComponent("news-list")]
        private readonly CustomCellListTableData _newsList = null!;

        [UIValue("news-cells")]
        private readonly List<object> _newsCells = [];

        [UIValue("is-loading")]
        private bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLoading)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsNotLoading)));
            }
        }

        [UIValue("is-not-loading")]
        private bool IsNotLoading => !_isLoading;

        [Inject]
        public void Construct(AccSaberStore accSaberStore)
        {
            _accSaberStore = accSaberStore;
        }

        [UIAction("#post-parse")]
        private void Parsed()
        {
            if (!_parsed)
            {
                _parsed = true;
            }
            _currentTab = AccSaberStore.NewsType.All;
            IsLoading = true;
            _ = SetNews(_currentTab);
        }

        [UIAction("tab-selected")]
        private void TabSelected(SegmentedControl segmentedControl, int index)
        {
            IsLoading = true;
            _currentTab = (AccSaberStore.NewsType)segmentedControl.selectedCellNumber;
            _ = SetNews(_currentTab);
        }

        [UIAction("post-selected")]
        private void PostSelected(TableView tableView, NewsCell post)
        {
            nmc.ShowModal(_newsList.transform, this, post._post);
            tableView.ClearSelection();
        }

        public void HideNewsModal() => nmc.ForceCloseModal();

        private async Task SetNews(AccSaberStore.NewsType tab)
        {
            _newsCells.Clear();
            _newsList.Data().Clear();

            try {
                _news = await _accSaberStore.GetNewsPosts(tab);
                
                foreach (var post in _news)
                {
                    _newsCells.Add(new NewsCell(post));
                }

                IEnumerator WaitThenUpdate()
                {
                    yield return new WaitForEndOfFrame();

                    _newsList.TableView().ReloadData();
                    IsLoading = false;
                }
                StartCoroutine(WaitThenUpdate());
            }
            catch (Exception e)
            {
                Plugin.Log.Error(e);
            }
        }
        internal class NewsCell(AccSaberNewsEntry post)
        {
            #region BSML Values
            [UIValue("news-title")]
            private readonly string _newsTitle = post.Title;

            [UIValue("news-desc")]
            private readonly string _newsDesc = post.Description;

            [UIValue("news-published")]
            private readonly string _newsPublished = post.PublishedAt.Date.ToString("dd/MM/yyyy");


            public AccSaberNewsEntry _post { get; private set; } = post;

            #endregion
        }
    }
}