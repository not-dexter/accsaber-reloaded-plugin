//#define PRINT_DEBUG

using AccSaber.Models.Base;
using AccSaber.Models.JsonConverters;
using AccSaber.Utils;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using UnityEngine;

namespace AccSaber.Models
{
    public abstract class CampaignModel : IModel 
    {
        public const float DEFAULT_NODE_SIZE = 48f;

        // From: https://github.com/accsaber/accsaber-reloaded-backend/blob/main/src/main/java/com/accsaber/backend/model/entity/campaign/CampaignStatus.java
        public enum CampaignStatus
        {
            DRAFT, PUBLISHED, EDITING, CURATED
        }

        // From: https://github.com/accsaber/accsaber-reloaded-backend/blob/main/src/main/java/com/accsaber/backend/model/entity/campaign/CampaignCompletionMode.java
        public enum CampaignCompletionMode
        {
            TERMINAL, ALL
        }

        // From: https://github.com/accsaber/accsaber-reloaded-backend/blob/main/src/main/java/com/accsaber/backend/model/entity/campaign/UserCampaignStatus.java
        public enum UserCampaignProgress
        {
            IN_PROGRESS,
            COMPLETED,
            ABANDONED
        }

        // From: https://github.com/accsaber/accsaber-reloaded-backend/blob/main/src/main/java/com/accsaber/backend/model/entity/campaign/CampaignTagKind.java
        public enum CampaignTagKind
        {
            CATEGORY, DIFFICULTY, THEME, GENRE
        }

        // From: https://github.com/accsaber/accsaber-reloaded-backend/blob/main/src/main/java/com/accsaber/backend/model/entity/campaign/CampaignRequirementType.java
        // On modification, check the following places:
        // CampaignCounter line 153
        // AccSaberCampaignViewController line 1073, 1283
        /// <summary>
        /// The types of requirements that can be set for a campaign node. These are used to determine if a player meets
        /// the requirements to access a node. See below for the places where this enum is used and should be updated if
        /// modified.<br/> - <see cref="Counter.Hosts.CampaignCounter"/> at line 153<br/> -
        /// <see cref="UI.MenuButton.Campaigns.ViewControllers.AccSaberCampaignViewController"/> at line 1073 and 1283
        /// </summary>
        public enum CampaignRequirementType
        {
            ACC,
            AP,
            SCORE,
            STREAK_115,
            FC,
            RANK,
            PASS,
            COMBO,
            BOMB_HITS,
            MISTAKES
        }

        // From: https://github.com/accsaber/accsaber-reloaded-backend/blob/main/src/main/java/com/accsaber/backend/model/entity/campaign/CampaignRequirementType.java#L15
        public static bool IsLowerBetter(CampaignRequirementType requirement) => requirement is CampaignRequirementType.RANK;


        // From: https://github.com/accsaber/accsaber-reloaded-backend/blob/main/src/main/java/com/accsaber/backend/model/entity/campaign/CampaignPrerequisiteMode.java
        public enum CampaignPrerequisiteMode
        {
            OR,
            AND
        }

        // From: https://github.com/accsaber/accsaber-reloaded-backend/blob/main/src/main/java/com/accsaber/backend/model/entity/campaign/CampaignLabelPosition.java
        public enum CampaignLabelPosition
        {
            LEFT,
            RIGHT,
            UP,
            DOWN,
            NONE
        }

        // From: https://github.com/accsaber/accsaber-reloaded-backend/blob/main/src/main/java/com/accsaber/backend/model/entity/campaign/BarrierConditionType.java
        public enum BarrierConditionType
        {
            AVERAGE_ACC,
            AVERAGE_AP,
            AP_MAX,
            ACC_MAX,
            STREAK_115_AVERAGE,
            STREAK_115_MAX,
            FC,
            AVERAGE_RANK,
            MAX_RANK,
            COMPLETION_COUNT,
            PASS
        }

        // From: https://github.com/accsaber/accsaber-reloaded-backend/blob/main/src/main/java/com/accsaber/backend/model/entity/campaign/CampaignNodeBorderLayer.java
        public enum CampaignNodeBorderLayer
        {
            ABOVE,
            BELOW
        }

        // From: https://github.com/accsaber/accsaber-reloaded-backend/blob/main/src/main/java/com/accsaber/backend/model/entity/campaign/CampaignModifierRequirement.java
        public enum CampaignModifierRequirement
        {
            REQUIRED,
            FORBIDDEN
        }
    }

    [UsedImplicitly]
    internal class AccSaberCampaign<T> : CampaignModel where T : Utils.Misc.INode<Guid> 
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("creatorId")]
        public string CreatorId { get; set; } = null!;

        [JsonProperty("creatorName")]
        public string CreatorName { get; set; } = null!;

        [JsonProperty("creatorAlias")]
        public string CreatorAlias { get; set; } = null!;

        [JsonProperty("name")]
        public string Name { get; set; } = null!;

        // slug

        [JsonProperty("summary")]
        public string Summary { get; set; } = null!;

        [JsonProperty("description")]
        public string Description { get; set; } = null!;

        [JsonProperty("status"), JsonConverter(typeof(EnumJsonConverter<CampaignStatus>))]
        public CampaignStatus? Status { get; set; }

        [JsonProperty("seekingCuration")]
        public bool SeekingCuration { get; set; }

        [JsonProperty("official")]
        public bool Official { get; set; }

        [JsonProperty("progressionAgnostic")]
        public bool ProgressionAgnostic { get; set; }

        [JsonProperty("completionMode"), JsonConverter(typeof(EnumJsonConverter<CampaignCompletionMode>))]
        public CampaignCompletionMode? CompletionMode { get; set; }

        [JsonProperty("legacy")]
        public bool Legacy { get; set; }

        [JsonProperty("completionXp")]
        public float CompletionXp { get; set; }

        [JsonProperty("playlistExportEnabled")]
        public bool PlaylistExportEnabled { get; set; }

        [JsonProperty("backgroundUrl")]
        public string? BackgroundUrl { get; set; }

        [JsonProperty("backgroundColor")]
        public string? BackgroundColor { get; set; }

        [JsonProperty("background")]
        public AccSaberCampaignBackgroundSizeInfo? BackgroundSizeInfo { get; set; }

        [JsonProperty("iconUrl")]
        public string? IconUrl { get; set; }

        [JsonProperty("verified")]
        public bool Verified { get; set; }

        [JsonProperty("difficultyCount")]
        public int? DifficultyCount { get; set; }

        [JsonProperty("totalDifficulties")]
        public int? TotalDifficulties { get; set; }

        [JsonProperty("curatorNotes")]
        public string CuratorNotes { get; set; } = null!;

        [JsonProperty("completedDifficulties")]
        public int? CompletedDifficulties { get; set; }

        [JsonProperty("submittedAt")]
        public DateTime SubmittedAt { get; set; }

        [JsonProperty("completionItems")]
        public List<AccSaberCampaignItem>? Items { get; set; }

        [JsonProperty("curatedAt")]
        public DateTime CuratedAt { get; set; }

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("tags")]
        public List<CampaignTag>? Tags { get; set; }

        [JsonProperty("difficulties")]
        public List<AccSaberCampaignMap>? Difficulties { get; set; }

        [JsonProperty("barriers")]
        public List<AccSaberCampaignBarrier>? Barriers { get; set; }

        [JsonProperty("texts")]
        public List<AccSaberCampaignText>? Texts { get; set; } 

        [JsonIgnore, JsonConverter(typeof(EnumJsonConverter<UserCampaignProgress>))]
        public UserCampaignProgress? ProgressStatus { get; set; }

        [JsonIgnore]
        public AccSaberCampaignOffsetData? OffsetData { get; set; }
    }
    internal class AccSaberCampaign : AccSaberCampaign<AccSaberCampaignMap>;

    internal class AccSaberCampaignBackgroundSizeInfo : CampaignModel, IAccSaberCampaignSizable, IAccSaberCampaignScalable
    {
        [JsonProperty("size")]
        public float Scale { get; set; }

        [JsonProperty("x")]
        public float PositionX { get; set; }

        [JsonProperty("y")]
        public float PositionY { get; set; }

        [JsonIgnore]
        public Vector2 Size { get; set; }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Scale /= 100f;
        }
    }

    internal class AccSaberCampaignOffsetData
    {
        public const float NODE_PADDING = 0f;
        public const int NODE_CONTAINER_PADDING = 5;

        public event Action? OnScaleChanging, OnScaleChanged;

        private readonly IAccSaberCampaignScalable[] nodes;
        public bool IgnorePadding { get; set; }

        public Vector2 ContainerSize { get; private set; }
        public Vector2 Offset { get; private set; }

        public float ScaleFactor { get; private set; }
        public float OffsetSize { get; private set; }

        public Vector2 BoundsMin { get; private set; }
        public Vector2 BoundsMax { get; private set; }

        public AccSaberCampaignOffsetData(float scaleFactor, IEnumerable<IAccSaberCampaignScalable> nodes, bool ignorePadding)
        {
            if (nodes is null || !nodes.Any())
                throw new ArgumentException("The node IEnumerable given must not be null and contain elements!");

            this.nodes = [.. nodes];
            IgnorePadding = ignorePadding;

            RecalculateValuesWithScale(scaleFactor);
        }

        public void RecalculateValues()
        {
            RecalculateValuesWithScale(ScaleFactor);
        }

        public void RecalculateValuesWithScale(float scaleFactor)
        {
            ScaleFactor = scaleFactor;

            const float SHAPE_OFFSET = 0.75f;

            // This is the distance between logical map coordinates.
            OffsetSize = SHAPE_OFFSET * CampaignModel.DEFAULT_NODE_SIZE * scaleFactor + NODE_PADDING;

            OnScaleChanging?.Invoke();

            float left = float.PositiveInfinity;
            float right = float.NegativeInfinity;
            float bottom = float.PositiveInfinity;
            float top = float.NegativeInfinity;

            foreach (IAccSaberCampaignScalable node in nodes)
            {
                float centerX = node.PositionX * OffsetSize;
                float centerY = node.PositionY * OffsetSize;

                Vector2 size = node is IAccSaberCampaignSizable sizeNode ? sizeNode.Size : Vector2.one * (node.Scale * scaleFactor);

                float halfWidth = size.x * 0.5f;
                float halfHeight = size.y * 0.5f;

                left = Mathf.Min(left, centerX - halfWidth);
                right = Mathf.Max(right, centerX + halfWidth);

                bottom = Mathf.Min(bottom, centerY - halfHeight);
                top = Mathf.Max(top, centerY + halfHeight);
            }

            BoundsMin = new Vector2(left, bottom);
            BoundsMax = new Vector2(right, top);

            float width = right - left;
            float height = top - bottom;

            ContainerSize = IgnorePadding ? new(width, height) : new(width + NODE_CONTAINER_PADDING * 2f, height + NODE_CONTAINER_PADDING * 2f);

            Vector2 boundsCenter = new((left + right) * 0.5f, (bottom + top) * 0.5f);

            Offset = -boundsCenter;

            OnScaleChanged?.Invoke();

#if PRINT_DEBUG && DEBUG
        Plugin.Log.Info($"BoundsMin = {BoundsMin}, BoundsMax = {BoundsMax}");
        Plugin.Log.Info($"ContainerSize = {ContainerSize}, Offset = {Offset}");
        Plugin.Log.Info($"ScaleFactor = {ScaleFactor}, OffsetSize = {OffsetSize}");
#endif
        }
    }

    [UsedImplicitly]
    internal class CampaignTag : CampaignModel
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("kind"), JsonConverter(typeof(EnumJsonConverter<CampaignTagKind>))]
        public CampaignTagKind? Kind { get; set; }

        [JsonProperty("categoryId")]
        public Guid CategoryId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = null!;

        [JsonProperty("system")]
        public bool System { get; set; }
    }

    [UsedImplicitly]
    internal class AccSaberCampaignItem : CampaignModel
    {
        [JsonProperty("itemId")]
        public Guid ItemId { get; set; }

        [JsonProperty("itemName")]
        public string ItemName { get; set; } = null!;

        [JsonProperty("quantity")]
        public int Quantity { get; set; }
    }

    internal interface IAccSaberCampaignPositionable : IModel
    {
        [JsonProperty("positionX")]
        public float PositionX { get; set; }

        [JsonProperty("positionY")]
        public float PositionY { get; set; }
    }

    internal interface IAccSaberCampaignId : IModel
    {
        public Guid Id { get; set; }
    }

    [UsedImplicitly]
    internal class AccSaberCampaignPositionable : CampaignModel, IAccSaberCampaignPositionable
    {
        [JsonProperty("positionX")]
        public float PositionX { get; set; }

        [JsonProperty("positionY")]
        public float PositionY { get; set; }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            const float OFFSET_AMOUNT = 0.45f;

            PositionY += OFFSET_AMOUNT * (1 - Mathf.Abs(1 - (Mathf.Abs(PositionX) % 2)));
        }
    }

    internal interface IAccSaberCampaignScalable : IAccSaberCampaignPositionable
    {
        [JsonProperty("size")]
        public float Scale { get; set; }
    }

    internal interface IAccSaberCampaignSizable : IAccSaberCampaignPositionable
    {
        [JsonIgnore]
        public Vector2 Size { get; set; }
    }

    [UsedImplicitly]
    internal class AccSaberCampaignPrereqInfo : CampaignModel, IAccSaberCampaignId
    {
        [JsonProperty("comesFromCampaignDifficultyId")]
        public Guid Id { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; } = "#FFF";

        [JsonIgnore]
        public string DimmedColor { get; set; } = null!;

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            DimmedColor = Color.DimColor(5, dimAlpha: true);
        }
    }

    internal interface IAccSaberCampaignPrereq : IAccSaberCampaignId
    {
        [JsonProperty("prerequisites")]
        public List<AccSaberCampaignPrereqInfo> PrerequisiteInfos { get; set; }
    }

    [UsedImplicitly]
    internal class AccSaberCampaignPositionablePrereq : AccSaberCampaignPositionable, Utils.Misc.INode<Guid>, IAccSaberCampaignPrereq
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("prerequisites")]
        public virtual List<AccSaberCampaignPrereqInfo> PrerequisiteInfos { get; set; } = [];

        [JsonIgnore]
        public IReadOnlyCollection<Guid> InwardArrows => [.. PrerequisiteInfos.Select(prereq => prereq.Id)];
    }

    [UsedImplicitly]
    internal class AccSaberCampaignPositionableScalablePrereq : AccSaberCampaignPositionablePrereq, IAccSaberCampaignScalable
    {
        [JsonProperty("size")]
        public virtual float Scale { get; set; }
    }

    [UsedImplicitly]
    internal class AccSaberCampaignPositionableSizablePrereq : AccSaberCampaignPositionableScalablePrereq, IAccSaberCampaignSizable
    {
        [JsonIgnore]
        public Vector2 Size { get; set; }
    }

    [UsedImplicitly]
    internal class AccSaberCampaignMap : AccSaberCampaignPositionableSizablePrereq
    {
        [JsonProperty("mapDifficultyId")]
        public Guid MapDifficultyId { get; set; }

        [JsonProperty("mapId")]
        public Guid MapId { get; set; }

        [JsonProperty("categoryId")]
        public Guid? CategoryId { get; set; }

        [JsonIgnore, JsonConverter(typeof(EnumJsonConverter<APCategory>))]
        public APCategory Category { get; set; } = APCategory.Overall;

        // complexity?, beatsaverCode?, maxScore?

        [JsonProperty("metadata")]
        public AccSaberMetadata Metadata { get; set; } = null!;

        // nps?, maxCombo?

        [JsonProperty("songName")]
        public string SongName { get; set; } = null!;

        [JsonProperty("songAuthor")]
        public string SongAuthor { get; set; } = null!;

        [JsonProperty("mapAuthor")]
        public string MapAuthor { get; set; } = null!;
        
        [JsonProperty("coverUrl")]
        public string CoverUrl { get; set; } = null!;

        [JsonProperty("difficulty"), JsonConverter(typeof(EnumJsonConverter<ReloadedDifficulty>))]
        public ReloadedDifficulty? Difficulty { get; set; }

        [JsonProperty("characteristic")]
        public string Characteristic { get; set; } = null!;

        // mapDifficultyStatus

        [JsonProperty("targetMode"), JsonConverter(typeof(EnumJsonConverter<CampaignPrerequisiteMode>))]
        public CampaignPrerequisiteMode? TargetMode { get; set; }

        [JsonProperty("targets")]
        public List<AccSaberCampaignTarget> Targets { get; set; } = null!; 

        [JsonProperty("prerequisiteMode"), JsonConverter(typeof(EnumJsonConverter<CampaignPrerequisiteMode>))]
        public CampaignPrerequisiteMode? PrerequisiteMode { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; } = null!;

        [JsonProperty("checkpointLabel")]
        public string? CheckpointLabel { get; set; }

        [JsonProperty("checkpointLabelPosition"), JsonConverter(typeof(EnumJsonConverter<CampaignLabelPosition>))]
        public CampaignLabelPosition CheckpointLabelPosition { get; set; } = CampaignLabelPosition.UP;

        [JsonProperty("checkpointAvatarUrl")]
        public string? CheckpointAvatarUrl { get; set; } 

        [JsonProperty("checkpointColor")]
        public string? CheckpointColor { get; set; }

        [JsonProperty("checkpointSize")]
        public int CheckpointSize { get; set; } = 30;

        [JsonProperty("borderColor")]
        public string? BorderColor { get; set; } 

        [JsonProperty("borderShape")]
        public string? BorderShape { get; set; }

        [JsonProperty("nodeBorderUrl")]
        public string? NodeBorderUrl { get; set; }

        [JsonProperty("nodeBorderLayer"), JsonConverter(typeof(EnumJsonConverter<CampaignNodeBorderLayer>))]
        public CampaignNodeBorderLayer NodeBorderLayer { get; set; } = CampaignNodeBorderLayer.ABOVE;

        [JsonProperty("xp")]
        public float XP { get; set; }

        [JsonProperty("items")]
        public List<AccSaberCampaignItem> Items { get; set; } = [];

        [JsonProperty("modifiers")]
        public List<AccSaberCampaignModifier> Modifiers { get; set; } = [];


        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (Scale == default)
                Scale = DEFAULT_NODE_SIZE;

            if (CategoryId is not null)
                Category = EnumUtils.ReloadedCategoryIdToCategory(CategoryId);
        }

    }

    [UsedImplicitly]
    internal class AccSaberCampaignTarget : CampaignModel, IAccSaberCampaignId, IComparable<Guid>, IComparable<AccSaberCampaignTarget>
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("requirementType"), JsonConverter(typeof(EnumJsonConverter<CampaignRequirementType>))]
        public CampaignRequirementType? RequirementType { get; set; }

        [JsonProperty("requirementValue")]
        public float RequirementValue { get; set; }

        [JsonProperty("requirementValueMax")]
        public float? RequirementValueMax { get; set; } = null;

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (RequirementType is not null && IsLowerBetter(RequirementType.Value))
            {
                float temp = RequirementValueMax ?? 0f;
                RequirementValueMax = RequirementValue;
                RequirementValue = temp;
            }
        }

        public int CompareTo(Guid other) => Id.CompareTo(other);
        public int CompareTo(AccSaberCampaignTarget? other) => other is not null ? Id.CompareTo(other.Id) : 1;

        public override int GetHashCode()
        {
            return MiscUtils.GetHashCode(Id, RequirementType, RequirementValue, RequirementValueMax);
        }
    }

    [UsedImplicitly]
    internal class AccSaberCampaignModifier : CampaignModel
    {
        [JsonProperty("modifier")]
        public AccSaberModifier Modifier { get; set; } = null!;

        [JsonProperty("requirement"), JsonConverter(typeof(EnumJsonConverter<CampaignModifierRequirement>))]
        public CampaignModifierRequirement? Requirement { get; set; }
    }

    [UsedImplicitly]
    internal class AccSaberCampaignBarrier : AccSaberCampaignPositionableSizablePrereq, Utils.Misc.INodeAffected<Guid>
    {
        [JsonProperty("conditionType"), JsonConverter(typeof(EnumJsonConverter<BarrierConditionType>))]
        public BarrierConditionType? ConditionType { get; set; }

        [JsonProperty("conditionValue")]
        public float ConditionValue { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("checkpointLabel")]
        public string? CheckpointLabel { get; set; }

        [JsonProperty("checkpointLabelPosition"), JsonConverter(typeof(EnumJsonConverter<CampaignLabelPosition>))]
        public CampaignLabelPosition? CheckpointLabelPosition { get; set; }

        [JsonProperty("checkpointAvatarUrl")]
        public string? CheckpointAvatarUrl { get; set; }

        [JsonProperty("checkpointColor")]
        public string? CheckpointColor { get; set; }

        [JsonProperty("borderColor")]
        public string? BorderColor { get; set; }

        [JsonProperty("borderShape")]
        public string? BorderShape { get; set; }

        [JsonProperty("checkpointSize")]
        public float? CheckpointSize { get; set; }

        [JsonProperty("xp")]
        public float XP { get; set; }

        [JsonProperty("affectedCampaignDifficultyIds")]
        public List<Guid> AffectedCampaignDifficultyIds { get; set; } = [];

        [JsonProperty("items")]
        public List<AccSaberCampaignItem> Items { get; set; } = [];

        [JsonIgnore]
        public IReadOnlyCollection<Guid> AffectedByIds { get; private set; } = null!;


        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (Scale == default)
                Scale = DEFAULT_NODE_SIZE;

            AffectedByIds = [.. AffectedCampaignDifficultyIds.Union(InwardArrows)];
        }
    }

    [UsedImplicitly]
    internal class AccSaberCampaignText : AccSaberCampaignPositionableSizablePrereq
    {
        [JsonProperty("content")]
        public string Content { get; set; } = null!;

        [JsonProperty("font")]
        public string Font { get; set; } = null!;

        [JsonProperty("scale")]
        public override float Scale { get; set; } = 1.0f;

        [JsonProperty("color")]
        public string? Color { get; set; } = null;

        [JsonProperty("effects")]
        public string Effects { get; set; } = null!;
    }

    internal class AccSaberCampaignPaged<T> : CampaignModel where T : Utils.Misc.INode<Guid>
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("campaign")]
        public AccSaberCampaign<T> Campaign { get; set; } = null!;

        [JsonProperty("progressStatus")]
        public string? ProgressStatus { get; set; }
    }
    internal class AccSaberCampaignPaged : CampaignModel
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("campaign")]
        public AccSaberCampaign Campaign { get; set; } = null!;

        [JsonProperty("progressStatus"), JsonConverter(typeof(EnumJsonConverter<UserCampaignProgress>))]
        public UserCampaignProgress? ProgressStatus { get; set; }

        [JsonProperty("startedAt")]
        public DateTime StartedAt { get; set; }

        [JsonProperty("completedAt")]
        public DateTime? CompletedAt { get; set; }

        [JsonProperty("completedDifficulties")]
        public int CompletedDifficulties { get; set; } = 0;
    }

    [UsedImplicitly]
    internal class AccSaberCampaignNode
    {
        [JsonProperty("node")]
        public AccSaberCampaignMap Node { get; set; } = null!;
    }

    [UsedImplicitly]
    internal class AccSaberCampaignProgressDifficulty : CampaignModel
    {
        [JsonProperty("node")]
        public AccSaberCampaignMap Node { get; set; } = null!;

        [JsonProperty("userValue")]
        public float? UserValue { get; set; } = null;

        [JsonProperty("targets")]
        public List<AccSaberCampaignProgressTarget> Targets { get; set; } = null!;

        [JsonProperty("userScore")]
        public float? UserScore { get; set; } = null;

        [JsonProperty("completed")]
        public bool Completed { get; set; }

        [JsonProperty("unlocked")]
        public bool Unlocked { get; set; }

        [JsonProperty("pathCompleted")]
        public bool PathCompleted { get; set; }

        [JsonProperty("rewardsEarned")]
        public bool RewardsEarned { get; set; }

        public static implicit operator Managers.CampaignProgress.CampaignProgressValue(AccSaberCampaignProgressDifficulty diffProgress) =>
            new([.. diffProgress.Targets], Managers.CampaignProgress.GetCompletionStatus(diffProgress.Unlocked, diffProgress.Completed));

        public static explicit operator KeyValuePair<Guid, Managers.CampaignProgress.CampaignProgressValue>(AccSaberCampaignProgressDifficulty diffProgress) =>
            new(diffProgress.Node.Id, diffProgress);
    }

    [UsedImplicitly]
    internal class AccSaberCampaignProgressTarget : CampaignModel
    {
        [JsonProperty("target")]
        public AccSaberCampaignTarget Target { get; set; } = null!;

        [JsonProperty("userValue")]
        public float UserValue { get; set; } = 0f;

        [JsonProperty("met")]
        public bool Met { get; set; }

        public static implicit operator Managers.CampaignProgress.CampaignTargetProgess(AccSaberCampaignProgressTarget targetProgress) =>
            new(targetProgress.Target.Id, targetProgress.UserValue);
    }

    [UsedImplicitly]
    internal class AccSaberCampaignProgressBarrier : CampaignModel
    {
        [JsonProperty("barrier")]
        public AccSaberCampaignBarrier Barrier { get; set; } = null!;

        [JsonProperty("currentValue")]
        public float CurrentValue { get; set; }

        [JsonProperty("satisfied")]
        public bool Satisfied { get; set; }

        [JsonProperty("unlocked")]
        public bool Unlocked { get; set; }

        public static implicit operator Managers.CampaignProgress.CampaignTargetProgess(AccSaberCampaignProgressBarrier barrierProgress) =>
            new(barrierProgress.Barrier.Id, barrierProgress.CurrentValue);
        public static implicit operator Managers.CampaignProgress.CampaignProgressValue(AccSaberCampaignProgressBarrier barrierProgress) =>
            new([barrierProgress], Managers.CampaignProgress.GetCompletionStatus(barrierProgress.Unlocked, barrierProgress.Satisfied));

        public static explicit operator KeyValuePair<Guid, Managers.CampaignProgress.CampaignProgressValue>(AccSaberCampaignProgressBarrier barrierProgress) =>
            new(barrierProgress.Barrier.Id, barrierProgress);
    }

    
}
