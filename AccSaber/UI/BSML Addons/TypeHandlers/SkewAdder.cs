using AccSaber.Utils;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.TypeHandlers;
using HMUI;
using IPA.Utilities;
using System;
using System.Collections.Generic;

namespace AccSaber.UI.BSML_Addons.TypeHandlers
{
    [ComponentHandler(typeof(Backgroundable))]
    internal class SkewAdder : TypeHandler
    {
        public override Dictionary<string, string[]> Props => new(1)
        {
            { "skew", ["skew"] }
        };

        public override void HandleType(BSMLParser.ComponentTypeWithData componentType, BSMLParserParams parserParams)
        {
            if (componentType.Component() is not Backgroundable bg)
                throw new Exception("Component is not Backgroundable");

            if (componentType.Data().TryGetValue("skew", out string skewStr) && float.TryParse(skewStr, out float skew))
            {
                if (bg.Background() is ImageView imageView)
                {
                    FieldAccessor<ImageView, float>.GetAccessor("_skew")(ref imageView) = skew;
                }
            }
        }
    }
}
