using System;
using System.Numerics;

namespace AccSaber.Utils
{
    public enum APCategory
    {
        True, Standard, Tech, Overall
    }
    public enum RelationType
    {
        follower, rival, blocked
    }
    public enum LeaderboardDisplayType
    {
        Global = 1, Country = 2, Followed = 4, Rivals = 8, Relations = Followed | Rivals | 16, Blocked = 32
    }
    public enum MapStatus
    {
        Queue, Qualified, Ranked
    }
    public enum MissionPool
    {
        Daily, Weekly, Event
    }
    public enum MissionBand
    {
        easy, medium, hard, extreme
    }
    public static class EnumUtils
    {
        public static string DiffNumToReloadedDiff(int diffNum) => diffNum switch
        {
            1 => "EASY",
            3 => "NORMAL",
            5 => "HARD",
            7 => "EXPERT",
            9 => "EXPERT_PLUS",
            _ => throw new ArgumentException("Invalid difficulty number. Must be one of the following: 1, 3, 5, 7, 9.")
        };
        public static string DiffToReloadedDiff(BeatmapDifficulty diff) => DiffNumToReloadedDiff(FromDiff(diff));
        public static int ReloadedDiffToDiffNum(string diff) => diff switch
        {
            "EASY" => 1,
            "NORMAL" => 3,
            "HARD" => 5,
            "EXPERT" => 7,
            "EXPERT_PLUS" => 9,
            _ => throw new ArgumentException("Invalid difficulty string. Must be one of the following: EASY, NORMAL, HARD, EXPERT, EXPERT_PLUS.")
        };
        public static BeatmapDifficulty ReloadedDiffToDiff(string diff) => ToDiff(ReloadedDiffToDiffNum(diff));
        public static APCategory? ReloadedCategoryToEnum(string category) => category switch
        {
            "b0000000-0000-0000-0000-000000000001" => APCategory.True,
            "b0000000-0000-0000-0000-000000000002" => APCategory.Standard,
            "b0000000-0000-0000-0000-000000000003" => APCategory.Tech,
            "b0000000-0000-0000-0000-000000000005" => APCategory.Overall,
            _ => null
        };
        public static string? CategoryIdToReloadedCategory(string category) => category switch
        {
            "True" => "b0000000-0000-0000-0000-000000000001",
            "Standard" => "b0000000-0000-0000-0000-000000000002",
            "Tech" => "b0000000-0000-0000-0000-000000000003",
            "Overall" => "b0000000-0000-0000-0000-000000000005",
            _ => null
        };
        public static string? EnumToReloadedCategory(APCategory category) => CategoryIdToReloadedCategory(category.ToString());
        public static string? EnumToRankedStatus(MapStatus status) => status switch
        {
            MapStatus.Qualified => "QUALIFIED",
            MapStatus.Ranked => "RANKED",
            MapStatus.Queue => "QUEUE",
            _ => null
        };
        public static MapStatus? RankedStatusToEnum(string status) => status switch
        {
            "QUALIFIED" => MapStatus.Qualified,
            "RANKED" => MapStatus.Ranked,
            "QUEUE" => MapStatus.Queue,
            _ => null
        };
        public static int FromDiff(BeatmapDifficulty diff) => (int)diff * 2 + 1;
        public static BeatmapDifficulty ToDiff(int diffNum) => (BeatmapDifficulty)((diffNum - 1) / 2);

        public static LeaderboardDisplayType Convert(this RelationType rt) => (LeaderboardDisplayType)BitOperations.Pow2((int)rt + 2);
        public static RelationType Convert(this LeaderboardDisplayType ldt)
        {
            RelationType outp = (RelationType)(BitOperations.Log2((uint)ldt) - 2);

            if (outp < RelationType.follower || outp > RelationType.blocked)
                throw new ArgumentException("The given display type must be able to be converted to RelationType");

            return outp;
        }
    }
}
