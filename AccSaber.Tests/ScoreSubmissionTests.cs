using AccSaber.Configuration;
using AccSaber.Models;
using AccSaber.ScoreTracking;
using AccSaber.UI.ViewControllers;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace AccSaber.Tests
{
    public class ScoreSubmissionTests
    {
        [Fact]
        public async Task Initialize_UnrankedMapOutsideCampaign_StopsBeforeSubscribing()
        {
            var counter = new TestScoreCounter(rankedMap: false, campaignStarted: false);
            await counter.InitializeInternal();
            Assert.False(counter.Subscribed);
        }

        [Fact]
        public async Task Initialize_UnrankedCampaignMap_PassesEligibilityAndSubscribes()
        {
            var counter = new TestScoreCounter(rankedMap: false, campaignStarted: true);
            await counter.InitializeInternal();
            Assert.True(counter.GameplaySubscriptionRequested);
        }

        private sealed class TestScoreCounter : ScoreCounter
        {
            private readonly TestStore _store;
            private readonly TestCampaign? _campaign;

            public TestScoreCounter(bool rankedMap, bool campaignStarted)
            {
                _store = new TestStore(rankedMap);
                _campaign = campaignStarted ? new TestCampaign(true) : null;
                SetField("beatmapData", null!);
            }

            public bool Subscribed => (bool)typeof(ScoreCounter).GetField("subscribed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(this)!;
            public bool GameplaySubscriptionRequested { get; private set; }

            protected override StandardLevelScenesTransitionSetupDataSO? ResolveTransition() => (StandardLevelScenesTransitionSetupDataSO)FormatterServices.GetUninitializedObject(typeof(StandardLevelScenesTransitionSetupDataSO));
            protected override IScoreCounterStore? ResolveStore() => _store;
            protected override AccSaberLeaderboardViewController? ResolveLeaderboardController() => null;
            protected override IScoreCounterCampaign? ResolveCampaignController() => _campaign;
            protected override PluginConfig? ResolveConfig() => new PluginConfig();
            protected override void SubscribeToGameplayEvents() => GameplaySubscriptionRequested = true;
            protected override int GetTotalNoteCount() => 120;
            protected override bool IsCurrentLevelInvalid(AccSaberBasicDifficulty map) => false;

            private void SetField(string name, object? value) => typeof(ScoreCounter).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(this, value);
        }

        private sealed class TestCampaign(bool mapStarted) : ScoreCounter.IScoreCounterCampaign
        {
            public bool MapStarted { get; } = mapStarted;
            public bool CampaignSubmission { get; set; }
        }

        private sealed class TestStore : ScoreCounter.IScoreCounterStore
        {
            public TestStore(bool ranked)
            {
                if (ranked)
                    CurrentRankedMap = new AccSaberBasicDifficulty();
                else
                    CurrentRankedMap = null;
            }

            public AccSaberBasicDifficulty? CurrentRankedMap { get; }
            public Task<AccSaberBasicDifficulty?> GetCurrentMap() => Task.FromResult<AccSaberBasicDifficulty?>(CurrentRankedMap ?? new AccSaberBasicDifficulty());
            public Task<AccSaber.Models.PlayerModels.AccSaberPlayer> GetCurrentUserAsync() => Task.FromResult(new AccSaber.Models.PlayerModels.AccSaberPlayer { Headset = "Test" });
        }
    }
}
