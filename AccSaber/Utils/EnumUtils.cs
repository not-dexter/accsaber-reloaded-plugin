using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AccSaber.Utils
{
    public enum ReloadedDifficulty
    {
        EASY = 1, NORMAL = 3, HARD = 5, EXPERT = 7, EXPERT_PLUS = 9
    }
    public enum ReloadedAPCategory
    {
        true_acc, standard_acc, tech_acc, overall
    }
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
    public enum BatchStatus
    {
        DRAFT, RELEASE_READY, RELEASED
    }

    // From: https://github.com/accsaber/accsaber-reloaded-backend/blob/main/src/main/java/com/accsaber/backend/model/entity/mission/MissionPool.java
    // (not exactly the same since "event" cannot be made an enum)
    public enum MissionPool
    {
        Daily, Weekly, Event
    }

    // From: https://github.com/accsaber/accsaber-reloaded-backend/blob/main/src/main/java/com/accsaber/backend/model/entity/mission/MissionBand.java
    public enum MissionBand
    {
        easy, medium, hard, extreme
    }
    // From: https://github.com/accsaber/accsaber-reloaded-backend/blob/main/src/main/java/com/accsaber/backend/model/entity/mission/MissionType.java
    // Moved them around to group up specific types
    public enum MissionType
    {
        PLAY_N_MAPS,
        XP_IN_WINDOW,
        ACC_ON_MAP,
        AP_ON_MAP,
        PB_SPECIFIC_MAP,
        SNIPE_PLAYER_ON_MAP,
        STREAK_ON_MAP,
        PB_ABOVE_THRESHOLD,
        STREAK_N_IN_CATEGORY,
        COMEBACK_PB,
        SCORES_N
    }
    // From: https://github.com/accsaber/accsaber-reloaded-backend/blob/main/src/main/java/com/accsaber/backend/model/entity/mission/MissionStatus.java
    public enum MissionStatus
    {
        active,
        completed,
        expired,
        voided
    }

    public enum ComparisonType
    {
        NONE = 0, NOT = 1, EQ = 2, NE = NOT | EQ, LT = 4, GT = 8, LTE = LT | EQ, GTE = GT | EQ, FLIP = EQ | LT | GT, ALL = NOT | EQ | LT | GT
    }

    public enum FunctionType
    {
        MIN, MAX, COUNT, COUNT_DISTINCT
    }

    public static class EnumUtils
    {
        //public static readonly Guid OverallReloadedCategory = Guid.Parse("b0000000-0000-0000-0000-000000000005");

        public static ReloadedDifficulty DiffToReloadedDiff(BeatmapDifficulty diff) => (ReloadedDifficulty)FromDiff(diff);
        public static BeatmapDifficulty ReloadedDiffToDiff(ReloadedDifficulty diff) => ToDiff((int)diff);
        private static APCategory ReloadedCategoryIdToCategory(string? categoryId) => categoryId switch
        {
            "b0000000-0000-0000-0000-000000000001" => APCategory.True,
            "b0000000-0000-0000-0000-000000000002" => APCategory.Standard,
            "b0000000-0000-0000-0000-000000000003" => APCategory.Tech,
            "b0000000-0000-0000-0000-000000000005" or null => APCategory.Overall,
            _ => throw new ArgumentException($"The given category id \"{categoryId}\" cannot be converted to an {nameof(APCategory)} enum.")
        };
        public static APCategory ReloadedCategoryIdToCategory(Guid? categoryId) => ReloadedCategoryIdToCategory(categoryId?.ToString());
        public static Guid CategoryIdToReloadedCategoryId(string? category) => Guid.Parse(category switch
        {
            nameof(APCategory.True) => "b0000000-0000-0000-0000-000000000001",
            nameof(APCategory.Standard) => "b0000000-0000-0000-0000-000000000002",
            nameof(APCategory.Tech) => "b0000000-0000-0000-0000-000000000003",
            nameof(APCategory.Overall) or null => "b0000000-0000-0000-0000-000000000005",
            _ => throw new ArgumentException($"The given category \"{category}\" cannot be converted to a reloaded UUID.")
        });
        public static Guid CategoryToReloadedCategoryId(APCategory category) => CategoryIdToReloadedCategoryId(category.ToString());
        public static ReloadedAPCategory CategoryToReloadedCategory(string? category) => category switch
        {
            nameof(APCategory.True) => ReloadedAPCategory.true_acc,
            nameof(APCategory.Standard) => ReloadedAPCategory.standard_acc,
            nameof(APCategory.Tech) => ReloadedAPCategory.tech_acc,
            nameof(APCategory.Overall) or null => ReloadedAPCategory.overall,
            _ => throw new ArgumentException($"The given category \"{category}\" cannot be converted to a {nameof(ReloadedAPCategory)} enum.")
        };
        public static ReloadedAPCategory CategoryToReloadedCategory(APCategory category) => (ReloadedAPCategory)(int)category;
        public static APCategory ReloadedCategoryToCategory(ReloadedAPCategory category) => (APCategory)(int)category;
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

        public static SpecifiedComparer<T> ToComparison<T>(this ComparisonType compType) where T : IComparable<T>
        {
            return compType switch
            {
                ComparisonType.EQ => (a, b) => a.CompareTo(b) == 0,
                ComparisonType.NE => (a, b) => a.CompareTo(b) != 0,
                ComparisonType.GT => (a, b) => a.CompareTo(b) > 0,
                ComparisonType.LT => (a, b) => a.CompareTo(b) < 0,
                ComparisonType.GTE => (a, b) => a.CompareTo(b) >= 0,
                ComparisonType.LTE => (a, b) => a.CompareTo(b) <= 0,
                _ => throw new ArgumentException($"The given ComparisonType must be a valid type (comType = {compType}).")
            };
        }
        public static SpecifiedComparer ToComparison(this ComparisonType compType)
        {
            return compType switch
            {
                ComparisonType.EQ => (a, b) => a.CompareTo(b) == 0,
                ComparisonType.NE => (a, b) => a.CompareTo(b) != 0,
                ComparisonType.GT => (a, b) => a.CompareTo(b) > 0,
                ComparisonType.LT => (a, b) => a.CompareTo(b) < 0,
                ComparisonType.GTE => (a, b) => a.CompareTo(b) >= 0,
                ComparisonType.LTE => (a, b) => a.CompareTo(b) <= 0,
                _ => throw new ArgumentException($"The given ComparisonType must be a valid type (comType = {compType}).")
            };
        }
        public static bool Compare<T>(this ComparisonType compType, T x, T y) where T : IComparable<T> => compType.ToComparison<T>()(x, y);
        public static bool Compare<T1, T2>(this ComparisonType compType, T1 x, T2 y) where T1 : IComparable where T2 : IComparable => compType.ToComparison()(x, y);
        public static string ToComparisonString(this ComparisonType compType)
        {
            string outp = "";

            if (compType >= ComparisonType.LT && (compType & ComparisonType.NOT) > 0)
                compType = compType.Invert();

            if ((compType & ComparisonType.NOT) > 0)
                outp += '!';
            if ((compType & ComparisonType.EQ) > 0)
                outp += '=';
            if ((compType & ComparisonType.LT) > 0)
                outp += '<';
            if ((compType & ComparisonType.GT) > 0)
                outp += '>';

            return outp;
        }
        public static ComparisonType FromComparisonString(this string str)
        {
            ComparisonType outp = ComparisonType.NONE;

            if (str.IndexOf('!') != -1)
                outp |= ComparisonType.NOT;
            if (str.IndexOf('=') != -1)
                outp |= ComparisonType.EQ;
            if (str.IndexOf('<') != -1)
                outp |= ComparisonType.LT;
            if (str.IndexOf('>') != -1)
                outp |= ComparisonType.GT;

            return outp;
        }
        public static ComparisonType Invert(this ComparisonType compType) => compType >= ComparisonType.LT ? compType ^ ComparisonType.ALL : compType;
        public static ComparisonType Flip(this ComparisonType compType) => compType >= ComparisonType.LT ? compType ^ ComparisonType.FLIP : compType ^ ComparisonType.NOT;


        public static bool Execute(this FunctionType funcType, string column, float targetValue, JObject score)
        {
            return funcType switch
            {
                FunctionType.MIN => score[column] is IComparable comp && comp.CompareTo(targetValue) >= 0,
                FunctionType.MAX => score[column] is IComparable comp && comp.CompareTo(targetValue) <= 0,
                _ => false
            };
        }
        public static IEnumerable<IComparable> Execute<T>(this FunctionType funcType, string column, IEnumerable<T> input, float targetValue = default) where T : notnull
        {
            IEnumerable<IComparable> arr = input.Select(item => (JObject.FromObject(item)[column] as IComparable)!).Where(item => item is not null);

            return funcType switch
            {
                FunctionType.MIN => arr.Where(comp => comp.CompareTo(targetValue) >= 0),
                FunctionType.MAX => arr.Where(comp => comp.CompareTo(targetValue) <= 0),
                FunctionType.COUNT => [arr.Count()],
                FunctionType.COUNT_DISTINCT => [arr.Distinct().Count()],
                _ => throw new ArgumentException("The given function type cannot be executed with the given arguments")
            };
        }
    }
}
