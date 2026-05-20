using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AccSaber.Consts
{
    internal static class ResourcePaths
    {
        public static Material BORDER_MATERIAL => Resources.FindObjectsOfTypeAll<Material>().Last(x => x.name == "UINoGlowRoundEdge");

        #region Image File paths

        public const string RESOURCE_PATH = "AccSaber.Resources";
        public const string LOGO_BORDER = RESOURCE_PATH + ".ACC_Logo_BlackBorder.png";
        public const string ACCSABER = RESOURCE_PATH + ".AccSaber.png";
        public const string COUNTRY = RESOURCE_PATH + ".country.png";
        public const string FRIEND = RESOURCE_PATH + ".friend.png";
        public const string GLOBAL = RESOURCE_PATH + ".global.png";
        public const string INFO_ICON = RESOURCE_PATH + ".InfoIcon.png";
        public const string PIXEL = RESOURCE_PATH + ".Pixel.png";
        public const string PLAYER_ICON = RESOURCE_PATH + ".PlayerIcon.png";

        public const string SWAP = RESOURCE_PATH + ".swap.png";
        public const string RIVALS = RESOURCE_PATH + ".rivals.png";
        public const string FOLLOWED = RESOURCE_PATH + ".followed.png";
        public const string RELATIONS = RESOURCE_PATH + ".relations.png";
        public const string TOP_ARROW = RESOURCE_PATH + ".topArrow.png";
        public const string YOU = RESOURCE_PATH + ".you.png";
        //public const string RESOURCE_BLOCK = RESOURCE_PATH + ".block.png";
        public const string GRADIENT = RESOURCE_PATH + ".gradient.png";
        public const string CELL_PIXEL = RESOURCE_PATH + ".rounded.png";
        //public const string RESOURCE_GRADIENT_PANEL = RESOURCE_PATH + ".panelGradient.png";
        public const string GRADIENT_CORNER = RESOURCE_PATH + ".cornerGradient.png";
        //public const string RESOURCE_GRADIENT_HEADER = RESOURCE_PATH + ".headerBG.png";

        #endregion
        #region BSML File Paths

        public const string MAIN_BSML_PATH = "AccSaber.UI.Views";
        public const string ACC_SABER_LEADERBOARD_VIEW = MAIN_BSML_PATH + ".AccSaberLeaderboardView.bsml";
        public const string ACC_SABER_PANEL_VIEW = MAIN_BSML_PATH + ".AccSaberPanelView.bsml";
        public const string LEADERBOARD_CELL = MAIN_BSML_PATH + ".LeaderboardCell.bsml";
        public const string LEADERBOARD_SCORE_MODAL = MAIN_BSML_PATH + ".LeaderboardScoreModal.bsml";
        public const string LEADERBOARD_USER_MODAL = MAIN_BSML_PATH + ".LeaderboardUserModal.bsml";
        public const string LEADERBOARD_TITLE_PANEL = MAIN_BSML_PATH + ".LeaderboardTitlePanel.bsml";
        public const string WHERE_SCORE_MODAL_VIEW = MAIN_BSML_PATH + ".WhereScoreModalView.bsml";

        public const string MENU_BSML_PATH = "AccSaber.UI.MenuButton.Views";
        public const string ACC_SABER_MENU_VIEW = MENU_BSML_PATH + ".AccSaberMenuView.bsml";
        public const string ACC_SABER_MENU_CELL = MENU_BSML_PATH + ".AccSaberMenuCell.bsml";
        public const string ACC_SABER_MILESTONE_VIEW = MENU_BSML_PATH + ".AccSaberMilestoneView.bsml";
        public const string ACC_SABER_NEWS_VIEW = MENU_BSML_PATH + ".AccSaberNewsView.bsml";
        public const string ACC_SABER_NEWS_MODAL = MENU_BSML_PATH + ".AccSaberNewsModal.bsml";

        #endregion
        #region Data File Paths

        public const string HOST_NAME = "AccSaber";

        // Note: Once a release is made, if the below consts are changed, extra steps for migration will be needed.
        public const string FOLDER_NAME = "Accsaber";
        public const string PLAYER_SCORE_CACHE_NAME = "PlayerScoreCache";
        public const string MAP_CACHE_NAME = "MapCache";

        public static readonly string ACC_SABER_DATA_FOLDER = Path.Combine(Environment.CurrentDirectory, "UserData", FOLDER_NAME);
        public static readonly string ACC_SABER_PLAYER_SCORE_CACHE = Path.Combine(ACC_SABER_DATA_FOLDER, PLAYER_SCORE_CACHE_NAME + ".json");
        public static readonly string ACC_SABER_MAP_CACHE = Path.Combine(ACC_SABER_DATA_FOLDER, MAP_CACHE_NAME + ".json");

        #endregion
    }
}
