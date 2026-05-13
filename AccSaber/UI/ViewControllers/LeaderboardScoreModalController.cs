using AccSaber.Consts;
using AccSaber.Models;
using AccSaber.Utils;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Parser;
using HMUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using Zenject;
using static AccSaber.API.AccsaberAPI;
using static AccSaber.Utils.ColorUtils;

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

        [UIValue("image1x1")] public const string image1x1 = ResourcePaths.PIXEL;
        [UIValue("playerImageBorder")] public const string playerImageBorderPath = ResourcePaths.GRADIENT_CORNER;

        [UIValue("containerWidth")] public const float containerWidth = 80f;
        [UIValue("containerHeight")] public const float containerHeight = 80f;

        [UIValue("valueWidth")] public const float valueWidth = containerWidth / 3f;

        [UIValue("playerImageSize")] public const float playerImageSize = 20f;
        public const float borderSize = 3f;
        [UIValue("playerImageBGSize")] public const float playerImageBGSize = borderSize + playerImageSize;

        [UIValue("playerNameFontSize")] public const float playerNameFontSize = 7f;
        [UIValue("valueFontSize")] public const float valueFontSize = 4f;
        [UIValue("labelFontSize")] public const float labelFontSize = 2.5f;

        #endregion
        #region UI Components & Objects

        [UIParams] private BSMLParserParams parserParams = null!;

        [UIObject("modal")] private GameObject modal = null!;
        [UIComponent("modal")] private ModalView modalView = null!;

        [UIObject("container")] private GameObject container = null!;
        [UIObject("loader")] private GameObject loader = null!;

        [UIComponent("playerNameText")] private TextMeshProUGUI playerNameText = null!;

        [UIComponent("playerImage")] private ImageView playerImage = null!;
        [UIComponent("playerImageBackground")] private ImageView playerImageBackground = null!;
        [UIComponent("playerImageBorder")] private ImageView playerImageBorder = null!;

        [UIComponent("complexityText")] private TextMeshProUGUI complexityText = null!;
        [UIComponent("timeSetText")] private TextMeshProUGUI timeSetText = null!;
        [UIComponent("accTypeText")] private TextMeshProUGUI accTypeText = null!;

        [UIComponent("rankText")] private TextMeshProUGUI rankText = null!;
        [UIComponent("apText")] private TextMeshProUGUI apText = null!;
        [UIComponent("accText")] private TextMeshProUGUI accText = null!;

        [UIComponent("weightedText")] private TextMeshProUGUI weightedText = null!;
        [UIComponent("xpText")] private TextMeshProUGUI xpText = null!;
        [UIComponent("scoreText")] private TextMeshProUGUI scoreText = null!;

        #endregion
        #region Normal Variables

        [Inject] private readonly LeaderboardUserModalController lumc = null!;
        private AccSaberUser lastUser = null!;
        private MonoBehaviour currentHost = null!;

        #endregion

        [UIAction("#post-parse")]
        private void PostParse()
        {
            playerImage.material = ResourcePaths.BORDER_MATERIAL;
            playerImageBackground.material = ResourcePaths.BORDER_MATERIAL;
            playerImageBorder.material = ResourcePaths.BORDER_MATERIAL;
            playerNameText.enableVertexGradient = true;
        }
        [UIAction("ShowProfile")]
        private void ShowProfile()
        {
            modalView.Hide(false);

            lumc.ShowModal(modal.transform.parent, currentHost, lastUser.PlayerId);
        }

        public void BindModal(GameObject parent)
        {
            VersionUtils.Parse(ResourcePaths.LEADERBOARD_SCORE_MODAL, parent.transform, this);
            modal.transform.SetParent(parent.transform);

            //lumc.Parse(parent.transform);
        }

        public Task ShowModal(MonoBehaviour host, AccSaberLeaderboardEntry scoreInfo, AccSaberUser? playerInfo = null)
        {
            ShowModalStart(host);

            return playerInfo is null ? Task.Run(() => ShowTextsAsync(host, scoreInfo)) : Task.Run(() => ShowTextsAsync(host, scoreInfo, playerInfo));
        }
        private async Task ShowTextsAsync(MonoBehaviour host, AccSaberLeaderboardEntry scoreInfo)
        {
            AccSaberUser? playerInfo = await GetPlayerInfo(scoreInfo.PlayerId, true);
            if (playerInfo is not null)
                await ShowTextsAsync(host, scoreInfo, playerInfo);
        }
        private async Task ShowTextsAsync(MonoBehaviour host, AccSaberLeaderboardEntry scoreInfo, AccSaberUser playerInfo) =>
            await Task.Run(() => host.StartCoroutine(ShowTexts(scoreInfo, playerInfo)));
        private void ShowModalStart(MonoBehaviour host)
        {
            currentHost = host;
            parserParams.EmitEvent(modalShowName);
            host.StartCoroutine(ShowStart());
        }
        private IEnumerator ShowStart()
        {
            yield return new WaitForEndOfFrame();

            (modal.transform as RectTransform)!.sizeDelta = new Vector2(containerWidth, containerHeight);
            loader.SetActive(true);
            container.SetActive(false);
        }
        private IEnumerator ShowTexts(AccSaberLeaderboardEntry scoreInfo, AccSaberUser playerInfo)
        {
            lastUser = playerInfo;
            LevelData levelInfo = playerInfo.LevelData;

            yield return new WaitForEndOfFrame();

            string titleColor = GetTitleColor(levelInfo.PlayerTitle);
            playerNameText.colorGradient = ColorUtils.ColorToGradient(titleColor);
            playerNameText.SetText(scoreInfo.PlayerName);

            if (ColorUtility.TryParseHtmlString(titleColor, out Color c))
                playerImageBorder.color = c;

            timeSetText.SetText(scoreInfo.TimeSet.ToRelativeTime(2));

            apText.SetText($"<color={AP}>{scoreInfo.AP:N2}ap</color>");
            accText.SetText($"<color={ACC}>{scoreInfo.Accuracy * 100f:N4}%</color>");
            rankText.SetText($"<color={RANK}>#{scoreInfo.Rank}</color>");

            weightedText.SetText($"<color={AP}>{scoreInfo.WeightedAp:N2}ap</color>");
            xpText.SetText($"<color={LEVEL}>{scoreInfo.XpGained:N2}xp</color>");
            scoreText.SetText($"<color={GREY}>{scoreInfo.Score:N0}</color>");

            playerImage.SetImageAsync(scoreInfo.AvatarURL);

            yield return new WaitForFixedUpdate();

            loader.SetActive(false);
            container.SetActive(true);
        }
    }
}
