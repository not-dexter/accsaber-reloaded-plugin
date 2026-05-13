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
	internal class AccSaberManager : INotifyLeaderboardSet
	{
        private readonly AccSaberStore _accSaberStore;
        private readonly BeatmapLevelsModel _beatmapLevelsModel;
#if NEW_VERSION
		private readonly SiraLog _log;
		private readonly WebUtils _webUtils;
        
		public AccSaberManager(SiraLog log, WebUtils webUtils, AccSaberStore accSaberStore, BeatmapLevelsModel beatmapLevelsModel)
		{
			_log = log;
			_webUtils = webUtils;
			_accSaberStore = accSaberStore;
			_beatmapLevelsModel = beatmapLevelsModel;
		}

		public void OnLeaderboardSet(BeatmapKey beatmapKey)
		{
			BeatmapLevel? level = _beatmapLevelsModel.GetBeatmapLevel(beatmapKey.levelId);

            if (level is null)
			{
				return;
			}

			var hash = $"{SongCore.Utilities.Hashing.GetCustomLevelHash(level)}/{beatmapKey.difficulty}".ToLower();
			var mapInfo = _accSaberStore.RankedMaps.TryGetValue(hash, out var ret) ? ret : null;

			_accSaberStore.CurrentRankedMap = mapInfo;
		}
#else
        public AccSaberManager(AccSaberStore accSaberStore, BeatmapLevelsModel beatmapLevelsModel)
        {
            _accSaberStore = accSaberStore;
            _beatmapLevelsModel = beatmapLevelsModel;
        }
        public void OnLeaderboardSet(IDifficultyBeatmap beatmapKey)
        {
			CustomPreviewBeatmapLevel? level = _beatmapLevelsModel.GetLevelPreviewForLevelId(beatmapKey.level.levelID) as CustomPreviewBeatmapLevel;

            if (level is null)
            {
                return;
            }

            string hash = SongCore.Utilities.Hashing.GetCustomLevelHash(level);
            

			if (_accSaberStore.RankedMaps is not null)
			{
                AccSaberBasicDifficulty? mapInfo = _accSaberStore.RankedMaps.TryGetValue(hash, out AccSaberBasicDifficulty[]? ret) ?
                ret.FirstOrDefault(diff => beatmapKey.difficulty == diff.Difficulty) : null;

                _accSaberStore.SetMapFromBasicDifficulty(mapInfo);
            }
			else
				_accSaberStore.SetMapFromBasicInfo(hash, beatmapKey.difficulty);
        }
#endif
    }
}