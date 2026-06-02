using AccSaber.Consts;
using AccSaber.Models.Base;
using AccSaber.Utils;
using AccsaberLeaderboard.UI.BSML_Addons.Components;
using BeatSaberMarkupLanguage.Attributes;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using static AccSaber.UI.ViewControllers.AccSaberLeaderboardViewController;
using static AccSaber.Utils.ColorUtils;

namespace AccSaber.Models
{
    [UsedImplicitly]
    internal class AccSaberLeaderboardEntry : Model
    {
        [JsonProperty("accuracy")]
        public float Accuracy { get; set; }

        [JsonProperty("active")]
        public bool Active { get; set; }

        [JsonProperty("ap")]
        public float AP { get; set; }

        [JsonProperty("avatarUrl")]
        public string AvatarURL { get; set; } = null!;

        [JsonProperty("badCuts")]
        public int BadCuts { get; set; }

        [JsonProperty("baseXp")]
        public int BaseXp { get; set; }

        [JsonProperty("blScoreId")]
        public string? BlScoreId { get; set; }

        [JsonProperty("bombHits")]
        public int? BombHits { get; set; }

        [JsonProperty("bonusXp")]
        public float BonusXp { get; set; }

        [JsonProperty("categoryId")]
        public string CategoryId { get; set; } = null!;

        [JsonProperty("country")]
        public string Country { get; set; } = null!;

        [JsonProperty("coverUrl")]
        public string CoverUrl { get; set; } = null!;

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("difficulty")]
        public string Difficulty { get; set; } = null!;

        [JsonProperty("hmd")]
        public string Hmd { get; set; } = null!;

        [JsonProperty("id")]
        public string Id { get; set; } = null!;

        [JsonProperty("mapAuthor")]
        public string MapAuthor { get; set; } = null!;

        [JsonProperty("mapDifficultyId")]
        public string DifficultyId { get; set; } = null!;

        [JsonProperty("mapId")]
        public string MapId { get; set; } = null!;

        [JsonProperty("misses")]
        public int Misses { get; set; }

        [JsonProperty("modifierIds")]
        public List<object> ModifierIds { get; set; } = null!;

        [JsonProperty("pauses")]
        public int? Pauses { get; set; }

        [JsonProperty("rank")]
        public int Rank { get; set; }

        [JsonProperty("rankWhenSet")]
        public int RankWhenSet { get; set; }

        [JsonProperty("reweightDerivative")]
        public bool ReweightDerivative { get; set; }

        [JsonProperty("score")]
        public int Score { get; set; }

        [JsonProperty("scoreNoMods")]
        public int ScoreNoMods { get; set; }

        [JsonProperty("songAuthor")]
        public string SongAuthor { get; set; } = null!;

        [JsonProperty("songHash")]
        public string SongHash { get; set; } = null!;

        [JsonProperty("songName")]
        public string SongName { get; set; } = null!;

        [JsonProperty("streak115")]
        public int? Streak115 { get; set; }

        [JsonProperty("timeSet")]
        public DateTime TimeSet { get; set; }

        [JsonProperty("userId")]
        public string PlayerId { get; set; } = null!;

        [JsonProperty("userName")]
        public string PlayerName { get; set; } = null!;

        [JsonProperty("wallHits")]
        public int? WallHits { get; set; }

        [JsonProperty("weightedAp")]
        public float WeightedAp { get; set; }

        [JsonProperty("xpGained")]
        public float XpGained { get; set; }

        [JsonIgnore]
        public bool FC => Misses + (BombHits ?? 0) + BadCuts + (WallHits ?? 0) == 0;

        
    }

    internal class LeaderboardEntryDisplay(AccSaberLeaderboardEntry data) : ICellDataSource
    {
        public string TemplatePath => ResourcePaths.LEADERBOARD_CELL;
        public float CellSize => LeaderboardOnPlayerPage ? BIG_CELL_SIZE : SMALL_CELL_SIZE;
        public int TemplateId { get; set; }

        public readonly AccSaberLeaderboardEntry ScoreData = data;

        [UIValue(nameof(Score))] public string Score => $"<color={GREY}>{ScoreData.Score:N0}</color>";

        [UIValue(nameof(PlayerName))] public string PlayerName => ScoreData.PlayerName;

        [UIValue(nameof(Rank))] public string Rank => $"<color={RANK}>#{ScoreData.Rank}</color>";

        [UIValue(nameof(Mistakes))] public string Mistakes => $"<color=#ef4444>{ScoreData.Misses + (ScoreData.BombHits ?? 0) + ScoreData.BadCuts + (ScoreData.WallHits ?? 0)}x</color>";

        [UIValue(nameof(FullCombo))] public bool FullCombo => ScoreData.FC;
        [UIValue(nameof(NotFullCombo))] public bool NotFullCombo => !FullCombo;

        [UIValue(nameof(AP))] public string AP => $"<color={ColorUtils.AP}>{ScoreData.AP:N2}ap</color>";

        [UIValue(nameof(Acc))] public string Acc => $"<color=#22c55e>{(ScoreData.Accuracy * 100f).ToString($"N{Instance.AccDecimals}")}%</color>";
        [UIValue(nameof(BGColor))]
        public string BGColor
        {
            get
            {
                if (ScoreData.PlayerId.Equals(PlayerSocialLife.PlayerID))
                    return HIGHLIGHT;

                if (Instance.DisplayType != LeaderboardDisplayType.Relations)
                    return Instance.MissionTargets.Contains((ScoreData.PlayerId, ScoreData.DifficultyId)) ? RELATIONS_TARGETED : DIMMER;

                if (PlayerSocialLife.PlayerRivalIDs_Internal.Contains(ScoreData.PlayerId))
                    return RELATIONS_TARGETED;
                return RELATIONS_ACC;
            }
        }


        [UIValue(nameof(Pixelimg))] private const string Pixelimg = ResourcePaths.PIXEL;
        [UIValue(nameof(FontSize))] public float FontSize => LeaderboardOnPlayerPage ? BIG_FONT_SIZE : SMALL_FONT_SIZE;
        [UIValue(nameof(ContainerHeight))] public float ContainerHeight => (LeaderboardOnPlayerPage ? BIG_CELL_SIZE : SMALL_CELL_SIZE) - 0.1f;

        [UIValue(nameof(parentContainerWidth))] public const float parentContainerWidth = containerWidth;
        [UIValue(nameof(containerPadding))] public const float containerPadding = 1f;
        [UIValue(nameof(elementSpacing))] public const float elementSpacing = 0f;

        [UIValue(nameof(rankWidth))] public const float rankWidth = 10f;
        [UIValue(nameof(apWidth))] public const float apWidth = 14f + apPadding;
        [UIValue(nameof(apPadding))] public const float apPadding = 5f;
        [UIValue(nameof(accWidth))] public const float accWidth = 14f;
        [UIValue(nameof(scoreWidth))] public const float scoreWidth = 14f;
        [UIValue(nameof(nameWidth))] public const float nameWidth = containerWidth - rankWidth - apWidth - accWidth - scoreWidth - elementSpacing * 4f - containerPadding * 2f;
    }
}