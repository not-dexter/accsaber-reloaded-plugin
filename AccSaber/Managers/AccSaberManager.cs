using LeaderboardCore.Interfaces;

namespace AccSaber.Managers
{
	internal class AccSaberManager(AccSaberStore accSaberStore, BeatmapLevelsModel beatmapLevelsModel) : INotifyLeaderboardSet
	{
        public const string CUSTOM_LEVEL_HASH = "custom_level_";

        private readonly AccSaberStore _accSaberStore = accSaberStore;
        private readonly BeatmapLevelsModel _beatmapLevelsModel = beatmapLevelsModel;

#if NEW_VERSION
        public void OnLeaderboardSet(BeatmapKey beatmapKey)
        {
            try
            {
                string? hash = GetHash(beatmapKey, out BeatmapLevel? level);

                if (hash is null)
                {
                    Plugin.Log.Critical("Cannot set the leaderboard, the hash is somehow null!!!");
                    return;
                }

                _accSaberStore.SetMapFromBasicInfo(hash, beatmapKey.difficulty);
                _accSaberStore.SetCurrentMap(beatmapKey, level);
            }
            catch (System.Exception e)
            {
                Plugin.Log.Error(e);
            }
        }

        public string? GetHash(BeatmapKey beatmapKey) => GetHash(beatmapKey, out _);
        public string? GetHash(BeatmapKey beatmapKey, out BeatmapLevel? level)
        {
            try
            {
                level = _beatmapLevelsModel.GetBeatmapLevel(beatmapKey.levelId);

#if V40
                string? hash = level is null ? null : SongCore.Utilities.Hashing.ComputeCustomLevelHash(level).ToLower();
#else
                string? hash = level is null ? null : SongCore.Utilities.Hashing.GetCustomLevelHash(level).ToLower();
#endif
                if (string.IsNullOrEmpty(hash))
                {
                    hash = beatmapKey.levelId;

                    if (hash.StartsWith(CUSTOM_LEVEL_HASH))
                        hash = hash[CUSTOM_LEVEL_HASH.Length..];

                    Plugin.Log.Warn("Hash was given as null, setting hash to: " + hash);
                }

                return hash;
            }
            catch (System.Exception e)
            {
                Plugin.Log.Error("There was an error trying to find the hash\n" + e);
                level = null;
                return null;
            }
        }
#else
        public void OnLeaderboardSet(IDifficultyBeatmap beatmapKey)
        {
			try
            {
                string? hash = GetHash(beatmapKey);

                if (hash is null)
                {
                    Plugin.Log.Critical("Cannot set the leaderboard, the hash is somehow null!!!");
                    return;
                }

                _accSaberStore.SetMapFromBasicInfo(hash, beatmapKey.difficulty);
                _accSaberStore.SetCurrentMap(beatmapKey);
            }
            catch (System.Exception e)
            {
                Plugin.Log.Error(e);
            }
        }

        public string? GetHash(IDifficultyBeatmap beatmapKey)
        {
            try
            {
                CustomPreviewBeatmapLevel? level = _beatmapLevelsModel.GetLevelPreviewForLevelId(beatmapKey.level.levelID) as CustomPreviewBeatmapLevel;

                string? hash = level is null ? null : SongCore.Utilities.Hashing.GetCustomLevelHash(level).ToLower();

                if (string.IsNullOrEmpty(hash))
                {
                    hash = beatmapKey.level.levelID;

                    if (hash.StartsWith(CUSTOM_LEVEL_HASH))
                        hash = hash[CUSTOM_LEVEL_HASH.Length..];

                    Plugin.Log.Warn("Hash was given as null, setting hash to: " + hash);
                }

                return hash;
            }
            catch (System.Exception e)
            {
                Plugin.Log.Error("There was an error trying to find the hash\n" + e);
                return null;
            }
        }
#endif
    }
}