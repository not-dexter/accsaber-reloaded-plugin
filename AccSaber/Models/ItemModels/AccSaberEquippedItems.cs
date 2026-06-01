using Accsaber.Utils;
using AccSaber.Models.Base;
using AccSaber.Models.ItemModels.Base;
using AccSaber.Models.ItemModels.ValueTypes;
using AccSaber.Models.ItemModels.ValueTypes.States;
using AccSaber.Utils;
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
        private static readonly ObjectCacher<Gradient, Sprite[]> TextureBuffer = new();

        public Coroutine Set(MonoBehaviour host, TextMeshProUGUI text)
        {
            text.SetText(Title.Item.Value.Text);

            Coroutine outp = host.StartCoroutine(SetValueUsingStates(model => model.SetText(text), null, Title.Item.Value.States, Title.Item.Value.DurationMs / 1000f));

            if (Title.Item.Value.States.Count == 1)
            {
                AccSaberColorState? state = Title.Item.Value.States.FirstOrDefault(state => state.Glisten is not null);

                IEnumerator DoBoth()
                {
                    yield return outp;
                    yield return DoGlisten(state.Glisten!, text);
                }

                if (state is not null)
                    return host.StartCoroutine(DoBoth());
            }

            return outp;
        }
        public Coroutine Set(MonoBehaviour host, Image borderImage)
        {
            return host.StartCoroutine(SetValueUsingStates(model => ApplyGradient(borderImage, model.GetGradient(), angle: model.GradientRotation),
                (model, degree) => ApplyGradient(borderImage, model.GetGradient(), angle: degree),
                ProfileBorderColor.Item.Value.States, Title.Item.Value.DurationMs / 1000f));
        }
        public Coroutine Set(MonoBehaviour host, params IEnumerable<Image> images)
        {
            return host.StartCoroutine(SetValueUsingStates(model => ApplyGradient(images, model.GetGradient(), angle: model.GradientRotation),
                (model, degree) => ApplyGradient(images, model.GetGradient(), angle: degree),
                ProfileBorderColor.Item.Value.States, Title.Item.Value.DurationMs / 1000f));
        }
        public static IEnumerator DoGlisten(AccSaberItemGlisten glisten, TextMeshProUGUI text)
        {
            text.ForceMeshUpdate();
            TMP_TextInfo textInfo = text.textInfo;
            int characterCount = text.text.Length;

            float highlightLen = glisten.DurationMs / (float)characterCount;
            WaitForSeconds delay = new(highlightLen / 1000f);
            WaitForSeconds fullDelay = new(glisten.IntervalMs / 1000f);

            Color32[] buffer = new Color32[4];
            Color highlight = glisten.Color.Color();

            //yield return fullDelay;

            while (true)
            {
                buffer[0] = textInfo.meshInfo[textInfo.characterInfo[0].materialReferenceIndex].colors32[0];
                buffer[1] = textInfo.meshInfo[textInfo.characterInfo[0].materialReferenceIndex].colors32[1];
                buffer[2] = textInfo.meshInfo[textInfo.characterInfo[0].materialReferenceIndex].colors32[2];
                buffer[3] = textInfo.meshInfo[textInfo.characterInfo[0].materialReferenceIndex].colors32[3];

                for (int i = 0; i < characterCount; i++)
                {
                    TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
                    if (!charInfo.isVisible) continue;

                    int materialIndex = charInfo.materialReferenceIndex;
                    int vertexIndex = charInfo.vertexIndex;
                    Color32[] vertexColors = textInfo.meshInfo[materialIndex].colors32;

                    buffer[0] = vertexColors[vertexIndex + 0];
                    buffer[1] = vertexColors[vertexIndex + 1];
                    buffer[2] = vertexColors[vertexIndex + 2];
                    buffer[3] = vertexColors[vertexIndex + 3];

                    vertexColors[vertexIndex + 0] = highlight;
                    vertexColors[vertexIndex + 1] = highlight;
                    vertexColors[vertexIndex + 2] = highlight;
                    vertexColors[vertexIndex + 3] = highlight;

                    text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);

                    yield return delay;

                    vertexColors[vertexIndex + 0] = buffer[0];
                    vertexColors[vertexIndex + 1] = buffer[1];
                    vertexColors[vertexIndex + 2] = buffer[2];
                    vertexColors[vertexIndex + 3] = buffer[3];

                    text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
                }

                yield return fullDelay;
            }
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

            texture.Apply();
            return texture;
        }
        public static Sprite GetGradientSprite(Gradient gradient, int width = 128, int height = 128, float angle = 0f)
        {
            if (angle < 0f)
            {
                angle += Mathf.Ceil(-angle / 360) * 360;
            }

            int degreesInt = (int)Mathf.Round(angle) % 360;

            if (TextureBuffer.TryGetCachedItem(gradient, out Sprite[]? arr) && arr![degreesInt] is not null)
                return arr[degreesInt];

            Texture2D tex = CreateGradientTexture(gradient, width, height, angle);
            Sprite outp = Sprite.Create(tex, new(0, 0, width, height), new(0.5f, 0.5f));

            if (!TextureBuffer.ContainsKey(gradient))
                TextureBuffer.CacheItem(new Sprite[360], gradient);

            TextureBuffer.GetCachedItem(gradient)![degreesInt] = outp;

            return outp;
        }
        public static void ApplyGradient(Image image, Gradient gradient, int width = 128, int height = 128, float angle = 0f) =>
            image.sprite = GetGradientSprite(gradient, width, height, angle);
        public static void ApplyGradient(IEnumerable<Image> images, Gradient gradient, int width = 128, int height = 128, float angle = 0f)
        {
            Sprite sprite = GetGradientSprite(gradient, width, height, angle);

            foreach (Image image in images)
                image.sprite = sprite;
        }
    }
}
