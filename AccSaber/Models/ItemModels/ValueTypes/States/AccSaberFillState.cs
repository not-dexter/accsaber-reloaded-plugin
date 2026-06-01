using Accsaber.Utils;
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

        [JsonIgnore]
        private static readonly ObjectCacher<Gradient> GradientCache = new(TimeSpan.FromMinutes(30)); 

        public override bool Equals(AccSaberFillState other)
        {
            return Fill.Equals(other.Fill);
        }

        public override Gradient GetGradient()
        {
            if (_gradient is not null)
                return _gradient;

            if (GradientCache.TryGetCachedItem(ItemId, out _gradient))
                return _gradient!;

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

                case FillType.pixel_metal:
                    g.colorKeys = [
                        new(Fill.Highlight!.Color(), 0f),
                        new(Fill.Base!.Color(), 0.5f),
                        new(Fill.Shadow!.Color(), 1f)
                        ];
                    g.alphaKeys = [new(1f, 0f), new(1f, 0.5f), new(1f, 1f)];
                    Fill.AngleDegree = 315;
                    break;
            }

            _gradient = g;
            GradientCache.CacheItem(g, ItemId);

            return g;
        }
    }
}
