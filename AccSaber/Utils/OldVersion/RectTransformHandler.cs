using BeatSaberMarkupLanguage.TypeHandlers;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AccSaber.Utils.OldVersion
{
    [ComponentHandler(typeof(RectTransform))]
    internal class RectTransformHandler : TypeHandler<RectTransform>
    {
        public override Dictionary<string, string[]> Props => new()
        {
            { "localScale", [ "local-scale", "scale" ] }
        };

        public override Dictionary<string, Action<RectTransform, string>> Setters => new()
        {
            { "localScale", new Action<RectTransform, string>((component, value) => component.localScale = Parse.Vector3(value, 1)) }
        };
    }
}
