using AccSaber.Managers;
using AccSaber.Models;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using AccSaber.Utils;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using UnityEngine;
using Zenject;
using System.Linq;

namespace AccSaber.UI.MenuButton.ViewControllers
{
    [ViewDefinition("AccSaber.UI.MenuButton.Views.AccSaberNewsView.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\AccSaberNewsView.bsml")]
    internal class AccSaberNewsViewController : BSMLAutomaticViewController, INotifyPropertyChanged
    {
        public new event PropertyChangedEventHandler? PropertyChanged;

        private bool _parsed;
        private bool _isLoading;
        private AccSaberStore.NewsType _currentTab;

        private AccSaberStore _accSaberStore = null!;

        private List<AccSaberNewsEntry> _news = null!;

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
        void Parsed()
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
        void TabSelected(SegmentedControl segmentedControl, int index)
        {
            IsLoading = true;
            _currentTab = (AccSaberStore.NewsType)segmentedControl.selectedCellNumber;
            _ = SetNews(_currentTab);
        }

        private async Task SetNews(AccSaberStore.NewsType tab)
        {
            _newsCells.Clear();
            _newsList.Data().Clear();

            try {
                _news = await _accSaberStore.GetNewsPosts(tab);
                
                if (_news.Count > 0)
                {
                    foreach (var post in _news)
                    {
                        _newsCells.Add(new NewsCell(post.Title, post.Description, post.PublishedAt.ToString()));
                    }
                }
                else
                    _newsCells.Add(new NewsCell("No posts found", "", ""));

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
        internal class NewsCell
        {
            #region BSML Values
            [UIValue("news-title")]
            private readonly string _newsTitle;

            [UIValue("news-desc")]
            private readonly string _newsDesc;

            /*[UIValue("news-content")]
			private readonly string _newsContent;

			[UIValue("news-author")]
			private readonly string _newsAuthor;*/

            [UIValue("news-published")]
            private readonly string _newsPublished;
            #endregion
            public NewsCell(string newsTitle, string newsDesc, /*string newsContent, string newsAuthor*/ string newsPublished)
            {
                _newsTitle = newsTitle;
                _newsDesc = newsDesc;
                _newsPublished = newsPublished;
            }
        }
    }
}