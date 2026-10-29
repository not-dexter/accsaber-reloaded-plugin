using AccSaber.API;
using AccSaber.Managers;
using AccSaber.Models;
using AccSaber.Models.PlayerModels;
using AccSaber.Models.WarModels;
using AccSaber.Utils.Misc;
using BeatSaberMarkupLanguage.Attributes;
using System;
using System.Threading.Tasks;
using Zenject;

namespace AccSaber.UI.MenuButton.ClanWars.ViewControllers
{
    [ViewDefinition("AccSaber.UI.MenuButton.ClanWars.Views.AccSaberClanWarsView.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\AccSaberClanWarsView.bsml")]
    internal class AccSaberClanWarsController : Utils.Safety.BSMLSafeAutomaticViewController, IInitializable
    {
        public ObjectCacher<Guid, AccSaberClanWarDetails> WarCacher { get; } = new();
        public Guid PlayerClan { get; private set; }

        [Inject] private readonly AccSaberStore store = null!;

        [UIAction("#post-parse")]
        private void PostParse()
        {

        }


        private async Task LoadWars()
        {
            AccSaberPlayer player = await store.GetCurrentUserAsync();
            PlayerClan = player.Clan.Id;

            AccSaberPagedContent<AccSaberClanWar>? wars = await APIHandler.CallAPI_Json<AccSaberPagedContent<AccSaberClanWar>>(
                string.Format(HelpfulPaths.APAPI_CLAN_WARS, PlayerClan, 0, 20), AccsaberAPI.Throttler);

            if (wars is null || wars.Content is null)
            {
                Plugin.Log.Warn("Failed to load clan wars.");
                return;
            }

            if (wars.Content.Count == 0)
            {
                Plugin.Log.Warn("Player is not in any wars currently.");
                return;
            }

            foreach (AccSaberClanWar war in wars.Content)
            {
                if (!WarCacher.ContainsKey(war.Id))
                {
                    AccSaberClanWarDetails? warDetails = await APIHandler.CallAPI_Json<AccSaberClanWarDetails>(
                        string.Format(HelpfulPaths.APAPI_CLAN_WAR_DETAILS, war.Id), AccsaberAPI.Throttler);
                    
                    if (warDetails is not null)
                        WarCacher.CacheItemPermanently(war.Id, warDetails);
                }
            }
        }
        public void Initialize()
        {
            _ = LoadWars();
        }
    }
}
