using System.Linq;
using UnityEngine;

namespace AccSaber.Consts
{
    internal static class ResourcePaths
    {
        public static readonly Material BORDER_MATERIAL = Resources.FindObjectsOfTypeAll<Material>().Last(x => x.name == "UINoGlowRoundEdge");

        public const string RESOURCE_PATH = "AccSaber.Resources";
        public const string LOGO_BORDER = RESOURCE_PATH + ".ACC_Logo_BlackBorder.png";
        public const string ACCSABER = RESOURCE_PATH + ".AccSaber.png";
        public const string COUNTRY = RESOURCE_PATH + ".country.png";
        public const string FRIEND = RESOURCE_PATH + ".friend.png";
        public const string GLOBAL_ICON = RESOURCE_PATH + ".GlobalIcon.png";
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
        //public const string RESOURCE_GRADIENT = RESOURCE_PATH + ".gradient.png";
        //public const string RESOURCE_GRADIENT_PANEL = RESOURCE_PATH + ".panelGradient.png";
        public const string GRADIENT_CORNER = RESOURCE_PATH + ".cornerGradient.png";
        //public const string RESOURCE_GRADIENT_HEADER = RESOURCE_PATH + ".headerBG.png";

        public const string BSML_PATH = "AccSaber.UI.Views";
        public const string ACC_SABER_LEADERBOARD_VIEW = BSML_PATH + ".AccSaberLeaderboardView.bsml";
        public const string ACC_SABER_PANEL_VIEW = BSML_PATH + ".AccSaberPanelView.bsml";
        public const string LEADERBOARD_CELL = BSML_PATH + ".LeaderboardCell.bsml";
        public const string LEADERBOARD_SCORE_MODAL = BSML_PATH + ".LeaderboardScoreModal.bsml";
        public const string LEADERBOARD_USER_MODAL = BSML_PATH + ".LeaderboardUserModal.bsml";
        public const string LEADERBOARD_TITLE_PANEL = BSML_PATH + ".LeaderboardTitlePanel.bsml";
        public const string WHERE_SCORE_MODAL_VIEW = BSML_PATH + ".WhereScoreModalView.bsml";
    }
}
