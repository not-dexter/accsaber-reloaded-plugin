using AccSaber.API;
using AccSaber.Configuration;
using AccSaber.Consts;
using AccSaber.Models;
using AccSaber.Utils;
using AccSaber.Utils.Misc;
using AccsaberLeaderboard.UI.BSML_Addons.Components;
using BeatSaberMarkupLanguage.Attributes;
using HMUI;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using Zenject;

namespace AccSaber.UI.MenuButton.ViewControllers
{
    internal class AccSaberPlaylistModalController : Utils.Safety.SafeNotifyPropertyChanged, IInitializable, IDisposable
    {
#pragma warning disable IDE0051
        [Inject] private readonly AccSaberMainFlowCoordinator mainFlowCoordinator = null!;
        [Inject] private readonly LevelUtils levelUtils = null!;
        [Inject] private readonly PlaylistUtils playlistUtils = null!;
        [Inject] private readonly PluginConfig PC = null!;
        [Inject] private readonly PlayerSocialLife playerInfo = null!;

        private ButtonType buttonType;
        private bool _cellsLoading, parsed = false;

        [UIComponent("modal")] 
        private readonly ModalView modal = null!;

        [UIComponent("categoryModal")] 
        private readonly ModalView categoryModal = null!;

        [UIComponent("overallButton")]
        private readonly TextMeshProUGUI overallButtonText = null!;

        [UIComponent("trueButton")]
        private readonly TextMeshProUGUI trueButtonText = null!;

        [UIComponent("standardButton")]
        private readonly TextMeshProUGUI standardButtonText = null!;

        [UIComponent("techButton")]
        private readonly TextMeshProUGUI techButtonText = null!;

        [UIComponent("batchList")]
        private readonly MyCustomCellListTableData batchList = null!;


        [UIValue("cellsLoading")]
        private bool CellsLoading
        {
            get => _cellsLoading;
            set
            {
                if (_cellsLoading == value)
                    return;

                _cellsLoading = value;
                NotifyPropertyChanged(nameof(CellsLoading));
                NotifyPropertyChanged(nameof(CellsNotLoading));
            }
        }

        [UIValue("cellsNotLoading")]
        private bool CellsNotLoading => !CellsLoading;

        [UIValue("goToPlaylist")]
        private bool GoToPlaylist { get; set; }

        private Action? CloseMenu => GoToPlaylist ? mainFlowCoordinator.CloseToMainMenu : null;


        [UIAction("getBatchCells")]
        private async Task<CellPageSource> GetBatchCells(int page, int size)
        {
            SetLoading(true);
            try
            {
                AccSaberPagedContent<AccSaberBatch>? content = await APIHandler.CallAPI_Json<AccSaberPagedContent<AccSaberBatch>>(string.Format(HelpfulPaths.APAPI_BATCHES, page, size), AccsaberAPI.Throttler);

                if (content is null || content.Content is null)
                    return default;

                return new(content.Content.Select(batch => new BatchCell(batch)), content.LastPage);
            } 
            catch (Exception e)
            {
                Plugin.Log.Error("There was an issue loading the batch cells!\n" + e);
                return default;
            }
            finally
            {
                SetLoading(false);
            }
        }

        private static readonly Regex FilenameEscapeRegex = new(@"[ \/]+");

        [UIAction("batchSelected")]
        private void BatchSelected(BatchCell cell)
        {
            string filename = $"accsaber-reloaded-{FilenameEscapeRegex.Replace(cell.Data.Name, "-")}";
            string playlistName = $"Accsaber {cell.Data.Name}";

            FinishLoadPlaylist(levelUtils.LoadPlaylist(filename, playlistName, playlistUtils.GetPlaylistData(cell.Data.Difficulties.Select(diff => diff.DifficultyId)), null, CloseMenu, cell.UpdateStatus), cell.UpdateStatus);
        }

        [UIAction("onCategoricalClicked")]
        private void OnCategoricalClicked()
        {
            buttonType = ButtonType.Categorical;
            ShowCategoryModal();
        }

        [UIAction("onMissingClicked")]
        private void OnMissingClicked()
        {
            buttonType = ButtonType.Missing;
            ShowCategoryModal();
        }

        [UIAction("overallClicked")]
        private void OverallClicked() => MakePlaylist(APCategory.Overall, overallButtonText);

        [UIAction("trueClicked")]
        private void TrueClicked() => MakePlaylist(APCategory.True, trueButtonText);

        [UIAction("standardClicked")]
        private void StandardClicked() => MakePlaylist(APCategory.Standard, standardButtonText);

        [UIAction("techClicked")]
        private void TechClicked() => MakePlaylist(APCategory.Tech, techButtonText);

        [UIAction("#post-parse")]
        private void PostParse()
        {
            if (parsed)
                return;

            typeof(ModalView).GetMethod("SetupView", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).Invoke(categoryModal, [modal.transform]);

            if (GoToPlaylist != PC.GoToPlaylist)
            {
                GoToPlaylist = PC.GoToPlaylist;
                NotifyPropertyChanged(nameof(GoToPlaylist));
            }

            parsed = true;
        }

        public void Initialize()
        {
            mainFlowCoordinator.OnHubDeactivated += Hide;
        }
        public void Dispose()
        {
            mainFlowCoordinator.OnHubDeactivated -= Hide;
        }
        public void Show()
        {
            modal.Show(true);

            foreach (BatchCell cell in batchList.Data.Cast<BatchCell>())
                cell.ShowStatus = false;
        }
        private void ShowCategoryModal()
        {
            categoryModal.Show(true);
        }
        public void Hide(bool animated)
        {
            if (categoryModal.IsShown())
                categoryModal.Hide(animated, () => modal.Hide(animated));
            else if (modal.IsShown())
                modal.Hide(animated);
        }
        public void Hide() => Hide(false);


        private void SetLoading(bool loading)
        {
            IEnumerator Update()
            {
                yield return new WaitForEndOfFrame();

                CellsLoading = loading;
            }
            mainFlowCoordinator.StartCoroutine(Update());
        }
        private Action<string?> GetTextSetter(TextMeshProUGUI text)
        {
            string original = text.text;
            return str =>
            {
                if (str is null)
                    text.SetText(original);
                else
                    text.SetText(str);
            };
        }
        private void MakePlaylist(APCategory category, TextMeshProUGUI text)
        {
            Action<string?> setter = GetTextSetter(text);

            switch (buttonType)
            {
                case ButtonType.Categorical:
                    FinishLoadPlaylist(levelUtils.LoadPlaylist(category, CloseMenu, setter, GoToPlaylist), setter);
                    break;
                case ButtonType.Missing:
                    FinishLoadPlaylist(levelUtils.LoadPlaylist(category, playerInfo.PlayerID!, CloseMenu, setter, GoToPlaylist), setter);
                    break;
            }
        }
        private void FinishLoadPlaylist(Task loadPlaylist, Action<string?> textSetter)
        {
            if (GoToPlaylist)
                return;

            IEnumerator DoFinish()
            {
                yield return new WaitForEndOfFrame();

                textSetter?.Invoke("Finished!");

                yield return new WaitForSeconds(1);

                textSetter?.Invoke(null);
            }

            _ = loadPlaylist.ContinueWith(task => mainFlowCoordinator.StartCoroutine(DoFinish()));
        }

        private enum ButtonType
        {
            Categorical, Missing
        }
        private sealed class BatchCell(AccSaberBatch data) : Utils.Safety.SafeNotifyPropertyChanged, ICellDataSource
        {
            public string TemplatePath => ResourcePaths.ACC_SABER_PLAYLIST_CELL;

            public float CellSize => 12f;

            public int TemplateId { get; set; }

            public readonly AccSaberBatch Data = data;

            private bool _showStatus = false;

            [UIValue("title")]
            public readonly string Title = data.Name;

            [UIValue("status")]
            public readonly string Status = data.Status.ToString().Replace('_', ' ').CapitializeWords();

            [UIValue("description")]
            public readonly string Description = data.Description;

            [UIValue("releaseTime")]
            public readonly string ReleaseTime = data.ReleasedAt.ToRelativeTime(1);

            [UIValue("showStatus")]
            public bool ShowStatus
            {
                get => _showStatus;
                set
                {
                    if (_showStatus == value) 
                        return;

                    _showStatus = value;
                    NotifyPropertyChanged(nameof(ShowStatus));
                    NotifyPropertyChanged(nameof(NotShowStatus));
                }
            }

            [UIValue("notShowStatus")]
            public bool NotShowStatus => !ShowStatus;


            [UIValue("grey")]
            private const string Grey = ColorUtils.GREY;

            [UIValue("darkGrey")]
            private const string DarkGrey = ColorUtils.GREY_DIM;


            [UIComponent("statusText")]
            public readonly TextMeshProUGUI StatusText = null!;


            internal void UpdateStatus(string? str)
            {
                bool showStatus = str is not null;

                if (showStatus)
                    StatusText.SetText(str!);

                ShowStatus = showStatus;
            }
        }
    }
}
