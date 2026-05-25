namespace AccSaber.API
{
    /// <summary>
    /// Collection of constant API path templates used to communicate with the AccSaber Reloaded web API.
    /// </summary>
    /// <remarks>
    /// Each string is either a full URL or a format string (use <see cref="string.Format(string, object[])"/>)
    /// where placeholders like "{0}", "{1}" represent parameters documented per constant.
    /// Docs: https://api.accsaberreloaded.com/v1/docs
    /// </remarks>
    public static class HelpfulPaths
    {
        // Category ID: b0000000-0000-0000-0000-000000000003 for Tech, 2 = Standard, 1 = True, 5 = overall.
        // Examples:
        // Score endpoint example: https://api.accsaberreloaded.com/v1/users/76561198306905129/scores/by-hash/2a579bb1a3efa58af7640f9663c972ee84fea44a?difficulty=EXPERT&characteristic=Standard
        // Diff endpoint example: https://api.accsaberreloaded.com/v1/maps/hash/2A579BB1A3EFA58AF7640F9663C972EE84FEA44A?difficulty=EXPERT
        // Page example: https://api.accsaberreloaded.com/v1/maps/difficulties/6af8724c-4cbd-4b8e-90c2-34b34a0a5d73/scores?page=0&size=10
        // Player example: https://api.accsaberreloaded.com/v1/users/76561198306905129?statistics=true
        // Milestone example: https://api.accsaberreloaded.com/v1/users/76561198306905129/milestones?page=0&size=10

        /// <summary>
        /// Base API URL for AccSaber Reloaded (version 1).
        /// </summary>
        public const string APAPI = "https://api.accsaberreloaded.com/v1/";

        /// <summary>
        /// Health check endpoint (no parameters).
        /// </summary>
        public const string APAPI_TEST = APAPI + "health/ping"; //no params

        /// <summary>
        /// Retrieves all map difficulties (no parameters).
        /// </summary>
        public const string APAPI_DIFFS = APAPI + "maps/difficulties/all"; //no params

        /// <summary>
        /// Retrieves all valid modifiers.
        /// </summary>
        public const string APAPI_MODS = APAPI + "modifiers"; //no params

        /// <summary>
        /// Retrieves paged list of maps.
        /// </summary>
        /// <remarks>
        /// Format parameters: page (zero-indexed), size (count).
        /// Example: <c>string.Format(APAPI_MAPS, page, size)</c>
        /// </remarks>
        public const string APAPI_MAPS = APAPI + "maps?page={0}&size={1}"; //page (zero indexed), count

        public const string APAPI_DIFF = APAPI + "maps/difficulties?status={0}&page={1}&size={2}"; // status (QUEUE, QUALIFIED, RANK), page (zero indexed), size 


        /// <summary>
        /// Retrieves a user's profile by ID.
        /// </summary>
        /// <remarks>
        /// Format parameters: user_id, includeStatistics (true/false).
        /// Example: <c>string.Format(APAPI_PLAYERID, userId, true)</c>
        /// </remarks>
        public const string APAPI_PLAYERID = APAPI + "users/{0}?statistics={1}"; //user_id, true or false for whether to include statistics in the response

        /// <summary>
        /// Retrieves a user's statistics filtered by category.
        /// </summary>
        /// <remarks>
        /// Format parameters: user_id, category (overall, true_acc, standard_acc, tech_acc).
        /// </remarks>
        public const string APAPI_PLAYERID_CATEGORY = APAPI + "users/{0}/statistics?category={1}"; //user_id, category (overall, true_acc, standard_acc, tech_acc)

        /// <summary>
        /// Retrieves statistics differences for a user in a given category.
        /// </summary>
        /// <remarks>
        /// Format parameters: user_id, category (overall, true_acc, standard_acc, tech_acc).
        /// </remarks>
        public const string APAPI_PLAYER_STATDIFF = APAPI + "users/{0}/stats-diff?category={1}"; //user_id, category (overall, true_acc, standard_acc, tech_acc)

        /// <summary>
        /// Retrieves all statistics differences for a user across all categories.
        /// </summary>
        /// <remarks>
        /// Format parameters: user_id.
        /// Example: <c>string.Format(APAPI_PLAYER_STATDIFF_ALL, userId)</c>
        /// </remarks>
        public const string APAPI_PLAYER_STATDIFF_ALL = APAPI + "users/{0}/stats-diff/all"; //user_id

        /// <summary>
        /// Retrieves a specific score by map hash for a user.
        /// </summary>
        /// <remarks>
        /// Format parameters: user_id, hash, difficulty (IN CAPS). Characteristic is set to Standard.
        /// </remarks>
        public const string APAPI_SCORE = APAPI + "users/{0}/scores/by-hash/{1}?difficulty={2}&characteristic=Standard"; //user_id, hash, difficulty IN CAPS

        /// <summary>
        /// Retrieves paged scores for a user.
        /// </summary>
        /// <remarks>
        /// Format parameters: user_id, page (zero-indexed), count.
        /// </remarks>
        public const string APAPI_SCORES = APAPI + "users/{0}/scores?page={1}&size={2}"; //user_id, page (zero indexed), count

        /// <summary>
        /// Retrieves a user's scores filtered by categoryId.
        /// </summary>
        /// <remarks>
        /// Format parameters: user_id, category_id, page (zero-indexed), count.
        /// </remarks>
        public const string APAPI_CATEGORY_SCORES = APAPI + "users/{0}/scores?categoryId={1}&page={2}&size={3}"; // user_id, category_id, page (zero indexed), count

        /// <summary>
        /// Retrieves map data by full hash.
        /// </summary>
        /// <remarks>
        /// Format parameters: hash.
        /// </remarks>
        public const string APAPI_HASH = APAPI + "maps/hash/{0}"; //hash

        /// <summary>
        /// Retrieves map data by hash filtered to a specific difficulty.
        /// </summary>
        /// <remarks>
        /// Format parameters: hash, difficulty (IN CAPS).
        /// </remarks>
        public const string APAPI_HASH_DIFF = APAPI + "maps/hash/{0}?difficulty={1}"; //hash, difficulty IN CAPS

        /// <summary>
        /// Retrieves leaderboard scores for a difficulty (paged).
        /// </summary>
        /// <remarks>
        /// Format parameters: diff_id, page (zero-indexed), count.
        /// </remarks>
        public const string APAPI_LEADERBOARD_DIFF = APAPI + "maps/difficulties/{0}/scores?page={1}&size={2}"; //diff_id, page (zero indexed), count

        /// <summary>
        /// Retrieves leaderboard scores for a difficulty with a relation filter. Requires authentication.
        /// </summary>
        /// <remarks>
        /// Format parameters: diff_id, relationType, page (zero-indexed), count.
        /// RelationType examples: "outgoing", "incoming".
        /// </remarks>
        public const string APAPI_LEADERBOARD_DIFF_RELATION = APAPI + "maps/difficulties/{0}/scores?relation={1}&page={2}&size={3}"; //[REQUIRES AUTH] diff_id, RelationType, page (zero indexed), count

        /// <summary>
        /// Retrieves leaderboard scores for a difficulty filtered by country.
        /// </summary>
        /// <remarks>
        /// Format parameters: diff_id, country, page (zero-indexed), count.
        /// </remarks>
        public const string APAPI_LEADERBOARD_DIFF_COUNTRY = APAPI + "maps/difficulties/{0}/scores?country={1}&page={2}&size={3}"; //diff_id, country, page (zero indexed), count

        /// <summary>
        /// Retrieves a user's milestones (paged).
        /// </summary>
        /// <remarks>
        /// Format parameters: user_id, page, size.
        /// </remarks>
        public const string APAPI_MILESTONE = APAPI + "users/{0}/milestones?page={1}&size={2}"; //user_id, page, size

        /// <summary>
        /// Retrieves milestone levels (no parameters).
        /// </summary>
        public const string APAPI_MILESTONES = APAPI + "levels"; //no params

        /// <summary>
        /// Retrieves milestones a user has completed.
        /// </summary>
        /// <remarks>
        /// Format parameters: user_id.
        /// </remarks>
        public const string APAPI_MILESTONE_COMPLETE = APAPI + "users/{0}/milestones/completed"; //user_id

        /// <summary>
        /// Retrieves milestones a user has not completed.
        /// </summary>
        /// <remarks>
        /// Format parameters: user_id.
        /// </remarks>
        public const string APAPI_MILESTONE_INCOMPLETE = APAPI + "users/{0}/milestones/uncompleted"; //user_id

        /// <summary>
        /// Retrieves a users missions.
        /// </summary>
        public const string APAPI_MISSIONS = APAPI + "users/me/missions";

        /// <summary>
        /// Retrieves a users completed missions.
        /// </summary>
        public const string APAPI_MISSIONS_COMPLETED = APAPI + "users/me/missions/completed";

        /// <summary>
        /// Retrieves a users missions from a specified pool.
        /// </summary>
        /// <remarks>
        /// Format parameters: pool.
        /// </remarks>
        public const string APAPI_MISSIONS_POOL = APAPI + "users/me/missions?pool={1}"; //pool

        /// <summary>
        /// Retrieves all news entries.
        /// </summary>
        public const string APAPI_NEWS = APAPI + "news"; //no params

        /// <summary>
        /// Retrieves specified news entries.
        /// </summary>
        /// <remarks>
        /// Format parameters: type.
        /// </remarks>
        public const string APAPI_NEWS_TYPE = APAPI + "news?type={0}"; //type


        /// <summary>
        /// Retrieves relations for a user with type and direction filters (paged).
        /// </summary>
        /// <remarks>
        /// Format parameters: user_id, RelationType, direction ("outgoing" or "incoming"), page (zero-indexed), size.
        /// </remarks>
        public const string APAPI_RELATIONS = APAPI + "users/{0}/relations?type={1}&direction={2}&page={3}&size={4}";

        /// <summary>
        /// Authenticated endpoint to set (POST) relations for the current authenticated user.
        /// </summary>
        public const string APAPI_AUTH_SET_RELATION = APAPI + "users/me/relations"; // POST endpoint

        /// <summary>
        /// Authenticated endpoint to delete a relation by id for the current authenticated user.
        /// </summary>
        /// <remarks>
        /// Format parameters: relationId
        /// </remarks>
        public const string APAPI_AUTH_DELETE_RELATION = APAPI_AUTH_SET_RELATION + "/{0}"; // DELETE endpoint: relationId

        /// <summary>
        /// Authenticated endpoint to get relations filtered by type (paged) for the current user.
        /// </summary>
        /// <remarks>
        /// Format parameters: RelationType, page (zero-indexed), size.
        /// </remarks>
        public const string APAPI_AUTH_GET_RELATIONS = APAPI_AUTH_SET_RELATION + "?type={0}&page={1}&size={2}"; // RelationType, page (zero indexed), size

        /// <summary>
        /// Authenticated endpoint to get all relations (paged) for the current user.
        /// </summary>
        /// <remarks>
        /// Format parameters: page (zero-indexed), size.
        /// </remarks>
        public const string APAPI_AUTH_GET_RELATIONS_ALL = APAPI_AUTH_SET_RELATION + "?page={0}&size={1}"; // page (zero indexed), size

        /// <summary>
        /// Authenticated endpoint to read user settings by group (e.g., "privacy", "appearance").
        /// </summary>
        /// <remarks>
        /// Format parameters: settings group name.
        /// </remarks>
        public const string APAPI_AUTH_GET_SETTINGS = APAPI + "users/me/settings/{0}"; // group ("privacy", "appearance", etc)

        /// <summary>
        /// Authenticated in-game authentication endpoint (POST).
        /// </summary>
        public const string APAPI_AUTH = APAPI + "auth/ingame"; // POST endpoint

        public const string APAPI_AUTH_END = APAPI + "auth/logout"; // POST endpoint

        internal const string APAPI_SCORE_SUBMIT = APAPI + "submit"; // POST endpoint

        /// <summary>
        /// WebSocket URL for real-time score updates.
        /// </summary>
        public const string APAPI_WEBSOCKET = "wss://accsaberreloaded.com/ws/scores";
    }
}