using AccSaber.Models.ItemModels.Base;
using AccSaber.Utils;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using UnityEngine;

namespace AccSaber.Models.ItemModels.ValueTypes.States
{
    [UsedImplicitly]
    internal class AccSaberFillState : ItemStateModel<AccSaberFillState>
    {
        [JsonProperty("fill")]
        public AccSaberItemFill Fill { get; set; } = null!;
        public override float GradientRotation => Fill.AngleDegree ?? 0f;

        [JsonIgnore]
        private Gradient? _gradient;

        public override bool Equals(AccSaberFillState other)
        {
            return Fill.Equals(other.Fill);
        }

        public override Gradient GetGradient()
        {
            if (_gradient is not null)
                return _gradient;

            Gradient g = new();

            switch (Fill.Type)
            {
                case FillType.solid:
                    g.colorKeys = [new GradientColorKey(Fill.Color!.Color(), 0f)];
                    g.alphaKeys = [new GradientAlphaKey(1f, 0f)];
                    break;

                case FillType.linear:

                    if (Fill.Stops is null)
                        throw new ArgumentException(nameof(Fill.Stops));

                    GradientColorKey[] arr = new GradientColorKey[Fill.Stops.Count];
                    GradientAlphaKey[] alphaArr = new GradientAlphaKey[arr.Length];

                    for (int i = 0; i < Fill.Stops.Count; ++i)
                    {
                        float time = Fill.Stops[i].AtPercent / 100f;

                        arr[i] = new(Fill.Stops[i].Color.Color(), time);
                        alphaArr[i] = new(1f, time);
                    }

                    g.SetKeys(arr, alphaArr);

                    break;
            }

            _gradient = g;
            return g;
        }
    }
}
