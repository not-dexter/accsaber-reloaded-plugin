using AccSaber.Models.Base;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace AccSaber.Models
{
    internal class AccSaberCampaign<T> : IModel
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

        [JsonProperty("summary")]
        public string Summary { get; set; } = null!;

        [JsonProperty("description")]
        public string Description { get; set; } = null!;

        [JsonProperty("status")]
        public CampaignStatus Status { get; set; }

        [JsonProperty("seekingCuration")]
        public bool SeekingCuration { get; set; }

        [JsonProperty("progressionAgnostic")]
        public bool ProgressionAgnostic { get; set; }

        [JsonProperty("completionMode")]
        public CampaignCompletionMode CompletionMode { get; set; }

        [JsonProperty("legacy")]
        public bool Legacy { get; set; }

        [JsonProperty("verified")]
        public bool Verified { get; set; }

        [JsonProperty("difficultyCount")]
        public int? DifficultyCount { get; set; }

        [JsonProperty("totalDifficulties")]
        public int? TotalDifficulties { get; set; }

        [JsonProperty("iconUrl")]
        public string? IconUrl { get; set; }

        [JsonProperty("completionXp")]
        public float CompletionXp { get; set; }

        [JsonProperty("curatorNotes")]
        public string CuratorNotes { get; set; } = null!;

        [JsonProperty("backgroundUrl")]
        public string BackgroundUrl { get; set; } = null!;

        [JsonProperty("completedDifficulties")]
        public int? CompletedDifficulties { get; set; }

        [JsonProperty("submittedAt")]
        public DateTime SubmittedAt { get; set; }

        [JsonProperty("playlistExportEnabled")]
        public bool PlaylistExportEnabled { get; set; }

        [JsonProperty("curatedAt")]
        public DateTime CuratedAt { get; set; }

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("tags")]
        public List<CampaignTags>? Tags { get; set; }

        [JsonProperty("difficulties")]
        public List<T>? Difficulties { get; set; }

        [JsonProperty("barriers")]
        public List<AccSaberCampaignBarrier>? Barriers { get; set; }

        [JsonProperty("texts")]
        public List<AccSaberCampaignText>? Texts { get; set; } 

        [JsonIgnore]
        public string? ProgressStatus { get; set; }


        // From: https://github.com/accsaber/accsaber-reloaded-backend/blob/main/src/main/java/com/accsaber/backend/model/entity/campaign/CampaignStatus.java
        public enum CampaignStatus
        {
            DRAFT, PUBLISHED, EDITING, CURATED
        }

        // From: 
        public enum CampaignCompletionMode
        {
            TERMINAL, ALL
        }
    }
    internal class AccSaberCampaign : AccSaberCampaign<AccSaberCampaignMap>;

    internal class CampaignTags
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


        // From: https://github.com/accsaber/accsaber-reloaded-backend/blob/main/src/main/java/com/accsaber/backend/model/entity/campaign/CampaignTagKind.java
        public enum CampaignTagKind
        {
            CATEGORY, DIFFICULTY, THEME, GENRE
        }
    }

    internal class AccSaberCampaignItem
    {
        [JsonProperty("itemId")]
        public Guid ItemId { get; set; }

        [JsonProperty("itemName")]
        public string ItemName { get; set; } = null!;

        [JsonProperty("quantity")]
        public int Quantity { get; set; }
    }

    internal class AccSaberCampaignPositionable
    {
        [JsonProperty("size")]
        public string SizeStr { get; set; } = "0";

        [JsonIgnore]
        public int Size { get; set; }

        [JsonProperty("positionX")]
        public int PositionX { get; set; }

        [JsonProperty("positionY")]
        public int PositionY { get; set; }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (int.TryParse(SizeStr, out int size))
                Size = size;
        }
    }
    internal class AccSaberCampaignPositionablePrereq : AccSaberCampaignPositionable
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("prerequisiteCampaignDifficultyIds")]
        public virtual List<Guid> PrerequisiteIds { get; set; } = [];
    }

    internal class AccSaberCampaignMap : AccSaberCampaignPositionablePrereq
    {
        [JsonProperty("mapDifficultyId")]
        public Guid MapDifficultyId { get; set; }

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

        [JsonProperty("requirementType")]
        public CampaignRequirementType RequirementType { get; set; }

        [JsonProperty("requirementValue")]
        public float RequirementValue { get; set; }

        [JsonProperty("prerequisiteMode")]
        public string PrerequisiteMode { get; set; } = null!;

        [JsonProperty("description")]
        public string Description { get; set; } = null!;

        [JsonProperty("borderColor")]
        public string? BorderColor { get; set; } 

        [JsonProperty("borderShape")]
        public string? BorderShape { get; set; }

        [JsonProperty("xp")]
        public float XP { get; set; }
        

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (Size == default)
                Size = 48;
        }

        // From: https://github.com/accsaber/accsaber-reloaded-backend/blob/main/src/main/java/com/accsaber/backend/model/entity/campaign/CampaignRequirementType.java
        public enum CampaignRequirementType
        {
            ACC,
            AP,
            SCORE,
            STREAK_115,
            FC,
            RANK
        }
    }
    internal class AccSaberCampaignBarrier : AccSaberCampaignPositionablePrereq
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
        public float Xp { get; set; }

        [JsonProperty("affectedCampaignDifficultyIds")]
        public List<Guid> AffectedCampaignDifficultyIds { get; set; } = [];

        [JsonProperty("items")]
        public List<AccSaberCampaignItem> Items { get; set; } = [];

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (Size == default)
                Size = 48;
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
            MAX_RANK
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
    }

    internal class AccSaberCampaignText
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; } = null!;

        [JsonProperty("positionX")]
        public int PositionX { get; set; }

        [JsonProperty("positionY")]
        public int PositionY { get; set; }

        [JsonProperty("font")]
        public string Font { get; set; } = null!;

        [JsonProperty("scale")]
        public float Scale { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; } = null!;

        [JsonProperty("effects")]
        public string Effects { get; set; } = null!;
    }

    internal class AccSaberCampaignPaged<T> : IModel
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("campaign")]
        public AccSaberCampaign<T> Campaign { get; set; } = null!;

        [JsonProperty("progressStatus")]
        public string? ProgressStatus { get; set; }
    }
    internal class AccSaberCampaignPaged : IModel
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("campaign")]
        public AccSaberCampaign Campaign { get; set; } = null!;

        [JsonProperty("progressStatus")]
        public string? ProgressStatus { get; set; }
    }

    internal class AccSaberCampaignNode
    {
        [JsonProperty("node")]
        public AccSaberCampaignMap Node { get; set; } = null!;
    }
}
