using AccSaber.UI.BSML_Addons.Tags;
using HMUI;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AccSaber.UI.BSML_Addons.Components
{

    public class ImageViewTiler : MonoBehaviour, IDisposable
    {
        public ImageView sourceImageView = null!;
        public AxisFilteredScrollRect? scrollRect;

        public float maxTileWidth = 100f;
        public float maxTileHeight = 100f;

        public bool disableSourceImage = true;
        public bool registerTilesForCulling = true;

        // Useful if you want it to rebuild itself when size/sprite changes.
        // If you resize manually, you can leave this false and call RequestRebuild().
        public bool autoRebuildOnSizeChange = false;

        private RectTransform _sourceRectTransform = null!;
        private RectTransform? _tileRoot;

        private readonly List<RectTransform> _tiles = [];
        private readonly List<Sprite> _createdSprites = [];

        private Vector2 _lastSize;
        private Sprite? _lastSprite;

        private bool _rebuildQueued;
        private bool _disposed;

        public static ImageViewTiler Create(ImageView sourceImageView, AxisFilteredScrollRect scrollRect, float maxTileSize = 100f)
        {
            ImageViewTiler tiler = sourceImageView.GetComponent<ImageViewTiler>() ?? sourceImageView.gameObject.AddComponent<ImageViewTiler>();

            tiler.Initialize(sourceImageView, scrollRect, maxTileSize, maxTileSize);

            tiler.Rebuild();

            return tiler;
        }

        public void Initialize(
            ImageView sourceImageView,
            AxisFilteredScrollRect scrollRect,
            float maxTileWidth,
            float maxTileHeight)
        {
            _disposed = false;

            this.sourceImageView = sourceImageView;
            this.scrollRect = scrollRect;
            this.maxTileWidth = maxTileWidth;
            this.maxTileHeight = maxTileHeight;

            _sourceRectTransform = sourceImageView.rectTransform;

            // Important:
            // If the source background object is a direct child of content,
            // generic direct-child culling could disable the whole background object.
            // We want the source object/root to stay active while individual tiles get culled.
            if (sourceImageView.GetComponent<ScrollCullIgnore>() is null)
                sourceImageView.gameObject.AddComponent<ScrollCullIgnore>();
        }

        private void LateUpdate()
        {
            if (_disposed)
                return;

            if (!autoRebuildOnSizeChange)
                return;

            if (sourceImageView is null)
                return;

            RectTransform rt = sourceImageView.rectTransform;
            Vector2 currentSize = rt.rect.size;

            if (currentSize != _lastSize || sourceImageView.sprite != _lastSprite)
                RequestRebuild();
        }

        public void RequestRebuild()
        {
            if (_disposed)
                return;

            if (_rebuildQueued)
                return;

            _rebuildQueued = true;
            StartCoroutine(RebuildAfterLayout());
        }

        private IEnumerator RebuildAfterLayout()
        {
            yield return null;

            Canvas.ForceUpdateCanvases();

            _rebuildQueued = false;

            if (!_disposed)
                Rebuild();
        }

        public void Rebuild()
        {
            if (_disposed)
                return;

            if (sourceImageView is null)
                return;

            _sourceRectTransform = sourceImageView.rectTransform;

            Sprite sourceSprite = sourceImageView.sprite;

            if (sourceSprite is null)
                return;

            Canvas.ForceUpdateCanvases();

            Vector2 size = _sourceRectTransform.rect.size;

            if (size.x <= 0f || size.y <= 0f)
                return;

            _lastSize = size;
            _lastSprite = sourceSprite;

            ClearTiles();

            CreateTileRoot();

            if (disableSourceImage)
                sourceImageView.enabled = false;

            int columns = Mathf.CeilToInt(size.x / maxTileWidth);
            int rows = Mathf.CeilToInt(size.y / maxTileHeight);

            Rect sourceSpriteTextureRect = sourceSprite.textureRect;
            Texture2D texture = sourceSprite.texture;

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    float tileX = column * maxTileWidth;
                    float tileY = row * maxTileHeight;

                    float tileWidth = Mathf.Min(maxTileWidth, size.x - tileX);
                    float tileHeight = Mathf.Min(maxTileHeight, size.y - tileY);

                    if (tileWidth <= 0f || tileHeight <= 0f)
                        continue;

                    bool shouldReuseSourceSprite = ShouldReuseSourceSprite(sourceSprite);

                    Sprite tileSprite;

                    if (shouldReuseSourceSprite)
                    {
                        // For 1x1 solid-color sprites, do not crop.
                        // Just stretch the same sprite over each tile.
                        tileSprite = sourceSprite;
                    }
                    else
                    {
                        tileSprite = CreateTileSprite(
                            texture,
                            sourceSpriteTextureRect,
                            size,
                            tileX,
                            tileY,
                            tileWidth,
                            tileHeight
                        );

                        _createdSprites.Add(tileSprite);
                    }

                    RectTransform tile = CreateTile(
                        column,
                        row,
                        tileX,
                        tileY,
                        tileWidth,
                        tileHeight,
                        tileSprite
                    );

                    _tiles.Add(tile);

                    if (registerTilesForCulling && scrollRect is not null)
                        scrollRect.RegisterCullTarget(tile);
                }
            }

            scrollRect?.ForceUpdateCulling();
        }

        private static bool ShouldReuseSourceSprite(Sprite sourceSprite)
        {
            if (sourceSprite is null)
                return false;

            Rect textureRect = sourceSprite.textureRect;

            // 1x1 white pixel / tiny solid sprites should be stretched, not cropped.
            return textureRect.width <= 1f || textureRect.height <= 1f;
        }

        private void CreateTileRoot()
        {
            GameObject tileRootGo = new($"{sourceImageView.gameObject.name} Tiles", typeof(RectTransform), typeof(ScrollCullIgnore));

            tileRootGo.transform.SetParent(_sourceRectTransform, false);

            _tileRoot = tileRootGo.GetComponent<RectTransform>();
            _tileRoot.anchorMin = Vector2.zero;
            _tileRoot.anchorMax = Vector2.one;
            _tileRoot.offsetMin = Vector2.zero;
            _tileRoot.offsetMax = Vector2.zero;
            _tileRoot.pivot = _sourceRectTransform.pivot;

            tileRootGo.transform.SetAsFirstSibling();
        }

        private RectTransform CreateTile(int column, int row, float tileX, float tileY, float tileWidth, float tileHeight, Sprite tileSprite)
        {
            GameObject tileGo = new($"Tile {column},{row}", typeof(RectTransform));

            tileGo.transform.SetParent(_tileRoot, false);

            RectTransform tileRt = tileGo.GetComponent<RectTransform>();
            tileRt.anchorMin = new Vector2(0f, 1f);
            tileRt.anchorMax = new Vector2(0f, 1f);
            tileRt.pivot = new Vector2(0f, 1f);

            tileRt.anchoredPosition = new Vector2(tileX, -tileY);
            tileRt.sizeDelta = new Vector2(tileWidth, tileHeight);

            // Add ImageView after parenting so HMUI curved settings are picked up.
            ImageView tileImage = tileGo.AddComponent<ImageView>();

            tileImage.sprite = tileSprite;
            tileImage.color = sourceImageView.color;
            tileImage.material = sourceImageView.material;
            tileImage.raycastTarget = sourceImageView.raycastTarget;
            tileImage.type = Image.Type.Simple;
            tileImage.preserveAspect = false;

            tileImage.SetAllDirty();

            return tileRt;
        }

        private static Sprite CreateTileSprite(Texture2D texture, Rect sourceTextureRect, Vector2 sourceUiSize, float tileX, float tileY, float tileWidth, float tileHeight)
        {
            float uMin = tileX / sourceUiSize.x;
            float uMax = (tileX + tileWidth) / sourceUiSize.x;

            // tileY is top-based. Sprite rect is bottom-based.
            float vTop = 1f - tileY / sourceUiSize.y;
            float vBottom = 1f - (tileY + tileHeight) / sourceUiSize.y;

            float pixelX = sourceTextureRect.x + uMin * sourceTextureRect.width;
            float pixelY = sourceTextureRect.y + vBottom * sourceTextureRect.height;
            float pixelWidth = (uMax - uMin) * sourceTextureRect.width;
            float pixelHeight = (vTop - vBottom) * sourceTextureRect.height;

            Rect tileTextureRect = new(pixelX, pixelY, pixelWidth, pixelHeight);

            return Sprite.Create(texture, tileTextureRect, new Vector2(0.5f, 0.5f), 100f);
        }

        public void ClearTiles()
        {
            if (scrollRect is not null)
            {
                foreach (RectTransform tile in _tiles)
                {
                    if (tile is not null)
                        scrollRect.UnregisterCullTarget(tile);
                }
            }

            _tiles.Clear();

            if (_tileRoot is not null)
            {
                if (Application.isPlaying)
                    Destroy(_tileRoot.gameObject);
                else
                    DestroyImmediate(_tileRoot.gameObject);
            }

            _tileRoot = null;

            foreach (Sprite sprite in _createdSprites)
            {
                if (sprite is null)
                    continue;

                if (Application.isPlaying)
                    Destroy(sprite);
                else
                    DestroyImmediate(sprite);
            }

            _createdSprites.Clear();
        }

        public void Dispose()
        {
            Dispose(restoreSourceImage: false, destroyComponent: true);
        }

        public void Dispose(bool restoreSourceImage)
        {
            Dispose(restoreSourceImage, destroyComponent: true);
        }

        public void Dispose(bool restoreSourceImage, bool destroyComponent)
        {
            if (_disposed)
                return;

            _disposed = true;
            _rebuildQueued = false;
            autoRebuildOnSizeChange = false;

            ClearTiles();

            if (sourceImageView is not null && disableSourceImage && restoreSourceImage)
                sourceImageView.enabled = true;

            if (destroyComponent)
            {
                if (Application.isPlaying)
                    Destroy(this);
                else
                    DestroyImmediate(this);
            }
        }

        private void OnDestroy()
        {
            if (_disposed)
                return;

            _disposed = true;

            ClearTiles();

            // Do not re-enable the source image here.
            // If the whole background object is being destroyed, re-enabling is unnecessary.
        }
    }
}
