using AccSaber.Managers;
using AccSaber.Models;
using AccSaber.Utils;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace AccSaber.UI.MenuButton.Campaigns.ViewControllers
{
    [ViewDefinition("AccSaber.UI.MenuButton.Campaigns.Views.AccSaberCampaignView.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\AccSaberCampaignView.bsml")]
    internal class AccSaberCampaignViewController : BSMLAutomaticViewController, INotifyPropertyChanged
    {
#pragma warning disable IDE0051
        private bool _parsed = false;
        private CategoryTab _currentTab;
        private bool _isLoading;
        private bool _inCampaign;
        private string _campaignTitle = null!;
        private string _campaignCreator = null!;
        private List<AccSaberCampaign> accSaberCampaigns = null!;
        private AccSaberCampaign _currentCampaign = null!;
        public new event PropertyChangedEventHandler? PropertyChanged;

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

        [Inject] private AccSaberStore _accSaberStore = null!;
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLoading)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsNotLoading)));
            }
        }
        [UIValue("InCampaign")]
        private bool InCampaign
        {
            get => _inCampaign;
            set
            {
                _inCampaign = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InCampaign)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NotInCampaign)));
            }
        }

        [UIValue("CampaignTitle")]
        private string CampaignTitle
        {
            get => _campaignTitle;
            set
            {
                _campaignTitle = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CampaignTitle)));
            }
        }

        [UIValue("CampaignCreator")]
        private string CampaignCreator
        {
            get => _campaignCreator;
            set
            {
                _campaignCreator = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CampaignCreator)));
            }
        }

        [UIValue("NotInCampaign")]
        private bool NotInCampaign => !_inCampaign;

        [UIValue("is-not-loading")]
        private bool IsNotLoading => !_isLoading;

        [UIAction("#post-parse")]
        private void Parsed()
        {
            if(!_parsed)
            {
                _parsed = true;

            }
            //accSaberCampaigns = await _accSaberStore.GetCampaigns();
            CurrentTab = 0;
            IsLoading = false;
            InCampaign = false;
        }

        [UIAction("campaign-selected")]
        private void CampaignSelected(TableView table, CampaignCell cellObj)
        {
            if (cellObj != null)
                _currentCampaign = cellObj.data;

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
        private void PlayCampaign()
        {
            InCampaign = true;
            if(_currentCampaign is not null)
                _ = SetMaps(_currentCampaign);
        }

        [UIAction("tab-selected")]
        private void CategoryTabSelected(SegmentedControl segmentedControl, int index)
        {
            CurrentTab = (CategoryTab)index;

            _ = UpdateTabs();
        }
        public async Task UpdateTabs()
        {
            _campaignCells.Clear();
            _campaignList.Data().Clear();
            List<AccSaberCampaign> tabCampaigns;

            switch (CurrentTab)
            {
                case CategoryTab.Active:
                    //tabCampaigns = await _accSaberStore.GetActiveCampaigns();
                    var camp = new AccSaberCampaign() { Name = "Active Campaign 1", CreatorName = "Creator 1", DifficultyCount = 2 };
                    var camp2 = new AccSaberCampaign() { Name = "Active Campaign 2", CreatorName = "Creator 2", DifficultyCount = 3 };

                    
                    tabCampaigns = new List<AccSaberCampaign> { camp, camp2 };
                    break;
                case CategoryTab.Curated:
                    // tabCampaigns = await _accSaberStore.GetCampaigns("CURATED");
                    var cur_list = new List<AccSaberCampaignMap>();

                    for (int i = 0;  i < 10; i++)
                        cur_list.Add(new AccSaberCampaignMap() { SongName = $"Curated song {i}", SongAuthor = $"Creator {i}", Difficulty = "HARD" });

                    var cur_camp = new AccSaberCampaign() { Name = "Curated Campaign 1", CreatorName = "Creator 1", DifficultyCount = 2, Difficulties = cur_list };
                    var cur_camp2 = new AccSaberCampaign() { Name = "Curated Campaign 2", CreatorName = "Creator 2", DifficultyCount = 3, Difficulties = cur_list };


                    tabCampaigns = new List<AccSaberCampaign> { cur_camp, cur_camp2 };
                    break;
                default:
                    //tabCampaigns = await _accSaberStore.GetCampaigns("PUBLISHED");
                    var pub_camp = new AccSaberCampaign() { Name = "Published Campaign 1", CreatorName = "Creator 1", DifficultyCount = 2 };
                    var pub_camp2 = new AccSaberCampaign() { Name = "Published Campaign 2", CreatorName = "Creator 2", DifficultyCount = 3 };


                    tabCampaigns = new List<AccSaberCampaign> { pub_camp, pub_camp2 };
                    break;
            }                    

            foreach(var campaign in tabCampaigns)
            {
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

        internal class CampaignCell(AccSaberCampaign campaign) : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler? PropertyChanged;

            public AccSaberCampaign data = campaign;

            [UIValue(nameof(Name))] private string Name => campaign.Name;
            [UIValue(nameof(Author))] private string Author => campaign.CreatorName;
            [UIValue(nameof(MapCount))] private int MapCount => campaign.DifficultyCount!.Value;

        }

        internal class CampaignMap(AccSaberCampaignMap map) : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler? PropertyChanged;

            [UIValue(nameof(Name))] private string Name => map.SongName;
            [UIValue(nameof(Author))] private string Author => map.SongAuthor;
            [UIValue(nameof(MapCount))] private string MapCount => map.Difficulty;

        }


    }
}
