using AccSaber.Managers;
using AccSaber.Models;
using AccSaber.Utils;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using HMUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace AccSaber.UI.MenuButton.Campaigns.ViewControllers
{
    [ViewDefinition("AccSaber.UI.MenuButton.Campaigns.Views.AccSaberCampaignView.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\AccSaberCampaignView.bsml")]
    internal class AccSaberCampaignViewController : Utils.Safety.BSMLSafeAutomaticViewController
    {
#pragma warning disable CS0414 // Field assigned to but never read.
        private bool _parsed = false;
        private CategoryTab _currentTab;
        private bool _isLoading;
        private bool _inCampaign;
        private string _campaignTitle = null!;
        private string _campaignDescription = null!;
        private string _campaignCreator = null!;
        private AccSaberCampaign _currentCampaign = null!;
        private List<AccSaberCampaign> _activeCampaigns = null!;

        [UIComponent("CampaignImage")]
        private readonly ImageView _campaignImage = null!;

        [UIComponent("campaign-list")]
        private readonly CustomCellListTableData _campaignList = null!;

        [UIValue("campaign-cells")]
        private readonly List<object> _campaignCells = [];

        [UIComponent("diff-list")]
        private readonly CustomCellListTableData _diffList = null!;

        [UIValue("diff-cells")]
        private readonly List<object> _diffCells = [];

        private enum CategoryTab
        {
            Active,
            Curated,
            All
        }

        [Inject] private readonly AccSaberStore _accSaberStore = null!;
        private CategoryTab CurrentTab
        {
            get => _currentTab;
            set
            {
                _currentTab = value;
                _ = UpdateTabs();
            }
        }
        [UIValue("is-loading")]
        private bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                NotifyPropertyChanged(nameof(IsLoading));
                NotifyPropertyChanged(nameof(IsNotLoading));
            }
        }
        [UIValue("InCampaign")]
        private bool InCampaign
        {
            get => _inCampaign;
            set
            {
                _inCampaign = value;
                NotifyPropertyChanged(nameof(InCampaign));
                NotifyPropertyChanged(nameof(NotInCampaign));
            }
        }

        [UIValue("CampaignTitle")]
        private string CampaignTitle
        {
            get => _campaignTitle;
            set
            {
                _campaignTitle = value;
                NotifyPropertyChanged(nameof(CampaignTitle));
            }
        }
        [UIValue("CampaignDescription")]
        private string CampaignDescription
        {
            get => _campaignDescription;
            set
            {
                _campaignDescription = value;
                NotifyPropertyChanged(nameof(CampaignDescription));
            }
        }
        [UIValue("CampaignCreator")]
        private string CampaignCreator
        {
            get => _campaignCreator;
            set
            {
                _campaignCreator = value;
                NotifyPropertyChanged(nameof(CampaignCreator));
            }
        }

        [UIValue("NotInCampaign")]
        private bool NotInCampaign => !_inCampaign;

        [UIValue("is-not-loading")]
        private bool IsNotLoading => !_isLoading;

        [UIAction("#post-parse")]
        private async void Parsed()
        {
            if(!_parsed)
            {
                _parsed = true;
            }

            _activeCampaigns = await _accSaberStore.GetActiveCampaigns();
            
            CurrentTab = 0;
            IsLoading = false;
            InCampaign = false;
        }

        [UIAction("campaign-selected")]
        private void CampaignSelected(TableView table, CampaignCell cellObj)
        {
            if (cellObj != null)
                _currentCampaign = cellObj.Data;

            table.ClearSelection();

            _ = UpdateCampaign(_currentCampaign);
        }

        [UIAction("BackPressed")]
        private void BackPressed()
        {
            InCampaign = false;
            _ = UpdateTabs();
            _diffCells.Clear();
            _diffList.Data().Clear();
            _diffList.TableView().ReloadData();
        }

        [UIAction("PlayCampaign")]
        private async void PlayCampaign()
        {
            InCampaign = true;
            if (_currentCampaign is not null)
            {
                if (!_activeCampaigns.Contains(_currentCampaign) && _currentCampaign.ProgressStatus != "IN_PROGRESS")
                {
                    if (await _accSaberStore.StartCampaign(_currentCampaign.Id) == false)
                        Plugin.Log.Error("Failed to start campaign!");
                    else
                        _activeCampaigns.Add(await _accSaberStore.GetCampaign(_currentCampaign.Id));
                }

                _currentCampaign = await _accSaberStore.GetCampaign(_currentCampaign.Id);

                _ = SetMaps(_currentCampaign);
            }
        }

        [UIAction("tab-selected")]
        private void CategoryTabSelected(SegmentedControl segmentedControl, int index)
        {
            CurrentTab = (CategoryTab)index;
        }
        public async Task UpdateTabs()
        {
            _campaignCells.Clear();
            _campaignList.Data().Clear();
            _campaignList.TableView().ReloadData();

            List<AccSaberCampaign> tabCampaigns = CurrentTab switch
            {
                CategoryTab.Active => await _accSaberStore.GetActiveCampaigns(),
                CategoryTab.Curated => await _accSaberStore.GetCampaigns("CURATED"),
                CategoryTab.All => await _accSaberStore.GetCampaigns("PUBLISHED"),
                _ => throw new NotImplementedException(),
            };

            foreach (var campaign in tabCampaigns)
            {
                if (CurrentTab == CategoryTab.Active && campaign.ProgressStatus != "IN_PROGRESS")
                    continue;

                _campaignCells.Add(new CampaignCell(campaign));
            }

            IEnumerator WaitThenUpdate()
            {
                yield return new WaitForEndOfFrame();

                _campaignList.TableView().ReloadData();
                IsLoading = false;
            }
            StartCoroutine(WaitThenUpdate());
        }

        public async Task UpdateCampaign(AccSaberCampaign campaign)
        {
            CampaignTitle = campaign.Name;
            CampaignCreator = campaign.CreatorName;
            CampaignDescription = campaign.Description;
            await _campaignImage.SetImageAsync(campaign.IconUrl); // its in webp </3 // update its no longer in webp :)
        }

        public async Task SetMaps(AccSaberCampaign campaign)
        {
            _diffCells.Clear();
            _diffList.Data().Clear(); 

            foreach (var diff in campaign.Difficulties!)
            {
                _diffCells.Add(new CampaignMap(diff));
            }
            IEnumerator WaitThenUpdate()
            {
                yield return new WaitForEndOfFrame();

                _diffList.TableView().ReloadData();
                IsLoading = false;
            }
            StartCoroutine(WaitThenUpdate());
        }

        internal class CampaignCell(AccSaberCampaign campaign) : Utils.Safety.SafeNotifyPropertyChanged
        {
            public readonly AccSaberCampaign Data = campaign;

            [UIValue(nameof(Name))] private string Name => Data.Name;
            [UIValue(nameof(Author))] private string Author => Data.CreatorName;
            [UIValue(nameof(MapCount))] private int MapCount => Data.DifficultyCount!.Value;

        }

        internal class CampaignMap(AccSaberCampaignMap map) : Utils.Safety.SafeNotifyPropertyChanged
        {
            [UIValue(nameof(Name))] private string Name => map.SongName;
            [UIValue(nameof(Author))] private string Author => map.SongAuthor;
            [UIValue(nameof(MapCount))] private string MapCount => map.Difficulty;

        }


    }
}
