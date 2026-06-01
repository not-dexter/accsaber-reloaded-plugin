using AccSaber.Utils;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace AccSaber.Models.ItemModels.Base
{
    [UsedImplicitly]
    internal abstract class ItemStateModel<T> : ItemValueModel, IEquatable<T> where T : ItemStateModel<T>
    {
        [JsonProperty("atMs")]
        public int AtMs { get; set; }

        [JsonIgnore]
        public virtual float GradientRotation { get; }

        public virtual void SetText(TextMeshProUGUI text) { }
        public virtual Gradient GetGradient() { return new(); }
        public virtual bool Equals(T other)
        {
            throw new NotImplementedException();
        }

        protected void ApplyGradientAcrossWholeText(TextMeshProUGUI textComponent, IEnumerable<AccSaberItemGradientStop> colorProvider)
        {
            textComponent.ForceMeshUpdate();
            TMP_TextInfo textInfo = textComponent.textInfo;
            int characterCount = textInfo.characterCount;

            IEnumerator<AccSaberItemGradientStop> colors = colorProvider.GetEnumerator();
            colors.MoveNext();

            Color c1 = colors.Current.Color.Color();

            colors.MoveNext();

            Color c2 = colors.Current.Color.Color();

            float currentProgress = colors.Current.AtPercent / 100f, lastProgress = 0f;
            float charLen = 1f / (characterCount - 1);

            for (int i = 0; i < characterCount; i++)
            {
                TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
                if (!charInfo.isVisible) continue;

                // Determine where this character sits horizontally (0.0 to 1.0)
                float progress = (float)i / (characterCount - 1);

                // Interpolate

                if (progress > currentProgress)
                {
                    c1 = c2;
                    colors.MoveNext();
                    c2 = colors.Current.Color.Color();
                    lastProgress = currentProgress;
                    currentProgress = colors.Current.AtPercent / 100f;
                }

                Color32 charColor1 = Color.Lerp(c1, c2, (progress - lastProgress) / (currentProgress - lastProgress));
                Color32 charColor2 = Color.Lerp(c1, c2, (progress - lastProgress + charLen) / (currentProgress - lastProgress));

                int materialIndex = charInfo.materialReferenceIndex;
                int vertexIndex = charInfo.vertexIndex;
                Color32[] vertexColors = textInfo.meshInfo[materialIndex].colors32;

                // Assign uniform horizontal progress color to all 4 corners
                vertexColors[vertexIndex + 0] = charColor1;
                vertexColors[vertexIndex + 1] = charColor1;
                vertexColors[vertexIndex + 2] = charColor2;
                vertexColors[vertexIndex + 3] = charColor2;
            }

            textComponent.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);


        }
    }
}
