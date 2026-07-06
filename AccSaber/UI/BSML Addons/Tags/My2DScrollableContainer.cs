using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Tags;
using HMUI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AccSaber.UI.BSML_Addons.Tags
{
    public class My2DScrollableContainer : BSMLTag
    {
        public override string[] Aliases => ["scrollable-container-2d"];

        public override GameObject CreateObject(Transform parent)
        {
            GameObject root = new(
            "2DScrollableContainer",
            typeof(RectTransform),
            typeof(ScrollRect)
            );

            root.transform.SetParent(parent, false);

            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.localPosition = Vector2.zero;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.anchoredPosition = Vector2.zero;
            rootRt.sizeDelta = Vector2.zero;



            GameObject viewport = new("Viewport");

            viewport.transform.SetParent(root.transform, false);

            RectTransform viewportRt = viewport.AddComponent<RectTransform>();
            viewportRt.localPosition = Vector2.zero;
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.anchoredPosition = Vector2.zero;
            viewportRt.sizeDelta = Vector2.zero;

            ImageView viewportImage = viewport.AddComponent<ImageView>();
            viewportImage.raycastTarget = true;
            viewportImage.color = Color.white;
            viewportImage.sprite = Utilities.ImageResources.WhitePixel;
            viewportImage.material = Utilities.ImageResources.NoGlowMat;
            viewportImage.type = Image.Type.Simple;

            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;



            GameObject content = new("Content", typeof(Backgroundable));

            content.transform.SetParent(viewport.transform, false);

            RectTransform contentRt = content.AddComponent<RectTransform>();
            contentRt.localPosition = Vector2.zero;
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.anchorMin = new Vector2(0.5f, 1f);
            contentRt.anchorMax = new Vector2(0.5f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = new Vector2(200f, 200f);

            ScrollRect scrollRect = root.GetComponent<ScrollRect>();
            scrollRect.viewport = viewportRt;
            scrollRect.content = contentRt;

            scrollRect.horizontal = true;
            scrollRect.vertical = true;

            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.135f;
            scrollRect.scrollSensitivity = 10f;

            AddScrollbars(scrollRect);

            return content;
        }

        public static void AddScrollbars(ScrollRect scrollRect, float thickness = 2f)
        {
            RectTransform root = scrollRect.GetComponent<RectTransform>();
            RectTransform viewport = scrollRect.viewport;

            // Make room for scrollbars on the right and bottom.
            viewport.offsetMin = new Vector2(0f, thickness);
            viewport.offsetMax = new Vector2(-thickness, 0f);

            SafeScrollbar horizontalScrollbar = CreateScrollbar(
                root,
                "Horizontal Scrollbar",
                Scrollbar.Direction.LeftToRight,
                new Color(1f, 1f, 1f, 0.12f),
                new Color(1f, 1f, 1f, 0.55f)
            );

            RectTransform hRt = horizontalScrollbar.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0f, 0f);
            hRt.anchorMax = new Vector2(1f, 0f);
            hRt.offsetMin = new Vector2(0f, 0f);
            hRt.offsetMax = new Vector2(-thickness, thickness);

            SafeScrollbar verticalScrollbar = CreateScrollbar(
                root,
                "Vertical Scrollbar",
                Scrollbar.Direction.BottomToTop,
                new Color(1f, 1f, 1f, 0.12f),
                new Color(1f, 1f, 1f, 0.55f)
            );

            RectTransform vRt = verticalScrollbar.GetComponent<RectTransform>();
            vRt.anchorMin = new Vector2(1f, 0f);
            vRt.anchorMax = new Vector2(1f, 1f);
            vRt.offsetMin = new Vector2(-thickness, thickness);
            vRt.offsetMax = new Vector2(0f, 0f);

            scrollRect.horizontalScrollbar = horizontalScrollbar;
            scrollRect.verticalScrollbar = verticalScrollbar;

            scrollRect.horizontalScrollbarVisibility =
                ScrollRect.ScrollbarVisibility.Permanent;

            scrollRect.verticalScrollbarVisibility =
                ScrollRect.ScrollbarVisibility.Permanent;

            scrollRect.horizontalScrollbarSpacing = 0f;
            scrollRect.verticalScrollbarSpacing = 0f;

            // Common initial position:
            // x = 0 means left.
            // y = 1 means top.
            scrollRect.normalizedPosition = new Vector2(0f, 1f);
        }

        private static SafeScrollbar CreateScrollbar(RectTransform parent, string name, Scrollbar.Direction direction, Color trackColor, Color handleColor)
        {
            GameObject scrollbarGo = new(
                name,
                typeof(RectTransform)
            );

            scrollbarGo.transform.SetParent(parent, false);

            ImageView trackImage = scrollbarGo.AddComponent<ImageView>();
            ConfigureImage(trackImage, trackColor, true);

            GameObject slidingAreaGo = new(
                "Sliding Area",
                typeof(RectTransform)
            );

            slidingAreaGo.transform.SetParent(scrollbarGo.transform, false);

            RectTransform slidingAreaRt = slidingAreaGo.GetComponent<RectTransform>();
            slidingAreaRt.anchorMin = Vector2.zero;
            slidingAreaRt.anchorMax = Vector2.one;
            slidingAreaRt.offsetMin = Vector2.zero;
            slidingAreaRt.offsetMax = Vector2.zero;

            GameObject handleGo = new(
                "Handle",
                typeof(RectTransform)
            );

            handleGo.transform.SetParent(slidingAreaGo.transform, false);

            RectTransform handleRt = handleGo.GetComponent<RectTransform>();
            handleRt.anchorMin = Vector2.zero;
            handleRt.anchorMax = Vector2.one;
            handleRt.offsetMin = Vector2.zero;
            handleRt.offsetMax = Vector2.zero;

            ImageView handleImage = handleGo.AddComponent<ImageView>();
            ConfigureImage(handleImage, handleColor, true);

            SafeScrollbar scrollbar = scrollbarGo.AddComponent<SafeScrollbar>();
            scrollbar.direction = direction;
            scrollbar.handleRect = handleRt;
            scrollbar.targetGraphic = handleImage;
            scrollbar.size = 0.25f;
            scrollbar.numberOfSteps = 0;

            scrollbar.transition = Selectable.Transition.None;
            scrollbar.navigation = new Navigation
            {
                mode = Navigation.Mode.None
            };

            return scrollbar;
        }
        private static void ConfigureImage(ImageView imageView, Color color, bool raycastTarget)
        {
            imageView.color = color;
            imageView.raycastTarget = raycastTarget;

            imageView.sprite = Utilities.ImageResources.WhitePixel;
            imageView.material = Utilities.ImageResources.NoGlowMat;
            imageView.type = Image.Type.Simple;
        }
    }

    public class SafeScrollbar : Scrollbar
    {
        private Vector2 _dragOffset;
        private bool _dragging;

        private float _lastGoodValue = 0f;
        private float _lastGoodSize = 1f;

        public override void Awake()
        {
            base.Awake();

            _lastGoodValue = IsFinite(value) ? Mathf.Clamp01(value) : 0f;
            _lastGoodSize = IsFinite(size) ? Mathf.Clamp01(size) : 1f;
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            // Do NOT call base.OnPointerDown.
            // Unity's default Scrollbar pointer/drag logic is what can poison value with NaN.

            if (!CanDrag())
            {
                eventData.Use();
                return;
            }

            _dragging = true;

            CalculateDragOffset(eventData);

            // If the user clicked the track rather than the handle, jump the handle there.
            if (!RectTransformUtility.RectangleContainsScreenPoint(
                    handleRect,
                    eventData.position,
                    eventData.pressEventCamera))
            {
                _dragOffset = Vector2.zero;
                UpdateValueFromPointer(eventData);
            }

            eventData.Use();
        }

        public override void OnBeginDrag(PointerEventData eventData)
        {
            // Do NOT call base.OnBeginDrag.

            if (!CanDrag())
            {
                _dragging = false;
                eventData.Use();
                return;
            }

            _dragging = true;
            CalculateDragOffset(eventData);
            eventData.Use();
        }

        public override void OnDrag(PointerEventData eventData)
        {
            // Do NOT call base.OnDrag.

            if (!_dragging || !CanDrag())
            {
                eventData.Use();
                return;
            }

            UpdateValueFromPointer(eventData);
            eventData.Use();
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            _dragging = false;

            // This is safe; it just updates Selectable state.
            base.OnPointerUp(eventData);
        }

        private bool CanDrag()
        {
            if (handleRect is null)
                return false;

            RectTransform container = (handleRect.parent as RectTransform)!;

            if (container is null)
                return false;

            float trackSize = container.rect.size[(int)axis];

            if (!IsFinite(trackSize) || trackSize <= 0.001f)
                return false;

            if (!IsFinite(size))
                return false;

            float clampedSize = Mathf.Clamp01(size);

            // If size is 1, the handle fills the whole track.
            // There is no valid travel distance, so dragging would divide by zero.
            if (clampedSize >= 0.999f)
                return false;

            float handleSize = trackSize * clampedSize;
            float travel = trackSize - handleSize;

            if (!IsFinite(travel) || travel <= 0.001f)
                return false;

            return true;
        }

        private void CalculateDragOffset(PointerEventData eventData)
        {
            _dragOffset = Vector2.zero;

            if (handleRect == null)
                return;

            RectTransform container = (handleRect.parent as RectTransform)!;

            if (container == null)
                return;

            if (!RectTransformUtility.RectangleContainsScreenPoint(
                    handleRect,
                    eventData.position,
                    eventData.pressEventCamera))
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    container,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPointer))
            {
                return;
            }

            Vector3 worldHandleCenter = handleRect.TransformPoint(handleRect.rect.center);
            Vector2 localHandleCenter = container.InverseTransformPoint(worldHandleCenter);

            _dragOffset = localPointer - localHandleCenter;
        }

        private void UpdateValueFromPointer(PointerEventData eventData)
        {
            if (!TryGetValueFromPointer(eventData, out float newValue))
                return;

            SetValueSafely(newValue, true);
        }

        private bool TryGetValueFromPointer(PointerEventData eventData, out float newValue)
        {
            newValue = _lastGoodValue;

            if (handleRect is null)
                return false;

            RectTransform container = (handleRect.parent as RectTransform)!;

            if (container is null)
                return false;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    container,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPointer))
            {
                return false;
            }

            localPointer -= _dragOffset;

            Rect trackRect = container.rect;

            float trackSize = trackRect.size[(int)axis];

            if (!IsFinite(trackSize) || trackSize <= 0.001f)
                return false;

            float clampedSize = Mathf.Clamp01(size);

            if (!IsFinite(clampedSize) || clampedSize >= 0.999f)
                return false;

            float handleSize = trackSize * clampedSize;
            float travel = trackSize - handleSize;

            if (!IsFinite(travel) || travel <= 0.001f)
                return false;

            float pointerAlongAxis;
            float trackMin;

            if (axis == Axis.Horizontal)
            {
                pointerAlongAxis = localPointer.x;
                trackMin = trackRect.xMin;
            }
            else
            {
                pointerAlongAxis = localPointer.y;
                trackMin = trackRect.yMin;
            }

            float centerFromTrackMin = pointerAlongAxis - trackMin;
            float handleStart = centerFromTrackMin - handleSize * 0.5f;

            float normalized = handleStart / travel;

            if (direction == Direction.RightToLeft ||
                direction == Direction.TopToBottom)
            {
                normalized = 1f - normalized;
            }

            if (!IsFinite(normalized))
                return false;

            newValue = Mathf.Clamp01(normalized);
            return true;
        }

        private void SetValueSafely(float newValue, bool sendCallback)
        {
            if (!IsFinite(newValue))
                return;

            newValue = Mathf.Clamp01(newValue);

            _lastGoodValue = newValue;

            // Calling base.Set directly is okay here.
            // The problem was trying to override/hide it.
            Set(newValue, sendCallback);
        }

        private void LateUpdate()
        {
            // Last-line-of-defense repair in case ScrollRect or another script writes bad data.

            if (IsFinite(size))
            {
                _lastGoodSize = Mathf.Clamp01(size);
            }
            else
            {
                size = _lastGoodSize;
            }

            if (IsFinite(value))
            {
                _lastGoodValue = Mathf.Clamp01(value);
            }
            else
            {
                SetValueSafely(_lastGoodValue, true);
            }

            if (handleRect != null)
            {
                if (!IsFinite(handleRect.anchorMin) ||
                    !IsFinite(handleRect.anchorMax) ||
                    !IsFinite(handleRect.anchoredPosition))
                {
                    size = _lastGoodSize;
                    SetValueSafely(_lastGoodValue, true);
                }
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }
    }
}
