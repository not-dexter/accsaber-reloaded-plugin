using AccSaber.Utils;
using LeaderboardCore.Interfaces;
using ModestTree;
using SiraUtil.Logging;
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

            var hash = $"{SongCore.Utilities.Hashing.GetCustomLevelHash(level)}/{beatmapKey.difficulty}".ToLower();
            var mapInfo = _accSaberStore.RankedMaps.TryGetValue(hash, out var ret) ? ret : null;

            _accSaberStore.CurrentRankedMap = mapInfo;
        }
#endif
    }
}