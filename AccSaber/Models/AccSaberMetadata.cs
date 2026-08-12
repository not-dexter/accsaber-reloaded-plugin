using AccSaber.Models.Base;
using AccSaber.Utils;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AccSaber.Models
{
    [UsedImplicitly]
    internal class AccSaberMetadata : IModel, IEquatable<AccSaberMetadata>, IEquatable<IReadonlyBeatmapData>
    {
        [JsonProperty("bpm")]
        public float Bpm { get; set; }

        [JsonProperty("notes")]
        public int Notes { get; set; }

        [JsonProperty("bombs")]
        public int Bombs { get; set; }

        [JsonProperty("walls")]
        public int Walls { get; set; }

        [JsonProperty("duration")]
        public int DurationInSeconds { get; set; } 

        public bool Equals(AccSaberMetadata other)
        {
            return Bpm == other.Bpm &&
                Notes == other.Notes &&
                Bombs == other.Bombs &&
                Walls == other.Walls &&
                DurationInSeconds == other.DurationInSeconds;
        }
        public bool Equals(IReadonlyBeatmapData other) // Note: This is not complete is checking for equality, as beatmapData doesn't give bpm or duration.
        {
            if (other.cuttableNotesCount == 0)
            {
                List<NoteData> noteData = [.. other.GetBeatmapDataItems<NoteData>(0)];

                int notes = noteData.Count;
                int bombs = noteData.Count(note => note.gameplayType == NoteData.GameplayType.Bomb);
                int walls = other.GetBeatmapDataItems<ObstacleData>(0).Count();
                return Notes == notes && Bombs == bombs && Walls == walls;
            }

            return Notes == other.cuttableNotesCount && Bombs == other.bombsCount && Walls == other.obstaclesCount;
        }

        public override bool Equals(object obj) => 
            obj is AccSaberMetadata other && Equals(other);
        public override int GetHashCode() => 
            MiscUtils.GetHashCode(MiscUtils.GetHashCode(Bpm, DurationInSeconds), Notes, Bombs, Walls);
    }
}
