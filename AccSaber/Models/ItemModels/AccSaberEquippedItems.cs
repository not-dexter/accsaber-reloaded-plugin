using AccSaber.Models.Base;
using AccSaber.Models.ItemModels.Base;
using AccSaber.Models.ItemModels.ValueTypes;
using AccSaber.Models.ItemModels.ValueTypes.States;
using HMUI;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AccSaber.Models.ItemModels
{
    [UsedImplicitly]
    internal class AccSaberEquippedItems : Model
    {
        [JsonProperty("title")]
        public AccSaberItemType<AccSaberItemTitleValue> Title { get; set; } = null!;

        [JsonProperty("profile_border_color")]
        public AccSaberItemType<AccSaberItemStateValue<AccSaberFillState>> ProfileBorderColor { get; set; } = null!;

        [JsonIgnore]
        private static readonly Dictionary<Gradient, Texture2D[]> TextureBuffer = [];

        public Coroutine SetTitle(TextMeshProUGUI text, MonoBehaviour host)
        {
            text.SetText(Title.Item.Value.Text);

            return host.StartCoroutine(SetValueUsingStates(model => model.SetText(text), null, Title.Item.Value.States, Title.Item.Value.DurationMs / 1000f));
        }
        public Coroutine SetProfileBorder(Image borderImage, MonoBehaviour host)
        {
            return host.StartCoroutine(SetValueUsingStates(model => ApplyGradient(borderImage, model.GetGradient(), angle: model.GradientRotation),
                (model, degree) => ApplyGradient(borderImage, model.GetGradient(), angle: degree),
                ProfileBorderColor.Item.Value.States, Title.Item.Value.DurationMs / 1000f));
        }
        public static IEnumerator SetValueUsingStates<T>(Action<T> setter, Action<T, float>? rotator, IEnumerable<T> states, float delayTime = 1f) where T : ItemStateModel<T>
        {
            WaitForSeconds delay = new(delayTime);
            WaitForEndOfFrame wait = new();

            List<(T state, WaitForSeconds delay, float delaySeconds)> mainLoopList = [];
            List<bool> rotate = [];
            Queue<float> rotations = [];
            int currentMs = 0;

            foreach (T state in states)
            {
                bool sameState = mainLoopList.Count > 0 && (mainLoopList.Last().state?.Equals(state) ?? false);

                int waitTime = state.AtMs - currentMs;
                currentMs = state.AtMs;

                if (sameState && mainLoopList.Last().state!.GradientRotation != state.GradientRotation)
                {
                    rotate.Add(true);
                    float rotation = Math.Abs(mainLoopList.Last().state!.GradientRotation - state.GradientRotation);
                    rotations.Enqueue(rotation);
                }
                else
                    rotate.Add(false);

                mainLoopList.Add((state, new(waitTime / 1000f), waitTime / 1000f));
            }

            rotations.Enqueue(0f);

            if (mainLoopList.Count == 1)
            {
                yield return wait;

                setter(states.First());
                yield break;
            }

            int len = mainLoopList.Count;
            float last = 0f;

            while (true)
            {
                for (int i = 0; i < len; ++i)
                {
                    if (rotate[i])
                    {
                        float rotation = rotations.Dequeue(), angle = last, mult = 1f / mainLoopList[i].delaySeconds;

                        while (angle < rotation)
                        {
                            angle += Time.deltaTime * rotation * mult;

                            rotator?.Invoke(mainLoopList[i].state, angle);

                            yield return null;
                        }

                        last = rotation;
                        rotations.Enqueue(rotation);
                        continue;
                    }

                    if (i != 0)
                        yield return mainLoopList[i].delay;

                    yield return wait;

                    setter(mainLoopList[i].state);
                }

                yield return delay;
            }
        }
        public static Texture2D CreateGradientTexture(Gradient gradient, int width = 128, int height = 128, float degrees = 0f)
        {
            int degreesInt = (int)Mathf.Round(degrees) % 360;

            if (TextureBuffer.ContainsKey(gradient) && TextureBuffer[gradient][degreesInt] is not null)
            {
                return TextureBuffer[gradient][degreesInt];
            }

            Texture2D texture = new(width, height)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            // Convert degrees to radians
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);

            float halfWidth = width * 0.5f;
            float halfHeight = height * 0.5f;

            // Calculate the projection length for normalization
            float projectionLength = Mathf.Abs(halfWidth * cos) + Mathf.Abs(halfHeight * sin);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    // Project pixel position onto the gradient direction
                    float projection = (x - halfWidth) * cos + (y - halfHeight) * sin;

                    // Normalize to 0-1 range
                    float t = Mathf.Clamp01(projection * 0.5f / projectionLength + 0.5f);

                    Color color = gradient.Evaluate(t);
                    texture.SetPixel(x, y, color);
                }
            }

            if (!TextureBuffer.ContainsKey(gradient))
                TextureBuffer.Add(gradient, new Texture2D[360]);

            TextureBuffer[gradient][degreesInt] = texture;

            texture.Apply();
            return texture;
        }
        public static void ApplyGradient(Image image, Gradient gradient, int width = 128, int height = 128, float angle = 0f)
        {
            //Plugin.Log.Info("angle = " + angle);
            Texture2D tex = CreateGradientTexture(gradient, width, height, angle);
            image.sprite = Sprite.Create(tex, new(0, 0, width, height), new(0.5f, 0.5f));
        }
    }
}
