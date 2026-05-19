using AccSaber.Models;
using AccSaber.Utils;
using LeaderboardCore.Interfaces;
using ModestTree;
using SiraUtil.Logging;
using System.Collections.Generic;
using System.Linq;
using Zenject;

namespace AccSaber.Managers
{
	internal class AccSaberManager(AccSaberStore accSaberStore, BeatmapLevelsModel beatmapLevelsModel) : INotifyLeaderboardSet
	{
        private readonly AccSaberStore _accSaberStore = accSaberStore;
        private readonly BeatmapLevelsModel _beatmapLevelsModel = beatmapLevelsModel;
#if NEW_VERSION


        public void OnLeaderboardSet(BeatmapKey beatmapKey)
		{
			BeatmapLevel? level = _beatmapLevelsModel.GetBeatmapLevel(beatmapKey.levelId);

            if (level is null)
			{
				return;
			}

            string hash = SongCore.Utilities.Hashing.GetCustomLevelHash(level).ToLower();

            _accSaberStore.SetMapFromBasicInfo(hash, beatmapKey.difficulty);
        }
#else
        
        public void OnLeaderboardSet(IDifficultyBeatmap beatmapKey)
        {
			CustomPreviewBeatmapLevel? level = _beatmapLevelsModel.GetLevelPreviewForLevelId(beatmapKey.level.levelID) as CustomPreviewBeatmapLevel;

            if (level is null)
            {
                return;
            }

            string hash = SongCore.Utilities.Hashing.GetCustomLevelHash(level).ToLower();
            
			_accSaberStore.SetMapFromBasicInfo(hash, beatmapKey.difficulty);
        }
#endif
    }
}