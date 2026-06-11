using LeaderboardCore.Interfaces;

namespace AccSaber.Managers
{
	internal class AccSaberManager(AccSaberStore accSaberStore, BeatmapLevelsModel beatmapLevelsModel) : INotifyLeaderboardSet
	{
        private readonly AccSaberStore _accSaberStore = accSaberStore;
        private readonly BeatmapLevelsModel _beatmapLevelsModel = beatmapLevelsModel;
#if NEW_VERSION


        public void OnLeaderboardSet(BeatmapKey beatmapKey)
        {
            try
            {
                BeatmapLevel? level = _beatmapLevelsModel.GetBeatmapLevel(beatmapKey.levelId);

                if (level is null)
                {
                    return;
                }

#if V40
                string hash = SongCore.Utilities.Hashing.ComputeCustomLevelHash(level).ToLower();
#else
            string hash = SongCore.Utilities.Hashing.GetCustomLevelHash(level).ToLower();
#endif

                _accSaberStore.SetMapFromBasicInfo(hash, beatmapKey.difficulty);
            }
            catch (System.Exception e)
            {
                Plugin.Log.Error(e);
            }
        }
#else
        
        public void OnLeaderboardSet(IDifficultyBeatmap beatmapKey)
        {
			try
            {
                CustomPreviewBeatmapLevel? level = _beatmapLevelsModel.GetLevelPreviewForLevelId(beatmapKey.level.levelID) as CustomPreviewBeatmapLevel;

                if (level is null)
                {
                    return;
                }

                string hash = SongCore.Utilities.Hashing.GetCustomLevelHash(level).ToLower();

                _accSaberStore.SetMapFromBasicInfo(hash, beatmapKey.difficulty);
            }
            catch (System.Exception e)
            {
                Plugin.Log.Error(e);
            }
        }
#endif
    }
}