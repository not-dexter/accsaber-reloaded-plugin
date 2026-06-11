using AccSaber.Consts;
using AccSaber.Models.Base;
using AccSaber.UI.ViewControllers;
using AccSaber.Utils;
using AccSaber.Utils.Misc;
using AccsaberLeaderboard.UI.BSML_Addons.Components;
using AccsaberLeaderboard.UI.Components;
using BeatSaberMarkupLanguage.Attributes;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using static AccSaber.UI.ViewControllers.AccSaberLeaderboardViewController;
using static AccSaber.Utils.ColorUtils;

namespace AccSaber.Models
{
    [UsedImplicitly]
    internal class AccSaberLeaderboardEntry : IModel, IEquatable<AccSaberLeaderboardEntry>
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
        public Guid CategoryId { get; set; }

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
        public Guid Id { get; set; }

        [JsonProperty("mapAuthor")]
        public string MapAuthor { get; set; } = null!;

        [JsonProperty("mapDifficultyId")]
        public Guid DifficultyId { get; set; }

        [JsonProperty("mapId")]
        public Guid MapId { get; set; }

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

        public bool Equals(AccSaberLeaderboardEntry other) =>
            PlayerId.Equals(other.PlayerId) && AP == other.AP && TimeSet == other.TimeSet;

        public override bool Equals(object obj) => obj is AccSaberLeaderboardEntry entry && Equals(entry);
        public override int GetHashCode() => MiscUtils.GetHashCode(PlayerId, TimeSet);
    }

    internal class LeaderboardEntryDisplay(AccSaberLeaderboardEntry data, AccSaberLeaderboardViewController parent, PlayerSocialLife playerInfo, LeaderboardSettingsModalController lsmc) : ICellDataSource, INotifyPropertyChanged
    {
        public string TemplatePath => ResourcePaths.LEADERBOARD_CELL;
        public float CellSize => Parent.OnPlayerPage ? BIG_CELL_SIZE : SMALL_CELL_SIZE;
        public int TemplateId { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public Coroutine? UnderlineFadeRoutine { get; set; }
        public Coroutine? BackgroundFadeRoutine { get; set; }

        public readonly AccSaberLeaderboardEntry ScoreData = data;
        private readonly AccSaberLeaderboardViewController Parent = parent;
        private readonly PlayerSocialLife PlayerInfo = playerInfo;
        private readonly LeaderboardSettingsModalController Lsmc = lsmc;

        [UIComponent(nameof(Container))] public readonly CustomBackground Container = null!;

        [UIValue(nameof(Score))] public string Score => $"<color={GREY}>{ScoreData.Score:N0}</color>";

        [UIValue(nameof(PlayerName))] public string PlayerName => ScoreData.PlayerName;

        [UIValue(nameof(Rank))] public string Rank => $"<color={RANK}>#{ScoreData.Rank}</color>";

        [UIValue(nameof(AP))] public string AP => $"<color={ColorUtils.AP}>{ScoreData.AP:N2}ap</color>";

        [UIValue(nameof(Acc))] public string Acc => $"<color={ACC}>{(ScoreData.Accuracy * 100f).ToString($"N{Parent.AccDecimals}")}%</color>";

        [UIValue(nameof(TimeSet))] public string TimeSet => $"<color={GetTimeSetColor()}><size=80%>{ScoreData.TimeSet.ToRelativeTime(1)[..^1]}</size></color>";

        [UIValue(nameof(BGColor))]
        public string BGColor
        {
            get
            {
                if (ScoreData.PlayerId.Equals(PlayerInfo.PlayerID))
                    return HIGHLIGHT;

                if (Parent.DisplayType != LeaderboardDisplayType.Relations)
                    return Parent.MissionTargets.Contains((ScoreData.PlayerId, ScoreData.DifficultyId)) ? RELATIONS_TARGETED : DIMMER;

                if (PlayerInfo.PlayerRivalIDs_Internal.Contains(ScoreData.PlayerId))
                    return RELATIONS_TARGETED;
                return RELATIONS_ACC;
            }
        }

        [UIValue(nameof(AllowUnderline))] public bool AllowUnderline => Parent.OnPlayerPage || !ScoreData.PlayerId.Equals(PlayerInfo.PlayerID);

        [UIValue(nameof(Mistakes))] public string Mistakes => $"<color=#ef4444>{ScoreData.Misses + (ScoreData.BombHits ?? 0) + ScoreData.BadCuts + (ScoreData.WallHits ?? 0)}x</color>";

        [UIValue(nameof(FullCombo))] public bool FullCombo => ScoreData.FC;
        [UIValue(nameof(NotFullCombo))] public bool NotFullCombo => !FullCombo;
        [UIValue(nameof(ShowCombo))] public bool ShowCombo => Parent.ShowCombo;



        [UIValue(nameof(Pixelimg))] private const string Pixelimg = ResourcePaths.PIXEL;
        [UIValue(nameof(FontSize))] public float FontSize => Parent.OnPlayerPage ? BIG_FONT_SIZE : SMALL_FONT_SIZE;
        [UIValue(nameof(TimeSize))] public float TimeSize => Parent.OnPlayerPage ? 2.8f : 2.5f;
        [UIValue(nameof(ContainerHeight))] public float ContainerHeight => (Parent.OnPlayerPage ? BIG_CELL_SIZE : SMALL_CELL_SIZE) - 0.1f;

        [UIValue(nameof(parentContainerWidth))] public const float parentContainerWidth = containerWidth;
        [UIValue(nameof(containerPadding))] public const float containerPadding = 1f;
        [UIValue(nameof(elementSpacing))] public const float elementSpacing = 0f;

        [UIValue(nameof(rankWidth))] public const float rankWidth = 10f;
        [UIValue(nameof(apWidth))] public const float apWidth = 12f + apPadding;
        [UIValue(nameof(apPadding))] public const float apPadding = 2f;
        [UIValue(nameof(accWidth))] public const float accWidth = 12f;
        [UIValue(nameof(scoreWidth))] public const float scoreWidth = 8f + scorePadding;
        [UIValue(nameof(scorePadding))] public const float scorePadding = 2f;
        [UIValue(nameof(timeSetWidth))] public const float timeSetWidth = 12f;
        [UIValue(nameof(comboWidth))] public const float comboWidth = 3.5f;

        [UIValue(nameof(nameWidth))]
        public float nameWidth = parent.ShowCombo ?
            containerWidth - rankWidth - apWidth - accWidth - scoreWidth - timeSetWidth - comboWidth - elementSpacing * 6f - containerPadding * 2f :
            containerWidth - rankWidth - apWidth - accWidth - scoreWidth - timeSetWidth - elementSpacing * 5f - containerPadding * 2f;

        public const string DefaultUnderlineColor = "#AAA6";

        public Color UnderlineColor
        {
            get => Container.Underline!.color;
            set => Container.Underline!.color = value;
        }
        public Color BackgroundColor
        {
            get => Container.Background!.color;
            set => Container.Background!.color = value;
        }

        private static readonly int[] TimeSetPointList = [MiscUtils.SECONDS_WEEK * 2, MiscUtils.SECONDS_YEAR / 2, MiscUtils.SECONDS_YEAR, MiscUtils.SECONDS_YEAR * 2];
        private static readonly float[] TimeSetStepSizes = [0.25f, 0.35f, 0.25f, 0.15f];

        private string GetTimeSetColor()
        {
            const string upperColor = GREY_DIM;

            float seconds = (float)(DateTime.UtcNow - ScoreData.TimeSet.ToUniversalTime()).TotalSeconds;

            int i = 0;
            float stepOffset = 0f;

            for (; i < TimeSetPointList.Length && seconds > TimeSetPointList[i]; ++i) 
                stepOffset += TimeSetStepSizes[i];

            if (i == TimeSetPointList.Length)
                return upperColor;

            int offset = i == 0 ? 0 : TimeSetPointList[i - 1];
            float percent = (seconds - offset) / ((TimeSetPointList[i] - offset) * TimeSetPointList.Length);

            return Color.Lerp(Color.white, upperColor.Color(), stepOffset + percent).Color();
        }

#pragma warning disable IDE0051
        [UIAction("#post-parse")]
        private void PostParse()
        {
            Container.Underline?.color = DefaultUnderlineColor.Color();

            Lsmc.OnSettingUpdated += () =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowCombo)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Acc)));
            };
        }
    }
}