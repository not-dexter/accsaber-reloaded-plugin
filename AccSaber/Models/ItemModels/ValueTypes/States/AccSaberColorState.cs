using AccSaber.Models.ItemModels.Base;
using AccSaber.Utils;
using Newtonsoft.Json;
using TMPro;

namespace AccSaber.Models.ItemModels.ValueTypes.States
{
    internal class AccSaberColorState : ItemStateModel<AccSaberColorState>
    {
        [JsonProperty("color")]
        public string? Color { get; set; }

        [JsonProperty("gradient")]
        public AccSaberItemGradient? Gradient { get; set; }

        [JsonProperty("glisten")]
        public AccSaberItemGlisten? Glisten { get; set; }

        public override float GradientRotation => Gradient?.AngleDegree ?? 0f;

        public override bool Equals(AccSaberColorState other)
        {
            if (Color is not null)
                return other.Color is not null && Color.Equals(other.Color);

            if (Gradient is not null)
                return other.Gradient is not null && Gradient.Equals(other.Gradient);

            return false;
        }

        public override void SetText(TextMeshProUGUI text)
        {
            if (Color is not null)
            {
                text.enableVertexGradient = false;
                text.color = Color.Color();
                return;
            }

            if (Gradient is not null)
            {
                text.enableVertexGradient = true;
                text.color = UnityEngine.Color.white;

                ApplyGradientAcrossWholeText(text, Gradient.Stops);
                return;
            }
        }
    }
}
