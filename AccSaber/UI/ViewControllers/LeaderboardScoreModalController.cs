using AccSaber.API;
using AccSaber.Configuration;
using AccSaber.Consts;
using AccSaber.Models;
using AccSaber.Models.PlayerModels;
using AccSaber.Utils;
using AccSaber.Utils.Safety;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Parser;
using HMUI;
using System.Collections;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using Zenject;
using static AccSaber.Utils.ColorUtils;

#if NEW_VERSION
#endif

namespace AccSaber.UI.ViewControllers
{
    internal sealed class LeaderboardScoreModalController
    {
#pragma warning disable IDE0044, IDE0051

        #region UI Values

        [UIValue("labelColor")] public const string labelColor = GREY;
        [UIValue("dimColor")] public const string dimColor = DARK_BLUE;

        [UIValue("modalShowName")] public const string modalShowName = "ShowContainer";
        [UIValue("modalHideName")] public const string modalHideName = "HideContainer";

        [UIValue("containerWidth")] public const float containerWidth = 50f;
        [UIValue("containerHeight")] public const float containerHeight = 40f;

        [UIValue("valueWidth")] public const float valueWidth = containerWidth / 3f;

        [UIValue("playerNameFontSize")] public const float playerNameFontSize = 5f;
        [UIValue("valueFontSize")] public const float valueFontSize = 4f;
        [UIValue("timeFontSize")] public const float timeFontSize = 3f;
        [UIValue("labelFontSize")] public const float labelFontSize = 2.5f;

        #endregion
        #region UI Components & Objects

        [UIParams] private BSMLParserParams parserParams = null!;

        [UIObject("modal")] private GameObject modal = null!;
        [UIComponent("modal")] private ModalView modalView = null!;

        [UIObject("container")] private GameObject container = null!;
        [UIObject("loader")] private GameObject loader = null!;

        [UIComponent("playerNameText")] private TextMeshProUGUI playerNameText = null!;
        [UIComponent("separator")] private ImageView _separator = null!;
        [UIComponent("timeSetText")] private TextMeshProUGUI timeSetText = null!;

        [UIComponent("apText")] private TextMeshProUGUI apText = null!;
        [UIComponent("accText")] private TextMeshProUGUI accText = null!;
        [UIComponent("rankText")] private TextMeshProUGUI rankText = null!;

        [UIComponent("weightedText")] private TextMeshProUGUI weightedText = null!;
        [UIComponent("xpText")] private TextMeshProUGUI xpText = null!;
        [UIComponent("scoreText")] private TextMeshProUGUI scoreText = null!;

        #endregion
        #region Normal Variables

        [Inject] private readonly LeaderboardUserModalController lumc = null!;
        [Inject] private readonly PluginConfig PC = null!;
        [Inject] private readonly AccsaberAPI api = null!;
        [Inject] private readonly MainThreadDispatcher mainThreadDispatcher = null!;

        private AccSaberPlayer lastUser = null!;

        #endregion

        [UIAction("#post-parse")]
        private void PostParse()
        {
            _separator.color = GREY.Color();
            playerNameText.enableVertexGradient = true;
        }
        [UIAction("ShowProfile")]
        private void ShowProfile()
        {
            modalView.Hide(false);

            lumc.ShowModal(modal.transform.parent, lastUser.PlayerId);
        }

        public void BindModal(GameObject parent)
        {
            VersionUtils.Parse(ResourcePaths.LEADERBOARD_SCORE_MODAL, parent.transform, this);
            mainThreadDispatcher.EnqueueAction(() => modal.transform.SetParent(parent.transform));
        }

        public Task ShowModal(AccSaberLeaderboardEntry scoreInfo, AccSaberPlayer? playerInfo = null)
        {
            ShowStart();

            return playerInfo is null ? Task.Run(() => ShowTextsAsync(scoreInfo)) : Task.Run(() => ShowTextsAsync(scoreInfo, playerInfo));
        }
        private async Task ShowTextsAsync(AccSaberLeaderboardEntry scoreInfo)
        {
            try
            {
                AccSaberPlayer? playerInfo = await api.GetPlayerInfo(scoreInfo.PlayerId, true, false);

                if (playerInfo is not null)
                    ShowTextsAsync(scoreInfo, playerInfo);
                else
                {
                    Plugin.Log.Error("Player info is null somehow, check api for weirdness.");
                    mainThreadDispatcher.EnqueueAction(() => loader.SetActive(false));
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.Error(e);
            }
        }
        private void ShowTextsAsync(AccSaberLeaderboardEntry scoreInfo, AccSaberPlayer playerInfo) =>
            mainThreadDispatcher.EnqueueAction(() => ShowTexts(scoreInfo, playerInfo));
        private void ShowStart()
        {
            mainThreadDispatcher.AssertOnMainThread();

            parserParams.EmitEvent(modalShowName);

            (modal.transform as RectTransform)!.sizeDelta = new Vector2(containerWidth, containerHeight);
            loader.SetActive(true);
            container.SetActive(false);
        }
        private void ShowTexts(AccSaberLeaderboardEntry scoreInfo, AccSaberPlayer playerInfo)
        {
            try
            {
                mainThreadDispatcher.AssertOnMainThread();

                lastUser = playerInfo;
                AccSaberPlayerLevelData levelInfo = playerInfo.LevelData;

                string titleColor = GetTitleColor(levelInfo.PlayerTitle);
                playerNameText.colorGradient = ColorUtils.ColorToGradient(titleColor);
                playerNameText.SetText(scoreInfo.PlayerName);

                timeSetText.SetText(scoreInfo.TimeSet.ToRelativeTime(PC.TimePlaces));

                apText.SetText($"<color={AP}>{scoreInfo.AP:N2}ap</color>");
                accText.SetText($"<color={ACC}>{scoreInfo.Accuracy * 100f:N4}%</color>");
                rankText.SetText($"<color={RANK}>#{scoreInfo.Rank}</color>");

                weightedText.SetText($"<color={AP}>{scoreInfo.WeightedAp:N2}ap</color>");
                xpText.SetText($"<color={LEVEL}>{scoreInfo.XpGained:N2}xp</color>");
                scoreText.SetText($"<color={GREY}>{scoreInfo.Score:N0}</color>");

                loader.SetActive(false);
                container.SetActive(true);
            }
            catch (System.Exception e)
            {
                Plugin.Log.Error(e);
            }
        }
    }
}
