//#define PRINT_DEBUG

using AccSaber.Models.Base;
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
        public enum CampaignRequirementType
        {
            ACC,
            AP,
            SCORE,
            STREAK_115,
            FC,
            RANK,
            PASS
        }

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

        [JsonProperty("status")]
        public CampaignStatus Status { get; set; }

        [JsonProperty("seekingCuration")]
        public bool SeekingCuration { get; set; }

        [JsonProperty("official")]
        public bool Official { get; set; }

        [JsonProperty("progressionAgnostic")]
        public bool ProgressionAgnostic { get; set; }

        [JsonProperty("completionMode")]
        public CampaignCompletionMode CompletionMode { get; set; }

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

        [JsonIgnore]
        public UserCampaignProgress? ProgressStatus { get; set; }

        [JsonIgnore]
        public AccSaberCampaignOffsetData? OffsetData { get; set; }


        
    }
    internal class AccSaberCampaign : AccSaberCampaign<AccSaberCampaignMap>;

    internal class AccSaberCampaignOffsetData
    {
        public const float NODE_PADDING = 1f;
        public const int NODE_CONTAINER_PADDING = 5;

        public event Action? OnScaleChanging, OnScaleChanged;

        private readonly List<AccSaberCampaignScalable> nodes;

        public Vector2 ContainerSize { get; private set; }
        public Vector2 Offset { get; private set; }

        public float ScaleFactor { get; private set; }
        public float OffsetSize { get; private set; }

        public Vector2 BoundsMin { get; private set; }
        public Vector2 BoundsMax { get; private set; }

        public AccSaberCampaignOffsetData(float scaleFactor, IEnumerable<AccSaberCampaignScalable> nodes)
        {
            if (nodes is null || !nodes.Any())
                throw new ArgumentException("The node IEnumerable given must not be null and contain elements!");

            this.nodes = [.. nodes];

            RecalculateValuesWithScale(scaleFactor);
        }

        public void RecalculateValues()
        {
            RecalculateValuesWithScale(ScaleFactor);
        }

        public void RecalculateValuesWithScale(float scaleFactor)
        {
            ScaleFactor = scaleFactor;

            // This is the distance between logical map coordinates.
            OffsetSize = 48f * scaleFactor + NODE_PADDING;

            OnScaleChanging?.Invoke();

            float left = float.PositiveInfinity;
            float right = float.NegativeInfinity;
            float bottom = float.PositiveInfinity;
            float top = float.NegativeInfinity;

            foreach (AccSaberCampaignScalable node in nodes)
            {
                float centerX = node.PositionX * OffsetSize;
                float centerY = node.PositionY * OffsetSize;

                Vector2 size = node is AccSaberCampaignSizable sizeNode ? sizeNode.Size : Vector2.one * (node.Scale * scaleFactor);

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

            ContainerSize = new Vector2(
                width + NODE_CONTAINER_PADDING * 2f,
                height + NODE_CONTAINER_PADDING * 2f
            );

            Vector2 boundsCenter = new((left + right) * 0.5f, (bottom + top) * 0.5f);

            Offset = -boundsCenter;

            OnScaleChanged?.Invoke();

#if PRINT_DEBUG
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

        [JsonProperty("kind")]
        public CampaignTagKind Kind { get; set; }

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

    [UsedImplicitly]
    internal class AccSaberCampaignPositionable : CampaignModel
    {

        [JsonProperty("positionX")]
        public int PositionX { get; set; }

        [JsonProperty("positionY")]
        public int PositionY { get; set; }
    }

    [UsedImplicitly]
    internal class AccSaberCampaignScalable : AccSaberCampaignPositionable
    {
        [JsonProperty("size")]
        public virtual float Scale { get; set; }
    }

    internal class AccSaberCampaignSizable : AccSaberCampaignScalable
    {
        [JsonIgnore]
        public Vector2 Size { get; set; }
    }

    [UsedImplicitly]
    internal class AccSaberCampaignPrereqInfo : CampaignModel
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

    internal interface IAccSaberCampaignPrereq
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("prerequisites")]
        public List<AccSaberCampaignPrereqInfo> PrerequisiteInfos { get; set; }
    }

    [UsedImplicitly]
    internal class AccSaberCampaignScalablePrereq : AccSaberCampaignScalable, Utils.Misc.INode<Guid>, IAccSaberCampaignPrereq
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("prerequisites")]
        public virtual List<AccSaberCampaignPrereqInfo> PrerequisiteInfos { get; set; } = [];

        [JsonIgnore]
        public IReadOnlyCollection<Guid> InwardArrows => [.. PrerequisiteInfos.Select(prereq => prereq.Id)];
    }

    [UsedImplicitly]
    internal class AccSaberCampaignSizablePrereq : AccSaberCampaignSizable, Utils.Misc.INode<Guid>, IAccSaberCampaignPrereq
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("prerequisites")]
        public virtual List<AccSaberCampaignPrereqInfo> PrerequisiteInfos { get; set; } = [];

        [JsonIgnore]
        public IReadOnlyCollection<Guid> InwardArrows => [.. PrerequisiteInfos.Select(prereq => prereq.Id)];
    }

    [UsedImplicitly]
    internal class AccSaberCampaignMap : AccSaberCampaignSizablePrereq
    {
        [JsonProperty("mapDifficultyId")]
        public Guid MapDifficultyId { get; set; }

        [JsonProperty("mapId")]
        public Guid MapId { get; set; }

        [JsonProperty("categoryId")]
        public Guid? CategoryId { get; set; }

        [JsonIgnore]
        public APCategory Category { get; set; } = APCategory.Overall;

        // complexity?, beatsaverCode?, maxScore?

        [JsonProperty("mapAuthor")]
        public string MapAuthor { get; set; } = null!;

        [JsonProperty("songName")]
        public string SongName { get; set; } = null!;

        [JsonProperty("songAuthor")]
        public string SongAuthor { get; set; } = null!;

        [JsonProperty("coverUrl")]
        public string CoverUrl { get; set; } = null!;

        [JsonProperty("difficulty")]
        public string Difficulty { get; set; } = null!;

        [JsonProperty("characteristic")]
        public string Characteristic { get; set; } = null!;

        // mapDifficultyStatus

        [JsonProperty("requirementType")]
        public CampaignRequirementType RequirementType { get; set; }

        [JsonProperty("requirementValue")]
        public float RequirementValue { get; set; }

        [JsonProperty("prerequisiteMode")]
        public CampaignPrerequisiteMode PrerequisiteMode { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; } = null!;

        [JsonProperty("checkpointLabel")]
        public string? CheckpointLabel { get; set; }

        [JsonProperty("checkpointLabelPosition")]
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

        [JsonProperty("xp")]
        public float XP { get; set; }

        [JsonProperty("items")]
        public List<AccSaberCampaignItem> Items { get; set; } = [];


        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (Scale == default)
                Scale = 48;

            if (CategoryId is not null)
                Category = EnumUtils.ReloadedCategoryIdToCategory(CategoryId);
        }

    }

    [UsedImplicitly]
    internal class AccSaberCampaignBarrier : AccSaberCampaignScalablePrereq, Utils.Misc.INodeAffected<Guid>
    {
        [JsonProperty("conditionType")]
        public BarrierConditionType ConditionType { get; set; }

        [JsonProperty("conditionValue")]
        public float ConditionValue { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("checkpointLabel")]
        public string? CheckpointLabel { get; set; }

        [JsonProperty("checkpointLabelPosition")]
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
                Scale = 48;

            AffectedByIds = [.. AffectedCampaignDifficultyIds.Union(InwardArrows)];
        }
    }

    [UsedImplicitly]
    internal class AccSaberCampaignText : AccSaberCampaignSizable
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; } = null!;

        [JsonProperty("font")]
        public string Font { get; set; } = null!;

        [JsonProperty("scale")]
        public override float Scale { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; } = null!;

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

        [JsonProperty("progressStatus")]
        public UserCampaignProgress? ProgressStatus { get; set; }

        [JsonProperty("startedAt")]
        public DateTime StartedAt { get; set; }

        [JsonProperty("completedAt")]
        public DateTime? CompletedAt { get; set; }

        [JsonProperty("completedDifficulties")]
        public int CompletedDifficulties { get; set; } = 0;
    }

    internal class AccSaberCampaignNode
    {
        [JsonProperty("node")]
        public AccSaberCampaignMap Node { get; set; } = null!;
    }
}
